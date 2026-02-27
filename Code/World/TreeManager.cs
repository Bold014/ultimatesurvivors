using Sandbox;
using SpriteTools;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Streams tree tiles around the player using noise-based forest clustering.
/// Produces natural-looking forest blobs with dense cores, soft edges, and
/// open clearings rather than uniform random scatter.
/// </summary>
[Category( "World" )]
[Title( "Tree Manager" )]
[Icon( "forest" )]
public class TreeManager : Component
{
	[Property] public TilesetComponent TreeTileset { get; set; }
	[Property] public int LayerIndex { get; set; } = 0;

	/// <summary>Must match the StreamChunkRadius used by RandomTileVariants.</summary>
	[Property] public int StreamChunkRadius { get; set; } = 6;

	/// <summary>Must match the ChunkSize used by RandomTileVariants.</summary>
	[Property] public int ChunkSize { get; set; } = 32;

	/// <summary>Scale of the forest cluster noise (tiles). Larger = bigger blobs.</summary>
	[Property, Range( 4, 80 )] public float ForestNoiseScale { get; set; } = 10f;

	/// <summary>Noise value above which a tile is inside a forest cluster (0–1). Higher = fewer/smaller clusters.</summary>
	[Property, Range( 0f, 1f )] public float ForestThreshold { get; set; } = 0.38f;

	/// <summary>Tree fill % inside the dense core of a forest cluster.</summary>
	[Property, Range( 0, 100 )] public int ForestDensity { get; set; } = 45;

	/// <summary>Tree fill % on the soft edge of a forest cluster.</summary>
	[Property, Range( 0, 100 )] public int EdgeDensity { get; set; } = 10;

	/// <summary>Tree fill % in open fields (sparse stragglers only).</summary>
	[Property, Range( 0, 100 )] public int OpenDensity { get; set; } = 2;

	// ── public static state ───────────────────────────────────────────────
	/// <summary>Tile positions (in tile coordinates) that contain a tree.</summary>
	public static readonly HashSet<Vector2Int> TreeTiles = new();

	/// <summary>World X extent of one tree tile.</summary>
	public const int TileWorldWidth = 32;

	/// <summary>World Y extent of one tree tile.</summary>
	public const int TileWorldHeight = 16;

	public static bool IsTreeAtWorldPos( float x, float y )
	{
		int tx = (int)MathF.Floor( x / TileWorldWidth );
		int ty = (int)MathF.Floor( y / TileWorldHeight );
		return TreeTiles.Contains( new Vector2Int( tx, ty ) );
	}

	public static bool IsTreeAtTile( int tx, int ty ) => TreeTiles.Contains( new Vector2Int( tx, ty ) );

	// ── runtime state ─────────────────────────────────────────────────────
	readonly HashSet<Vector2Int> _generatedChunks = new();
	TilesetComponent.Layer _layer;
	Guid _treeGuid;
	bool _ready;
	System.Random _rng;
	int _noiseSeed;

	protected override void OnStart()
	{
		if ( !TreeTileset.IsValid() )
			TreeTileset = GameObject.Components.Get<TilesetComponent>();

		if ( !TreeTileset.IsValid() )
		{
			Log.Warning( "[TreeMgr] TreeTileset property not set and no TilesetComponent on this GameObject!" );
			return;
		}

		var layers = TreeTileset.Layers;
		if ( layers == null || LayerIndex >= layers.Count )
		{
			Log.Warning( $"[TreeMgr] LayerIndex {LayerIndex} out of range." );
			return;
		}

		_layer = layers[LayerIndex];
		if ( _layer == null ) { Log.Warning( "[TreeMgr] Layer is null." ); return; }

		var res = _layer.TilesetResource;
		if ( res == null ) { Log.Warning( "[TreeMgr] Layer has no TilesetResource." ); return; }

		_layer.Height = 0f;

		var tile = res.Tiles.FirstOrDefault();
		if ( tile == null ) { Log.Warning( "[TreeMgr] Tree tileset has no tiles." ); return; }
		_treeGuid = tile.Id;

		TreeTiles.Clear();

		int baseSeed = (int)(System.DateTime.UtcNow.Ticks & 0x7FFFFFFF);
		_rng = new System.Random( baseSeed );
		_noiseSeed = _rng.Next();

		_ready = true;
	}

