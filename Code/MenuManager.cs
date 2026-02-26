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

	protected override void OnStart()
	{
		Instance = this;
		PlayerProgress.Load();
	}

	protected override void OnUpdate()
	{
		Mouse.Visibility = MouseVisibility.Visible;
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}

	public static void StartGame( string characterName, string mapId = "dark_forest", int tier = 1 )
	{
		SelectedCharacter = characterName;
		SelectedMap       = mapId ?? "dark_forest";
		SelectedTier     = System.Math.Clamp( tier, 1, 3 );
		var options = new SceneLoadOptions();
		options.SetScene( ResourceLibrary.Get<SceneFile>( "scenes/game.scene" ) );
		Game.ChangeScene( options );
	}
}
