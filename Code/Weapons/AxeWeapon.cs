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
		LocalGameRunner.ParentRuntimeObject( go );

		var proj = go.Components.Create<AxeProjectile>();
		proj.ThrowDirection = dir;
		proj.Speed = 280f * _state.ProjectileSpeedMultiplier;
		proj.Damage = _state.Damage * GetDamageMultiplier();
		proj.TravelTime = 1.6f * _state.DurationMultiplier;
		proj.SpriteSize = 22f * _state.Area;
		proj.Piercing = WeaponLevel >= 4;
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
