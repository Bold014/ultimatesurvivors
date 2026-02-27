using System.Linq;

/// <summary>
/// Developer console commands for testing without grinding through progression.
/// None of these mutate save data; they only affect the current session.
/// </summary>
public static class DevTools
{
	[ConCmd( "dev_unlockall" )]
	public static void ToggleDevUnlocks()
	{
		PlayerProgress.DevUnlockAll = !PlayerProgress.DevUnlockAll;
	}

	[ConCmd( "dev_spawndragon" )]
	public static void SpawnDragonBoss()
	{
		var scene = GameManager.Instance?.Scene;
		if ( scene == null )
		{
			Log.Warning( "dev_spawndragon: No game scene. Are you in a run?" );
			return;
		}
		var spawner = scene.GetAllComponents<EnemySpawner>().FirstOrDefault();
		if ( spawner == null )
		{
			Log.Warning( "dev_spawndragon: No EnemySpawner found. Are you in the game scene?" );
			return;
		}
		spawner.SpawnDragonBossForTesting();
	}
}
