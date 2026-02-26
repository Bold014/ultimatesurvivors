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
		float damage = _state.Damage * (0.4f + WeaponLevel * 0.25f);

		foreach ( var enemy in Scene.GetAllComponents<EnemyBase>() )
		{
			var dist = (enemy.WorldPosition - WorldPosition).WithZ( 0f ).Length;
			// Damage if touching the circle (enemy bounds overlap with circle edge)
			if ( dist <= radius + enemy.HalfExtent )
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

	private void UpdateCooldown()
	{
		BaseCooldown = WeaponLevel switch
		{
			>= 5 => 0.7f,
			>= 4 => 0.8f,
			>= 2 => 0.9f,
			_    => 1.0f,
		};
	}

	private float GetEffectiveRadius()
	{
		float baseRadius = 12f + WeaponLevel * 5f;
		return _state != null ? baseRadius * _state.Area : baseRadius;
	}

	public override string GetUpgradeDescription( int nextLevel ) => nextLevel switch
	{
		2 => "Damage: +25%, Pulse rate: +10%",
		3 => "Radius: +5, Damage: +25%",
		4 => "Radius: +5, Damage: +25%, Pulse rate: +11%",
		5 => "Radius: +5, Damage: +25%, Fastest pulse rate",
		_ => $"Level {nextLevel}: improved stats",
	};
}
