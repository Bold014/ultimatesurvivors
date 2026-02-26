using System.Buffers;

/// <summary>
/// Renders a flat filled disc that fades from fully transparent at the center
/// to semi-opaque at the edge. Used as a subtle AOE radius indicator.
/// </summary>
public sealed class CircleRingRenderer : Component
{
	public float Radius { get; set; } = 100f;
	public Color Tint { get; set; } = Color.White;

	private AoeDiscSceneObj _disc;

	protected override void OnStart()
	{
		_disc = new AoeDiscSceneObj( Scene.SceneWorld );
		SyncDisc();
	}

	protected override void OnUpdate()
	{
		SyncDisc();
	}

	private void SyncDisc()
	{
		if ( _disc == null ) return;
		_disc.Radius = Radius;
		_disc.ColorTint = Tint;
		_disc.Transform = new Transform( WorldPosition, Rotation.Identity, 1f );
	}

	protected override void OnDestroy()
	{
		_disc?.Delete();
		_disc = null;
	}
}

/// <summary>
/// Draws a filled disc using a triangle fan. UVs go from 0 at the center
/// to 1 at the edge so the gradient texture creates a transparent-center effect.
/// </summary>
internal sealed class AoeDiscSceneObj : SceneCustomObject
{
	public float Radius { get; set; } = 100f;

	private const int Segments = 64;

	private static Material _sharedMat;
	private static Material SharedMat
	{
		get
		{
			if ( _sharedMat != null ) return _sharedMat;
			_sharedMat = Material.Load( "materials/sprite_2d.vmat" ).CreateCopy();
			_sharedMat.Set( "Texture", BuildGradientTexture() );
			return _sharedMat;
		}
	}

	/// <summary>
	/// 64x1 horizontal gradient: fully transparent at x=0 (center),
	/// ramping up with a power curve to ~75% opacity at x=1 (edge).
	/// </summary>
	private static Texture BuildGradientTexture()
	{
		const int W = 64;
		var pixels = new byte[W * 4];
		for ( int i = 0; i < W; i++ )
		{
			float t = i / (float)(W - 1);
			// cubic ease-in: slow start at center, visible near edge
			float alpha = t * t * t * 0.75f;
			int idx = i * 4;
			pixels[idx + 0] = 255;
			pixels[idx + 1] = 255;
			pixels[idx + 2] = 255;
			pixels[idx + 3] = (byte)(alpha * 255f);
		}
		return Texture.Create( W, 1 ).WithData( pixels ).Finish();
	}

	public AoeDiscSceneObj( SceneWorld world ) : base( world ) { }

	public override void RenderSceneObject()
	{
		// Triangle fan: center + Segments outer verts = Segments triangles = Segments*3 verts
		var verts = ArrayPool<Vertex>.Shared.Rent( Segments * 3 );
		int vi = 0;

		var center = new Vertex( Vector3.Zero );
		center.Normal = Vector3.Up;
		center.TexCoord0 = new Vector2( 0f, 0.5f ); // UV x=0 → transparent

		for ( int i = 0; i < Segments; i++ )
		{
			float a0 = (i / (float)Segments) * MathF.PI * 2f;
			float a1 = ((i + 1) / (float)Segments) * MathF.PI * 2f;

			var outer0 = new Vertex( new Vector3( MathF.Cos( a0 ) * Radius, MathF.Sin( a0 ) * Radius, 0f ) );
			outer0.Normal = Vector3.Up;
			outer0.TexCoord0 = new Vector2( 1f, 0.5f ); // UV x=1 → opaque

			var outer1 = new Vertex( new Vector3( MathF.Cos( a1 ) * Radius, MathF.Sin( a1 ) * Radius, 0f ) );
			outer1.Normal = Vector3.Up;
			outer1.TexCoord0 = new Vector2( 1f, 0.5f );

			verts[vi++] = center;
			verts[vi++] = outer0;
			verts[vi++] = outer1;
		}

		Graphics.Draw( verts, vi, SharedMat, Attributes );
		ArrayPool<Vertex>.Shared.Return( verts );

		Bounds = new BBox( new Vector3( -Radius, -Radius, -1f ), new Vector3( Radius, Radius, 1f ) )
			.Rotate( Rotation )
			.Translate( Position );
	}
}
