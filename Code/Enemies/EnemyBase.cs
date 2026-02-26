/// <summary>
/// Local-only enemy. Moves toward the player, deals contact damage, drops XP on death.
/// Never networked — only exists on the owning player's client.
/// </summary>
public sealed class EnemyBase : Component
{
	[Property] public float MaxHP { get; set; } = 30f;
	[Property] public float Speed { get; set; } = 24f;
	[Property] public float ContactDamage { get; set; } = 10f;
	/// <summary>Seconds between damage ticks. Each enemy type has its own attack rhythm.</summary>
	[Property] public float DamageCooldownDuration { get; set; } = 1f;
	[Property] public int XPValue { get; set; } = 5;
	[Property] public Color EnemyColor { get; set; } = Color.Red;
	/// <summary>Multiplier applied to the default 0.35 base scale. Set before OnStart runs.</summary>
	[Property] public float SizeScale { get; set; } = 1f;

	/// <summary>If set, a SpriteRenderer is used instead of the dev box model.</summary>
	[Property] public string SpritePath { get; set; }
	/// <summary>Animation name to play when the enemy dies. The GameObject is destroyed after DieAnimDuration.</summary>
	[Property] public string DieAnimation { get; set; }
	/// <summary>How long (in seconds) the die animation plays before the GameObject is destroyed.</summary>
	[Property] public float DieAnimDuration { get; set; } = 0.5f;

	public float HP { get; private set; }
	public GameObject Target { get; set; }

	/// <summary>World-space half-extent on the XY plane. Dev box is 100 units; WorldScale = 0.35 × SizeScale.</summary>
	public float HalfExtent => 17.5f * SizeScale;
	/// <summary>Radius for projectile hit detection. Tighter so projectile visually reaches sprite before destroying.</summary>
	public float ProjectileHitRadius => HalfExtent + 12f;

	private float _damageCooldown = 0f;
	private ModelRenderer _renderer;
	private SpriteRenderer _spriteRenderer;
	private float _flashTimer = 0f;
	private float _dieTimer = -1f;

	protected override void OnStart()
	{
		HP = MaxHP;

		if ( !string.IsNullOrEmpty( SpritePath ) )
		{
			var spriteGo = new GameObject( true, "EnemySprite" );
			spriteGo.SetParent( GameObject );
			spriteGo.LocalPosition = new Vector3( 0f, 0f, 2f );
			spriteGo.LocalScale = new Vector3( SizeScale * 2f, SizeScale * 2f, SizeScale * 2f );

			var sprite = ResourceLibrary.Get<Sprite>( SpritePath );
			_spriteRenderer = spriteGo.Components.Create<SpriteRenderer>();
			_spriteRenderer.Sprite = sprite;
			_spriteRenderer.TextureFilter = Sandbox.Rendering.FilterMode.Point;
		}
		else
		{
			_renderer = Components.Create<ModelRenderer>();
			_renderer.Model = Model.Load( "models/dev/box.vmdl" );
			_renderer.Tint = EnemyColor;
			float s = 0.35f * SizeScale;
			GameObject.WorldScale = new Vector3( s, s, 0.05f * SizeScale );
		}
	}

	protected override void OnUpdate()
	{
		// Count down die animation and destroy when it finishes
		if ( _dieTimer >= 0f )
		{
			_dieTimer -= Time.Delta;
			if ( _dieTimer < 0f )
				GameObject.Destroy();
			return;
		}

		if ( HP <= 0f ) return;

		// Flash white on hit, then restore color
		if ( _flashTimer > 0f )
		{
			_flashTimer -= Time.Delta;
			if ( _flashTimer <= 0f && _renderer != null )
				_renderer.Tint = EnemyColor;
		}

		if ( Target == null ) return;

		// Freeze while the player is choosing an upgrade — prevents dying during the selection screen
		var upgradeSystem = Target.Components.Get<UpgradeSystem>();
		if ( upgradeSystem?.IsShowingUpgrades == true ) return;

		// Move toward player on XY plane, sliding around tree tiles
		var dir = (Target.WorldPosition - WorldPosition).WithZ( 0f );
		if ( dir.LengthSquared > 1f )
		{
			var step = dir.Normal * Speed * Time.Delta;
			var desired = WorldPosition + step;

			if ( !TreeManager.IsTreeAtWorldPos( desired.x, desired.y ) )
			{
				WorldPosition = desired;
			}
			else
			{
				// Try sliding along X only, then Y only
				var slideX = WorldPosition + new Vector3( step.x, 0f, 0f );
				var slideY = WorldPosition + new Vector3( 0f, step.y, 0f );
				if ( !TreeManager.IsTreeAtWorldPos( slideX.x, slideX.y ) )
					WorldPosition = slideX;
				else if ( !TreeManager.IsTreeAtWorldPos( slideY.x, slideY.y ) )
					WorldPosition = slideY;
				// Fully blocked — don't move this frame
			}
		}
		WorldPosition = WorldPosition.WithZ( 0f );

		SeparateFromOtherEnemies();

		// Contact damage with cooldown
		_damageCooldown -= Time.Delta;
		if ( _damageCooldown <= 0f )
		{
			var dist = (Target.WorldPosition - WorldPosition).WithZ( 0f ).Length;
			// Player hitbox half-extent for enemies is 1 unit (tighter than world collision).
			// Contact damage fires at the same range the collision push-out resolves to.
			if ( dist < HalfExtent + 1f )
			{
				var playerState = Target.Components.Get<PlayerLocalState>();
				if ( playerState != null )
				{
					bool died = playerState.TakeDamage( ContactDamage );
					_damageCooldown = DamageCooldownDuration;
					if ( died )
						Target.Components.Get<PlayerStats>()?.Die();
				}
			}
		}
	}

