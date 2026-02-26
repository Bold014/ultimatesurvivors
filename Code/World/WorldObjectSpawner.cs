/// <summary>
/// Spawns Chests and LevelUpBeacons throughout the run.
/// On start: spawns 2 chests + 1 beacon.
/// Every 2 waves cleared: spawns 1 more chest.
/// Every 3 waves cleared: spawns 1 more beacon.
/// </summary>
public sealed class WorldObjectSpawner : Component
{
	private EnemySpawner _spawner;
	private Random       _rand;
	private int          _lastWaveSpawned = 0;

	// Spawn radius range from the world origin (not player, so objects stay on-screen)
	private const float SpawnMinDist = 200f;
	private const float SpawnMaxDist = 400f;

	protected override void OnStart()
	{
		_spawner = Components.Get<EnemySpawner>();

		_rand = new Random();

		Chest.ResetRunState();

		// Initial world objects at run start
		for ( int i = 0; i < 2; i++ )
			SpawnChest();
		SpawnBeacon();
	}

	protected override void OnUpdate()
	{
		if ( _spawner == null ) return;

		int wave = _spawner.WaveNumber;
		if ( wave <= _lastWaveSpawned ) return;

		// Check if the spawner just finished a wave (entered intermission)
		// We detect this by watching wave transitions
		for ( int w = _lastWaveSpawned + 1; w <= wave; w++ )
		{
			// Chest every 2 completed waves
			if ( w % 2 == 0 )
				SpawnChest();

			// Beacon every 3 completed waves
			if ( w % 3 == 0 )
				SpawnBeacon();
		}

		_lastWaveSpawned = wave;
	}

	private void SpawnChest()
	{
		var pos   = RandomSpawnPosition();
		var go    = new GameObject( true, "Chest" );
		go.WorldPosition = pos;
		var chest = go.Components.Create<Chest>();
		chest.SpawnedOnWave = Math.Max( 1, _spawner?.WaveNumber ?? 1 );
		Log.Info( $"[WorldObjectSpawner] Spawned chest at {pos} (wave={chest.SpawnedOnWave}, opens so far={Chest.ChestsOpened})" );
	}

	private void SpawnBeacon()
	{
		var pos = RandomSpawnPosition();
		var go  = new GameObject( true, "LevelUpBeacon" );
		go.WorldPosition = pos;
		go.Components.Create<LevelUpBeacon>();
		Log.Info( $"[WorldObjectSpawner] Spawned beacon at {pos}" );
	}

	private Vector3 RandomSpawnPosition()
	{
		// Retry up to 10 times to avoid placing on a tree tile
		for ( int attempt = 0; attempt < 10; attempt++ )
		{
			float angle = (float)(_rand.NextDouble() * 360.0);
			float dist  = SpawnMinDist + (float)(_rand.NextDouble() * (SpawnMaxDist - SpawnMinDist));
			var pos = new Vector3(
				MathF.Cos( angle * MathF.PI / 180f ) * dist,
				MathF.Sin( angle * MathF.PI / 180f ) * dist,
				0f );

			// Check a footprint matching one tree tile (16×32) so objects don't clip a tree
			if ( !TreeManager.IsTreeAtWorldPos( pos.x, pos.y ) &&
			     !TreeManager.IsTreeAtWorldPos( pos.x + TreeManager.TileWorldWidth - 1, pos.y ) &&
			     !TreeManager.IsTreeAtWorldPos( pos.x, pos.y + TreeManager.TileWorldHeight - 1 ) &&
			     !TreeManager.IsTreeAtWorldPos( pos.x + TreeManager.TileWorldWidth - 1, pos.y + TreeManager.TileWorldHeight - 1 ) )
				return pos;
		}

		// Fallback: return origin area (shouldn't happen often)
		return Vector3.Zero;
	}
}
