/// <summary>
/// Manages the shared lobby chat message history and open/close state.
/// The actual [Rpc.Broadcast] for sending messages lives on PlayerController
/// (which is on a networked object). This component just stores and exposes messages.
/// </summary>
public sealed class ChatComponent : Component
{
	public static ChatComponent Instance { get; private set; }

	public class ChatMessage
	{
		public string PlayerName { get; set; }
		public string Text { get; set; }
		public Color NameColor { get; set; } = new Color( 0.2f, 0.9f, 1f );
	}

	public List<ChatMessage> Messages { get; } = new();
	public bool IsChatOpen { get; private set; } = false;
	public string CurrentInput { get; set; } = "";

	public event Action OnMessageAdded;

	protected override void OnStart()
	{
		Instance = this;
	}

	protected override void OnUpdate()
	{
		if ( IsChatOpen )
		{
			if ( Input.Pressed( "menu" ) )
				CloseChat();
		}
		else
		{
			if ( Input.Pressed( "chat" ) )
				OpenChat();
		}
	}

	public void AddMessage( string playerName, string text, Color? nameColor = null )
	{
		Messages.Add( new ChatMessage
		{
			PlayerName = playerName,
			Text = text,
			NameColor = nameColor ?? new Color( 0.2f, 0.9f, 1f )
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
		CloseChat();

		if ( string.IsNullOrEmpty( msg ) ) return;

		var localPlayer = Scene.GetAllComponents<PlayerController>()
			.FirstOrDefault( p => !p.IsProxy );

		if ( localPlayer != null )
		{
			localPlayer.BroadcastChat( msg );
		}
		else
		{
			// In menu scene there is no PlayerController — broadcast directly.
			var name = Connection.Local?.DisplayName ?? Connection.Local?.Name ?? "Unknown";
			BroadcastMenuMessage( name, msg );
		}
	}

	/// <summary>Used to send chat from the menu where no PlayerController exists.</summary>
	[Rpc.Broadcast]
	public void BroadcastMenuMessage( string playerName, string message )
	{
		AddMessage( playerName, message );
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}
}
