/// <summary>
/// Fires a single projectile at the nearest enemy.
/// Level 2+: +30% damage. Level 3+: fires 2 projectiles in a spread.
/// Level 4+: faster projectiles and +60% damage. Level 5+: piercing and +80% damage.
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
		int count = (WeaponLevel >= 3 ? 2 : 1) + _state.ProjectileCount;
		float spread = 12f;

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

		var proj = go.Components.Create<Projectile>();
		proj.Direction = dir;
		proj.Speed = (WeaponLevel >= 4 ? 204f : 170f) * _state.ProjectileSpeedMultiplier;
		proj.Damage = _state.Damage * GetDamageMultiplier();
		proj.Lifetime = 2.5f * _state.DurationMultiplier;
		proj.Piercing = WeaponLevel >= 5;
		proj.TintColor = new Color( 0.5f, 0.8f, 1f );
		proj.SpritePath = "sprites/fireballcast.sprite";
		proj.SpriteSize = 1f;
		proj.SourceWeaponId = WeaponId;
	}

	private float GetDamageMultiplier() => WeaponLevel switch
	{
		>= 5 => 1.8f,
		>= 4 => 1.6f,
		>= 2 => 1.3f,
		_    => 1.0f,
	};

	public override string GetUpgradeDescription( int nextLevel ) => nextLevel switch
	{
		2 => "Damage: +30%",
		3 => "Fires 2 projectiles in a spread",
		4 => "Projectile speed +20%, Damage: +60%",
		5 => "Projectiles pierce through enemies",
		_ => $"Level {nextLevel}: improved stats",
	};
}
