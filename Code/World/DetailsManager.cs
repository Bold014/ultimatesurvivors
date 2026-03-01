using Sandbox;
using SpriteTools;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Streams tree tiles around the player using noise-based forest clustering.
/// Uses only singletreetop and singletreebottom, layered in the same cell (transparent backgrounds
/// allow them to composite into a complete tree). Layer 0 = bottom, Layer 1 = top.
/// </summary>
[Category( "World" )]
[Title( "Details Manager" )]
[Icon( "park" )]
public class DetailsManager : Component
{
	[Property] public TilesetComponent DetailsTileset { get; set; }
	[Property] public int LayerIndex { get; set; } = 0;

	[Property] public int StreamChunkRadius { get; set; } = 6;
	[Property] public int ChunkSize { get; set; } = 32;

	/// <summary>Scale of the forest cluster noise. Larger = bigger blobs.</summary>
	[Property, Range( 4, 80 )] public float ForestNoiseScale { get; set; } = 10f;

	/// <summary>Noise value above which a tile is inside a forest cluster.</summary>
	[Property, Range( 0f, 1f )] public float ForestThreshold { get; set; } = 0.38f;
	/// <summary>Dense center fill chance (%) for forest blobs.</summary>
	[Property, Range( 0, 100 )] public int ForestCoreDensity { get; set; } = 66;
	/// <summary>Soft edge fill chance (%) near the forest boundary.</summary>
	[Property, Range( 0, 100 )] public int ForestEdgeDensity { get; set; } = 34;
	/// <summary>Sparse straggler fill chance (%) in open ground.</summary>
	[Property, Range( 0, 100 )] public int OpenFieldDensity { get; set; } = 2;
	/// <summary>How wide the soft edge band is around ForestThreshold.</summary>
	[Property, Range( 0.02f, 0.30f )] public float ForestEdgeBand { get; set; } = 0.14f;
	/// <summary>Post-process passes to smooth tiny holes/spikes in the forest mask.</summary>
	[Property, Range( 0, 3 )] public int ForestSmoothingPasses { get; set; } = 0;
	/// <summary>Higher values break forests into smaller groves (noise carve).</summary>
	[Property, Range( 4, 60 )] public float BreakupNoiseScale { get; set; } = 7f;
	/// <summary>Breakup noise threshold (higher = less breakup).</summary>
	[Property, Range( 0f, 1f )] public float BreakupThreshold { get; set; } = 0.60f;
	/// <summary>Chance (%) to remove a tree when breakup noise is active.</summary>
	[Property, Range( 0, 100 )] public int BreakupPercent { get; set; } = 45;
	/// <summary>Chance (%) for isolated single trees in open areas (no blob/caps).</summary>
	[Property, Range( 0, 100 )] public int SingleTreeDensity { get; set; } = 3;
	/// <summary>Scale of the single-tree scatter noise.</summary>
	[Property, Range( 4, 80 )] public float SingleTreeNoiseScale { get; set; } = 14f;
	/// <summary>Minimum tile gap from forest blobs for single-tree placement.</summary>
	[Property, Range( 1, 4 )] public int SingleTreeBlobGap { get; set; } = 1;

	/// <summary>Tile radius around world origin (0,0) guaranteed free of trees.</summary>
	[Property, Range( 1, 10 )] public int SpawnClearRadius { get; set; } = 3;

	/// <summary>Enable to log edge overflow placement and coordinate info to console.</summary>
	[Property] public bool DebugEdgeOverflow { get; set; } = false;

	/// <summary>If true: y+1=above, y-1=below (S&box Z-up). If false: y-1=above, y+1=below.</summary>
	[Property] public bool InvertY { get; set; } = true;

	/// <summary>Clears generated chunks so they regenerate next frame. Use after changing InvertY.</summary>
	public void ForceRegenerateChunks()
	{
		_generatedChunks.Clear();
		_topLayerOccupied.Clear();
		TreeManager.TreeTiles.Clear();
		if ( _layerBottom != null ) _layerBottom.Tiles?.Clear();
		if ( _layerTop != null ) _layerTop.Tiles?.Clear();
		if ( DetailsTileset != null ) DetailsTileset.IsDirty = true;
	}

	// ── runtime state ─────────────────────────────────────────────────────
	readonly HashSet<Vector2Int> _generatedChunks = new();
	readonly HashSet<Vector2Int> _topLayerOccupied = new();
	TilesetComponent.Layer _layerBottom;
	TilesetComponent.Layer _layerTop;
	Guid _singleTreeTop;    // singletreetop (0,4)
	Guid _singleTreeBottom; // singletreebottom (0,5)
	bool _ready;
	int _noiseSeed;
	const int TreeRotation = 0;

