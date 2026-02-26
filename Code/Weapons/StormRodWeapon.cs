/// <summary>
/// Spawns lightning strikes on random enemies near the player. Appears quickly then disappears.
/// Scales with Quantity Tome (more bolts) and Size Tome (larger strike radius).
/// Level 2+: increased damage. Level 3+: targets 1 more enemy. Level 4+: increased damage.
/// Level 5+: targets 1 more enemy, larger strike radius.
/// </summary>
public sealed class StormRodWeapon : WeaponBase
{
	private const float MaxTargetRange = 120f;
	private const float BoltLifetime = 0.22f;
	private const float BoltRadius = 18f;

	protected override void OnStart()
	{
		base.OnStart();
		BaseCooldown = 1.2f;
	}

	protected override void OnFire()
	{
		if ( _state == null ) return;

		// Quantity Tome + weapon level: how many enemies to strike
		int maxTargets = GetMaxTargets() + _state.ProjectileCount;
		maxTargets = Math.Max( 1, maxTargets );

		// Find enemies near the player and pick random ones
		var myPos = WorldPosition.WithZ( 0f );
		var nearbyEnemies = Scene.GetAllComponents<EnemyBase>()
			.Where( e => e.HP > 0f )
			.Where( e => (e.WorldPosition.WithZ( 0f ) - myPos).LengthSquared <= MaxTargetRange * MaxTargetRange )
			.ToList();

		if ( nearbyEnemies.Count == 0 ) return;

		// Shuffle and take up to maxTargets
		var rng = System.Random.Shared;
		var shuffled = nearbyEnemies.OrderBy( _ => rng.Next() ).Take( maxTargets );

		float damage = _state.Damage * GetDamageMultiplier();
		float strikeRadius = BoltRadius * _state.Area * (WeaponLevel >= 5 ? 1.2f : 1f);

		foreach ( var enemy in shuffled )
		{
			SpawnLightning( enemy.WorldPosition.WithZ( 0f ), damage, strikeRadius );
		}
	}

	private void SpawnLightning( Vector3 position, float damage, float radius )
	{
		var go = new GameObject( true, "Lightning_StormRod" );
		go.WorldPosition = position;

		var strike = go.Components.Create<LightningStrike>();
		strike.Damage = damage;
		strike.Radius = radius;
		strike.Lifetime = BoltLifetime * (_state?.DurationMultiplier ?? 1f);
		strike.SourceWeaponId = WeaponId;
	}

	private int GetMaxTargets() => WeaponLevel switch
	{
		>= 5 => 3,
		>= 3 => 2,
		_    => 1,
	};

	private float GetDamageMultiplier() => WeaponLevel switch
	{
		>= 4 => 1.25f,
		>= 2 => 1.1f,
		_    => 0.9f,
	};

	public override string GetUpgradeDescription( int nextLevel ) => nextLevel switch
	{
		2 => "Damage: +22%",
		3 => "Targets 1 additional enemy",
		4 => "Damage: +14%",
		5 => "Targets 1 more enemy, strike radius +20%",
		_ => $"Level {nextLevel}: improved stats",
	};
}
