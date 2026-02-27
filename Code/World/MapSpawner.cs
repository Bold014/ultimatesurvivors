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
	[Property] public string TreeTilesetPath { get; set; } = "scenes/trees.tileset";

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
		var treeRes = ResourceLibrary.Get<TilesetResource>( TreeTilesetPath );

		if ( groundRes == null )
		{
			Log.Warning( $"[MapSpawner] Ground tileset not found: {GroundTilesetPath}" );
			return;
		}
		if ( treeRes == null )
		{
			Log.Warning( $"[MapSpawner] Tree tileset not found: {TreeTilesetPath}" );
			return;
		}

		// Parent to prefab/scene root so Map and Trees are in the same scene as the player.
		// GameObject.Parent = inner game_run's parent = prefab root (when loaded from game.scene).
		var parent = GameObject.Parent as GameObject;
		if ( parent == null || !parent.IsValid() )
			parent = GameObject;

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
		mapGen.GroundAngle = 270;
		// Variants: user (col, row) → tileset (col, 4-row), angle=90 to correct orientation
		// Flowers: (1,0-4), (2,1-4)   Grass: (5,1-4)
		mapGen.VariantPositions = new List<string>
		{
			// Flowers col 1 — user rows 0–4 → tileset rows 4–0
			"1,4,270", "1,3,270", "1,2,270", "1,1,270", "1,0,270",
			// Flowers col 2 — user rows 1–4 → tileset rows 3–0
			"2,3,270", "2,2,270", "2,1,270", "2,0,270",
			// Grass col 5 — user rows 1–4 → tileset rows 3–0
			"5,3,270", "5,2,270", "5,1,270", "5,0,270",
		};
		mapGen.VariantPercent = 10;

		// Trees
		var treesGo = new GameObject( true, "Trees" );
		treesGo.SetParent( parent );
		treesGo.WorldPosition = Vector3.Zero;

		var treeTileset = treesGo.Components.Create<TilesetComponent>();
		treeTileset.Layers = new List<TilesetComponent.Layer>
		{
			new TilesetComponent.Layer( "Trees" ) { TilesetResource = treeRes }
		};
		treeTileset.Layers[0].TilesetComponent = treeTileset;

		var treeMgr = treesGo.Components.Create<TreeManager>();
		treeMgr.TreeTileset = treeTileset;
		treeMgr.LayerIndex = 0;
		treeMgr.TreePercent = 3;

		_spawned = true;
		Log.Info( $"[MapSpawner] Map spawned at runtime. Map and Trees parented to {parent?.Name ?? "?"}." );
	}
}
