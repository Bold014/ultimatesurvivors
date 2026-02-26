/// <summary>
/// Plays a background music track that loops continuously.
/// Uses a static SoundEvent so s&box can find it. Looping: set on the mp3 in Assets Browser.
/// Mute state is persisted via PlayerProgress.Data.MusicMuted.
/// </summary>
public sealed class MusicManager : Component
{
	public static MusicManager Instance { get; private set; }

	/// <summary>
	/// Static SoundEvent — s&box looks up sounds by "ClassName.FieldName".
	/// Path is to the raw audio file (mp3/wav) in your addon.
	/// </summary>
	static SoundEvent SongLoop = new( "sounds/songloop.mp3" );

	[Property, Range( 0f, 1f )] public float Volume { get; set; } = 0.4f;

	private SoundHandle _handle;
	private bool _playing;

	protected override void OnStart()
	{
		Instance = this;
		Log.Info( "[MusicManager] OnStart — playing SongLoop" );

		try
		{
			_handle = Sound.Play( SongLoop );
			_playing = true;
			ApplyMute();
			Log.Info( $"[MusicManager] Music started (muted={PlayerProgress.Data.MusicMuted})" );
		}
		catch ( System.Exception e )
		{
			Log.Error( $"[MusicManager] OnStart failed: {e.Message}\n{e.StackTrace}" );
			_playing = false;
		}
	}

	/// <summary>Toggle mute and persist. Call from UI.</summary>
	public void ToggleMute()
	{
		PlayerProgress.Data.MusicMuted = !PlayerProgress.Data.MusicMuted;
		PlayerProgress.Save();
		ApplyMute();
	}

	void ApplyMute()
	{
		if ( !_playing ) return;
		try { _handle.Volume = PlayerProgress.Data.MusicMuted ? 0f : Volume; } catch { }
	}

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
		if ( !_playing ) return;

		try
		{
			_handle.Stop();
		}
		catch ( System.Exception e )
		{
			Log.Warning( $"[MusicManager] OnDestroy: {e.Message}" );
		}
	}
}