	/// <summary>
	/// Pushes this enemy away from every other living enemy it overlaps with.
	/// Uses circular separation so enemies form a natural crowd rather than a grid.
	/// </summary>
	private void SeparateFromOtherEnemies()
	{
		var pos = WorldPosition.WithZ( 0f );

		foreach ( var other in Scene.GetAllComponents<EnemyBase>() )
		{
			if ( other == this || other.HP <= 0f ) continue;

			var diff    = pos - other.WorldPosition.WithZ( 0f );
			float minDist = HalfExtent + other.HalfExtent;
			float distSq  = diff.LengthSquared;

			if ( distSq < minDist * minDist && distSq > 0.001f )
			{
				float dist = MathF.Sqrt( distSq );
				// Push this enemy halfway out (the other will push the other half on its own turn)
				pos += diff / dist * (minDist - dist) * 0.5f;
			}
		}

		WorldPosition = pos;
	}

	public void TakeDamage( float amount, string weaponId = null )
	{
		if ( _dieTimer >= 0f ) return;

		// Roll for critical hit using the player's CritChance stat
		bool isCrit = false;
		var playerState = Target?.Components.Get<PlayerLocalState>();
		if ( playerState != null && playerState.CritChance > 0f )
		{
			float critChance = playerState.CritChance;
			float roll = System.Random.Shared.NextSingle();

			if ( critChance >= 1f )
			{
				// Overcrit: guaranteed crit + bonus damage from excess percentage
				isCrit = true;
				float overcritBonus = critChance - 1f;
				float multiplier = playerState.CritMultiplier + overcritBonus;
				amount *= multiplier;
			}
			else if ( roll < critChance )
			{
				isCrit = true;
				amount *= playerState.CritMultiplier;
			}
		}

		HP -= amount;

		if ( _renderer != null )
		{
			_renderer.Tint = Color.White;
			_flashTimer = 0.1f;
		}

		SpawnDamageIndicator( amount, isCrit );

		if ( HP <= 0f )
		{
			Die( weaponId );
		}
	}

	private void SpawnDamageIndicator( float amount, bool isCritical = false )
	{
		var pos = WorldPosition + new Vector3( 20f, (System.Random.Shared.NextSingle() - 0.5f) * 18f, 1f );
		DamageIndicatorManager.Instance?.RequestIndicator( pos, amount, isCritical );
	}

	private void Die( string weaponId = null )
	{
		Target?.Components.Get<PlayerStats>()?.AddKill( weaponId, XPValue );
		Target?.Components.Get<PlayerCoins>()?.AddCoins( 1 );
		SpawnXPGem();

		if ( _spriteRenderer != null && !string.IsNullOrEmpty( DieAnimation ) )
		{
			_spriteRenderer.PlayAnimation( DieAnimation );
			_spriteRenderer.PlaybackSpeed = 1f;
			_dieTimer = DieAnimDuration;
		}
		else
		{
			GameObject.Destroy();
		}
	}

	private void SpawnXPGem()
	{
		if ( Target == null ) return;

		var gemGo = new GameObject( true, "XPGem" );
		gemGo.WorldPosition = WorldPosition.WithZ( 0f );
		var gem = gemGo.Components.Create<XPGem>();
		gem.XPValue = XPValue;
		gem.PlayerObject = Target;
	}
}
