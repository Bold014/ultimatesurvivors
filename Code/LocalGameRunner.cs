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
	private GameObject _runtimeEntitiesRoot;

	// Snapshot of scene-root objects taken just before cloning the game prefab.
	// Anything NOT in this set that appears at the scene root during gameplay was
	// spawned by game code (enemies, player, gems, map, projectiles, etc.) and must
	// be explicitly destroyed when the game ends, because those objects are created
	// with plain `new GameObject(...)` which parents them to the scene root rather
	// than to _localGameRoot.
	private HashSet<GameObject> _preGameRootObjects;

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

	/// <summary>
	/// Returns the cloned local game root for this client's active run, if any.
	/// </summary>
	public static GameObject GetLocalGameRoot()
	{
		var instance = Instance;
		if ( instance == null ) return null;
		if ( instance._localGameRoot != null && instance._localGameRoot.IsValid() )
			return instance._localGameRoot;
		return null;
	}

	/// <summary>
	/// Returns the active runtime parent for dynamically spawned gameplay entities.
	/// In local runs this is the RuntimeEntities container under the cloned game root.
	/// Returns null when no local run is active so callers can safely fall back.
	/// </summary>
	public static GameObject GetRuntimeParent()
	{
		var instance = Instance;
		if ( instance == null ) return null;

		if ( instance._runtimeEntitiesRoot != null && instance._runtimeEntitiesRoot.IsValid() )
			return instance._runtimeEntitiesRoot;

		if ( instance._localGameRoot != null && instance._localGameRoot.IsValid() )
			return instance._localGameRoot;

		return null;
	}

	/// <summary>
	/// Parents a runtime-spawned object under the local run hierarchy, preserving world transform.
	/// No-op when no local run is active.
	/// </summary>
	public static void ParentRuntimeObject( GameObject go )
	{
		if ( go == null || !go.IsValid() ) return;

		var parent = GetRuntimeParent();
		if ( parent == null || !parent.IsValid() ) return;

		var worldPosition = go.WorldPosition;
		var worldRotation = go.WorldRotation;
		var worldScale = go.WorldScale;
		go.SetParent( parent );
		go.WorldPosition = worldPosition;
		go.WorldRotation = worldRotation;
		go.WorldScale = worldScale;
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

		// Snapshot scene-root objects before cloning so we can clean up orphans on end.
		_preGameRootObjects = new HashSet<GameObject>(
			Scene.Children.OfType<GameObject>().Where( g => g.IsValid() ) );

		_localGameRoot = GamePrefab.Clone( Vector3.Zero );
		_runtimeEntitiesRoot = new GameObject( true, "RuntimeEntities" );
		_runtimeEntitiesRoot.SetParent( _localGameRoot );
		_runtimeEntitiesRoot.LocalPosition = Vector3.Zero;
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
		// Stop game audio explicitly before destroy — deferred destruction would otherwise
		// let the game's MusicManager keep playing until end-of-frame.
		if ( _localGameRoot != null )
		{
			foreach ( var music in _localGameRoot.Components.GetAll<MusicManager>( FindMode.EverythingInDescendants ).ToList() )
				music.Stop();
		}

		_localGameRoot?.Destroy();
		_localGameRoot = null;
		_runtimeEntitiesRoot = null;

		// Destroy every scene-root object that wasn't there before the game started.
		// This catches all runtime-spawned objects (player, enemies, gems, map tiles,
		// projectiles, chests, damage indicators, etc.) that use `new GameObject()`
		// without setting a parent and therefore land at the scene root.
		if ( _preGameRootObjects != null )
		{
			foreach ( var go in Scene.Children.OfType<GameObject>().ToList() )
			{
				if ( go.IsValid() && !_preGameRootObjects.Contains( go ) )
					go.Destroy();
			}
			_preGameRootObjects = null;
		}

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
