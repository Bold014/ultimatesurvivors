/// <summary>
/// Scene singleton. Spawns the local player immediately on start (no networking).
/// This avoids all IsProxy / [Sync] / lobby-timing issues for a singleplayer run.
/// Multiplayer support can be layered on top later once core gameplay is stable.
/// </summary>
public sealed class GameManager : Component
{
	public static GameManager Instance { get; private set; }

	/// <summary>Seconds until return to menu (for death screen countdown). 0 when not returning.</summary>
	public static float ReturnToMenuCountdown { get; private set; } = 0f;

	private bool _welcomeSent           = false;
	private float _welcomeDelay         = 1.5f;
	private bool _runEnded              = false;
	private float _returnToMenuDelay    = 0f;
	private bool _tierCompleteGoldAwarded = false;

	protected override void OnStart()
	{
		Instance = this;
		PlayerProgress.Load();
		SpawnPlayer();
	}

	protected override void OnUpdate()
	{
		// ── Welcome message ───────────────────────────────────────────────────
		if ( !_welcomeSent )
		{
			_welcomeDelay -= Time.Delta;
			if ( _welcomeDelay <= 0f )
			{
				_welcomeSent = true;
				int tier = MenuManager.SelectedTier;
				int waves = tier * 10;
				ChatComponent.Instance?.AddMessage( "System", "Welcome to Ultimate Survivors!", Color.Yellow );
				ChatComponent.Instance?.AddMessage( "System", "Survive 10 minutes! Beat 3 mini-bosses, then the final boss!", new Color( 1f, 0.8f, 0.2f ) );
			}
		}

		// ── Run-end return to menu ────────────────────────────────────────────
		if ( _runEnded )
		{
			ReturnToMenuCountdown = MathF.Max( 0f, _returnToMenuDelay );
			_returnToMenuDelay -= Time.Delta;
			if ( _returnToMenuDelay <= 0f )
			{
				ReturnToMenuCountdown = 0f;
				var opts = new SceneLoadOptions();
				opts.SetScene( ResourceLibrary.Get<SceneFile>( "scenes/menu.scene" ) );
				Game.ChangeScene( opts );
			}
			return;
		}

		ReturnToMenuCountdown = 0f;

		CheckTierComplete();
		CheckRunEnd();
	}

	// ── Tier completion: award gold when player defeats final boss ─────────────
	private void CheckTierComplete()
	{
		var spawner = Scene.GetAllComponents<EnemySpawner>().FirstOrDefault();
		if ( spawner == null || !spawner.TierJustCompleted ) return;

		var allStats = Scene.GetAllComponents<PlayerStats>();
		if ( !allStats.Any() || !allStats.All( s => s.IsAlive ) ) return;

		var stats = allStats.FirstOrDefault();
		if ( stats == null ) return;

		int tier   = MenuManager.SelectedTier;
		string map = MenuManager.SelectedMap ?? "dark_forest";

		int surviveMinutes = (int)(stats.TimeAlive / 60f);
		int baseGold       = 50 + (tier * 20) + (surviveMinutes * 2) + (stats.Kills / 20);
		float mult         = tier switch { 1 => 1f, 2 => 1.1f, 3 => 1.2f, _ => 1f };
		int gold           = (int)(baseGold * mult);

		var state = stats.GameObject.Components.Get<PlayerLocalState>();
		if ( state != null )
			gold = (int)(gold * state.GoldMultiplier);

		PlayerProgress.Data.Coins += gold;
		PlayerProgress.Data.HighestTierCompletedByMap.TryGetValue( map, out var prev );
		if ( tier > prev )
			PlayerProgress.Data.HighestTierCompletedByMap[map] = tier;
		PlayerProgress.Save();

		_tierCompleteGoldAwarded = true;
		spawner.ClearTierJustCompleted();

		ChatComponent.Instance?.AddMessage( "System", $"+{gold} gold for defeating the final boss!", new Color( 1f, 0.85f, 0.2f ) );

		// Victory — return to menu
		_runEnded          = true;
		_returnToMenuDelay = 8f;
		ChatComponent.Instance?.AddMessage( "System", "Victory! Returning to menu...", new Color( 0.4f, 1f, 0.4f ) );
	}

