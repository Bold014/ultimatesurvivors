/// <summary>
/// A single rock shard that orbits its parent (the player) at a fixed radius.
/// Damages enemies it passes near. Part of the Orbital Shards weapon system.
/// </summary>
public sealed class OrbitalShard : Component
{
	/// <summary>Angle offset in degrees so multiple shards space evenly.</summary>
	public float AngleOffset { get; set; } = 0f;
	public float OrbitRadius { get; set; } = 80f;
	public float OrbitSpeed { get; set; } = 180f;
	public float Damage { get; set; } = 12f;
	/// <summary>Weapon that spawned this shard — used to attribute kills for quest tracking.</summary>
	public string SourceWeaponId { get; set; } = null;

	private float _currentAngle = 0f;
	private readonly HashSet<EnemyBase> _hitCooldowns = new();
	private float _hitResetTimer = 0f;
	private const float HitCooldown = 0.5f;

	protected override void OnStart()
	{
		var renderer = Components.Create<ModelRenderer>();
		renderer.Model = Model.Load( "models/dev/box.vmdl" );
		renderer.Tint = new Color( 0.7f, 0.55f, 0.3f );
		GameObject.WorldScale = new Vector3( 0.16f, 0.16f, 0.06f );
	}

	protected override void OnUpdate()
	{
		_currentAngle += OrbitSpeed * Time.Delta;

		var rad = MathF.PI / 180f * (_currentAngle + AngleOffset);
		LocalPosition = new Vector3(
			MathF.Cos( rad ) * OrbitRadius,
			MathF.Sin( rad ) * OrbitRadius,
			0f
		);

		// Reset hit cooldowns periodically so the same enemy can be hit again
		_hitResetTimer += Time.Delta;
		if ( _hitResetTimer >= HitCooldown )
		{
			_hitCooldowns.Clear();
			_hitResetTimer = 0f;
		}

		CheckEnemyHits();
	}

	private void CheckEnemyHits()
	{
		foreach ( var enemy in Scene.GetAllComponents<EnemyBase>() )
		{
			if ( _hitCooldowns.Contains( enemy ) ) continue;

			var dist = (enemy.WorldPosition - WorldPosition).WithZ( 0f ).Length;
			if ( dist < 20f )
			{
				enemy.TakeDamage( Damage, SourceWeaponId, WorldPosition );
				_hitCooldowns.Add( enemy );
			}
		}
	}
}
