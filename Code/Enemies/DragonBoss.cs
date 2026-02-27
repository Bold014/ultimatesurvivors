using System.Linq;
using SpriteTools;

/// <summary>
/// Final boss: multi-phase dragon with ground and aerial attacks.
/// Controls movement and animations; EnemyBase handles HP, damage, death.
/// </summary>
public sealed class DragonBoss : Component
{
	public static DragonBoss Instance { get; private set; }
	public float HPPercent => _enemy != null && _enemy.MaxHP > 0f ? _enemy.HP / _enemy.MaxHP : 0f;

	private EnemyBase _enemy;
	private GameObject _player;
	private SpriteRenderer _spriteRenderer;
	private SpriteComponent _spriteComponent;

	private enum State
	{
		FlyIn,
		Idle,
		Walking,
		MeleeAttack,
		FireBreath,
		Launch,
		Hovering,
		DiveBomb,
		FireBombDrop,
		Landing
	}

	private State _state = State.FlyIn;
	private float _stateTimer;
	private float _meleeCooldown;
	private float _fireBreathCooldown;
	private float _airPhaseCooldown;
	private float _diveBombCooldown;
	private float _fireBombCooldown;
	private string _lastDir = "down";
	private Vector3 _diveBombTarget;
	private float _fireBombIndex;
	private int _fireBombCount;
	private int _fireBombSpawned;
	private int _fireBreathZonesSpawned;

	private const float FlyInSpeed = 180f;
	private const float FlyInArriveDist = 140f;
	private const float WalkSpeed = 18f;
	private const float MeleeRange = 55f;
	private const float MeleeDamage = 70f;
	private const float MeleeDuration = 0.8f;
	private const float MeleeCooldownBase = 3f;
	private const float FireBreathDuration = 1.5f;
	private const float FireBreathCooldownBase = 6f;
	private const float FireBreathZoneCount = 8;
	private const float FireBreathZoneCountPhase3 = 12;
	private const float FireBreathConeDeg = 35f;
	private const float FireBreathConeDegPhase3 = 50f;
	private const float FireBreathZoneInterval = 0.15f;
	private const float LaunchDuration = 0.8f;
	private const float HoverDuration = 1.5f;
	private const float DiveBombWarnDuration = 0.8f;
	private const float DiveBombSpeed = 400f;
	private const float DiveBombDamage = 80f;
	private const float DiveBombRadius = 80f;
	private const float DiveBombCooldownBase = 8f;
	private const float FireBombCooldownBase = 6f;
	private const float FireBombCount = 4;
	private const float FireBombCountPhase3 = 7;
	private const float FireBombInterval = 0.5f;
	private const float FireBombWarnDuration = 0.5f;
	private const float AirPhaseCooldownBase = 12f;
	private const float BurnZoneDamage = 12f;
	private const float BurnZoneLifetime = 3f;
	private const float BurnZoneSizeScale = 1.5f;

	protected override void OnStart()
	{
		Instance = this;
		_enemy = Components.Get<EnemyBase>();
		_player = _enemy?.Target;
		if ( _player == null ) return;

		_enemy.DisableMovement = true;

		var spriteGo = GameObject.Children.FirstOrDefault( c => c.Name == "EnemySprite" );
		_spriteRenderer = spriteGo?.Components.Get<SpriteRenderer>();
		_spriteComponent = spriteGo?.Components.Get<SpriteComponent>();
		if ( _spriteRenderer != null )
			_spriteRenderer.PlayAnimation( "idledown" );
		else if ( _spriteComponent != null )
			_spriteComponent.PlayAnimation( "idledown" );

		_meleeCooldown = 0f;
		_fireBreathCooldown = 2f;
		_airPhaseCooldown = 0f;
		_diveBombCooldown = 0f;
		_fireBombCooldown = 0f;
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}

	protected override void OnUpdate()
	{
		if ( _enemy == null || _player == null || !_player.IsValid ) return;
		if ( _enemy.HP <= 0f ) return;

		var upgradeSystem = _player.Components.Get<UpgradeSystem>();
		if ( upgradeSystem?.IsShowingUpgrades == true ) return;

		int phase = GetPhase();
		float cooldownMult = phase >= 3 ? 0.5f : 1f;

		_stateTimer -= Time.Delta;
		_meleeCooldown -= Time.Delta;
		_fireBreathCooldown -= Time.Delta;
		_diveBombCooldown -= Time.Delta;
		_fireBombCooldown -= Time.Delta;
		if ( _state == State.Idle || _state == State.Walking )
			_airPhaseCooldown -= Time.Delta;

		var dir = (_player.WorldPosition - WorldPosition).WithZ( 0f );
		_lastDir = GetDirection( dir );

		switch ( _state )
		{
			case State.FlyIn:
				TickFlyIn( dir );
				break;
			case State.Idle:
				TickIdle( phase, cooldownMult, dir );
				break;
			case State.Walking:
				TickWalking( phase, cooldownMult, dir );
				break;
			case State.MeleeAttack:
				TickMeleeAttack( dir );
				break;
			case State.FireBreath:
				TickFireBreath( dir );
				break;
			case State.Launch:
				TickLaunch();
				break;
			case State.Hovering:
				TickHovering( phase, cooldownMult );
				break;
			case State.DiveBomb:
				TickDiveBomb();
				break;
			case State.FireBombDrop:
				TickFireBombDrop();
				break;
			case State.Landing:
				TickLanding();
				break;
		}
	}

