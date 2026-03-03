using System.Threading.Tasks;

/// <summary>
/// Custom network manager. Replaces Sandbox.NetworkHelper.
/// Creates a lobby named "rogue" on scene load and exposes connection events.
/// </summary>
public sealed class NetworkManager : Component, Component.INetworkListener
{
	// Set when the local client disconnects while a local run is active.
	// On next successful local join we force run teardown and home reset.
	private static bool _resetRunOnNextLocalJoin;

	protected override async Task OnLoad()
	{
		if ( Scene.IsEditor ) return;

		if ( !Networking.IsActive )
		{
			LoadingScreen.Title = "Creating Lobby";
			await Task.DelayRealtimeSeconds( 0.1f );
			Networking.CreateLobby( new() { Name = "rogue", MaxPlayers = 64 } );
		}
	}

	public void OnActive( Connection channel )
	{
		Log.Info( $"[NetworkManager] {channel.DisplayName} joined the lobby." );

		// When THIS client joins a lobby, always tear down any running local game
		// and return to the homepage. A client-local game cannot safely survive a
		// network reconnection (input/components break).
		if ( channel == Connection.Local )
		{
			_resetRunOnNextLocalJoin = false;
			LocalGameRunner.Instance?.EndLocalGame();
			MenuManager.ReturnToHomepage();
		}
	}

	public void OnDisconnected( Connection channel )
	{
		Log.Info( $"[NetworkManager] {channel.DisplayName} left the lobby." );

		if ( channel == Connection.Local && LocalGameRunner.IsInLocalGame )
		{
			// Immediately clean up the broken run. Also set the deferred flag
			// as a safety net in case EndLocalGame can't fully tear down yet.
			_resetRunOnNextLocalJoin = true;
			Log.Info( "[NetworkManager] Local client disconnected during active run; cleaning up immediately." );
			LocalGameRunner.Instance?.EndLocalGame();
			MenuManager.ReturnToHomepage();
		}
	}

	public void OnBecomeHost( Connection previousHost )
	{
		Log.Info( "[NetworkManager] This client became the new host." );
	}
}
