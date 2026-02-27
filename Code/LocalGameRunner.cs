/// <summary>
/// Runs the game as a client-local prefab instance when multiple players are in the lobby.
/// Clones GamePrefab without NetworkSpawn so only the clicking client sees/runs the game.
/// </summary>
public sealed class LocalGameRunner : Component
{
	public static LocalGameRunner Instance { get; private set; }
	public static bool IsInLocalGame => Instance?._localGameRoot != null;
	/// <summary>True when game objects should be networked (solo scene). False when running from client-only prefab.</summary>
	public static bool IsNetworked => !IsInLocalGame;

	[Property] public GameObject GamePrefab { get; set; }

	private GameObject _localGameRoot;

	// Track objects we disabled so we can re-enable them even after they are inactive
	// (Scene.GetAllComponents skips disabled GameObjects, so we must hold explicit refs).
	private readonly List<GameObject> _disabledObjects = new();
	private readonly List<MusicManager> _stoppedMusic = new();

	protected override void OnStart()
	{
		Instance = this;
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}

	public void StartLocalGame()
	{
		if ( _localGameRoot != null )
		{
			Log.Info( "[LocalGameRunner] StartLocalGame skipped: game already running" );
			return;
		}

		if ( GamePrefab == null )
		{
			Log.Warning( "LocalGameRunner: GamePrefab not set. Assign prefabs/game_run in the inspector, then save the scene (Ctrl+S)." );
			return;
		}

		_localGameRoot = GamePrefab.Clone( Vector3.Zero );
		int childCount = _localGameRoot?.Children?.Count ?? 0;
		Log.Info( $"[LocalGameRunner] Prefab cloned. Root={_localGameRoot?.Name}, Children={childCount}" );
		// Do NOT call _localGameRoot.NetworkSpawn() — keeps it client-only

		_disabledObjects.Clear();
		_stoppedMusic.Clear();

		// Hide the menu UI immediately — store refs so EndLocalGame can find them when disabled
		foreach ( var panel in Scene.GetAllComponents<Sandbox.UI.MainMenuPanel>().ToList() )
		{
			_disabledObjects.Add( panel.GameObject );
			panel.GameObject.Enabled = false;
		}

		// Stop menu's music so we don't double with the prefab's MusicManager
		foreach ( var music in Scene.GetAllComponents<MusicManager>().ToList() )
		{
			if ( IsUnder( music.GameObject, _localGameRoot ) ) continue;
			_stoppedMusic.Add( music );
			music.Stop();
		}

		// Disable menu's camera so the player's camera (created by PlayerController) is used
		foreach ( var cam in Scene.GetAllComponents<CameraComponent>().ToList() )
		{
			if ( IsUnder( cam.GameObject, _localGameRoot ) ) continue;
			_disabledObjects.Add( cam.GameObject );
			cam.GameObject.Enabled = false;
		}
	}

	public void EndLocalGame()
	{
		_localGameRoot?.Destroy();
		_localGameRoot = null;

		// Re-enable every object we disabled — use stored refs because GetAllComponents
		// skips disabled GameObjects and would miss them.
		foreach ( var go in _disabledObjects )
		{
			if ( go.IsValid() )
				go.Enabled = true;
		}
		_disabledObjects.Clear();

		// Restart menu music that was stopped when the game launched
		foreach ( var music in _stoppedMusic )
		{
			if ( music.IsValid() )
				music.Play();
		}
		_stoppedMusic.Clear();

		// Restore the cursor — PlayerController hides it during gameplay.
		Mouse.Visibility = MouseVisibility.Visible;
	}

	static bool IsUnder( GameObject go, GameObject root )
	{
		if ( !go.IsValid() || !root.IsValid() ) return false;
		var p = go.Parent;
		while ( p != null && p.IsValid() )
		{
			if ( p == root ) return true;
			if ( p is Scene ) break;
			p = ( p as GameObject )?.Parent;
		}
		return false;
	}
}
