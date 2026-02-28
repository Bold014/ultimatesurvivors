/// <summary>
/// Spawns Chests and LevelUpBeacons throughout the run.
/// On start: spawns 3 chests + 2 beacons.
/// Every 1 minute: spawns 1 more chest.
/// Every 1.5 minutes: spawns 1 more beacon.
/// </summary>
public sealed class WorldObjectSpawner : Component
{
	private EnemySpawner _spawner;
	private Random       _rand;
	private int          _chestsSpawned  = 3;   // 3 spawned in OnStart
	private int          _beaconsSpawned = 2;   // 2 spawned in OnStart

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

		for ( int i = 0; i < 3; i++ )
			SpawnChest();
		for ( int i = 0; i < 2; i++ )
			SpawnBeacon();
	}

	protected override void OnUpdate()
	{
		if ( _spawner == null ) return;

		float runMinutes = _spawner.RunTime / 60f;
		int targetChests  = 3 + (int)(runMinutes / 1f);    // 3 initial + 1 per 1 min
		int targetBeacons = 2 + (int)(runMinutes / 1.5f); // 2 initial + 1 per 1.5 min

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

	private Vector3 PlayerPosition()
	{
		var player = Scene.GetAllComponents<PlayerController>().FirstOrDefault();
		return player?.WorldPosition ?? Vector3.Zero;
	}

	private Vector3 RandomSpawnPosition()
	{
		var center = PlayerPosition();

		for ( int attempt = 0; attempt < 10; attempt++ )
		{
			float angle = (float)(_rand.NextDouble() * 360.0);
			float dist  = SpawnMinDist + (float)(_rand.NextDouble() * (SpawnMaxDist - SpawnMinDist));
			var pos = new Vector3(
				center.x + MathF.Cos( angle * MathF.PI / 180f ) * dist,
				center.y + MathF.Sin( angle * MathF.PI / 180f ) * dist,
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
			center.x + MathF.Cos( a * MathF.PI / 180f ) * d,
			center.y + MathF.Sin( a * MathF.PI / 180f ) * d,
			0f );
	}
}
