/// <summary>
/// Throws 1+ spinning axes at the nearest enemy.
/// Level 2+: increased damage. Level 3+: 2 axes in a radial spread.
/// Level 4+: axes pierce through enemies. Level 5+: 3 axes, max damage.
/// </summary>
public sealed class AxeWeapon : WeaponBase
{
	protected override void OnStart()
	{
		base.OnStart();
		BaseCooldown = 2.0f;
	}

	protected override void OnFire()
	{
		var target = FindNearestEnemy();
		if ( target == null ) return;

		int axeCount = GetAxeCount() + _state.ProjectileCount;
		var baseDir = (target.WorldPosition - WorldPosition).WithZ( 0f ).Normal;

		for ( int i = 0; i < axeCount; i++ )
		{
			float angleOffset = axeCount > 1 ? i * (360f / axeCount) : 0f;
			var dir = Rotation.FromAxis( Vector3.Up, angleOffset ) * baseDir;
			SpawnAxe( dir );
		}
	}

	private void SpawnAxe( Vector3 dir )
	{
		if ( _state == null ) return;

		var go = new GameObject( true, "Projectile_Axe" );
		go.WorldPosition = WorldPosition;

		var proj = go.Components.Create<Projectile>();
		proj.Direction = dir;
		proj.Speed = 230f;
		proj.Damage = _state.Damage * GetDamageMultiplier();
		proj.Lifetime = 3.2f * _state.DurationMultiplier;
		proj.Piercing = WeaponLevel >= 4;
		proj.TintColor = new Color( 1f, 0.55f, 0.1f );
		proj.SourceWeaponId = WeaponId;
	}

	private int GetAxeCount() => WeaponLevel switch
	{
		>= 5 => 3,
		>= 3 => 2,
		_    => 1,
	};

	private float GetDamageMultiplier() => WeaponLevel switch
	{
		>= 5 => 2.8f,
		>= 4 => 2.4f,
		>= 3 => 2.0f,
		>= 2 => 2.0f,
		_    => 1.6f,
	};

	public override string GetUpgradeDescription( int nextLevel ) => nextLevel switch
	{
		2 => "Damage: +25%",
		3 => "Throws 2 axes in a radial spread",
		4 => "Axes pierce through enemies, Damage: +20%",
		5 => "Throws 3 axes, Damage: +17%",
		_ => $"Level {nextLevel}: improved stats",
	};
}
