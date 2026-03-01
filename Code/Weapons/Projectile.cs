/// <summary>
/// Local-only moving projectile. Damages the first enemy it touches (or all if piercing).
/// Never networked.
/// </summary>
public sealed class Projectile : Component
{
	[Property] public Vector3 Direction { get; set; } = Vector3.Forward;
	[Property] public float Speed { get; set; } = 320f;
	[Property] public float Damage { get; set; } = 10f;
	[Property] public float Lifetime { get; set; } = 3f;
	[Property] public bool Piercing { get; set; } = false;
	[Property] public Color TintColor { get; set; } = Color.Yellow;
	/// <summary>If set, renders a sprite instead of the dev box. Rotated to face Direction.</summary>
	[Property] public string SpritePath { get; set; }
	/// <summary>Uniform scale applied to the sprite GameObject. Tune per weapon.</summary>
	[Property] public float SpriteSize { get; set; } = 1f;
	/// <summary>Weapon that fired this projectile — used to attribute kills for quest tracking.</summary>
	public string SourceWeaponId { get; set; } = null;
	/// <summary>Nudge projectile toward enemy center at impact. Compensates for sprite padding — visible content often doesn't extend to full bounds.</summary>
	[Property] public float ImpactOverlap { get; set; } = 12f;
	private float _timeAlive = 0f;
	private Vector3 _prevPosition;
	private readonly HashSet<EnemyBase> _alreadyHit = new();
	private bool _destroyNextFrame;
	private bool _spriteSetup = false;
	private GameObject _spriteGo;
	private Rotation _spriteTargetRot;

	protected override void OnStart()
	{
		_prevPosition = WorldPosition.WithZ( 0f );
		if ( string.IsNullOrEmpty( SpritePath ) )
		{
			var renderer = Components.Create<ModelRenderer>();
			renderer.Model = Model.Load( "models/dev/box.vmdl" );
			renderer.Tint = TintColor;
			GameObject.WorldScale = new Vector3( 0.16f, 0.16f, 0.04f );
			_spriteSetup = true;
		}
	}

	private void SetupSprite()
	{
		var sprite = ResourceLibrary.Get<Sprite>( SpritePath );
		if ( sprite == null )
		{
			var renderer = Components.Create<ModelRenderer>();
			renderer.Model = Model.Load( "models/dev/box.vmdl" );
			renderer.Tint = Color.Yellow;
			GameObject.WorldScale = new Vector3( 0.16f, 0.16f, 0.04f );
			_spriteSetup = true;
			return;
		}

		_spriteGo = new GameObject( true, "ProjectileSprite" );
		_spriteGo.SetParent( GameObject );
		_spriteGo.LocalPosition = Vector3.Zero;
		_spriteGo.LocalScale = new Vector3( SpriteSize, SpriteSize, SpriteSize );

		// Compute and cache the world-space rotation once at spawn time.
		// faceUp: rotate -90° around Y so the sprite lies flat facing the top-down camera.
		// dirSpin: rotate around world Z to aim the arrow in its travel direction.
		// Subtract 90° from angleDeg because after faceUp the arrow tip points in +Y, not +X.
		float angleDeg = MathF.Atan2( Direction.y, Direction.x ) * (180f / MathF.PI) - 90f;
		var faceUp  = Rotation.FromAxis( new Vector3( 0f, 1f, 0f ), -90f );
		var dirSpin = Rotation.FromAxis( new Vector3( 0f, 0f, 1f ), angleDeg );
		_spriteTargetRot = dirSpin * faceUp;
		_spriteGo.WorldRotation = _spriteTargetRot;

		var sr = _spriteGo.Components.Create<SpriteRenderer>();
		sr.Sprite = sprite;
		sr.Billboard = SpriteRenderer.BillboardMode.None;
		sr.TextureFilter = Sandbox.Rendering.FilterMode.Point;

		_spriteSetup = true;
	}

	protected override void OnUpdate()
	{
		if ( _destroyNextFrame )
		{
			GameObject.Destroy();
			return;
		}

		if ( !_spriteSetup )
			SetupSprite();

		// Re-lock the sprite's world rotation every frame so nothing (billboard internals,
		// parent transform drift, etc.) can rotate it after it's been fired.
		if ( _spriteGo != null && _spriteGo.IsValid() )
			_spriteGo.WorldRotation = _spriteTargetRot;

		_timeAlive += Time.Delta;
		if ( _timeAlive >= Lifetime )
		{
			GameObject.Destroy();
			return;
		}

		_prevPosition = WorldPosition.WithZ( 0f );
		WorldPosition += Direction.Normal * Speed * Time.Delta;
		WorldPosition = WorldPosition.WithZ( 0f );

		// Check enemy hits first (swept collision) so we register hits before tree collision
		if ( CheckEnemyHits() )
			return;

		// Destroy on tree impact
		if ( TreeManager.IsTreeAtWorldPos( WorldPosition.x, WorldPosition.y ) )
		{
			GameObject.Destroy();
			return;
		}
	}

	/// <summary>Returns true if the projectile was destroyed (non-piercing hit).</summary>
	private bool CheckEnemyHits()
	{
		var pos = WorldPosition.WithZ( 0f );
		var prev = _prevPosition;

		foreach ( var enemy in Scene.GetAllComponents<EnemyBase>() )
		{
			if ( enemy.HP <= 0f || _alreadyHit.Contains( enemy ) ) continue;
			if ( !IsOnScreen( enemy.WorldPosition ) ) continue;

			float hitRadius = enemy.ProjectileHitRadius;
			var enemyPos = enemy.WorldPosition.WithZ( 0f );
			if ( SegmentIntersectsCircle( prev, pos, enemyPos, hitRadius, out Vector3 closest ) )
			{
				// Push enemy in projectile travel direction (knockback from point behind impact)
				var knockbackFrom = pos - Direction.Normal * 50f;
				enemy.TakeDamage( Damage, SourceWeaponId, knockbackFrom );
				_alreadyHit.Add( enemy );

				if ( !Piercing )
				{
					var toEnemy = (enemyPos - closest).WithZ( 0f );
					float dist = toEnemy.Length;
					WorldPosition = dist > 0.01f
						? closest + toEnemy.Normal * MathF.Min( ImpactOverlap, dist )
						: closest;
					_destroyNextFrame = true;
					return true;
				}
			}
		}
		return false;
	}

	/// <summary>True if the world position is within the orthographic camera view (enemy must be on screen to be hit).</summary>
	private bool IsOnScreen( Vector3 worldPos )
	{
		var player = Scene.GetAllComponents<PlayerController>().FirstOrDefault();
		if ( player == null ) return true;
		var center = player.WorldPosition.WithZ( 0f );
		const float halfHeight = 100f;
		const float halfWidth = 200f * (16f / 9f) * 0.5f;
		var p = worldPos.WithZ( 0f );
		return MathF.Abs( p.x - center.x ) <= halfWidth && MathF.Abs( p.y - center.y ) <= halfHeight;
	}

	/// <summary>True if the line segment from A to B intersects the circle at center C with radius r. closest is the impact point.</summary>
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