	protected override void OnStart()
	{
		if ( !DetailsTileset.IsValid() )
			DetailsTileset = GameObject.Components.Get<TilesetComponent>();

		if ( !DetailsTileset.IsValid() )
		{
			Log.Warning( "[DetailsMgr] DetailsTileset not set and no TilesetComponent on this GameObject!" );
			return;
		}

		var layers = DetailsTileset.Layers;
		if ( layers == null || layers.Count < 2 )
		{
			Log.Warning( "[DetailsMgr] Need at least 2 layers (bottom + top) for tree layering." );
			return;
		}

		_layerBottom = layers[LayerIndex];
		_layerTop = layers[LayerIndex + 1];
		if ( _layerBottom == null || _layerTop == null )
		{
			Log.Warning( "[DetailsMgr] Tree layers are null." );
			return;
		}

		var res = _layerBottom.TilesetResource;
		if ( res == null ) { Log.Warning( "[DetailsMgr] Layer has no TilesetResource." ); return; }

		_layerBottom.Height = 0f;
		_layerTop.Height = 0.001f; // Slightly above bottom so top draws on top
		// Ensure stale generated tiles from prior runs/configs are removed before streaming.
		_layerBottom.Tiles?.Clear();
		_layerTop.Tiles?.Clear();
		_topLayerOccupied.Clear();

		var posToGuid = res.Tiles.ToDictionary( t => t.Position, t => t.Id );

		if ( !posToGuid.TryGetValue( new Vector2Int( 0, 4 ), out _singleTreeTop ) ||
		     !posToGuid.TryGetValue( new Vector2Int( 0, 5 ), out _singleTreeBottom ) )
		{
			Log.Warning( "[DetailsMgr] singletreetop (0,4) or singletreebottom (0,5) missing from tileset." );
			return;
		}

		TreeManager.TreeTiles.Clear();
		_noiseSeed = (int)(DateTime.UtcNow.Ticks & 0x7FFFFFFF) | 1;
		_ready = true;
		Log.Info( $"[DetailsMgr] Ready (layered trees). seed={_noiseSeed}" );
	}

