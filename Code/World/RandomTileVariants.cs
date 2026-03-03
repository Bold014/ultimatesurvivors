using Sandbox;
using SpriteTools;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Streams the ground tilemap around the camera in chunks.
/// Variants (flowers, grass tufts) are placed in noise-based patches so they
/// form natural-looking clusters instead of uniform salt-and-pepper scatter.
/// </summary>
[Category( "World" )]
[Title( "Map Generator" )]
[Icon( "landscape" )]
public class RandomTileVariants : Component
{
	[Property] public TilesetComponent Tileset { get; set; }
	[Property] public int LayerIndex { get; set; } = 0;

	/// <summary>How many chunks ahead of the camera to keep generated (in chunk units).</summary>
	[Property] public int StreamChunkRadius { get; set; } = 6;

	/// <summary>Tiles per chunk side. Each chunk = ChunkSize² tiles generated in one frame.</summary>
	[Property] public int ChunkSize { get; set; } = 32;

	/// <summary>Atlas column of the base ground tile.</summary>
	[Property] public int GroundColumn { get; set; } = 4;

	/// <summary>Atlas row of the base ground tile.</summary>
	[Property] public int GroundRow { get; set; } = 0;

	/// <summary>
	/// Variant/decoration tiles scattered over the ground.
	/// Format: "col,row".
	/// </summary>
	[Property] public List<string> VariantPositions { get; set; } = new();

	/// <summary>Scale of the variant patch noise (tiles). Larger = bigger patches.</summary>
	[Property, Range( 4, 60 )] public float PatchNoiseScale { get; set; } = 8f;

	/// <summary>Noise threshold above which a patch is "active" (0–1). Higher = fewer patches.</summary>
	[Property, Range( 0f, 1f )] public float PatchThreshold { get; set; } = 0.58f;

	/// <summary>Variant chance (%) inside an active patch.</summary>
	[Property, Range( 0, 100 )] public int PatchVariantPercent { get; set; } = 35;

	/// <summary>Variant chance (%) outside patches (sparse single tiles).</summary>
	[Property, Range( 0, 100 )] public int SparseVariantPercent { get; set; } = 2;

	/// <summary>
	/// When true, no base tile is written — only variants are placed sparsely.
	/// Use for decoration overlay layers on top of the ground.
	/// </summary>
	[Property] public bool OverlayMode { get; set; } = false;

	// ── runtime state ─────────────────────────────────────────────────────────
	readonly HashSet<Vector2Int> _generatedChunks = new();
	TilesetComponent.Layer _layer;
	Guid _groundGuid;
	List<Guid> _variants = new();
	bool _ready;
	System.Random _rng;
	int _noiseSeed;
	const int GroundRotation = 0;

	protected override void OnStart()
	{
		if ( !Tileset.IsValid() )
			Tileset = GameObject.Components.Get<TilesetComponent>();

		if ( !Tileset.IsValid() )
		{
			Log.Warning( "[MapGen] Tileset property is not set and no TilesetComponent on this GameObject!" );
			return;
		}

		var layers = Tileset.Layers;
		if ( layers == null || LayerIndex >= layers.Count )
		{
			Log.Warning( $"[MapGen] LayerIndex {LayerIndex} out of range (count={layers?.Count ?? 0})." );
			return;
		}

		_layer = layers[LayerIndex];
		if ( _layer == null )
		{
			Log.Warning( $"[MapGen] Layer {LayerIndex} is null." );
			return;
		}

		var tilesetRes = _layer.TilesetResource;
		if ( tilesetRes == null )
		{
			Log.Warning( "[MapGen] Layer has no TilesetResource assigned." );
			return;
		}

		_layer.Height = -1f;

		var posToGuid = tilesetRes.Tiles.ToDictionary( t => t.Position, t => t.Id );

		if ( !OverlayMode )
		{
			var groundPos = new Vector2Int( GroundColumn, GroundRow );
			if ( !posToGuid.TryGetValue( groundPos, out _groundGuid ) )
			{
				Log.Warning( $"[MapGen] Ground tile not found at ({GroundColumn},{GroundRow})." );
				return;
			}
		}

		_variants = new List<Guid>();
		foreach ( var s in VariantPositions )
		{
			var parts = s.Split( ',' );
			if ( parts.Length >= 2 &&
			     int.TryParse( parts[0].Trim(), out int col ) &&
			     int.TryParse( parts[1].Trim(), out int row ) )
			{
				var vp = new Vector2Int( col, row );
				if ( posToGuid.TryGetValue( vp, out var vg ) )
					_variants.Add( vg );
				else
					Log.Warning( $"[MapGen] Variant ({col},{row}) not in tileset — skipped." );
			}
		}

		int seed = (int)(System.DateTime.UtcNow.Ticks & 0x7FFFFFFF);
		_rng = new System.Random( seed );
		_noiseSeed = _rng.Next();

		Log.Info( $"[MapGen] Ready. seed={seed} groundTile=({GroundColumn},{GroundRow}) variants={_variants.Count}" );
		_ready = true;
	}

