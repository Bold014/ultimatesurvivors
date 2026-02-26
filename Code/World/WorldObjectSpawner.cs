/// <summary>
/// Spawns Chests and LevelUpBeacons throughout the run.
/// On start: spawns 2 chests + 1 beacon.
/// Every 2 minutes: spawns 1 more chest.
/// Every 3 minutes: spawns 1 more beacon.
/// </summary>
public sealed class WorldObjectSpawner : Component
{
	private EnemySpawner _spawner;
	private Random       _rand;
	private int          _chestsSpawned  = 2;   // 2 spawned in OnStart
	private int          _beaconsSpawned = 1;   // 1 spawned in OnStart

	private const float SpawnMinDist = 200f;
	private const float SpawnMaxDist = 400f;

	protected override void OnStart()
	{
		_spawner = Components.Get<EnemySpawner>();
		_rand    = new Random();
		Chest.ResetRunState();

		foreach ( var chest in Scene.GetAllComponents<Chest>().ToList() )
			chest.GameObject.Destroy();
		foreach ( var beacon in Scene.GetAllComponents<LevelUpBeacon>().ToList() )
			beacon.GameObject.Destroy();

		for ( int i = 0; i < 2; i++ )
			SpawnChest();
		SpawnBeacon();
	}

	protected override void OnUpdate()
	{
		if ( _spawner == null ) return;

		float runMinutes = _spawner.RunTime / 60f;
		int targetChests  = 2 + (int)(runMinutes / 2f);   // 2 initial + 1 per 2 min
		int targetBeacons = 1 + (int)(runMinutes / 3f);   // 1 initial + 1 per 3 min

		while ( _chestsSpawned < targetChests )
		{
			SpawnChest();
			_chestsSpawned++;
		}

		while ( _beaconsSpawned < targetBeacons )
		{
			SpawnBeacon();
			_beaconsSpawned++;
		}
	}

	private void SpawnChest()
	{
		var pos   = RandomSpawnPosition();
		var go    = new GameObject( true, "Chest" );
		go.WorldPosition = pos;
		var chest = go.Components.Create<Chest>();
		chest.SpawnedOnWave = Math.Max( 1, (int)(_spawner?.RunTime ?? 0) / 60 );
	}

	private void SpawnBeacon()
	{
		var pos = RandomSpawnPosition();
		var go  = new GameObject( true, "LevelUpBeacon" );
		go.WorldPosition = pos;
		go.Components.Create<LevelUpBeacon>();
	}

	private Vector3 RandomSpawnPosition()
	{
		for ( int attempt = 0; attempt < 10; attempt++ )
		{
			float angle = (float)(_rand.NextDouble() * 360.0);
			float dist  = SpawnMinDist + (float)(_rand.NextDouble() * (SpawnMaxDist - SpawnMinDist));
			var pos = new Vector3(
				MathF.Cos( angle * MathF.PI / 180f ) * dist,
				MathF.Sin( angle * MathF.PI / 180f ) * dist,
				0f );

			if ( !TreeManager.IsTreeAtWorldPos( pos.x, pos.y ) &&
			     !TreeManager.IsTreeAtWorldPos( pos.x + TreeManager.TileWorldWidth - 1, pos.y ) &&
			     !TreeManager.IsTreeAtWorldPos( pos.x, pos.y + TreeManager.TileWorldHeight - 1 ) &&
			     !TreeManager.IsTreeAtWorldPos( pos.x + TreeManager.TileWorldWidth - 1, pos.y + TreeManager.TileWorldHeight - 1 ) )
				return pos;
		}

		float a = (float)(_rand.NextDouble() * 360.0);
		float d = SpawnMinDist + (float)(_rand.NextDouble() * (SpawnMaxDist - SpawnMinDist));
		return new Vector3(
			MathF.Cos( a * MathF.PI / 180f ) * d,
			MathF.Sin( a * MathF.PI / 180f ) * d,
			0f );
	}
}
