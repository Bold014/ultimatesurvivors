/// <summary>
/// A brief lightning strike at a fixed position. Damages enemies in radius on spawn,
/// shows a quick visual, then disappears. Used by Storm Rod for its circle of lightning.
/// </summary>
public sealed class LightningStrike : Component
{
	public float Damage { get; set; } = 10f;
	public float Radius { get; set; } = 18f;
	public float Lifetime { get; set; } = 0.25f;
	/// <summary>Weapon that created this strike — used to attribute kills for quest tracking.</summary>
	public string SourceWeaponId { get; set; } = null;

	private float _timeAlive = 0f;
	private CircleRingRenderer _ring;
	private bool _damageDealt = false;

	protected override void OnStart()
	{
		_ring = Components.Create<CircleRingRenderer>();
		_ring.Radius = Radius;
		_ring.Tint = new Color( 0.5f, 0.9f, 1f, 1f );
	}

	protected override void OnUpdate()
	{
		_timeAlive += Time.Delta;

		// Deal damage once on first frame
		if ( !_damageDealt )
		{
			_damageDealt = true;
			DamageEnemiesInRadius();
		}

		// Fade out quickly
		if ( _ring != null )
		{
			float fade = 1f - (_timeAlive / Lifetime);
			_ring.Tint = new Color( 0.5f, 0.9f, 1f, fade );
		}

		if ( _timeAlive >= Lifetime )
			GameObject.Destroy();
	}

	private void DamageEnemiesInRadius()
	{
		foreach ( var enemy in Scene.GetAllComponents<EnemyBase>() )
		{
			var dist = (enemy.WorldPosition - WorldPosition).WithZ( 0f ).Length;
			if ( dist <= Radius + enemy.HalfExtent )
				enemy.TakeDamage( Damage, SourceWeaponId, WorldPosition );
		}
	}
}
