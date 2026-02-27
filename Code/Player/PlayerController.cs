/// <summary>
/// Handles local player input, movement, dash, camera setup, and death.
/// Also owns the [Rpc.Broadcast] for chat messages.
/// </summary>
public sealed class PlayerController : Component
{
	[Property] public string SelectedCharacter { get; set; } = "Archer";

	private PlayerLocalState _state;
	private PlayerStats _stats;
	private PlayerWeapons _weapons;
	private EnemySpawner _spawner;
	private bool _isDead = false;

	private GameObject _hpBarGo;
	private PlayerHealthBar _hpBar;

	// Dash state
	private float _dashCooldown = 0f;
	private bool _isDashing = false;
	private Vector3 _dashDir = Vector3.Forward;
	private float _dashTime = 0f;

	// Sprite animation state
	private SpriteRenderer _spriteRenderer;
	private bool _facingRight = true;
	private string _currentAnim = "";
	private SpriteConfig _spriteConfig;

	private record SpriteConfig( string Path, string Idle, string WalkRight, string WalkUp, string WalkDown );

	private static readonly Dictionary<string, SpriteConfig> CharacterSprites = new()
	{
		["Knight"] = new( "sprites/knightanimations.sprite", "knightidle", "knightwalkright", "knightwalkup", "knightwalkdown" ),
		["Archer"]   = new( "sprites/archeranimations.sprite",   "idle", "walkright", "walkup", "walkdown" ),
		["Warrior"]  = new( "sprites/warrioranimations.sprite",  "idle", "walkright", "walkup", "walkdown" ),
		["Mage"]       = new( "sprites/mageanimations.sprite",       "idle", "walkright", "walkup", "walkdown" ),
		["Templar"]    = new( "sprites/templaranimations.sprite",    "idle", "walkright", "walkup", "walkdown" ),
		["Druid"]      = new( "sprites/druidanimations.sprite",      "idle", "walkright", "walkup", "walkdown" ),
		["Pyromancer"] = new( "sprites/pyromanceranimations.sprite", "idle", "walkright", "walkup", "walkdown" ),
	};

	protected override void OnStart()
	{
		if ( IsProxy )
		{
			foreach ( var r in Components.GetAll<ModelRenderer>() )
				r.Enabled = false;
			return;
		}

		_state = Components.Get<PlayerLocalState>();
		_stats = Components.Get<PlayerStats>();
		_weapons = Components.Get<PlayerWeapons>();
		_spawner = Components.Get<EnemySpawner>();

		// Create PlayerLocalState if missing (e.g. scene load order, editor play-in-place)
		if ( _state == null )
			_state = Components.Create<PlayerLocalState>();

		var charDef = CharacterDefinition.GetByName( MenuManager.SelectedCharacter );
		_state.Initialize( charDef );

		if ( _stats != null )
		{
			// Connection.Local may be null if game started without a lobby (editor fallback)
			_stats.PlayerName    = Connection.Local?.DisplayName ?? "Player";
			_stats.CharacterName = SelectedCharacter;
			_stats.IsAlive       = true;
		}

		_weapons?.AddWeaponByName( charDef.StartingWeapon );

		if ( CharacterSprites.TryGetValue( charDef.Name, out var cfg ) )
			SetupSprite( cfg );
		else
		{
			var renderer = Components.Create<ModelRenderer>();
			renderer.Model = Model.Load( "models/dev/box.vmdl" );
			renderer.Tint = GetCharacterColor( SelectedCharacter );
			GameObject.WorldScale = new Vector3( 0.3f, 0.3f, 0.05f );
		}

		CreateHealthBar();
		UpdateHealthBar();
		SetupCamera();
	}

	protected override void OnDestroy()
	{
		_hpBarGo?.Destroy();
	}

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;

		if ( !_isDead && (_state?.IsDead == true) )
		{
			_isDead = true;
			OnDied();
			return;
		}

		if ( _isDead ) return;

		// Show cursor only when the upgrade panel is open, hide it during normal play
		Mouse.Visibility = (UpgradeSystem.LocalInstance?.IsShowingUpgrades == true)
			? MouseVisibility.Visible
			: MouseVisibility.Hidden;

		bool uiOpen = UpgradeSystem.LocalInstance?.IsShowingUpgrades == true;
		if ( uiOpen )
		{
			UpdateSpriteAnimation( Vector3.Zero );
			UpdateHealthBar();
			return;
		}