	// ── End-of-run check ──────────────────────────────────────────────────────
	private void CheckRunEnd()
	{
		var allStats = Scene.GetAllComponents<PlayerStats>();
		if ( !allStats.Any() ) return;
		if ( !allStats.All( s => !s.IsAlive ) ) return;

		foreach ( var stats in allStats )
		{
			var playerState = stats.GameObject.Components.Get<PlayerLocalState>();
			var tomes       = PlayerTomes.LocalInstance;
			var weapons    = stats.GameObject.Components.Get<PlayerWeapons>();

			var tomeLevels = new System.Collections.Generic.Dictionary<string, int>();
			if ( tomes != null )
				foreach ( var kv in tomes.GetAllLevels() )
					tomeLevels[kv.Key] = kv.Value;

			var weaponLevels = new System.Collections.Generic.Dictionary<string, int>();
			if ( weapons != null )
				foreach ( var w in weapons.Weapons )
					weaponLevels[w.WeaponDisplayName] = w.WeaponLevel;

			// Completed = survived tier's final wave (we already awarded gold in CheckTierComplete)
			bool completed = _tierCompleteGoldAwarded;

			// Gold: already awarded at tier complete; on death before tier complete, no completion gold
			int goldEarned = 0;
			if ( !_tierCompleteGoldAwarded && !completed )
			{
				// Died before completing tier — optional partial gold (plan says gold for completing only)
				// We could add a small consolation, but plan says "gold for completing level" — so 0
			}

			var result = new RunResult
			{
				Kills               = stats.Kills,
				Completed           = completed,
				Died                = !completed,
				CharacterId         = CharacterNameToId( stats.CharacterName ),
				MaxLevel            = stats.Level,
				SurviveMinutes      = (int)(stats.TimeAlive / 60f),
				KillsByWeapon       = new System.Collections.Generic.Dictionary<string, int>( stats.KillsByWeapon ),
				NoDamageSeconds     = playerState?.LongestNoDamageSeconds ?? 0,
				TomeLevels          = tomeLevels,
				WeaponLevels        = weaponLevels,
				ChestsOpenedThisRun = Chest.ChestsOpened,
				ProjectilesFiredThisRun = stats.ProjectilesFired,
				MapId               = MenuManager.SelectedMap ?? "dark_forest",
				TierCompleted       = MenuManager.SelectedTier,
				GoldEarned          = goldEarned,
			};
			PlayerProgress.RecordRunResult( result );
		}

		_runEnded          = true;
		_returnToMenuDelay = 8f;
		ChatComponent.Instance?.AddMessage( "System", "All players defeated. Returning to menu...", new Color( 1f, 0.4f, 0.4f ) );
	}

	// ── Player spawning ───────────────────────────────────────────────────────
	private void SpawnPlayer()
	{
		// Destroy any player already placed in the scene (editor remnant) to avoid duplicates
		var existingControllers = Scene.GetAllComponents<PlayerController>().ToList();
		foreach ( var existing in existingControllers )
			existing.GameObject.Destroy();

		// Also nuke any orphaned HPBar GOs that survived
		var orphanBars = Scene.Children.Where( c => c.Name == "HPBar" ).ToList();
		foreach ( var bar in orphanBars )
			bar.Destroy();

		var go = new GameObject( true, "Player" );
		go.WorldPosition = Vector3.Zero;

		go.Components.Create<PlayerStats>();
		go.Components.Create<PlayerLocalState>();
		go.Components.Create<PlayerCoins>();
		go.Components.Create<PlayerWeapons>();
		go.Components.Create<PlayerPassives>();
		go.Components.Create<PlayerTomes>();
		go.Components.Create<PlayerXP>();
		go.Components.Create<UpgradeSystem>();
		go.Components.Create<EnemySpawner>();
		go.Components.Create<WorldObjectSpawner>();
		go.Components.Create<PlayerController>();
	}

	private static string CharacterNameToId( string name ) => name?.ToLower() switch
	{
		"archer"     => "char_archer",
		"warrior"    => "char_warrior",
		"mage"       => "char_mage",
		"knight"     => "char_knight",
		"templar"    => "char_templar",
		"druid"      => "char_druid",
		"pyromancer" => "char_pyromancer",
		_            => null
	};

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}
}
