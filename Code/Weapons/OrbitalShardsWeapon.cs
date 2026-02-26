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

		// Sync damage and orbit radius to current player state (Area affects radius)
		if ( _state != null )
		{
			float baseRadius = 35f + WeaponLevel * 5f;
			float effectiveRadius = baseRadius * _state.Area;
			foreach ( var shardGo in _shards )
			{
				var shard = shardGo.Components.Get<OrbitalShard>();
				if ( shard != null )
				{
					shard.Damage = _state.Damage * (0.9f + WeaponLevel * 0.15f);
					shard.OrbitRadius = effectiveRadius;
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
			float baseRadius = 35f + WeaponLevel * 5f;
			shard.OrbitRadius = baseRadius * (_state?.Area ?? 1f);
			shard.OrbitSpeed = 120f + WeaponLevel * 20f;
			shard.Damage = _state != null ? _state.Damage * (0.9f + WeaponLevel * 0.15f) : 10f;
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
			>= 5 => 4,
			>= 3 => 3,
			_    => 2
		};
		return baseCount + (_state != null ? _state.ProjectileCount : 0);
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
		_ => $"Level {nextLevel}: improved stats",
	};
}