	Vector3 GetTrackingPosition()
	{
		var player = Scene.GetAllComponents<PlayerController>().FirstOrDefault();
		if ( player is not null ) return player.WorldPosition;
		return Scene.Camera?.WorldPosition ?? Vector3.Zero;
	}

	protected override void OnUpdate()
	{
		if ( !_ready || _layer == null ) return;

		var tileSizeF = _layer.TilesetResource?.GetTileSize() ?? new Vector2( 16, 16 );
		var tileSize = new Vector2Int( (int)tileSizeF.x, (int)tileSizeF.y );
		if ( tileSize.x <= 0 || tileSize.y <= 0 ) return;

		var trackPos = GetTrackingPosition();
		int centerChunkX = (int)MathF.Floor( trackPos.x / ( tileSize.x * ChunkSize ) );
		int centerChunkY = (int)MathF.Floor( trackPos.y / ( tileSize.y * ChunkSize ) );

		Vector2Int? closest = null;
		int closestDistSq = int.MaxValue;

		for ( int dx = -StreamChunkRadius; dx <= StreamChunkRadius; dx++ )
		{
			for ( int dy = -StreamChunkRadius; dy <= StreamChunkRadius; dy++ )
			{
				var chunk = new Vector2Int( centerChunkX + dx, centerChunkY + dy );
				if ( _generatedChunks.Contains( chunk ) ) continue;

				int distSq = dx * dx + dy * dy;
				if ( distSq < closestDistSq )
				{
					closestDistSq = distSq;
					closest = chunk;
				}
			}
		}

		if ( closest.HasValue )
		{
			GenerateChunk( closest.Value );
			_generatedChunks.Add( closest.Value );
		}
	}

	void GenerateChunk( Vector2Int chunk )
	{
		int startX = chunk.x * ChunkSize;
		int startY = chunk.y * ChunkSize;
		bool useVariants = _variants.Count > 0;

		for ( int x = startX; x < startX + ChunkSize; x++ )
		{
			for ( int y = startY; y < startY + ChunkSize; y++ )
			{
				var mapPos = new Vector2Int( x, y );

				if ( OverlayMode )
				{
					// Only place a decoration tile; leave everything else empty
					if ( useVariants )
					{
						float patchNoise = SmoothNoise( x, y, _noiseSeed, PatchNoiseScale );
						bool inPatch = patchNoise > PatchThreshold;
						int chance = inPatch ? PatchVariantPercent : SparseVariantPercent;

						if ( _rng.Next( 100 ) < chance )
						{
							int vi = (int)(Math.Abs( (long)x * 31 + (long)y * 17 ) % _variants.Count);
							_layer.SetTile( mapPos, _variants[vi], Vector2Int.Zero, GroundRotation, rebuild: false );
						}
					}
				}
				else
				{
					// Normal mode: fill every tile with ground or variant
					Guid tileGuid = _groundGuid;
					int tileAngle = GroundRotation;

					if ( useVariants )
					{
						// Noise-based patch detection
						float patchNoise = SmoothNoise( x, y, _noiseSeed, PatchNoiseScale );
						bool inPatch = patchNoise > PatchThreshold;
						int chance = inPatch ? PatchVariantPercent : SparseVariantPercent;

						if ( _rng.Next( 100 ) < chance )
						{
							// Pick variant based on position hash for determinism within a tile
							int vi = (int)(Math.Abs( (long)x * 31 + (long)y * 17 ) % _variants.Count);
							tileGuid = _variants[vi];
						}
					}

					_layer.SetTile( mapPos, tileGuid, Vector2Int.Zero, tileAngle, rebuild: false );
				}
			}
		}

		Tileset.IsDirty = true;
	}

	// ── Smooth value noise ────────────────────────────────────────────────

	static float SmoothNoise( int px, int py, int seed, float scale )
	{
		float x = px / scale;
		float y = py / scale;
		int ix = (int)MathF.Floor( x );
		int iy = (int)MathF.Floor( y );
		float fx = x - ix;
		float fy = y - iy;

		fx = fx * fx * (3f - 2f * fx);
		fy = fy * fy * (3f - 2f * fy);

		float v00 = HashFloat( ix,     iy,     seed );
		float v10 = HashFloat( ix + 1, iy,     seed );
		float v01 = HashFloat( ix,     iy + 1, seed );
		float v11 = HashFloat( ix + 1, iy + 1, seed );

		return v00 + (v10 - v00) * fx
		           + (v01 - v00) * fy
		           + (v00 - v10 - v01 + v11) * fx * fy;
	}

	static float HashFloat( int x, int y, int seed )
	{
		unchecked
		{
			int h = seed + x * 374761393 + y * 668265263;
			h = (h ^ (h >> 13)) * 1274126177;
			h ^= h >> 16;
			return (float)(uint)h / (float)uint.MaxValue;
		}
	}
}
