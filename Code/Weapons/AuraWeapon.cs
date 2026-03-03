/// <summary>
/// Pulses area damage around the player every few seconds.
/// Level 2+: faster pulses and more damage. Level 4+: largest area.
/// Scales with player's Area stat. Shows a visual ring as a child object.
/// </summary>
public sealed class AuraWeapon : WeaponBase
{
	private CircleRingRenderer _ring;
	private int _lastLevel = 0;

	protected override void OnStart()
	{
		base.OnStart();
		UpdateCooldown();

		_ring = Components.Create<CircleRingRenderer>();
		_ring.Tint = new Color( 0.6f, 0.2f, 1f, 1f );
	}

	protected override void OnFire()
	{
		if ( _state == null ) return;

		float radius = GetEffectiveRadius();
		float damage = _state.Damage * GetDamageMultiplier();

		foreach ( var enemy in Scene.GetAllComponents<EnemyBase>() )
		{
			var dist = (enemy.WorldPosition - WorldPosition).WithZ( 0f ).Length;
			// Use projectile hit radius so damage matches visible sprite bounds.
			if ( dist <= radius + enemy.ProjectileHitRadius )
				enemy.TakeDamage( damage, WeaponId, WorldPosition );
		}
	}

	protected override void OnUpdate()
	{
		if ( WeaponLevel != _lastLevel )
		{
			UpdateCooldown();
			_lastLevel = WeaponLevel;
		}

		base.OnUpdate();

		if ( _ring != null )
			_ring.Radius = GetEffectiveRadius();
	}

	private float GetDamageMultiplier()
	{
		int clamped = Math.Min( WeaponLevel, 5 );
		int extra = Math.Max( 0, WeaponLevel - 5 );
		return 0.4f + clamped * 0.25f + extra * 0.06f;
	}

	private void UpdateCooldown()
	{
		BaseCooldown = WeaponLevel switch
		{
			>= 30 => 0.5f,
			>= 20 => 0.55f,
			>= 10 => 0.6f,
			>= 5  => 0.7f,
			>= 4  => 0.8f,
			>= 2  => 0.9f,
			_     => 1.0f,
		};
	}

	private float GetEffectiveRadius()
	{
		int clamped = Math.Min( WeaponLevel, 5 );
		int extra = Math.Max( 0, WeaponLevel - 5 );
		float baseRadius = 12f + clamped * 5f + extra * 1.5f;
		return _state != null ? baseRadius * _state.Area : baseRadius;
	}

	public override string GetUpgradeDescription( int nextLevel ) => nextLevel switch
	{
		2 => "Damage: +25%, Pulse rate: +10%",
		3 => "Radius: +5, Damage: +25%",
		4 => "Radius: +5, Damage: +25%, Pulse rate: +11%",
		5 => "Radius: +5, Damage: +25%, Fastest pulse rate",
		10 => "Pulse rate: 0.6s",
		20 => "Pulse rate: 0.55s",
		30 => "Pulse rate: 0.5s (max)",
		_ => $"Level {nextLevel}: +6% damage, +1.5 radius",
	};
}
