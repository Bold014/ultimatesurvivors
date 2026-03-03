using SpriteTools;

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

	/// <summary>True while the in-game escape/pause menu is open.</summary>
	public static bool EscapeMenuOpen { get; set; } = false;

	/// <summary>True after the final boss is defeated. Waves continue forever; run ends only on player death.</summary>
	public bool IsEndlessModeActive { get; private set; } = false;

	private bool _welcomeSent           = false;
	private float _welcomeDelay         = 1.5f;
	private bool _runEnded              = false;
	private float _returnToMenuDelay    = 0f;
	private bool _tierCompleteGoldAwarded = false;
	private bool _challengeReachedFinalSwarm = false;
	private bool _challengeResolved = false;
	/// <summary>Set true after RecordWinResult fires so CheckRunEnd skips double-recording in endless mode.</summary>
	private bool _runResultRecorded = false;

	protected override void OnStart()
	{
		Instance = this;
		EscapeMenuOpen = false;
		Log.Info( $"[GameManager] OnStart. Scene={Scene?.Name}, IsInLocalGame={LocalGameRunner.IsInLocalGame}" );

		// If running outside of LocalGameRunner (e.g. game.scene loaded directly via lobby rejoin),
		// redirect to the menu scene so the player goes through proper character/map selection.
		if ( !Scene.IsEditor && LocalGameRunner.Instance == null )
		{
			Log.Info( "[GameManager] No LocalGameRunner found — redirecting to menu.scene." );
			var opts = new SceneLoadOptions();
			opts.SetScene( ResourceLibrary.Get<SceneFile>( "scenes/menu.scene" ) );
			Game.ChangeScene( opts );
			return;
		}

		PlayerProgress.Load();
		var selectedChallengeId = MenuManager.SelectedChallengeId;
		if ( !PlayerProgress.CanUseChallengesFor( MenuManager.SelectedMap ?? "dark_forest", MenuManager.SelectedTier ) )
			selectedChallengeId = null;
		ChallengeRuntime.SetActive( ChallengeDefinition.GetById( selectedChallengeId ) );
		if ( ChallengeRuntime.IsChallengeRun )
		{
			var c = ChallengeRuntime.ActiveChallenge;
			GameNotification.Show( $"Challenge Active: {c.Name}", new Color( 0.95f, 0.7f, 0.2f ), 4f );
		}
		EnsureMapExists();
		SpawnPlayer();
	}

	/// <summary>Ensures a MapSpawner exists so the procedural map will be created.</summary>
	/// <remarks>
	/// Always creates the MapSpawner unconditionally — do NOT check for existing TilesetComponents here.
	/// During a second run, orphaned tilesets from the previous run may still exist due to deferred Destroy().
	/// MapSpawner's own 1-frame delay lets those deferred destroys complete before it checks.
	/// </remarks>
	private void EnsureMapExists()
	{
		GameObject.Components.GetOrCreate<MapSpawner>();
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
			GameNotification.Show( "Survive 10 minutes and defeat the final boss!", new Color( 1f, 0.8f, 0.2f ), 4f );
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
				if ( LocalGameRunner.IsInLocalGame )
					LocalGameRunner.Instance?.EndLocalGame();
				else
				{
					var opts = new SceneLoadOptions();
					opts.SetScene( ResourceLibrary.Get<SceneFile>( "scenes/menu.scene" ) );
					Game.ChangeScene( opts );
				}
			}
			return;
		}

		ReturnToMenuCountdown = 0f;
		var spawner = Scene.GetAllComponents<EnemySpawner>().FirstOrDefault();
		if ( spawner?.IsFinalBossPhase == true )
			_challengeReachedFinalSwarm = true;

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

		// Soul Essence reward for tier completion (victory)
		int essenceWin = 5 + (tier * 3) + (stats.Kills / 50);
		PlayerProgress.Data.SoulEssence += essenceWin;
		PlayerProgress.Save();

		_tierCompleteGoldAwarded = true;
		spawner.ClearTierJustCompleted();

		GameNotification.Show( $"+{gold} gold, +{essenceWin} essence for defeating the final boss!", new Color( 1f, 0.85f, 0.2f ), 4f );

		// Broadcast victory to lobby chat so all players see it
		var victoryName  = Connection.Local?.DisplayName ?? Connection.Local?.Name ?? "A player";
		var victoryStats = Scene.GetAllComponents<PlayerStats>().FirstOrDefault();
		int victoryKills = victoryStats?.Kills ?? 0;
		ChatComponent.Instance?.SendServerMessage( $"{victoryName} defeated the final boss with {victoryKills} kills!", new Color( 1f, 0.85f, 0.2f ) );

		// Record run result so kills, survival time, and run count are synced to the leaderboard.
		RecordWinResult( stats, tier, map );
		ResolveChallengeOutcome( completedTier: true, died: false, stats );

		// Boss defeated — activate endless mode instead of returning to menu
		EscapeMenuOpen      = false;
		IsEndlessModeActive = true;
		GameNotification.Show( "ENDLESS MODE! Survive as long as you can!", new Color( 1f, 0.6f, 0f ), 5f );
	}

	// ── Abandon recording (escape menu quit/restart) ───────────────────────────
	public void RecordAbandonResult()
	{
		if ( _runEnded ) return; // already recorded (win or death path fired first)
		var stats = Scene.GetAllComponents<PlayerStats>().FirstOrDefault();
		if ( stats == null ) return;

		// Only count runs where the player had meaningful activity.
		bool hasActivity = stats.TimeAlive >= 60f || stats.Kills >= 1;
		if ( !hasActivity ) return;

		_runEnded = true; // prevent any second call from double-recording
		ResolveChallengeOutcome( completedTier: false, died: false, stats );

		var result = new RunResult
		{
			Kills                   = stats.Kills,
			Completed               = false,
			Died                    = false,
			CharacterId             = CharacterNameToId( stats.CharacterName ),
			MaxLevel                = stats.Level,
			SurviveMinutes          = (int)(stats.TimeAlive / 60f),
			KillsByWeapon           = new System.Collections.Generic.Dictionary<string, int>( stats.KillsByWeapon ),
			NoDamageSeconds         = stats.GameObject.Components.Get<PlayerLocalState>()?.LongestNoDamageSeconds ?? 0,
			MapId                   = MenuManager.SelectedMap ?? "dark_forest",
			TierCompleted           = 0,
			GoldEarned              = 0,
		};
		PlayerProgress.RecordRunResult( result );
	}

	// ── Win result recording ──────────────────────────────────────────────────
	private void RecordWinResult( PlayerStats stats, int tier, string map )
	{
		var tomes   = PlayerTomes.LocalInstance;
		var weapons = stats.GameObject.Components.Get<PlayerWeapons>();
		var state   = stats.GameObject.Components.Get<PlayerLocalState>();

		var tomeLevels   = new System.Collections.Generic.Dictionary<string, int>();
		if ( tomes != null )
			foreach ( var kv in tomes.GetAllLevels() )
				tomeLevels[kv.Key] = kv.Value;

		var weaponLevels = new System.Collections.Generic.Dictionary<string, int>();
		if ( weapons != null )
			foreach ( var w in weapons.Weapons )
				weaponLevels[w.WeaponDisplayName] = w.WeaponLevel;

		var result = new RunResult
		{
			Kills                   = stats.Kills,
			Completed               = true,
			Died                    = false,
			CharacterId             = CharacterNameToId( stats.CharacterName ),
			MaxLevel                = stats.Level,
			SurviveMinutes          = (int)(stats.TimeAlive / 60f),
			KillsByWeapon           = new System.Collections.Generic.Dictionary<string, int>( stats.KillsByWeapon ),
			NoDamageSeconds         = state?.LongestNoDamageSeconds ?? 0,
			TomeLevels              = tomeLevels,
			WeaponLevels            = weaponLevels,
			ChestsOpenedThisRun     = Chest.ChestsOpened,
			ProjectilesFiredThisRun = stats.ProjectilesFired,
			MapId                   = map,
			TierCompleted           = tier,
			GoldEarned              = 0,
		};
		PlayerProgress.RecordRunResult( result );
		_runResultRecorded = true;
	}

	// ── End-of-run check ──────────────────────────────────────────────────────
	private void CheckRunEnd()
	{
		List<PlayerStats> statsToRecord;
		if ( LocalGameRunner.IsInLocalGame )
		{
			// In client-local lobby runs, only this client's own gameplay player should drive run end.
			var localRunStats = Scene.GetAllComponents<PlayerStats>().FirstOrDefault( s => !s.IsProxy );
			if ( localRunStats == null || localRunStats.IsAlive ) return;
			statsToRecord = new List<PlayerStats> { localRunStats };
		}
		else
		{
			var allStats = Scene.GetAllComponents<PlayerStats>().ToList();
			if ( !allStats.Any() ) return;
			if ( !allStats.All( s => !s.IsAlive ) ) return;
			statsToRecord = allStats;
		}

		// Save best endless wave on any death during endless mode
		if ( IsEndlessModeActive )
		{
			var endlessSpawner = Scene.GetAllComponents<EnemySpawner>().FirstOrDefault();
			int endlessWaves   = endlessSpawner?.EndlessWavesCompleted ?? 0;
			if ( endlessWaves > PlayerProgress.Data.BestEndlessWave )
			{
				PlayerProgress.Data.BestEndlessWave = endlessWaves;
				PlayerProgress.Save();
			}
		}

		foreach ( var stats in statsToRecord )
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

			// Soul Essence: consolation reward on death (smaller than victory)
			int essenceDeath = completed ? 0 : 3 + (stats.Kills / 100);

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
				SoulEssenceEarned   = essenceDeath,
			};
			// Skip re-recording if we already recorded a win at tier complete (endless mode death)
			if ( !_runResultRecorded )
			{
				PlayerProgress.RecordRunResult( result );
				ResolveChallengeOutcome( completedTier: completed, died: !completed, stats );
			}
		}

		// Show essence earned notification
		var firstStats = statsToRecord.FirstOrDefault();
		int essenceEarned = _tierCompleteGoldAwarded ? 0 : 3 + ((firstStats?.Kills ?? 0) / 100);
		if ( essenceEarned > 0 )
			GameNotification.Show( $"+{essenceEarned} soul essence", new Color( 0.7f, 0.5f, 1f ), 3f );

		// Broadcast death stats to lobby chat so all players see it
		var deathName  = Connection.Local?.DisplayName ?? Connection.Local?.Name ?? "A player";
		var spawner    = Scene.GetAllComponents<EnemySpawner>().FirstOrDefault();
		int wave       = spawner?.WaveNumber ?? 0;
		var deathStats = statsToRecord.FirstOrDefault();
		int kills      = deathStats?.Kills ?? 0;
		ChatComponent.Instance?.SendServerMessage( $"{deathName} died and survived Wave {wave} with {kills} kills", new Color( 1f, 0.4f, 0.3f ) );

		EscapeMenuOpen     = false;
		_runEnded          = true;
		_returnToMenuDelay = 3f;
	}

	private void ResolveChallengeOutcome( bool completedTier, bool died, PlayerStats stats )
	{
		if ( _challengeResolved || !ChallengeRuntime.IsChallengeRun ) return;
		_challengeResolved = true;

		var challenge = ChallengeRuntime.ActiveChallenge;
		bool success = challenge.Goal switch
		{
			ChallengeGoalType.KillAllBosses => completedTier,
			ChallengeGoalType.ReachFinalSwarm => _challengeReachedFinalSwarm,
			_ => false
		};

		// Time-limit modifiers (Speedrunner variants) are additional constraints.
		if ( success && ChallengeRuntime.HasModifier( ChallengeModifierType.RunTimeLimitSeconds ) )
		{
			float limit = ChallengeRuntime.GetModifier( ChallengeModifierType.RunTimeLimitSeconds, 0f );
			if ( limit > 0f && (stats?.TimeAlive ?? 0f) > limit )
				success = false;
		}

		if ( success )
		{
			bool firstTime = PlayerProgress.TryCompleteChallenge( challenge.Id );
			if ( firstTime )
			{
				GameNotification.Show( $"Challenge complete: {challenge.Name} (+{challenge.PermanentBonusPercent:F0}% permanent coins)", new Color( 0.35f, 0.95f, 0.35f ), 5f );
			}
			else
			{
				GameNotification.Show( $"Challenge complete: {challenge.Name}", new Color( 0.35f, 0.95f, 0.35f ), 4f );
			}
		}
		else if ( died || !completedTier )
		{
			GameNotification.Show( $"Challenge failed: {challenge.Name}", new Color( 0.95f, 0.35f, 0.35f ), 4f );
		}
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
		var localRoot = LocalGameRunner.GetLocalGameRoot();
		if ( localRoot != null && localRoot.IsValid() )
			go.SetParent( localRoot );

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
		var sceneChildCount = Scene?.Children?.Count ?? 0;
		Log.Info( $"[GameManager] Player spawned. Scene children: {sceneChildCount}. If you see blue screen, game_run prefab may be missing map/HUD - recreate from full game.scene." );
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
		ChallengeRuntime.Clear();
		EscapeMenuOpen = false;
		ReturnToMenuCountdown = 0f;
		if ( Instance == this )
			Instance = null;
	}
}
