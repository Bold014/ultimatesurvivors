/// <summary>
/// A short-lived fire zone dropped on the ground.
/// Pulses damage to all enemies within its radius until it expires.
/// Created by EmberTrailWeapon.
/// </summary>
public sealed class BurnZone : Component
{
	public float Damage { get; set; } = 6f;
	public float Radius { get; set; } = 55f;
	public float Lifetime { get; set; } = 3f;
	public float PulseInterval { get; set; } = 0.5f;
	/// <summary>Weapon that created this zone — used to attribute kills for quest tracking.</summary>
	public string SourceWeaponId { get; set; } = null;

	private float _timeAlive = 0f;
	private float _pulseTimer = 0f;
	private CircleRingRenderer _ring;

	protected override void OnStart()
	{
		_ring = Components.Create<CircleRingRenderer>();
		_ring.Radius = Radius;
		_ring.Tint = new Color( 1f, 0.35f, 0.05f, 1f );
	}

	protected override void OnUpdate()
	{
		_timeAlive += Time.Delta;
		_pulseTimer += Time.Delta;

		// Fade out as lifetime expires
		if ( _ring != null )
		{
			float fade = 1f - (_timeAlive / Lifetime);
			_ring.Tint = new Color( 1f, 0.35f, 0.05f, fade );
		}

		if ( _pulseTimer >= PulseInterval )
		{
			_pulseTimer = 0f;
			DamageEnemiesInRadius();
		}

		if ( _timeAlive >= Lifetime )
			GameObject.Destroy();
	}

	private void DamageEnemiesInRadius()
	{
		foreach ( var enemy in Scene.GetAllComponents<EnemyBase>() )
		{
			var dist = (enemy.WorldPosition - WorldPosition).WithZ( 0f ).Length;
			if ( dist <= Radius )
				enemy.TakeDamage( Damage, SourceWeaponId );
		}
	}
}
