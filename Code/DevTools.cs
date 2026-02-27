using System.Linq;

/// <summary>
/// Developer console commands for testing without grinding through progression.
/// None of these mutate save data; they only affect the current session.
/// Restricted to the developer's Steam account only.
/// </summary>
public static class DevTools
{
	private const ulong DevSteamId = 76561198165180167UL;

	private static bool IsDev()
	{
		return Connection.Local?.SteamId == DevSteamId;
	}

	[ConCmd( "dev_unlockall" )]
	public static void ToggleDevUnlocks()
	{
		if ( !IsDev() ) return;
		PlayerProgress.DevUnlockAll = !PlayerProgress.DevUnlockAll;
		Log.Info( $"[DevTools] DevUnlockAll = {PlayerProgress.DevUnlockAll}" );
	}

	[ConCmd( "dev_wipleaderboard" )]
	public static void WipeLeaderboard()
	{
		if ( !IsDev() ) return;
		PlayerProgress.ResetLeaderboardStats();
		Log.Info( "[DevTools] Leaderboard stats reset to 0 (total_kills, runs_completed, longest_survival)" );
	}

	[ConCmd( "dev_spawndragon" )]
	public static void SpawnDragonBoss()
	{
		if ( !IsDev() ) return;
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
