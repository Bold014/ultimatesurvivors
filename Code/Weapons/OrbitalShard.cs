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
	/// <summary>World-space size of the shard sprite. Hit radius is derived from this.</summary>
	public float ShardSize { get; set; } = 10f;
	/// <summary>Scales the sprite-derived hit radius so collision matches visuals.</summary>
	public float HitboxScale { get; set; } = 1f;
	/// <summary>Weapon that spawned this shard — used to attribute kills for quest tracking.</summary>
	public string SourceWeaponId { get; set; } = null;

	private float _currentAngle = 0f;
	private readonly HashSet<EnemyBase> _hitCooldowns = new();
	private float _hitResetTimer = 0f;
	private const float HitCooldown = 0.5f;
	private SpriteRenderer _renderer;

	protected override void OnStart()
	{
		_renderer = Components.Create<SpriteRenderer>();
		_renderer.Sprite = ResourceLibrary.Get<Sprite>( "ui/weapons/orbitalshards.sprite" );
		_renderer.Size = new Vector2( ShardSize, ShardSize );
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

		if ( _renderer != null )
			_renderer.Size = new Vector2( ShardSize, ShardSize );

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
		// Use sprite size as the shard's collision radius so contact matches what the player sees.
		float shardHitRadius = MathF.Max( 0.5f, ShardSize * 0.5f * HitboxScale );
		foreach ( var enemy in Scene.GetAllComponents<EnemyBase>() )
		{
			if ( _hitCooldowns.Contains( enemy ) ) continue;

			var dist = (enemy.WorldPosition - WorldPosition).WithZ( 0f ).Length;
			// Shards are persistent contact damage, so use the enemy's core sprite bounds
			// (no extra projectile forgiveness radius) to avoid "far away" hits.
			float combinedRadius = shardHitRadius + enemy.HalfExtent;
			if ( dist <= combinedRadius )
			{
				enemy.TakeDamage( Damage, SourceWeaponId, WorldPosition, knockbackMultiplier: 5f );
				_hitCooldowns.Add( enemy );
			}
		}
	}
}
