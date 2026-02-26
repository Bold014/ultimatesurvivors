/// <summary>
/// Scene singleton. Spawns the local player immediately on start (no networking).
/// This avoids all IsProxy / [Sync] / lobby-timing issues for a singleplayer run.
/// Multiplayer support can be layered on top later once core gameplay is stable.
/// </summary>
public sealed class GameManager : Component
{
	public static GameManager Instance { get; private set; }

	private bool _welcomeSent           = false;
	private float _welcomeDelay         = 1.5f;
	private bool _runEnded              = false;
	private float _returnToMenuDelay    = 0f;
	private bool _tierCompleteGoldAwarded = false;

	protected override void OnStart()
	{
		Instance = this;
		PlayerProgress.Load();

		Log.Info( "[GameManager] OnStart — spawning player" );
		SpawnPlayer();
		Log.Info( "[GameManager] SpawnPlayer() returned" );

		// Add DamageIndicatorManager to HUD for screen-space damage numbers (avoids WorldPanel culling)
		var hud = Scene.Children.FirstOrDefault( c => c.Name == "HUD" );
		if ( hud != null && hud.Components.Get<DamageIndicatorManager>() == null )
			hud.Components.Create<DamageIndicatorManager>();
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
				ChatComponent.Instance?.AddMessage( "System", $"Survive {waves} waves. Beat the boss to enter endless mode!", new Color( 1f, 0.8f, 0.2f ) );
			}
		}

		// ── Run-end return to menu ────────────────────────────────────────────
		if ( _runEnded )
		{
			_returnToMenuDelay -= Time.Delta;
			if ( _returnToMenuDelay <= 0f )
			{
				var opts = new SceneLoadOptions();
				opts.SetScene( ResourceLibrary.Get<SceneFile>( "scenes/menu.scene" ) );
				Game.ChangeScene( opts );
			}
			return;
		}

		CheckTierComplete();
		CheckRunEnd();
	}

	// ── Tier completion: award gold and update unlocks when player clears final wave ─
	private void CheckTierComplete()
	{
		var spawner = Scene.GetAllComponents<EnemySpawner>().FirstOrDefault();
		if ( spawner == null || !spawner.TierJustCompleted ) return;

		// Player must still be alive (we're transitioning to endless)
		var allStats = Scene.GetAllComponents<PlayerStats>();
		if ( !allStats.Any() || !allStats.All( s => s.IsAlive ) ) return;

		var stats = allStats.FirstOrDefault();
		if ( stats == null ) return;

		int tier   = MenuManager.SelectedTier;
		string map = MenuManager.SelectedMap ?? "dark_forest";

		// Base gold: 50 + (tier * 20) + (SurviveMinutes * 2) + (Kills / 20)
		int surviveMinutes = (int)(stats.TimeAlive / 60f);
		int baseGold       = 50 + (tier * 20) + (surviveMinutes * 2) + (stats.Kills / 20);
		float mult         = tier switch { 1 => 1f, 2 => 1.1f, 3 => 1.2f, _ => 1f };
		int gold           = (int)(baseGold * mult);

		// Apply PlayerLocalState gold multiplier (tomes, etc.)
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

		ChatComponent.Instance?.AddMessage( "System", $"+{gold} gold for completing Tier {tier}!", new Color( 1f, 0.85f, 0.2f ) );
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

			var tomeLevels = new System.Collections.Generic.Dictionary<string, int>();
			if ( tomes != null )
				foreach ( var kv in tomes.GetAllLevels() )
					tomeLevels[kv.Key] = kv.Value;

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
		Log.Info( $"[GameManager] SpawnPlayer — found {existingControllers.Count} existing PlayerController(s)" );
		foreach ( var existing in existingControllers )
		{
			Log.Info( $"[GameManager] Destroying existing player GO: '{existing.GameObject.Name}' id={existing.GameObject.Id}" );
			existing.GameObject.Destroy();
		}

		// Also nuke any orphaned HPBar GOs that survived
		var orphanBars = Scene.Children.Where( c => c.Name == "HPBar" ).ToList();
		Log.Info( $"[GameManager] Found {orphanBars.Count} orphaned HPBar GO(s) — destroying" );
		foreach ( var bar in orphanBars )
			bar.Destroy();

		var go = new GameObject( true, "Player" );
		Log.Info( $"[GameManager] Created new Player GO id={go.Id}" );
		go.WorldPosition = Vector3.Zero;

		go.Components.Create<PlayerStats>();
		Log.Info( "[GameManager] Created PlayerStats" );
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
		Log.Info( "[GameManager] All components created on player GO" );

		// Verify components are actually findable via scene query
		var statsFound = Scene.GetAllComponents<PlayerStats>().FirstOrDefault();
		var xpFound    = Scene.GetAllComponents<PlayerXP>().FirstOrDefault();
		Log.Info( $"[GameManager] Scene query — PlayerStats found: {statsFound != null}, PlayerXP found: {xpFound != null}" );
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