	private int GetPhase()
	{
		if ( _enemy == null ) return 1;
		float pct = _enemy.HP / _enemy.MaxHP;
		if ( pct <= 0.33f ) return 3;
		if ( pct <= 0.66f ) return 2;
		return 1;
	}

	private static string GetDirection( Vector3 dir )
	{
		if ( dir.LengthSquared < 1f ) return "down";
		float ax = MathF.Abs( dir.x ), ay = MathF.Abs( dir.y );
		return ax > ay
			? (dir.x > 0 ? "right" : "left")
			: (dir.y > 0 ? "up" : "down");
	}

	private void PlayAnim( string baseName )
	{
		var anim = baseName + _lastDir;
		if ( _spriteRenderer != null )
			_spriteRenderer.PlayAnimation( anim );
		else if ( _spriteComponent != null )
			_spriteComponent.PlayAnimation( anim );
	}

	private void TickFlyIn( Vector3 dir )
	{
		PlayAnim( "fly" );

		float dist = dir.Length;
		if ( dist <= FlyInArriveDist )
		{
			_state = State.Walking;
			PlayAnim( "walk" );
			return;
		}

		var step = dir.Normal * FlyInSpeed * Time.Delta;
		var desired = WorldPosition + step;
		if ( !TreeManager.IsTreeAtWorldPos( desired.x, desired.y ) )
			WorldPosition = desired;
		else
		{
			var slideX = WorldPosition + new Vector3( step.x, 0f, 0f );
			var slideY = WorldPosition + new Vector3( 0f, step.y, 0f );
			if ( !TreeManager.IsTreeAtWorldPos( slideX.x, slideX.y ) )
				WorldPosition = slideX;
			else if ( !TreeManager.IsTreeAtWorldPos( slideY.x, slideY.y ) )
				WorldPosition = slideY;
		}
		WorldPosition = WorldPosition.WithZ( 0f );
	}

	private void TickIdle( int phase, float cooldownMult, Vector3 dir )
	{
		PlayAnim( "idle" );
		if ( _stateTimer > 0f ) return;
		_state = State.Walking;
	}

	private void TickWalking( int phase, float cooldownMult, Vector3 dir )
	{
		PlayAnim( "walk" );

		// Phase 2+: consider air phase
		if ( phase >= 2 && _airPhaseCooldown <= 0f )
		{
			_state = State.Launch;
			_stateTimer = LaunchDuration;
			PlayAnim( "launch" );
			_airPhaseCooldown = AirPhaseCooldownBase * cooldownMult;
			return;
		}

		// Melee if close
		float dist = dir.Length;
		if ( dist < MeleeRange && _meleeCooldown <= 0f )
		{
			_state = State.MeleeAttack;
			_stateTimer = MeleeDuration;
			PlayAnim( "melee" );
			_meleeCooldown = MeleeCooldownBase * cooldownMult;
			return;
		}

		// Fire breath at medium range
		if ( dist > MeleeRange * 0.5f && dist < 120f && _fireBreathCooldown <= 0f )
		{
			_state = State.FireBreath;
			_stateTimer = FireBreathDuration;
			_fireBreathZonesSpawned = 0;
			PlayAnim( "breath" );
			_fireBreathCooldown = FireBreathCooldownBase * cooldownMult;
			return;
		}

		// Move toward player
		if ( dir.LengthSquared > 1f )
		{
			var step = dir.Normal * WalkSpeed * Time.Delta;
			var desired = WorldPosition + step;
			if ( !TreeManager.IsTreeAtWorldPos( desired.x, desired.y ) )
				WorldPosition = desired;
			else
			{
				var slideX = WorldPosition + new Vector3( step.x, 0f, 0f );
				var slideY = WorldPosition + new Vector3( 0f, step.y, 0f );
				if ( !TreeManager.IsTreeAtWorldPos( slideX.x, slideX.y ) )
					WorldPosition = slideX;
				else if ( !TreeManager.IsTreeAtWorldPos( slideY.x, slideY.y ) )
					WorldPosition = slideY;
			}
		}
		WorldPosition = WorldPosition.WithZ( 0f );
	}

