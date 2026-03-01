using Sandbox;
using SpriteTools;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Spawns the procedural map (ground + trees) at runtime when the scene has no
/// TilesetComponent. Ensures map generation works for both scene-based and
/// prefab-based (local game) play.
/// </summary>
[Category( "World" )]
[Title( "Map Spawner" )]
[Icon( "map" )]
public class MapSpawner : Component
{
	[Property] public string GroundTilesetPath { get; set; } = "scenes/forestlevelone.tileset";
	[Property] public string DetailsTilesetPath { get; set; } = "scenes/details.tileset";

	int _framesWaited;
	bool _spawned;
	const int FramesBeforeSpawn = 1; // Defer 1 frame so scene/player is ready

	protected override void OnStart()
	{
		// Defer to next frame so scene/scene-world is ready
	}

	protected override void OnUpdate()
	{
		if ( _spawned ) return;

		// Wait for scene to be fully initialized (player spawned, etc.)
		if ( _framesWaited < FramesBeforeSpawn )
		{
			_framesWaited++;
			return;
		}

		// Skip if map already exists (e.g. from scene)
		var existing = Scene.GetAllComponents<TilesetComponent>().FirstOrDefault();
		if ( existing != null )
		{
			Log.Info( "[MapSpawner] Map already present, skipping spawn." );
			return;
		}

		var groundRes = ResourceLibrary.Get<TilesetResource>( GroundTilesetPath );
		var detailsRes = ResourceLibrary.Get<TilesetResource>( DetailsTilesetPath );

		if ( groundRes == null )
		{
			Log.Warning( $"[MapSpawner] Ground tileset not found: {GroundTilesetPath}" );
			return;
		}
		if ( detailsRes == null )
		{
			Log.Warning( $"[MapSpawner] Details tileset not found: {DetailsTilesetPath}" );
			return;
		}

		// Prefer the local run container so map/details are scoped to the active local game instance.
		// Fall back to current object hierarchy for non-local/editor contexts.
		var parent = LocalGameRunner.GetRuntimeParent();
		if ( parent == null || !parent.IsValid() )
		{
			parent = GameObject.Parent as GameObject;
			if ( parent == null || !parent.IsValid() )
				parent = GameObject;
		}

		// Ground map
		var mapGo = new GameObject( true, "Map" );
		mapGo.SetParent( parent );
		mapGo.WorldPosition = Vector3.Zero;

		var groundTileset = mapGo.Components.Create<TilesetComponent>();
		groundTileset.Layers = new List<TilesetComponent.Layer>
		{
			new TilesetComponent.Layer( "Ground" ) { TilesetResource = groundRes }
		};
		groundTileset.Layers[0].TilesetComponent = groundTileset;

		var mapGen = mapGo.Components.Create<RandomTileVariants>();
		mapGen.Tileset = groundTileset;
		mapGen.LayerIndex = 0;
		// Ground tile: user (4,4) → tileset (4,0) = plain dirt/ground
		mapGen.GroundColumn = 4;
		mapGen.GroundRow = 0;
		// Variants: user (col, row) → tileset (col, 4-row)
		// Flowers: (1,0-4), (2,1-4)   Grass: (5,1-4)
		mapGen.VariantPositions = new List<string>
		{
			// Flowers col 1 — user rows 0–4 → tileset rows 4–0
			"1,4", "1,3", "1,2", "1,1", "1,0",
			// Flowers col 2 — user rows 1–4 → tileset rows 3–0
			"2,3", "2,2", "2,1", "2,0",
			// Grass col 5 — user rows 1–4 → tileset rows 3–0
			"5,3", "5,2", "5,1", "5,0",
		};
		// Flower/grass variant patches — concentrated in noise-defined areas
		mapGen.PatchNoiseScale = 8f;
		mapGen.PatchThreshold = 0.58f;
		mapGen.PatchVariantPercent = 35;
		mapGen.SparseVariantPercent = 2;

		// Details (trees) — slight Z offset so trees render on top of ground
		var detailsGo = new GameObject( true, "Details" );
		detailsGo.SetParent( parent );
		detailsGo.WorldPosition = new Vector3( 0, 0, 1 );

		var detailsTileset = detailsGo.Components.Create<TilesetComponent>();
		detailsTileset.Layers = new List<TilesetComponent.Layer>
		{
			new TilesetComponent.Layer( "Details" ) { TilesetResource = detailsRes },
			new TilesetComponent.Layer( "DetailsTop" ) { TilesetResource = detailsRes }
		};
		detailsTileset.Layers[0].TilesetComponent = detailsTileset;
		detailsTileset.Layers[1].TilesetComponent = detailsTileset;

		var detailsMgr = detailsGo.Components.Create<DetailsManager>();
		detailsMgr.DetailsTileset = detailsTileset;
		detailsMgr.LayerIndex = 0;
		detailsMgr.ForestNoiseScale = 16f;    // medium forest structures
		detailsMgr.ForestThreshold = 0.45f;   // ~50% higher cluster activation rate
		detailsMgr.ForestCoreDensity = 28;    // 50% lower core fill
		detailsMgr.ForestEdgeDensity = 12;    // 50% lower edge fill
		detailsMgr.OpenFieldDensity = 1;      // very sparse stragglers
		detailsMgr.ForestSmoothingPasses = 1; // smooth tiny artifacts from breakup
		detailsMgr.BreakupNoiseScale = 10f;   // broader breakup, less speckle
		detailsMgr.BreakupThreshold = 0.66f;  // breakup only in high breakup-noise zones
		detailsMgr.BreakupPercent = 22;       // light breakup to keep natural clusters
		detailsMgr.SingleTreeDensity = 5;     // ~60% higher individual-tree spawn rate (from default 3)
		detailsMgr.InvertY = true;            // S&box Z-up: tile y+1 = above on screen

		_spawned = true;
		Log.Info( $"[MapSpawner] Map spawned at runtime. Map and Trees parented to {parent?.Name ?? "?"}." );
	}
}
