/// <summary>
/// Fires a fast arrow at the nearest enemy.
/// Level 2+: +25% damage. Level 3+: fires 2 arrows in a tight spread.
/// Level 4+: arrows pierce through enemies. Level 5+: 3 arrows and +60% damage.
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
		int count = (WeaponLevel >= 5 ? 3 : WeaponLevel >= 3 ? 2 : 1) + _state.ProjectileCount;
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

		var proj = go.Components.Create<Projectile>();
		proj.Direction = dir;
		proj.Speed = WeaponLevel >= 4 ? 320f : 260f;
		proj.Damage = _state.Damage * GetDamageMultiplier();
		proj.Lifetime = 2.0f;
		proj.Piercing = WeaponLevel >= 4;
		proj.SpritePath = "sprites/arrow.sprite";
		proj.SpriteSize = 1f;
		proj.SourceWeaponId = WeaponId;
	}

	private float GetDamageMultiplier() => WeaponLevel switch
	{
		>= 5 => 1.6f,
		>= 4 => 1.4f,
		>= 2 => 1.25f,
		_    => 1.0f,
	};

	public override string GetUpgradeDescription( int nextLevel ) => nextLevel switch
	{
		2 => "Damage: +25%",
		3 => "Fires 2 arrows in a tight spread",
		4 => "Arrows pierce through enemies, speed +18%",
		5 => "Fires 3 arrows, Damage: +60%",
		_ => $"Level {nextLevel}: improved stats",
	};
}