	private void TickMeleeAttack( Vector3 dir )
	{
		if ( _stateTimer > 0f ) return;

		// Deal damage in cone
		float angle = MathF.Atan2( dir.y, dir.x );
		float halfCone = MathF.PI * 0.4f;
		var playerPos = _player.WorldPosition.WithZ( 0f );
		var toPlayer = (playerPos - WorldPosition.WithZ( 0f )).Normal;
		float playerAngle = MathF.Atan2( toPlayer.y, toPlayer.x );
		float diff = MathF.Abs( playerAngle - angle );
		if ( diff > MathF.PI ) diff = MathF.PI * 2f - diff;
		if ( diff < halfCone && (playerPos - WorldPosition.WithZ( 0f )).Length < MeleeRange )
		{
			var playerState = _player.Components.Get<PlayerLocalState>();
			if ( playerState != null )
			{
				bool died = playerState.TakeDamage( MeleeDamage );
				if ( died )
					_player.Components.Get<PlayerStats>()?.Die();
			}
		}

		_state = State.Walking;
	}

	private void TickFireBreath( Vector3 dir )
	{
		// Spawn BurnZones staggered during the breath
		float elapsed = FireBreathDuration - _stateTimer;
		int zoneCount = GetPhase() >= 3 ? (int)FireBreathZoneCountPhase3 : (int)FireBreathZoneCount;
		float coneDeg = GetPhase() >= 3 ? FireBreathConeDegPhase3 : FireBreathConeDeg;
		int toSpawn = (int)(elapsed / FireBreathZoneInterval);
		while ( _fireBreathZonesSpawned < toSpawn && _fireBreathZonesSpawned < zoneCount )
		{
			_fireBreathZonesSpawned++;
			float baseAngle = MathF.Atan2( dir.y, dir.x );
			float spread = coneDeg * (MathF.PI / 180f);
			float t = (_fireBreathZonesSpawned - 0.5f) / zoneCount;
			float angle = baseAngle - spread * 0.5f + spread * t;
			float dist = 40f + _fireBreathZonesSpawned * 8f;
			var offset = new Vector3( MathF.Cos( angle ) * dist, MathF.Sin( angle ) * dist, 0f );
			SpawnBurnZone( WorldPosition + offset, _player );
		}

		if ( _stateTimer > 0f ) return;
		_fireBreathZonesSpawned = 0;
		_state = State.Walking;
	}

	private void TickLaunch()
	{
		if ( _stateTimer > 0f ) return;
		_state = State.Hovering;
		_stateTimer = HoverDuration;
		PlayAnim( "hover" );
	}

	private void TickHovering( int phase, float cooldownMult )
	{
		PlayAnim( "hover" );
		if ( _stateTimer > 0f ) return;

		// Pick dive bomb or fire bomb drop
		bool doDive = System.Random.Shared.NextSingle() < 0.5f;
		if ( doDive && _diveBombCooldown <= 0f )
		{
			_state = State.DiveBomb;
			_diveBombTarget = _player.WorldPosition.WithZ( 0f );
			_stateTimer = DiveBombWarnDuration;
			SpawnDiveBombWarning( _diveBombTarget );
			_diveBombCooldown = DiveBombCooldownBase * (GetPhase() >= 3 ? 0.5f : 1f);
		}
		else
		{
			_state = State.FireBombDrop;
			_fireBombIndex = 0f;
			_fireBombSpawned = 0;
			_fireBombCount = GetPhase() >= 3 ? (int)FireBombCountPhase3 : (int)FireBombCount;
			_fireBombCooldown = FireBombCooldownBase * (GetPhase() >= 3 ? 0.5f : 1f);
		}
	}

	private void TickDiveBomb()
	{
		PlayAnim( "fly" );

		if ( _stateTimer > 0f )
		{
			// Warning phase - stay in place
			return;
		}

		// Rush toward target
		var toTarget = (_diveBombTarget - WorldPosition.WithZ( 0f )).WithZ( 0f );
		float dist = toTarget.Length;
		if ( dist < 20f )
		{
			// Impact: spawn BurnZones in ring, deal AoE damage
			for ( int i = 0; i < 8; i++ )
			{
				float a = (i / 8f) * MathF.PI * 2f;
				var offset = new Vector3( MathF.Cos( a ) * 50f, MathF.Sin( a ) * 50f, 0f );
				SpawnBurnZone( _diveBombTarget + offset, _player );
			}

			var playerDist = (_player.WorldPosition - _diveBombTarget).WithZ( 0f ).Length;
			if ( playerDist < DiveBombRadius )
			{
				var playerState = _player.Components.Get<PlayerLocalState>();
				if ( playerState != null )
				{
					bool died = playerState.TakeDamage( DiveBombDamage );
					if ( died )
						_player.Components.Get<PlayerStats>()?.Die();
				}
			}

			_state = State.Landing;
			_stateTimer = 0.5f;
			PlayAnim( "idle" );
		}
		else
		{
			var step = toTarget.Normal * DiveBombSpeed * Time.Delta;
			WorldPosition = (WorldPosition + step).WithZ( 0f );
		}
	}

