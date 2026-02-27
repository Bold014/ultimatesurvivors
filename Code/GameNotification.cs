/// <summary>
/// Local-only in-game notification banners for wave events, boss alerts, etc.
/// Call Tick() every frame from a Component (GameHUD does this).
/// </summary>
public static class GameNotification
{
	public static string CurrentText  { get; private set; }
	public static Color  CurrentColor { get; private set; } = Color.White;

	private static float _remaining;
	private static readonly System.Collections.Generic.Queue<(string text, Color color, float dur)> _queue = new();

	public static void Show( string text, Color color, float duration = 2.5f )
	{
		_queue.Enqueue( (text, color, duration) );
	}

	/// <summary>Advance the timer. Call once per frame from a Component.</summary>
	public static void Tick( float delta )
	{
		if ( CurrentText != null )
		{
			_remaining -= delta;
			if ( _remaining <= 0f )
				CurrentText = null;
		}

		if ( CurrentText == null && _queue.Count > 0 )
		{
			var item     = _queue.Dequeue();
			CurrentText  = item.text;
			CurrentColor = item.color;
			_remaining   = item.dur;
		}
	}
}
