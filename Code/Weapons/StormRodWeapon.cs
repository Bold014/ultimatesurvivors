/// <summary>
/// Fires simultaneous projectiles at up to 3 nearby enemies.
/// Level 2+: increased damage. Level 3+: targets 4 enemies.
/// Level 4+: increased damage. Level 5+: targets 5 enemies, projectiles pierce.
/// Inspired by the Lightning Staff concept, renamed Storm Rod.
/// </summary>
public sealed class StormRodWeapon : WeaponBase
{
	protected override void OnStart()
	{
		base.OnStart();
		BaseCooldown = 1.4f;
	}

	protected override void OnFire()
	{
		if ( _state == null ) return;

		int maxTargets = GetMaxTargets();
		bool piercing = WeaponLevel >= 5;

		var myPos = WorldPosition;
		var targets = Scene.GetAllComponents<EnemyBase>()
			.OrderBy( e => (e.WorldPosition - myPos).LengthSquared )
			.Take( maxTargets );

		foreach ( var target in targets )
		{
			var dir = (target.WorldPosition - myPos).WithZ( 0f ).Normal;
			SpawnBolt( dir, piercing );
		}
	}

	private void SpawnBolt( Vector3 dir, bool piercing )
	{
		var go = new GameObject( true, "Projectile_StormRod" );
		go.WorldPosition = WorldPosition;

		var proj = go.Components.Create<Projectile>();
		proj.Direction = dir;
		proj.Speed = 420f;
		proj.Damage = _state.Damage * GetDamageMultiplier();
		proj.Lifetime = 1.8f;
		proj.Piercing = piercing;
		proj.TintColor = new Color( 0.6f, 1f, 1f );
		proj.SourceWeaponId = WeaponId;
	}

	private int GetMaxTargets() => WeaponLevel switch
	{
		>= 5 => 5,
		>= 3 => 4,
		_    => 3,
	};

	private float GetDamageMultiplier() => WeaponLevel switch
	{
		>= 4 => 1.25f,
		>= 2 => 1.05f,
		_    => 0.85f,
	};

	public override string GetUpgradeDescription( int nextLevel ) => nextLevel switch
	{
		2 => "Damage: +24%",
		3 => "Targets 1 additional enemy",
		4 => "Damage: +19%",
		5 => "Targets 1 more enemy, bolts pierce through",
		_ => $"Level {nextLevel}: improved stats",
	};
}
