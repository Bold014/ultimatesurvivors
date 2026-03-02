/// <summary>
/// Manages the shared lobby chat message history and open/close state.
/// Lives on the networked "Menu" scene object so BroadcastMenuMessage can reach all clients.
/// </summary>
public sealed class ChatComponent : Component
{
	public static ChatComponent Instance { get; private set; }
	private static readonly TimeSpan HistoryWindow = TimeSpan.FromMinutes( 15 );
	private const int MaxBufferedMessages = 300;

	public class ChatMessage
	{
		public string PlayerName { get; set; }
		public string Text { get; set; }
		public Color NameColor { get; set; } = Color.White;
		public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
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
		// Avoid duplicate when optimistic add + RPC both run on sender.
		// Check the last 3 messages in case another player's message arrived in between.
		int checkFrom = Math.Max( 0, Messages.Count - 3 );
		for ( int i = checkFrom; i < Messages.Count; i++ )
		{
			if ( Messages[i].PlayerName == playerName && Messages[i].Text == text )
				return;
		}

		Messages.Add( new ChatMessage
		{
			PlayerName = playerName,
			Text = text,
			NameColor = nameColor ?? Color.White,
			TimestampUtc = DateTime.UtcNow
		} );

		PruneHistory();

		OnMessageAdded?.Invoke();
	}

	public IReadOnlyList<ChatMessage> GetRecentMessages( TimeSpan maxAge )
	{
		PruneHistory();
		var cutoff = DateTime.UtcNow - maxAge;
		return Messages.Where( m => m.TimestampUtc >= cutoff ).ToList();
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

		if ( ShouldBroadcastLobbyRpc() )
			BroadcastMenuMessage( name, msg );
	}

	private static bool ShouldBroadcastLobbyRpc()
	{
		// Local prefab runs are intentionally client-only, so lobby RPCs will
		// spam failed P2P session logs if we try to broadcast from that mode.
		if ( LocalGameRunner.IsInLocalGame ) return false;
		if ( !Networking.IsActive ) return false;
		return Connection.All.Count > 1;
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

	public void SendServerMessage( string message, Color color )
	{
		if ( ShouldBroadcastLobbyRpc() )
			BroadcastServerMessage( message, color.r, color.g, color.b );
		else
			AddMessage( "Server", message, color );
	}

	private void PruneHistory()
	{
		var cutoff = DateTime.UtcNow - HistoryWindow;
		Messages.RemoveAll( m => m.TimestampUtc < cutoff );

		if ( Messages.Count > MaxBufferedMessages )
			Messages.RemoveRange( 0, Messages.Count - MaxBufferedMessages );
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}
}
