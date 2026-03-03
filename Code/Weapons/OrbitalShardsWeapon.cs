/// <summary>
/// Spawns rock shards that orbit the player, damaging enemies on contact.
/// Level 3+: 3 shards. Level 5+: 4 shards. Orbit speed increases per level.
/// Inspired by the Chunkers concept, renamed Orbital Shards.
/// </summary>
public sealed class OrbitalShardsWeapon : WeaponBase
{
	private readonly List<GameObject> _shards = new();
	private int _lastSpawnedLevel = 0;
	private int _lastProjectileCount = -1;

	protected override void OnStart()
	{
		base.OnStart();
		// Orbital Shards doesn't use the cooldown fire loop — shards are always active.
		// Set a long cooldown so OnFire only triggers on level changes.
		BaseCooldown = 9999f;
		SpawnShards();
		_lastSpawnedLevel = WeaponLevel;
		_lastProjectileCount = _state != null ? _state.ProjectileCount : 0;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		// Rebuild shards whenever WeaponLevel or ProjectileCount changes
		int projCount = _state != null ? _state.ProjectileCount : 0;
		if ( WeaponLevel != _lastSpawnedLevel || projCount != _lastProjectileCount )
		{
			ClearShards();
			SpawnShards();
			_lastSpawnedLevel = WeaponLevel;
			_lastProjectileCount = projCount;
		}

		// Sync damage, orbit radius, and sprite size to current player state
		if ( _state != null )
		{
			float baseRadius = GetBaseOrbitRadius();
			float effectiveRadius = baseRadius * _state.Area;
			float effectiveSize = 10f * _state.Area;
			foreach ( var shardGo in _shards )
			{
				var shard = shardGo.Components.Get<OrbitalShard>();
				if ( shard != null )
				{
					shard.Damage = _state.Damage * GetDamageMultiplier();
					shard.OrbitRadius = effectiveRadius;
					shard.OrbitSpeed = GetOrbitSpeed();
					shard.ShardSize = effectiveSize;
				}
			}
		}
	}

	protected override void OnFire() { }

	private void SpawnShards()
	{
		int count = ShardCount();
		float angleStep = count > 0 ? 360f / count : 0f;

		for ( int i = 0; i < count; i++ )
		{
			var go = new GameObject( true, $"OrbitalShard_{i}" );
			go.SetParent( GameObject );
			go.LocalPosition = Vector3.Zero;

			var shard = go.Components.Create<OrbitalShard>();
			shard.AngleOffset = i * angleStep;
			shard.OrbitRadius = GetBaseOrbitRadius() * (_state?.Area ?? 1f);
			shard.OrbitSpeed = GetOrbitSpeed();
			shard.Damage = _state != null ? _state.Damage * GetDamageMultiplier() : 10f;
			shard.ShardSize = 10f * (_state?.Area ?? 1f);
			shard.SourceWeaponId = WeaponId;

			_shards.Add( go );
		}
	}

	private void ClearShards()
	{
		foreach ( var go in _shards )
			go.Destroy();
		_shards.Clear();
	}

	private int ShardCount()
	{
		int baseCount = WeaponLevel switch
		{
			>= 30 => 7,
			>= 20 => 6,
			>= 10 => 5,
			>= 5  => 4,
			>= 3  => 3,
			_     => 2
		};
		return baseCount + (_state != null ? _state.ProjectileCount : 0);
	}

	private float GetDamageMultiplier()
	{
		int clamped = Math.Min( WeaponLevel, 5 );
		int extra = Math.Max( 0, WeaponLevel - 5 );
		return 0.9f + clamped * 0.15f + extra * 0.04f;
	}

	private float GetBaseOrbitRadius()
	{
		int clamped = Math.Min( WeaponLevel, 5 );
		int extra = Math.Max( 0, WeaponLevel - 5 );
		return 35f + clamped * 5f + extra * 1.5f;
	}

	private float GetOrbitSpeed()
	{
		int clamped = Math.Min( WeaponLevel, 5 );
		int extra = Math.Max( 0, WeaponLevel - 5 );
		return 120f + clamped * 20f + extra * 6f;
	}

	protected override void OnDestroy()
	{
		ClearShards();
	}

	public override string GetUpgradeDescription( int nextLevel ) => nextLevel switch
	{
		2 => "Damage: +14%, faster orbit",
		3 => "Spawns a 3rd orbital shard",
		4 => "Damage: +11%, wider orbit radius",
		5 => "Spawns a 4th orbital shard, Damage: +10%",
		10 => "Spawns a 5th orbital shard",
		20 => "Spawns a 6th orbital shard",
		30 => "Spawns a 7th orbital shard",
		_ => $"Level {nextLevel}: +4% damage, faster orbit",
	};
}
