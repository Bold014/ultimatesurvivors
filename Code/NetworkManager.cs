using System.Threading.Tasks;

/// <summary>
/// Custom network manager. Replaces Sandbox.NetworkHelper.
/// Creates a lobby named "rogue" on scene load and exposes connection events.
/// </summary>
public sealed class NetworkManager : Component, Component.INetworkListener
{
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
	}

	public void OnDisconnected( Connection channel )
	{
		Log.Info( $"[NetworkManager] {channel.DisplayName} left the lobby." );
	}

	public void OnBecomeHost( Connection previousHost )
	{
		Log.Info( "[NetworkManager] This client became the new host." );
	}
}
