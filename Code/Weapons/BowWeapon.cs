/// <summary>
/// Fires a fast arrow at the nearest enemy.
/// Level 2+: +18% damage. Level 3+: fires 2 arrows in a tight spread.
/// Level 4+: arrows pierce through enemies. Level 5+: 3 arrows and +45% damage.
/// </summary>
public sealed class BowWeapon : WeaponBase
{
	protected override void OnStart()
	{
		base.OnStart();
		BaseCooldown = 0.9f;
	}

	protected override void OnFire()
	{
		var target = FindNearestEnemy();
		if ( target == null ) return;

		var baseDir = (target.WorldPosition - WorldPosition).WithZ( 0f ).Normal;
		int count = GetArrowCount() + _state.ProjectileCount;
		float spread = 8f;

		for ( int i = 0; i < count; i++ )
		{
			var dir = count > 1
				? Rotation.FromAxis( Vector3.Up, (i - (count - 1) * 0.5f) * spread ) * baseDir
				: baseDir;

			SpawnArrow( dir );
		}
	}

	private void SpawnArrow( Vector3 dir )
	{
		if ( _state == null ) return;

		var go = new GameObject( true, "Projectile_Arrow" );
		go.WorldPosition = WorldPosition;
		LocalGameRunner.ParentRuntimeObject( go );

		var proj = go.Components.Create<Projectile>();
		proj.Direction = dir;
		proj.Speed = (WeaponLevel >= 4 ? 320f : 260f) * _state.ProjectileSpeedMultiplier;
		proj.Damage = _state.Damage * GetDamageMultiplier();
		proj.Lifetime = 2.0f * _state.DurationMultiplier;
		proj.Piercing = WeaponLevel >= 4;
		proj.SpritePath = "sprites/arrow.sprite";
		proj.SpriteSize = 1f * _state.Area;
		proj.SourceWeaponId = WeaponId;
	}

	private int GetArrowCount() => WeaponLevel switch
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
		if ( WeaponLevel <= 3 ) return 1.18f;
		if ( WeaponLevel <= 4 ) return 1.3f;
		if ( WeaponLevel <= 5 ) return 1.45f;
		return 1.45f + (WeaponLevel - 5) * 0.035f;
	}

	public override string GetUpgradeDescription( int nextLevel ) => nextLevel switch
	{
		2 => "Damage: +18%",
		3 => "Fires 2 arrows in a tight spread",
		4 => "Arrows pierce through enemies, speed +18%",
		5 => "Fires 3 arrows, Damage: +45%",
		10 => "Fires 4 arrows",
		20 => "Fires 5 arrows",
		30 => "Fires 6 arrows",
		_ => $"Level {nextLevel}: +3.5% damage",
	};
}
