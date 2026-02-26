using Sandbox;
using SpriteTools;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Streams tree tiles around the player in the same chunk grid as the ground layer.
/// Tracks occupied tile positions so enemies, projectiles, and world object spawns
/// can avoid them.
///
/// Attach this component to the same GameObject as the tree TilesetComponent.
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

	/// <summary>Approximate % of tiles that become trees (0–100).</summary>
	[Property, Range( 0, 100 )] public int TreePercent { get; set; } = 3;

	// ── public static state ───────────────────────────────────────────────
	/// <summary>Tile positions (in tile coordinates) that contain a tree.</summary>
	public static readonly HashSet<Vector2Int> TreeTiles = new();

	/// <summary>World X extent of one tree tile (TileSize.x × TileScale = 32 × 1). Maps to screen-vertical.</summary>
	public const int TileWorldWidth = 32;

	/// <summary>World Y extent of one tree tile (TileSize.y × TileScale = 16 × 1). Maps to screen-horizontal.</summary>
	public const int TileWorldHeight = 16;

	/// <summary>Returns true if the given WORLD position overlaps a tree tile.</summary>
	public static bool IsTreeAtWorldPos( float x, float y )
	{
		int tx = (int)MathF.Floor( x / TileWorldWidth );
		int ty = (int)MathF.Floor( y / TileWorldHeight );
		return TreeTiles.Contains( new Vector2Int( tx, ty ) );
	}

	/// <summary>Returns true if the given TILE coordinate has a tree.</summary>
	public static bool IsTreeAtTile( int tx, int ty ) => TreeTiles.Contains( new Vector2Int( tx, ty ) );

	// ── runtime state ─────────────────────────────────────────────────────
	readonly HashSet<Vector2Int> _generatedChunks = new();
	TilesetComponent.Layer _layer;
	Guid _treeGuid;
	bool _ready;

	protected override void OnStart()
	{
		if ( !TreeTileset.IsValid() )
		{
			Log.Warning( "[TreeMgr] TreeTileset property not set!" );
			return;
		}

		var layers = TreeTileset.Layers;
		if ( layers == null || LayerIndex >= layers.Count )
		{
			Log.Warning( $"[TreeMgr] LayerIndex {LayerIndex} out of range." );
			return;
		}

		_layer = layers[LayerIndex];
		if ( _layer == null )
		{
			Log.Warning( "[TreeMgr] Layer is null." );
			return;
		}

		var res = _layer.TilesetResource;
		if ( res == null )
		{
			Log.Warning( "[TreeMgr] Layer has no TilesetResource." );
			return;
		}

		// Trees render at z=0 (same level as entities), above the floor (floor is at -1)
		_layer.Height = 0f;

		var tile = res.Tiles.FirstOrDefault();
		if ( tile == null )
		{
			Log.Warning( "[TreeMgr] Tree tileset has no tiles." );
			return;
		}
		_treeGuid = tile.Id;

		// Clear any leftover tree tiles from a previous run
		TreeTiles.Clear();

		_ready = true;
		Log.Info( $"[TreeMgr] Ready — streaming {ChunkSize}² chunks at {TreePercent}% density." );
	}

	protected override void OnUpdate()
	{
		if ( !_ready || _layer == null ) return;

		var player = Scene.GetAllComponents<PlayerController>().FirstOrDefault();
		var trackPos = player?.WorldPosition ?? Scene.Camera?.WorldPosition ?? Vector3.Zero;

		int centerChunkX = (int)MathF.Floor( trackPos.x / ( TileWorldWidth  * ChunkSize ) );
		int centerChunkY = (int)MathF.Floor( trackPos.y / ( TileWorldHeight * ChunkSize ) );

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

		for ( int x = startX; x < startX + ChunkSize; x++ )
		{
			for ( int y = startY; y < startY + ChunkSize; y++ )
			{
				// Deterministic per-tile hash so the world is consistent
				int hash = Math.Abs( x * 92837111 ^ y * 689287499 );
				if ( hash % 100 >= TreePercent ) continue;

				var tilePos = new Vector2Int( x, y );
			_layer.SetTile( tilePos, _treeGuid, Vector2Int.Zero, 0, rebuild: false );
				// Each tree tile is TileWorldWidth × TileWorldHeight world units — one entry covers the full sprite
				TreeTiles.Add( tilePos );
			}
		}

		TreeTileset.IsDirty = true;
	}
}
