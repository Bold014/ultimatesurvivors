/// <summary>
/// Scene singleton for the main menu scene.
/// Loads player progress on startup and exposes scene navigation.
/// </summary>
public sealed class MenuManager : Component
{
	public static MenuManager Instance { get; private set; }

	/// <summary>
	/// The character name selected on the character select screen.
	/// Persists across the scene change so GameManager can read it.
	/// </summary>
	public static string SelectedCharacter { get; private set; } = "Archer";

	/// <summary>
	/// The map selected on the map select screen (e.g. "dark_forest").
	/// </summary>
	public static string SelectedMap { get; private set; } = "dark_forest";

	/// <summary>
	/// The difficulty tier selected (1, 2, or 3).
	/// </summary>
	public static int SelectedTier { get; private set; } = 1;
	/// <summary>
	/// Optional selected challenge ID for this run.
	/// </summary>
	public static string SelectedChallengeId { get; private set; } = null;

	// Deferred game start — set by StartGame, executed next OnUpdate so the
	// onclick lambda has fully returned before we clone the prefab.
	private static bool _pendingStart;

	protected override void OnStart()
	{
		Instance = this;
		PlayerProgress.Load();
		ReturnToHomepage();
	}

	protected override void OnUpdate()
	{
		Mouse.Visibility = MouseVisibility.Visible;

		if ( _pendingStart )
		{
			_pendingStart = false;
			var runner = LocalGameRunner.Instance;
			if ( runner == null )
			{
				Log.Error( "[MenuManager] LocalGameRunner.Instance is null — make sure a LocalGameRunner component exists in the menu scene." );
				return;
			}
			runner.StartLocalGame();
		}
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}

	public static void StartGame( string characterName, string mapId = "dark_forest", int tier = 1, string challengeId = null )
	{
		SelectedCharacter = characterName;
		SelectedMap       = mapId ?? "dark_forest";
		SelectedTier     = System.Math.Clamp( tier, 1, 3 );
		SelectedChallengeId = string.IsNullOrWhiteSpace( challengeId ) ? null : challengeId;
		if ( SelectedChallengeId != null && !PlayerProgress.CanUseChallengesFor( SelectedMap, SelectedTier ) )
			SelectedChallengeId = null;

		Log.Info( $"[MenuManager] StartGame — char={SelectedCharacter} map={SelectedMap} tier={SelectedTier} challenge={(SelectedChallengeId ?? "none")}" );

		// Defer to next frame so the panel onclick finishes before the prefab is cloned.
		_pendingStart = true;
	}

	/// <summary>
	/// Clears any delayed local-run start request.
	/// Used when joining a lobby to avoid starting a stale run.
	/// </summary>
	public static void ReturnToHomepage()
	{
		_pendingStart = false;
	}
}