	private void TickFireBombDrop()
	{
		PlayAnim( "hover" );

		_fireBombIndex += Time.Delta;
		int nextBomb = (int)(_fireBombIndex / FireBombInterval);
		while ( _fireBombSpawned < nextBomb && _fireBombSpawned < _fireBombCount )
		{
			_fireBombSpawned++;
			var playerPos = _player.WorldPosition.WithZ( 0f );
			var offset = new Vector3(
				(System.Random.Shared.NextSingle() - 0.5f) * 60f,
				(System.Random.Shared.NextSingle() - 0.5f) * 60f,
				0f );
			var targetPos = playerPos + offset;
			SpawnFireBombWithWarning( targetPos );
		}

		if ( _fireBombIndex >= _fireBombCount * FireBombInterval + FireBombWarnDuration )
		{
			_state = State.Landing;
			_stateTimer = 0.5f;
			PlayAnim( "idle" );
		}
	}

	private void TickLanding()
	{
		if ( _stateTimer > 0f ) return;
		_state = State.Walking;
	}

	private void SpawnBurnZone( Vector3 pos, GameObject damageTarget )
	{
		var go = new GameObject( true, "DragonBurnZone" );
		go.WorldPosition = pos.WithZ( 0f );
		var zone = go.Components.Create<BurnZone>();
		zone.Damage = BurnZoneDamage;
		zone.Lifetime = BurnZoneLifetime;
		zone.SizeScale = BurnZoneSizeScale;
		zone.PulseInterval = 0.5f;
		zone.DamageTarget = damageTarget;
	}

	private void SpawnDiveBombWarning( Vector3 pos )
	{
		var go = new GameObject( true, "DiveBombWarning" );
		go.WorldPosition = pos.WithZ( 0f );
		var ring = go.Components.Create<CircleRingRenderer>();
		ring.Radius = DiveBombRadius * 0.5f;
		ring.Tint = new Color( 1f, 0.3f, 0.1f, 0.6f );
		go.Components.Create<DragonBombWarning>().Duration = DiveBombWarnDuration;
	}

	private void SpawnFireBombWithWarning( Vector3 pos )
	{
		var go = new GameObject( true, "FireBombWarning" );
		go.WorldPosition = pos.WithZ( 0f );
		var ring = go.Components.Create<CircleRingRenderer>();
		ring.Radius = 30f;
		ring.Tint = new Color( 1f, 0.4f, 0.1f, 0.5f );
		var warn = go.Components.Create<DragonBombWarning>();
		warn.Duration = FireBombWarnDuration;
		warn.SpawnPosition = pos;
		warn.DamageTarget = _player;
		warn.BurnZoneDamage = BurnZoneDamage;
		warn.BurnZoneLifetime = BurnZoneLifetime;
		warn.BurnZoneSizeScale = BurnZoneSizeScale;
	}
}

/// <summary>
/// Temporary warning indicator for dragon bomb attacks. Spawns BurnZone at SpawnPosition when done.
/// </summary>
public sealed class DragonBombWarning : Component
{
	public float Duration { get; set; } = 0.8f;
	public Vector3 SpawnPosition { get; set; }
	public GameObject DamageTarget { get; set; }
	public float BurnZoneDamage { get; set; } = 12f;
	public float BurnZoneLifetime { get; set; } = 3f;
	public float BurnZoneSizeScale { get; set; } = 1.5f;

	private float _elapsed;

	protected override void OnUpdate()
	{
		_elapsed += Time.Delta;
		if ( _elapsed >= Duration )
		{
			var go = new GameObject( true, "DragonBurnZone" );
			go.WorldPosition = SpawnPosition.WithZ( 0f );
			var zone = go.Components.Create<BurnZone>();
			zone.Damage = BurnZoneDamage;
			zone.Lifetime = BurnZoneLifetime;
			zone.SizeScale = BurnZoneSizeScale;
			zone.PulseInterval = 0.5f;
			zone.DamageTarget = DamageTarget;
			GameObject.Destroy();
		}
	}
}