	protected override void OnUpdate()
	{
		if ( !_ready || _layerBottom == null ) return;

		var player = Scene.GetAllComponents<PlayerController>().FirstOrDefault();
		var trackPos = player?.WorldPosition ?? Scene.Camera?.WorldPosition ?? Vector3.Zero;

		var tileSizeF = _layerBottom.TilesetResource?.GetTileSize() ?? new Vector2( 16, 16 );

		int centerChunkX = (int)MathF.Floor( trackPos.x / ( tileSizeF.x * ChunkSize ) );
		int centerChunkY = (int)MathF.Floor( trackPos.y / ( tileSizeF.y * ChunkSize ) );

		Vector2Int? closest = null;
		int closestDistSq = int.MaxValue;

		for ( int dx = -StreamChunkRadius; dx <= StreamChunkRadius; dx++ )
		{
			for ( int dy = -StreamChunkRadius; dy <= StreamChunkRadius; dy++ )
			{
				var chunk = new Vector2Int( centerChunkX + dx, centerChunkY + dy );
				if ( _generatedChunks.Contains( chunk ) ) continue;

				int distSq = dx * dx + dy * dy;
				if ( distSq < closestDistSq ) { closestDistSq = distSq; closest = chunk; }
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

		// Build initial tree mask with border so edge caps can inspect neighbors outside the chunk.
		var treeZone = new HashSet<Vector2Int>();
		for ( int x = startX - 4; x <= startX + ChunkSize + 3; x++ )
			for ( int y = startY - 4; y <= startY + ChunkSize + 3; y++ )
				if ( ShouldPlaceTreeSeed( x, y ) )
					treeZone.Add( new Vector2Int( x, y ) );

		// Smooth the mask to reduce checker/noisy singles and create more natural forest blobs.
		for ( int pass = 0; pass < ForestSmoothingPasses; pass++ )
		{
			var smoothed = new HashSet<Vector2Int>( treeZone );
			for ( int x = startX - 4; x <= startX + ChunkSize + 3; x++ )
			{
				for ( int y = startY - 4; y <= startY + ChunkSize + 3; y++ )
				{
					if ( IsSpawnClear( x, y ) )
					{
						smoothed.Remove( new Vector2Int( x, y ) );
						continue;
					}

					var pos = new Vector2Int( x, y );
					bool hasTree = treeZone.Contains( pos );
					int neighbours = CountNeighbours8( treeZone, pos );

					// Remove tiny spikes/singletons, fill enclosed tiny holes.
					if ( hasTree && neighbours <= 1 )
						smoothed.Remove( pos );
					else if ( !hasTree && neighbours >= 5 )
						smoothed.Add( pos );
				}
			}

			treeZone = smoothed;
		}

		int treesInChunk = 0;
		var topLayerReserved = new HashSet<Vector2Int>();

		var aboveOffset = GetAboveOffset();
		for ( int x = startX - 1; x <= startX + ChunkSize; x++ )
		{
			for ( int y = startY + ChunkSize; y >= startY - 1; y-- )
			{
				var pos = new Vector2Int( x, y );
				if ( !treeZone.Contains( pos ) ) continue;

				var above = pos + aboveOffset;
				var below = pos - aboveOffset;

				bool hasTreeAbove = treeZone.Contains( above ) || TreeManager.IsTreeAtTile( above.x, above.y );
				bool hasTreeBelow = treeZone.Contains( below ) || TreeManager.IsTreeAtTile( below.x, below.y );
				bool hasTreeLeft = treeZone.Contains( new Vector2Int( pos.x - 1, pos.y ) ) || TreeManager.IsTreeAtTile( pos.x - 1, pos.y );
				bool hasTreeRight = treeZone.Contains( new Vector2Int( pos.x + 1, pos.y ) ) || TreeManager.IsTreeAtTile( pos.x + 1, pos.y );
				bool hasAnyNeighbor = hasTreeAbove || hasTreeBelow || hasTreeLeft || hasTreeRight;
				int neighbourCount = (hasTreeAbove ? 1 : 0) + (hasTreeBelow ? 1 : 0) + (hasTreeLeft ? 1 : 0) + (hasTreeRight ? 1 : 0);
				if ( !hasAnyNeighbor )
					continue;
				// Horizontal-only side protrusions (1-2 side-connected tiles, no vertical support)
				// should render as single trees, not blob caps. This avoids top/bottom swaps on blob sides.
				bool hasOnlyHorizontalConnections = !hasTreeAbove && !hasTreeBelow && (hasTreeLeft || hasTreeRight);
				if ( hasOnlyHorizontalConnections )
				{
					if ( !IsSpawnClear( above.x, above.y ) && CanWriteTopLayer( above, topLayerReserved ) )
					{
						treesInChunk++;
						_layerBottom.SetTile( pos, _singleTreeBottom, Vector2Int.Zero, TreeRotation, rebuild: false );
						_layerTop.SetTile( above, _singleTreeTop, Vector2Int.Zero, TreeRotation, rebuild: false );
						topLayerReserved.Add( above );
						_topLayerOccupied.Add( above );
						TreeManager.TreeTiles.Add( pos );
					}
					continue;
				}

				// Blob trees are rendered as the same stable vertical pair model as singles:
				// trunk in base cell + canopy one tile above.
				treesInChunk++;
				_layerBottom.SetTile( pos, _singleTreeBottom, Vector2Int.Zero, TreeRotation, rebuild: false );
				if ( !IsSpawnClear( above.x, above.y ) && CanWriteTopLayer( above, topLayerReserved ) )
				{
					_layerTop.SetTile( above, _singleTreeTop, Vector2Int.Zero, TreeRotation, rebuild: false );
					topLayerReserved.Add( above );
					_topLayerOccupied.Add( above );
				}
				TreeManager.TreeTiles.Add( pos );
			}
		}

		if ( DebugEdgeOverflow && treesInChunk > 0 )
		{
			Log.Info( $"[DetailsMgr] Chunk ({chunk.x},{chunk.y}): trees={treesInChunk} | InvertY={InvertY}" );
		}

		PlaceSingleTrees( startX, startY, treeZone );
		DetailsTileset.IsDirty = true;
	}

	bool ShouldPlaceTreeSeed( int x, int y )
	{
		if ( IsSpawnClear( x, y ) ) return false;

		float combined = CombinedForestNoise( x, y );

		int chance;
		if ( combined > ForestThreshold )
			chance = ForestCoreDensity;
		else if ( combined > ForestThreshold - ForestEdgeBand )
			chance = ForestEdgeDensity;
		else
			chance = OpenFieldDensity;

		if ( chance <= 0 ) return false;
		bool placed = HashPercent( x, y, _noiseSeed + 1337 ) < chance;
		if ( !placed ) return false;

		// Extra breakup pass: carve paths/clearings so large masks split into smaller groves.
		if ( BreakupPercent > 0 )
		{
			float breakupNoise = SmoothNoise( x, y, _noiseSeed + 17713, BreakupNoiseScale );
			if ( breakupNoise > BreakupThreshold && HashPercent( x, y, _noiseSeed + 22229 ) < BreakupPercent )
				return false;
		}

		return true;
	}

	void PlaceSingleTrees( int startX, int startY, HashSet<Vector2Int> treeZone )
	{
		if ( SingleTreeDensity <= 0 ) return;

		int endX = startX + ChunkSize;
		int endY = startY + ChunkSize;
		float isolatedLimit = ForestThreshold - ForestEdgeBand - 0.05f;
		var aboveOffset = GetAboveOffset();

		for ( int x = startX; x < endX; x++ )
		{
			for ( int y = startY; y < endY; y++ )
			{
				if ( IsSpawnClear( x, y ) ) continue;
				var pos = new Vector2Int( x, y );
				var above = pos + aboveOffset;

				// Never add singles where forest blobs already exist.
				if ( treeZone.Contains( pos ) || treeZone.Contains( above ) ) continue;
				if ( TreeManager.TreeTiles.Contains( pos ) || TreeManager.TreeTiles.Contains( above ) ) continue;

				// Keep singles away from blob edges and existing tree tiles.
				if ( HasNearbyTree( x, y, SingleTreeBlobGap ) ) continue;
				if ( CombinedForestNoise( x, y ) >= isolatedLimit ) continue;
				if ( IsSpawnClear( above.x, above.y ) ) continue;

				float scatter = SmoothNoise( x, y, _noiseSeed + 4241, SingleTreeNoiseScale );
				if ( scatter < 0.36f || scatter > 0.84f ) continue;
				if ( HashPercent( x, y, _noiseSeed + 9001 ) >= SingleTreeDensity ) continue;

				// Isolated tree pair: bottom in base cell, top in the above cell.
				_layerBottom.SetTile( pos, _singleTreeBottom, Vector2Int.Zero, TreeRotation, rebuild: false );
				if ( CanWriteTopLayer( above, null ) )
				{
					_layerTop.SetTile( above, _singleTreeTop, Vector2Int.Zero, TreeRotation, rebuild: false );
					_topLayerOccupied.Add( above );
				}
				TreeManager.TreeTiles.Add( pos );
			}
		}
	}

	bool CanWriteTopLayer( Vector2Int pos, HashSet<Vector2Int> chunkReserved ) =>
		!_topLayerOccupied.Contains( pos ) && (chunkReserved == null || !chunkReserved.Contains( pos ));

	Vector2Int GetAboveOffset()
	{
		// Tree parts are authored as vertical top/bottom pairs in tile Y.
		return InvertY ? new Vector2Int( 0, 1 ) : new Vector2Int( 0, -1 );
	}

	bool IsSpawnClear( int x, int y ) =>
		Math.Abs( x ) <= SpawnClearRadius && Math.Abs( y ) <= SpawnClearRadius;

	static int CountNeighbours8( HashSet<Vector2Int> set, Vector2Int pos )
	{
		int count = 0;
		for ( int dx = -1; dx <= 1; dx++ )
		{
			for ( int dy = -1; dy <= 1; dy++ )
			{
				if ( dx == 0 && dy == 0 ) continue;
				if ( set.Contains( new Vector2Int( pos.x + dx, pos.y + dy ) ) )
					count++;
			}
		}

		return count;
	}

	static bool HasNearbyTree( int x, int y, int radius )
	{
		for ( int dx = -radius; dx <= radius; dx++ )
		{
			for ( int dy = -radius; dy <= radius; dy++ )
			{
				if ( dx == 0 && dy == 0 ) continue;
				if ( TreeManager.IsTreeAtTile( x + dx, y + dy ) )
					return true;
			}
		}

		return false;
	}

	static int HashPercent( int x, int y, int seed )
	{
		// Stable, deterministic 0..99 roll per tile.
		return (int)(HashFloat( x, y, seed ) * 100f);
	}

	float CombinedForestNoise( int x, int y )
	{
		// Two-octave value noise gives broad clumps with organic edge variation.
		float n1 = SmoothNoise( x, y, _noiseSeed, ForestNoiseScale );
		float n2 = SmoothNoise( x, y, _noiseSeed + 7919, ForestNoiseScale * 0.45f );
		return n1 * 0.72f + n2 * 0.28f;
	}

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