		HandleMovement();
		HandleDash();
		UpdateHealthBar();
	}

	// Half-extents (world units) used for AABB push-out against solid world objects.
	// Sprite is 32×32 world units (scale 2) but the character body doesn't fill the full tile.
	// 8 units (¼ of sprite width) gives a snug inner hitbox that matches the visible body.
	private const float PlayerHalfExtent = 8f;
	/// <summary>Tighter hitbox for player-enemy collisions only. Reduces padding so contact feels closer to the sprite.</summary>
	private const float PlayerEnemyHalfExtent = 1f;
	/// <summary>Effective mass for collision push. Higher = player pushes enemies more, gets pushed less.</summary>
	private const float PlayerMass = 3f;

	private void HandleMovement()
	{
		var dir = Vector3.Zero;

		// Try Input.Down first (reliable once InputActions are loaded in .sbproj)
		if ( Input.Down( "forward" ) )  dir.x += 1f;
		if ( Input.Down( "backward" ) ) dir.x -= 1f;
		if ( Input.Down( "right" ) )    dir.y -= 1f;
		if ( Input.Down( "left" ) )     dir.y += 1f;

		// Fall back to AnalogMove if actions aren't registering yet
		if ( dir.LengthSquared < 0.01f )
		{
			var m = Input.AnalogMove;
			dir = new Vector3( m.y, -m.x, 0f );
		}

		if ( dir.LengthSquared > 0.01f )
			WorldPosition += dir.Normal * (_state?.Speed ?? 160f) * Time.Delta;

		WorldPosition = WorldPosition.WithZ( 0f );

		ResolveWorldObjectCollisions();
		UpdateSpriteAnimation( dir );
	}

	/// <summary>
	/// AABB push-out: moves the player outside any overlapping chest or shrine.
	/// Enemies intentionally do NOT block movement (Vampire Survivors style swarming).
	/// </summary>
	private void ResolveWorldObjectCollisions()
	{
		var pos = WorldPosition.WithZ( 0f );

		// Tree tiles — rectangular AABB push-out. Check a small neighbourhood around the player.
		int ptx = (int)MathF.Floor( pos.x / TreeManager.TileWorldWidth );
		int pty = (int)MathF.Floor( pos.y / TreeManager.TileWorldHeight );
		for ( int dtx = -2; dtx <= 2; dtx++ )
		{
			for ( int dty = -2; dty <= 2; dty++ )
			{
				if ( !TreeManager.IsTreeAtTile( ptx + dtx, pty + dty ) ) continue;
				var treeCenter = new Vector3(
					(ptx + dtx) * TreeManager.TileWorldWidth  + TreeManager.TileWorldWidth  * 0.5f,
					(pty + dty) * TreeManager.TileWorldHeight + TreeManager.TileWorldHeight * 0.5f,
					0f );
				pos = AabbPushOutRect( pos, treeCenter,
					PlayerHalfExtent, PlayerHalfExtent,
					TreeManager.TileWorldWidth  * 0.5f,
					TreeManager.TileWorldHeight * 0.5f );
			}
		}

		// Solid world objects — use AABB push-out (square boxes, cardinal movement)
		foreach ( var chest in Scene.GetAllComponents<Chest>() )
		{
			pos = AabbPushOut( pos, chest.WorldPosition.WithZ( 0f ), PlayerHalfExtent, 12f );
		}

		foreach ( var shrine in Scene.GetAllComponents<LevelUpBeacon>() )
		{
			if ( !shrine.IsActive ) continue;
			// Shrine WorldScale=(0.3,0.3,0.5) on a 100-unit box → 30 world units wide → half=15
			pos = AabbPushOut( pos, shrine.WorldPosition.WithZ( 0f ), PlayerHalfExtent, 15f );
		}

		// Enemies — mutual push-out with mass ratio. Player has higher mass so they push enemies more than they get pushed.
		const float enemyMass = 1f;
		foreach ( var enemy in Scene.GetAllComponents<EnemyBase>() )
		{
			if ( enemy.HP <= 0f ) continue;

			var diff    = pos - enemy.WorldPosition.WithZ( 0f );
			float minDist = PlayerEnemyHalfExtent + enemy.HalfExtent;
			float distSq  = diff.LengthSquared;

			if ( distSq < minDist * minDist && distSq > 0.001f )
			{
				float dist = MathF.Sqrt( distSq );
				float overlap = minDist - dist;
				float totalMass = PlayerMass + enemyMass;
				float playerPush = overlap * (enemyMass / totalMass);
				float enemyPush = overlap * (PlayerMass / totalMass);

				pos += diff / dist * playerPush;
				enemy.GameObject.WorldPosition = enemy.WorldPosition.WithZ( 0f ) - (diff / dist) * enemyPush;
			}
		}

		WorldPosition = pos;
	}

	/// <summary>
	/// Returns <paramref name="aPos"/> pushed out of <paramref name="bPos"/> along the
	/// axis of minimum penetration, or unchanged if there is no overlap.
	/// </summary>
	private static Vector3 AabbPushOut( Vector3 aPos, Vector3 bPos, float aHalf, float bHalf )
	{
		var diff   = aPos - bPos;
		float total = aHalf + bHalf;

		float overlapX = total - MathF.Abs( diff.x );
		float overlapY = total - MathF.Abs( diff.y );

		if ( overlapX <= 0f || overlapY <= 0f ) return aPos;

		if ( overlapX < overlapY )
			aPos.x += overlapX * (diff.x >= 0f ? 1f : -1f);
		else
			aPos.y += overlapY * (diff.y >= 0f ? 1f : -1f);

		return aPos;
	}

	/// <summary>
	/// Rectangular AABB push-out with independent X and Y half-extents for the solid object.
	/// Used for non-square obstacles like tree tiles.
	/// </summary>
	private static Vector3 AabbPushOutRect( Vector3 aPos, Vector3 bPos, float aHalfX, float aHalfY, float bHalfX, float bHalfY )
	{
		var diff = aPos - bPos;

		float overlapX = (aHalfX + bHalfX) - MathF.Abs( diff.x );
		float overlapY = (aHalfY + bHalfY) - MathF.Abs( diff.y );

		if ( overlapX <= 0f || overlapY <= 0f ) return aPos;

		if ( overlapX < overlapY )
			aPos.x += overlapX * (diff.x >= 0f ? 1f : -1f);
		else
			aPos.y += overlapY * (diff.y >= 0f ? 1f : -1f);

		return aPos;
	}

	private void SetupSprite( SpriteConfig cfg )
	{
		_spriteConfig = cfg;
		var sprite = ResourceLibrary.Get<Sprite>( cfg.Path );

		// Child GO so scaling doesn't push the camera (also a child) past ZFar
		var spriteGo = new GameObject( true, "CharacterSprite" );
		spriteGo.SetParent( GameObject );
		spriteGo.LocalPosition = new Vector3( 0f, 0f, 2f );
		spriteGo.LocalScale = new Vector3( 2f, 2f, 2f );

		_spriteRenderer = spriteGo.Components.Create<SpriteRenderer>();
		_spriteRenderer.Sprite = sprite;
		_spriteRenderer.PlaybackSpeed = 1f;
		_spriteRenderer.TextureFilter = Sandbox.Rendering.FilterMode.Point;
	}

	private void UpdateSpriteAnimation( Vector3 dir )
	{
		if ( _spriteRenderer == null || _spriteConfig == null ) return;

		bool moving = dir.LengthSquared > 0.01f;

		if ( !moving )
		{
			if ( _currentAnim != _spriteConfig.Idle )
			{
				_spriteRenderer.PlayAnimation( _spriteConfig.Idle );
				_currentAnim = _spriteConfig.Idle;
			}
			return;
		}

		string anim;
		bool flipH = !_facingRight;

		if ( MathF.Abs( dir.y ) >= MathF.Abs( dir.x ) )
		{
			anim = _spriteConfig.WalkRight;
			if ( dir.y < -0.01f ) { _facingRight = true;  flipH = false; }
			else                  { _facingRight = false; flipH = true;  }
		}
		else
		{
			anim  = dir.x > 0f ? _spriteConfig.WalkUp : _spriteConfig.WalkDown;
			flipH = false;
		}

		_spriteRenderer.FlipHorizontal = flipH;

		if ( anim != _currentAnim )
		{
			_spriteRenderer.PlayAnimation( anim );
			_currentAnim = anim;
		}
	}

	private void HandleDash()
	{
		_dashCooldown -= Time.Delta;

		if ( Input.Pressed( "jump" ) && _dashCooldown <= 0f )
		{
			_dashDir = Vector3.Zero;
			if ( Input.Down( "forward" ) )  _dashDir.x += 1f;
			if ( Input.Down( "backward" ) ) _dashDir.x -= 1f;
			if ( Input.Down( "right" ) )    _dashDir.y -= 1f;
			if ( Input.Down( "left" ) )     _dashDir.y += 1f;
			if ( _dashDir.LengthSquared < 0.01f )
			{
				var m = Input.AnalogMove;
				_dashDir = new Vector3( m.y, m.x, 0f );
			}
			if ( _dashDir.LengthSquared < 0.01f )
				_dashDir = Vector3.Forward;
			_dashDir = _dashDir.Normal;

			_isDashing = true;
			_dashTime = 0.15f;
			float baseCooldown = _state?.DashCooldownBase ?? 1.5f;
			float mult = _state?.DashCooldownMultiplier ?? 1f;
			_dashCooldown = baseCooldown * mult;
		}

		if ( _isDashing )
		{
			_dashTime -= Time.Delta;
			WorldPosition += _dashDir * 260f * Time.Delta;
			WorldPosition = WorldPosition.WithZ( 0f );

			if ( _dashTime <= 0f )
				_isDashing = false;
		}
	}

	private void SetupCamera()
	{
		var camGo = new GameObject( true, "PlayerCamera" );
		camGo.SetParent( GameObject );
		camGo.LocalPosition = new Vector3( 0f, 0f, 1000f );
		camGo.LocalRotation = Rotation.From( new Angles( 90f, 0f, 0f ) );

		var cam = camGo.Components.Create<CameraComponent>();
		cam.Orthographic = true;
		cam.OrthographicHeight = 200f;
		cam.IsMainCamera = true;
		cam.ZFar = 10000f;
		cam.ZNear = 1f;
		cam.BackgroundColor = new Color( 0.08f, 0.08f, 0.12f );
		cam.Priority = 10;
	}

	private void OnDied()
	{
		_stats?.Die();
		_spawner?.StopSpawning();
		_weapons?.SetPaused( true );

		_hpBarGo?.Destroy();

		// Clean up local enemies and gems
		foreach ( var enemy in Scene.GetAllComponents<EnemyBase>().ToList() )
			enemy.GameObject.Destroy();
		foreach ( var gem in Scene.GetAllComponents<XPGem>().ToList() )
			gem.GameObject.Destroy();
	}

	// WorldPanel health bar.
	// Default is ~100 px per world unit, so PanelSize(180,20) / 100 * scale(20) = 36×4 world units.
	// That's wider than the 32-unit player sprite and clearly visible.
	private void CreateHealthBar()
	{
		_hpBarGo = new GameObject( true, "HPBar" );
		_hpBarGo.SetParent( GameObject );
		_hpBarGo.WorldRotation = Rotation.From( new Angles( 90f, 0f, 0f ) );
		_hpBarGo.WorldScale    = new Vector3( 1f, 1f, 1f );

		var wp = _hpBarGo.Components.Create<Sandbox.WorldPanel>();
		wp.PanelSize = new Vector2( 240f, 44f );

		// The Razor PanelComponent renders automatically into the WorldPanel on the same GameObject
		_hpBar = _hpBarGo.Components.Create<PlayerHealthBar>();
	}

	private void UpdateHealthBar()
	{
		if ( _hpBarGo == null || _hpBar == null || _state == null ) return;

		// Follow player every frame — 20 units below in screen space (-X = screen-down)
		_hpBarGo.WorldPosition = WorldPosition + new Vector3( -14f, 0f, 1f );
		_hpBar.HPPercent = Math.Clamp( _state.HPPercent, 0f, 1f );
		_hpBar.ShieldPercent = _state.MaxShield > 0f
			? Math.Clamp( _state.Shield / _state.MaxShield, 0f, 1f )
			: 0f;
	}

	/// <summary>Broadcasts a chat message to all clients in the lobby.</summary>
	[Rpc.Broadcast]
	public void BroadcastChat( string message )
	{
		var name = GameObject.Network.Owner?.DisplayName ?? "Unknown";
		ChatComponent.Instance?.AddMessage( name, message );
	}

	private static Color GetCharacterColor( string charName ) => charName switch
	{
		"Archer"     => new Color( 0.2f, 0.6f, 1f ),
		"Warrior"    => new Color( 0.9f, 0.7f, 0.1f ),
		"Mage"       => new Color( 0.8f, 0.2f, 0.9f ),
		"Knight"     => new Color( 0.8f, 0.5f, 0.1f ),
		"Templar"    => new Color( 0.1f, 0.9f, 0.85f ),
		"Druid"      => new Color( 0.2f, 0.8f, 0.2f ),
		"Pyromancer" => new Color( 1f,   0.3f, 0.05f ),
		_            => Color.White
	};
}
