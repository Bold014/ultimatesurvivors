/// <summary>
/// Fires a single projectile at the nearest enemy.
/// Level 2+: +20% damage. Level 3+: fires 2 projectiles in a spread.
/// Level 4+: faster projectiles and +45% damage. Level 5+: piercing and +60% damage.
/// Fireballs now create a small, short-lived burn burst on impact.
/// </summary>
public sealed class MagicWand : WeaponBase
{
	protected override void OnStart()
	{
		base.OnStart();
		BaseCooldown = 1.2f;
	}

	protected override void OnFire()
	{
		var target = FindNearestEnemy();
		if ( target == null ) return;

		var baseDir = (target.WorldPosition - WorldPosition).WithZ( 0f ).Normal;
		int count = GetProjectileCount() + _state.ProjectileCount;
		float spread = GetSpreadAngle();

		for ( int i = 0; i < count; i++ )
		{
			var dir = count > 1
				? Rotation.FromAxis( Vector3.Up, (i - (count - 1) * 0.5f) * spread ) * baseDir
				: baseDir;

			SpawnProjectile( dir );
		}
	}

	private void SpawnProjectile( Vector3 dir )
	{
		if ( _state == null ) return;

		var go = new GameObject( true, "Projectile_Wand" );
		go.WorldPosition = WorldPosition;
		LocalGameRunner.ParentRuntimeObject( go );

		var proj = go.Components.Create<Projectile>();
		proj.Direction = dir;
		proj.Speed = (WeaponLevel >= 4 ? 204f : 170f) * _state.ProjectileSpeedMultiplier;
		proj.Damage = _state.Damage * GetDamageMultiplier();
		proj.Lifetime = 2.5f * _state.DurationMultiplier;
		proj.Piercing = WeaponLevel >= 5;
		proj.ImpactBurnEnabled = true;
		proj.ImpactBurnSizeScale = GetImpactBurnSizeScale() * _state.Area;
		proj.ImpactBurnLifetime = GetImpactBurnLifetime() * _state.DurationMultiplier;
		proj.ImpactBurnPulseInterval = 0.8f;
		proj.ImpactBurnDamageMultiplier = GetImpactBurnDamageMultiplier();
		proj.TintColor = new Color( 0.5f, 0.8f, 1f );
		proj.SpritePath = "sprites/fireballcast.sprite";
		proj.SpriteSize = 1f * _state.Area;
		proj.SourceWeaponId = WeaponId;
	}

	private int GetProjectileCount() => WeaponLevel switch
	{
		>= 30 => 6,
		>= 20 => 5,
		>= 10 => 4,
		>= 5  => 3,
		>= 3  => 2,
		_     => 1,
	};

	private float GetDamageMultiplier()
	{
		if ( WeaponLevel <= 1 ) return 1.0f;
		if ( WeaponLevel <= 3 ) return 1.2f;
		if ( WeaponLevel <= 4 ) return 1.45f;
		if ( WeaponLevel <= 5 ) return 1.6f;
		return 1.6f + (WeaponLevel - 5) * 0.04f;
	}

	private float GetImpactBurnDamageMultiplier() => WeaponLevel switch
	{
		>= 5 => 0.22f,
		>= 4 => 0.18f,
		>= 3 => 0.16f,
		>= 2 => 0.14f,
		_    => 0.12f,
	};

	private float GetImpactBurnSizeScale() => WeaponLevel switch
	{
		>= 5 => 1.05f,
		>= 4 => 0.95f,
		>= 3 => 0.86f,
		_    => 0.75f,
	};

	private float GetImpactBurnLifetime() => WeaponLevel switch
	{
		>= 5 => 2.0f,
		>= 4 => 1.8f,
		_    => 1.6f,
	};

	private float GetSpreadAngle() => WeaponLevel switch
	{
		>= 5 => 22f,
		>= 4 => 18f,
		>= 3 => 15f,
		_    => 12f,
	};

	public override string GetUpgradeDescription( int nextLevel ) => nextLevel switch
	{
		2 => "Damage: +20%, impact burn now ticks slower",
		3 => "Fires 2 fireballs, wider spread, impact burst size +15%",
		4 => "Projectile speed +20%, Damage: +45%, impact burn damage +2%",
		5 => "Fires 3 fireballs, widest spread, piercing and stronger impact burst",
		10 => "Fires 4 fireballs",
		20 => "Fires 5 fireballs",
		30 => "Fires 6 fireballs",
		_ => $"Level {nextLevel}: +4% damage",
	};
}
