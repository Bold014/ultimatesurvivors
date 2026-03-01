/// <summary>
/// Boomerang-style axe projectile.
/// Phase 1 (Outward): travels in the thrown direction for TravelTime seconds.
/// Phase 2 (Returning): steers back toward the player each frame, destroys on arrival.
/// The sprite spins continuously throughout both phases.
/// Non-piercing: stops on first hit outward then returns; destroys on first hit returning.
/// Piercing: slices through all enemies in both directions.
/// </summary>
public sealed class AxeProjectile : Component
{
	public Vector3 ThrowDirection { get; set; }
	public float Speed { get; set; } = 280f;
	public float Damage { get; set; } = 10f;
	/// <summary>How long (seconds) the axe travels outward before reversing.</summary>
	public float TravelTime { get; set; } = 1.0f;
	public bool Piercing { get; set; } = false;
	public string SourceWeaponId { get; set; }
	/// <summary>World-space size (width and height) of the sprite in game units.</summary>
	public float SpriteSize { get; set; } = 22f;

	private enum Phase { Outward, Returning }
	private Phase _phase = Phase.Outward;
	private float _timeAlive = 0f;
	private Vector3 _prevPosition;
	private Vector3 _currentDir;

	private readonly HashSet<EnemyBase> _outwardHit = new();
	private readonly HashSet<EnemyBase> _returnHit = new();

	private SpriteRenderer _spriteRenderer;
	private float _spinAngle = 0f;
	private const float SpinSpeed = 540f; // degrees per second (~1.5 full rotations/sec)

	protected override void OnStart()
	{
		_currentDir = ThrowDirection.WithZ( 0f ).Normal;
		_prevPosition = WorldPosition.WithZ( 0f );
		SetupSprite();
	}

	private void SetupSprite()
	{
		var sprite = ResourceLibrary.Get<Sprite>( "ui/weapons/axe.sprite" );
		if ( sprite != null )
		{
			_spriteRenderer = Components.Create<SpriteRenderer>();
			_spriteRenderer.Sprite = sprite;
			_spriteRenderer.Billboard = SpriteRenderer.BillboardMode.None;
			_spriteRenderer.TextureFilter = Sandbox.Rendering.FilterMode.Point;
			_spriteRenderer.Size = new Vector2( SpriteSize, SpriteSize );
		}
		else
		{
			var renderer = Components.Create<ModelRenderer>();
			renderer.Model = Model.Load( "models/dev/box.vmdl" );
			renderer.Tint = new Color( 1f, 0.55f, 0.1f );
			GameObject.WorldScale = new Vector3( 0.16f, 0.16f, 0.04f );
		}
	}

	protected override void OnUpdate()
	{
		_timeAlive += Time.Delta;

		// Spin the sprite: lie flat relative to the top-down camera (-90° around Y),
		// then rotate around world Up so it visually spins like a thrown axe.
		_spinAngle = (_spinAngle + SpinSpeed * Time.Delta) % 360f;
		var faceUp = Rotation.FromAxis( new Vector3( 0f, 1f, 0f ), -90f );
		var spin = Rotation.FromAxis( Vector3.Up, _spinAngle );
		WorldRotation = spin * faceUp;

		if ( _phase == Phase.Outward && _timeAlive >= TravelTime )
			_phase = Phase.Returning;

		if ( _phase == Phase.Outward )
		{
			_currentDir = ThrowDirection.WithZ( 0f ).Normal;
		}
		else
		{
			var player = Scene.GetAllComponents<PlayerController>().FirstOrDefault();
			if ( player == null ) { GameObject.Destroy(); return; }

			var toPlayer = (player.WorldPosition - WorldPosition).WithZ( 0f );
			if ( toPlayer.Length < 10f ) { GameObject.Destroy(); return; }
			_currentDir = toPlayer.Normal;
		}

		_prevPosition = WorldPosition.WithZ( 0f );
		WorldPosition += _currentDir * Speed * Time.Delta;
		WorldPosition = WorldPosition.WithZ( 0f );

		CheckEnemyHits();
	}

	private void CheckEnemyHits()
	{
		var hitSet = _phase == Phase.Outward ? _outwardHit : _returnHit;
		var pos = WorldPosition.WithZ( 0f );

		foreach ( var enemy in Scene.GetAllComponents<EnemyBase>() )
		{
			if ( enemy.HP <= 0f || hitSet.Contains( enemy ) ) continue;

			if ( !SegmentIntersectsCircle( _prevPosition, pos, enemy.WorldPosition.WithZ( 0f ), enemy.ProjectileHitRadius, out _ ) )
				continue;

			var knockbackOrigin = pos - _currentDir * 50f;
			enemy.TakeDamage( Damage, SourceWeaponId, knockbackOrigin );
			hitSet.Add( enemy );

			if ( !Piercing )
			{
				if ( _phase == Phase.Outward )
				{
					// Hit something on the way out — start returning from here
					_phase = Phase.Returning;
					_timeAlive = TravelTime;
				}
				else
				{
					GameObject.Destroy();
				}
				return;
			}
		}
	}

	private static bool SegmentIntersectsCircle( Vector3 a, Vector3 b, Vector3 c, float r, out Vector3 closest )
	{
		var ab = (b - a).WithZ( 0f );
		var ac = (c - a).WithZ( 0f );
		float abLenSq = ab.LengthSquared;
		if ( abLenSq < 0.0001f )
		{
			closest = a.WithZ( 0f );
			return ac.Length <= r;
		}
		float t = MathF.Max( 0f, MathF.Min( 1f, Vector3.Dot( ac, ab ) / abLenSq ) );
		closest = (a + ab * t).WithZ( 0f );
		return ((c - closest).WithZ( 0f )).LengthSquared <= r * r;
	}
}