	protected override void OnUpdate()
	{
		if ( !_ready || _layer == null ) return;

		var player = Scene.GetAllComponents<PlayerController>().FirstOrDefault();
		var trackPos = player?.WorldPosition ?? Scene.Camera?.WorldPosition ?? Vector3.Zero;

		var tileSizeF = _layer.TilesetResource?.GetTileSize() ?? new Vector2( TileWorldWidth, TileWorldHeight );
		float tw = tileSizeF.x;
		float th = tileSizeF.y;
		int centerChunkX = (int)MathF.Floor( trackPos.x / ( tw * ChunkSize ) );
		int centerChunkY = (int)MathF.Floor( trackPos.y / ( th * ChunkSize ) );

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

		// First pass: place trees using noise
		var candidates = new List<Vector2Int>();
		for ( int x = startX; x < startX + ChunkSize; x++ )
		{
			for ( int y = startY; y < startY + ChunkSize; y++ )
			{
				// Two noise octaves: large shape + fine detail
				float clusterNoise = SmoothNoise( x, y, _noiseSeed, ForestNoiseScale );
				float detailNoise  = SmoothNoise( x, y, _noiseSeed + 7919, ForestNoiseScale * 0.35f );
				float combined = clusterNoise * 0.72f + detailNoise * 0.28f;

				int chance;
				if ( combined > ForestThreshold )
					chance = ForestDensity;
				else if ( combined > ForestThreshold - 0.13f )
					chance = EdgeDensity;
				else
					chance = OpenDensity;

				if ( chance > 0 && _rng.Next( 100 ) < chance )
					candidates.Add( new Vector2Int( x, y ) );
			}
		}

		// Second pass: remove isolated trees (0 or 1 neighbours within the chunk+border)
		// Only clean up inside the chunk; border tiles may connect to adjacent chunks
		foreach ( var pos in candidates )
		{
			bool isEdge = pos.x == startX || pos.x == startX + ChunkSize - 1
			           || pos.y == startY || pos.y == startY + ChunkSize - 1;
			if ( isEdge )
			{
				// Keep edge tiles — they may join a neighbour chunk's cluster
				PlaceTree( pos );
				continue;
			}

			int neighbours = CountNeighbours( pos, candidates );
			if ( neighbours >= 1 )
				PlaceTree( pos );
			// Completely isolated single tiles are dropped
		}

		TreeTileset.IsDirty = true;
	}

	void PlaceTree( Vector2Int tilePos )
	{
		_layer.SetTile( tilePos, _treeGuid, Vector2Int.Zero, 0, rebuild: false );
		TreeTiles.Add( tilePos );
	}

	static int CountNeighbours( Vector2Int pos, List<Vector2Int> set )
	{
		int count = 0;
		for ( int dx = -1; dx <= 1; dx++ )
			for ( int dy = -1; dy <= 1; dy++ )
			{
				if ( dx == 0 && dy == 0 ) continue;
				if ( set.Contains( new Vector2Int( pos.x + dx, pos.y + dy ) ) )
					count++;
			}
		return count;
	}

	// ── Smooth value noise (bilinear interpolation of hashed lattice) ─────

	static float SmoothNoise( int px, int py, int seed, float scale )
	{
		float x = px / scale;
		float y = py / scale;
		int ix = (int)MathF.Floor( x );
		int iy = (int)MathF.Floor( y );
		float fx = x - ix;
		float fy = y - iy;

		// Smoothstep
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
