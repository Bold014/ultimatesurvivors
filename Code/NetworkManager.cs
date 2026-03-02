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

		// When THIS client joins a lobby from menu/startup, return to homepage state.
		// If the local client previously disconnected mid-run, force a clean reset.
		if ( channel == Connection.Local )
		{
			if ( _resetRunOnNextLocalJoin )
			{
				_resetRunOnNextLocalJoin = false;
				Log.Info( "[NetworkManager] Local client rejoined after disconnect; ending local run and returning to homepage." );
				LocalGameRunner.Instance?.EndLocalGame();
				MenuManager.ReturnToHomepage();
				return;
			}

			if ( LocalGameRunner.IsInLocalGame )
			{
				Log.Info( "[NetworkManager] Local reconnect detected during active run; preserving current run state." );
				return;
			}

			LocalGameRunner.Instance?.EndLocalGame();
			MenuManager.ReturnToHomepage();
		}
	}

	public void OnDisconnected( Connection channel )
	{
		Log.Info( $"[NetworkManager] {channel.DisplayName} left the lobby." );

		if ( channel == Connection.Local && LocalGameRunner.IsInLocalGame )
		{
			_resetRunOnNextLocalJoin = true;
			Log.Info( "[NetworkManager] Local client disconnected during active run; scheduling reset on next join." );
		}
	}

	public void OnBecomeHost( Connection previousHost )
	{
		Log.Info( "[NetworkManager] This client became the new host." );
	}
}
