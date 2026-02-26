/// <summary>
/// Plays a background music track that loops continuously.
/// Uses a static SoundEvent so s&box can find it. Looping is set in songloop.sound asset.
/// Mute state is persisted via PlayerProgress.Data.MusicMuted.
/// </summary>
public sealed class MusicManager : Component
{
	public static MusicManager Instance { get; private set; }

	/// <summary>
	/// Static SoundEvent — references the .sound asset so looping is applied.
	/// songloop.sound has "Looping": true; raw mp3 path bypasses that.
	/// </summary>
	static SoundEvent SongLoop = new( "sounds/songloop" );

	[Property, Range( 0f, 1f )] public float Volume { get; set; } = 0.4f;

	private SoundHandle _handle;
	private bool _playing;

	protected override void OnStart()
	{
		Instance = this;

		try
		{
			_handle = Sound.Play( SongLoop );
			_playing = true;
			ApplyMute();
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

	protected override void OnUpdate()
	{
		if ( !_playing ) return;
		// Manual loop fallback: s&box .sound Looping may not always work
		try
		{
			if ( !_handle.IsPlaying )
			{
				_handle = Sound.Play( SongLoop );
				ApplyMute();
			}
		}
		catch { }
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
