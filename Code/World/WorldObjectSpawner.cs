/// <summary>
/// Spawns Chests and LevelUpBeacons throughout the run.
/// On start: spawns 2 chests + 1 beacon.
/// Every 1.5 minutes: spawns 1 more chest.
/// Every 2.25 minutes: spawns 1 more beacon.
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
		for ( int i = 0; i < 1; i++ )
			SpawnBeacon();
	}

	protected override void OnUpdate()
	{
		if ( _spawner == null ) return;

		float runMinutes = _spawner.RunTime / 60f;
		int targetChests  = 2 + (int)(runMinutes / 1.5f);    // 2 initial + 1 per 1.5 min
		int targetBeacons = 1 + (int)(runMinutes / 2.25f);   // 1 initial + 1 per 2.25 min

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
		var pos   = RandomSpawnPosition( 16f );
		var go    = new GameObject( true, "Chest" );
		go.WorldPosition = pos;
		LocalGameRunner.ParentRuntimeObject( go );
		var chest = go.Components.Create<Chest>();
		chest.SpawnedOnWave = Math.Max( 1, (int)(_spawner?.RunTime ?? 0) / 60 );
	}

	private void SpawnBeacon()
	{
		var pos = RandomSpawnPosition( 15f );
		var go  = new GameObject( true, "LevelUpBeacon" );
		go.WorldPosition = pos;
		LocalGameRunner.ParentRuntimeObject( go );
		go.Components.Create<LevelUpBeacon>();
	}

	private Vector3 PlayerPosition()
	{
		var player = Scene.GetAllComponents<PlayerController>().FirstOrDefault();
		return player?.WorldPosition ?? Vector3.Zero;
	}

	private Vector3 RandomSpawnPosition( float halfExtent )
	{
		var center = PlayerPosition();

		for ( int attempt = 0; attempt < 32; attempt++ )
		{
			float angle = (float)(_rand.NextDouble() * 360.0);
			float dist  = SpawnMinDist + (float)(_rand.NextDouble() * (SpawnMaxDist - SpawnMinDist));
			var pos = new Vector3(
				center.x + MathF.Cos( angle * MathF.PI / 180f ) * dist,
				center.y + MathF.Sin( angle * MathF.PI / 180f ) * dist,
				0f );

			if ( IsAreaClearOfTrees( pos, halfExtent ) )
				return pos;
		}

		// Fallback sweep with increasing radius to avoid placing objects in trees.
		for ( int i = 0; i < 40; i++ )
		{
			float t = i / 39f;
			float a = t * 360f;
			float d = SpawnMinDist + t * (SpawnMaxDist * 1.6f - SpawnMinDist);
			var pos = new Vector3(
				center.x + MathF.Cos( a * MathF.PI / 180f ) * d,
				center.y + MathF.Sin( a * MathF.PI / 180f ) * d,
				0f );
			if ( IsAreaClearOfTrees( pos, halfExtent ) )
				return pos;
		}

		return center.WithZ( 0f );
	}

	private static bool IsAreaClearOfTrees( Vector3 pos, float halfExtent )
	{
		// Include a small margin so collision bounds don't graze trees.
		float margin = 2f;
		float minX = pos.x - halfExtent - margin;
		float maxX = pos.x + halfExtent + margin;
		float minY = pos.y - halfExtent - margin;
		float maxY = pos.y + halfExtent + margin;

		int minTx = (int)MathF.Floor( minX / TreeManager.TileWorldWidth );
		int maxTx = (int)MathF.Floor( maxX / TreeManager.TileWorldWidth );
		int minTy = (int)MathF.Floor( minY / TreeManager.TileWorldHeight );
		int maxTy = (int)MathF.Floor( maxY / TreeManager.TileWorldHeight );

		for ( int tx = minTx; tx <= maxTx; tx++ )
		{
			for ( int ty = minTy; ty <= maxTy; ty++ )
			{
				if ( TreeManager.IsTreeAtTile( tx, ty ) )
					return false;
			}
		}

		return true;
	}
}
