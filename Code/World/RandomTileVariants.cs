using Sandbox;
using SpriteTools;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Streams the ground tilemap around the camera in chunks so the map is
/// effectively infinite with no startup cost and no FPS hit.
/// Attach to the same GameObject as the TilesetComponent (LevelOneMap).
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

	/// <summary>Rotation applied to the base ground tile (0 / 90 / 180 / 270).</summary>
	[Property] public int GroundAngle { get; set; } = 0;

	/// <summary>
	/// Variant/decoration tiles scattered randomly over the ground.
	/// Format: "col,row" or "col,row,angle" (angle = 0/90/180/270).
	/// </summary>
	[Property] public List<string> VariantPositions { get; set; } = new();

	/// <summary>Approximate % of tiles replaced with a variant (0–100).</summary>
	[Property, Range( 0, 100 )] public int VariantPercent { get; set; } = 4;

	// ── runtime state ─────────────────────────────────────────────────────────
	readonly HashSet<Vector2Int> _generatedChunks = new();
	TilesetComponent.Layer _layer;
	Guid _groundGuid;
	List<(Guid guid, int angle)> _variants = new();
	bool _ready;
	System.Random _rng;

	protected override void OnStart()
	{
		// Fallback: if Tileset not assigned (e.g. prefab serialization), get from same GameObject
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

		// Ensure tiles render behind entities
		_layer.Height = -1f;

		var posToGuid = tilesetRes.Tiles.ToDictionary( t => t.Position, t => t.Id );

		var groundPos = new Vector2Int( GroundColumn, GroundRow );
		if ( !posToGuid.TryGetValue( groundPos, out _groundGuid ) )
		{
			Log.Warning( $"[MapGen] Ground tile not found at ({GroundColumn},{GroundRow})." );
			return;
		}

		_variants = new List<(Guid, int)>();
		foreach ( var s in VariantPositions )
		{
			var parts = s.Split( ',' );
			if ( parts.Length >= 2 &&
			     int.TryParse( parts[0].Trim(), out int col ) &&
			     int.TryParse( parts[1].Trim(), out int row ) )
			{
				int angle = parts.Length >= 3 && int.TryParse( parts[2].Trim(), out int a ) ? a : 0;
				var vp = new Vector2Int( col, row );
				if ( posToGuid.TryGetValue( vp, out var vg ) )
					_variants.Add( (vg, angle) );
				else
					Log.Warning( $"[MapGen] Variant ({col},{row}) not in tileset — skipped." );
			}
		}

		// Seed with current time so every run produces a different map layout
		int seed = (int)(System.DateTime.UtcNow.Ticks & 0x7FFFFFFF);
		_rng = new System.Random( seed );

		Log.Info( $"[MapGen] Ready. seed={seed} groundTile=({GroundColumn},{GroundRow}) guid={_groundGuid} variants={_variants.Count} variantPercent={VariantPercent}" );
		for ( int i = 0; i < _variants.Count; i++ )
			Log.Info( $"[MapGen] Variant[{i}] guid={_variants[i].guid} angle={_variants[i].angle}" );

		_ready = true;
	}

	Vector3 GetTrackingPosition()
	{
		// Track the player directly by component — Scene.Camera is often fixed at (0,0,z)
		var player = Scene.GetAllComponents<PlayerController>().FirstOrDefault();
		if ( player is not null )
			return player.WorldPosition;
		return Scene.Camera?.WorldPosition ?? Vector3.Zero;
	}

	protected override void OnUpdate()
	{
		if ( !_ready || _layer == null )
			return;

		var tileSizeF = _layer.TilesetResource?.GetTileSize() ?? new Vector2( 16, 16 );
		var tileSize = new Vector2Int( (int)tileSizeF.x, (int)tileSizeF.y );

		if ( tileSize.x <= 0 || tileSize.y <= 0 )
		{
			Log.Warning( $"[MapGen] Invalid tileSize {tileSize}" );
			return;
		}

		var trackPos = GetTrackingPosition();
		int centerChunkX = (int)MathF.Floor( trackPos.x / ( tileSize.x * ChunkSize ) );
		int centerChunkY = (int)MathF.Floor( trackPos.y / ( tileSize.y * ChunkSize ) );

		// Find the closest ungenerated chunk to the tracking position
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
			GenerateChunk( closest.Value, tileSize );
			_generatedChunks.Add( closest.Value );
		}
	}

	private int _chunksGenerated = 0;

	void GenerateChunk( Vector2Int chunk, Vector2Int tileSize )
	{
		int startX = chunk.x * ChunkSize;
		int startY = chunk.y * ChunkSize;
		bool useVariants = _variants.Count > 0 && VariantPercent > 0;
		int variantCount = 0;

		for ( int x = startX; x < startX + ChunkSize; x++ )
		{
			for ( int y = startY; y < startY + ChunkSize; y++ )
			{
			var mapPos = new Vector2Int( x, y );
			Guid tileGuid = _groundGuid;
			int tileAngle = GroundAngle;

				if ( useVariants && _rng.Next( 100 ) < VariantPercent )
				{
					int vi = (int)(Math.Abs( (long)x * 31 + (long)y * 17 ) % _variants.Count);
					(tileGuid, tileAngle) = _variants[vi];
					variantCount++;
				}

				_layer.SetTile( mapPos, tileGuid, Vector2Int.Zero, tileAngle, rebuild: false );
			}
		}

		Tileset.IsDirty = true;

		// Log first 3 chunks so we can verify variants are actually being placed
		if ( _chunksGenerated < 3 )
			Log.Info( $"[MapGen] Chunk {_chunksGenerated} @ ({chunk.x},{chunk.y}): {variantCount} variants placed out of {ChunkSize * ChunkSize} tiles (useVariants={useVariants})" );
		_chunksGenerated++;
	}
}
