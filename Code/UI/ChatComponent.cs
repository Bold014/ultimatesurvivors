/// <summary>
/// Manages the shared lobby chat message history and open/close state.
/// Lives on the networked "Menu" scene object so BroadcastMenuMessage can reach all clients.
/// </summary>
public sealed class ChatComponent : Component
{
	public static ChatComponent Instance { get; private set; }

	public class ChatMessage
	{
		public string PlayerName { get; set; }
		public string Text { get; set; }
		public Color NameColor { get; set; } = Color.White;
	}

	public List<ChatMessage> Messages { get; } = new();
	public bool IsChatOpen { get; private set; } = false;
	public string CurrentInput { get; set; } = "";

	public event Action OnMessageAdded;

	protected override void OnStart()
	{
		Instance = this;
	}

	public void AddMessage( string playerName, string text, Color? nameColor = null )
	{
		// Avoid duplicate when optimistic add + RPC both run on sender
		if ( Messages.Count > 0 )
		{
			var last = Messages[^1];
			if ( last.PlayerName == playerName && last.Text == text )
				return;
		}

		Messages.Add( new ChatMessage
		{
			PlayerName = playerName,
			Text = text,
			NameColor = nameColor ?? Color.White
		} );

		if ( Messages.Count > 50 )
			Messages.RemoveAt( 0 );

		OnMessageAdded?.Invoke();
	}

	public void OpenChat()
	{
		IsChatOpen = true;
		CurrentInput = "";
	}

	public void CloseChat()
	{
		IsChatOpen = false;
		CurrentInput = "";
	}

	public void Submit()
	{
		var msg = CurrentInput?.Trim() ?? "";
		if ( string.IsNullOrEmpty( msg ) ) { CloseChat(); return; }

		var name = Connection.Local?.DisplayName ?? Connection.Local?.Name ?? "Unknown";

		// Add message first so UI shows it before chat closes (no frame gap)
		AddMessage( name, msg );
		CloseChat();

		// Only route through PlayerController when the game objects are truly networked.
		// In LocalGame mode the player prefab is client-only (no NetworkSpawn), so its
		// [Rpc.Broadcast] never reaches other clients. Use BroadcastMenuMessage instead,
		// which lives on this component — a networked scene object in the menu scene.
		if ( LocalGameRunner.IsNetworked )
		{
			var localPlayer = Scene.GetAllComponents<PlayerController>()
				.FirstOrDefault( p => !p.IsProxy );

			if ( localPlayer != null )
			{
				localPlayer.BroadcastChat( msg );
				return;
			}
		}

		BroadcastMenuMessage( name, msg );
	}

	/// <summary>Used to send chat from the menu where no PlayerController exists.</summary>
	[Rpc.Broadcast]
	public void BroadcastMenuMessage( string playerName, string message )
	{
		AddMessage( playerName, message );
	}

	/// <summary>Broadcasts a server-style announcement with a custom color (r/g/b 0-1).</summary>
	[Rpc.Broadcast]
	public void BroadcastServerMessage( string message, float r, float g, float b )
	{
		AddMessage( "Server", message, new Color( r, g, b ) );
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}
}
