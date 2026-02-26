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
		Log.Info( $"[Dev] DevUnlockAll = {PlayerProgress.DevUnlockAll}" );
	}
}
