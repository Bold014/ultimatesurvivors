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

	private float _timeAlive = 0f;
	private readonly HashSet<EnemyBase> _alreadyHit = new();
	private bool _spriteSetup = false;
	private GameObject _spriteGo;
	private Rotation _spriteTargetRot;

	protected override void OnStart()
	{
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
		if ( !_spriteSetup )
			SetupSprite();

		// Re-lock the sprite's world rotation every frame so nothing (billboard internals,
		// parent transform drift, etc.) can rotate it after it's been fired.
		if ( _spriteGo.IsValid() )
			_spriteGo.WorldRotation = _spriteTargetRot;

		_timeAlive += Time.Delta;
		if ( _timeAlive >= Lifetime )
		{
			GameObject.Destroy();
			return;
		}

		WorldPosition += Direction.Normal * Speed * Time.Delta;
		WorldPosition = WorldPosition.WithZ( 0f );

		// Destroy on tree impact
		if ( TreeManager.IsTreeAtWorldPos( WorldPosition.x, WorldPosition.y ) )
		{
			GameObject.Destroy();
			return;
		}

		CheckEnemyHits();
	}

	private void CheckEnemyHits()
	{
		foreach ( var enemy in Scene.GetAllComponents<EnemyBase>() )
		{
			if ( _alreadyHit.Contains( enemy ) ) continue;

			var dist = (enemy.WorldPosition - WorldPosition).WithZ( 0f ).Length;
			if ( dist < 20f )
			{
				enemy.TakeDamage( Damage, SourceWeaponId );
				_alreadyHit.Add( enemy );

				if ( !Piercing )
				{
					GameObject.Destroy();
					return;
				}
			}
		}
	}
}
