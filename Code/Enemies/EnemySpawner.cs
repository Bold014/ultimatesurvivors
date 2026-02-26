/// <summary>
/// Megabonk-style wave spawner. Alternates between a calm Intermission and an
/// active WaveActive phase. Each wave has a defined enemy composition; every
/// 5th wave is a Swarm (high-density burst) and waves 10 / 20 / 30 force a Boss.
/// Lives on the player GameObject — enemies are local-only and target the owner.
/// </summary>
public sealed class EnemySpawner : Component
{
	// ── Public API (consumed by GameHUD and PlayerController) ─────────────────
	public int   WaveNumber   => _waveNumber;
	public float RunTime      => _runTimer;
	public bool  IsActive     => _isActive;
	public bool  IsEndlessMode => _isEndlessMode;
	/// <summary>True for one frame when the tier's final wave is cleared. GameManager uses this to trigger endless transition.</summary>
	public bool  TierJustCompleted => _tierJustCompleted;

	// ── Wave definition ────────────────────────────────────────────────────────
	private readonly struct WaveDef
	{
		public readonly int   EnemyCount;   // total enemies to drip-spawn this wave
		public readonly float SpawnRate;    // seconds between individual spawns
		public readonly bool  IsSwarm;      // flood of a focused enemy type
		public readonly bool  HasBoss;      // force at least one boss spawn

		public WaveDef( int count, float rate, bool swarm = false, bool boss = false )
		{
			EnemyCount = count;
			SpawnRate  = rate;
			IsSwarm    = swarm;
			HasBoss    = boss;
		}
	}

	// 30 waves — one per minute of the 30-minute run.
	// Wave index is 0-based internally; WaveNumber exposed as 1-based.
	private static readonly WaveDef[] Waves = new WaveDef[30]
	{
		// Wave  1-3  — tutorial warmup (bats + basics)
		new( 12, 0.9f ),
		new( 14, 0.85f ),
		new( 16, 0.8f ),

		// Wave  4-5  — armored introduced; wave 5 is first swarm (bats)
		new( 18, 0.75f ),
		new( 40, 0.35f, swarm: true ),

		// Wave  6-9  — full mix ramps up
		new( 20, 0.7f ),
		new( 22, 0.65f ),
		new( 24, 0.6f ),
		new( 26, 0.55f ),

		// Wave 10   — first boss wave
		new( 30, 0.5f, boss: true ),

		// Wave 11-14 — heavier armored presence
		new( 28, 0.55f ),
		new( 30, 0.52f ),
		new( 32, 0.5f ),
		new( 34, 0.48f ),

		// Wave 15   — armored swarm
		new( 50, 0.28f, swarm: true ),

		// Wave 16-19 — dense mixed
		new( 36, 0.45f ),
		new( 38, 0.43f ),
		new( 40, 0.4f ),
		new( 42, 0.38f ),

		// Wave 20   — second boss wave
		new( 45, 0.35f, boss: true ),

		// Wave 21-24 — escalating density
		new( 44, 0.38f ),
		new( 46, 0.36f ),
		new( 48, 0.34f ),
		new( 50, 0.32f ),

		// Wave 25   — chaos swarm (all types)
		new( 60, 0.22f, swarm: true ),

		// Wave 26-29 — brutal late game
		new( 52, 0.3f ),
		new( 55, 0.28f ),
		new( 58, 0.26f ),
		new( 60, 0.24f ),

		// Wave 30   — final swarm (boss + everything)
		new( 80, 0.18f, swarm: true, boss: true ),
	};

	// ── State machine ──────────────────────────────────────────────────────────
	private enum SpawnState { Intermission, WaveActive }

	private SpawnState _state      = SpawnState.Intermission;
	private float      _stateTimer = 5f;   // 5 s grace before wave 1
	private int        _waveNumber = 0;    // 1-based; 0 = before first wave
	private int        _enemiesLeft = 0;
	private float      _spawnTimer  = 0f;
	private bool       _bossFired   = false;

	// ── Shared fields ──────────────────────────────────────────────────────────
	private float  _runTimer = 0f;
	private bool   _isActive = false;
	private Random _rand;
	private readonly List<GameObject> _spawnedEnemies = new();
	// Enemies spawned in the current wave — used to decide when the wave is truly cleared
	private readonly List<GameObject> _waveEnemies = new();

	// ── Tier and endless mode ───────────────────────────────────────────────────
	private int  _tier              = 3;  // 1, 2, or 3 — from MenuManager
	private int  _maxWavesForTier   = 30;
	private bool _isEndlessMode     = false;
	private bool _tierJustCompleted = false;

	private float RunDurationForTier => _maxWavesForTier * 60f;
	private const float IntermissionTime = 8f;
	private const float WaveHardCap      = 90f;   // safety: end wave after this many seconds

	/// <summary>Exponential growth per wave: speed/damage multiplier = Pow(1 + WaveScaleFactor, wave - 1).</summary>
	private const float WaveScaleFactor = 0.04f;  // ~3.2x at wave 30

	// ── Countdown chat state ───────────────────────────────────────────────────
	private int _lastCountdownSecond = -1;

	// ── Lifecycle ──────────────────────────────────────────────────────────────
	protected override void OnStart()
	{
		_tier            = MenuManager.SelectedTier;
		_maxWavesForTier = _tier * 10;
		int seed = Connection.Local != null
			? Connection.Local.SteamId.GetHashCode()
			: (int)(Time.Now * 1000);
		_rand     = new Random( seed );
		_isActive = true;
		Log.Info( $"[EnemySpawner] OnStart — seed={seed}, tier={_tier}, maxWaves={_maxWavesForTier}" );
	}

	protected override void OnUpdate()
	{
		if ( !_isActive || IsPaused ) return;

		_runTimer   += Time.Delta;
		_stateTimer -= Time.Delta;

		// Time limit only applies before endless mode
		if ( !_isEndlessMode && _runTimer >= RunDurationForTier )
		{
			_isActive = false;
			return;
		}

		switch ( _state )
		{
			case SpawnState.Intermission:
				TickIntermission();
				break;
			case SpawnState.WaveActive:
				TickWaveActive();
				break;
		}
	}

	// ── Intermission ──────────────────────────────────────────────────────────
	private void TickIntermission()
	{
		// Countdown chat: 3, 2, 1 seconds before next wave
		int nextWave = _waveNumber + 1;
		WaveDef? nextDef = GetWaveDef( nextWave );
		if ( nextDef.HasValue )
		{
			int secondsLeft = (int)Math.Ceiling( _stateTimer );
			if ( secondsLeft <= 3 && secondsLeft > 0 && secondsLeft != _lastCountdownSecond )
			{
				_lastCountdownSecond = secondsLeft;
				string label = WaveLabel( nextWave, nextDef.Value );
				ChatComponent.Instance?.AddMessage( "Wave", $"{label} — {secondsLeft}…", new Color( 1f, 0.85f, 0.2f ) );
			}
		}

		if ( _stateTimer > 0f ) return;

		// Advance to next wave
		_waveNumber++;
		WaveDef? def = GetWaveDef( _waveNumber );
		if ( !def.HasValue )
		{
			_isActive = false;
			return;
		}

		StartWave( def.Value );
	}

	// ── Wave active ───────────────────────────────────────────────────────────
	private void TickWaveActive()
	{
		// Hard cap — force-end the wave if the player takes too long
		bool timedOut = _stateTimer <= -WaveHardCap;

		WaveDef def = GetWaveDef( _waveNumber ) ?? Waves[^1];

		// Spawning phase: drip enemies until the wave count is exhausted
		if ( _enemiesLeft > 0 )
		{
			_spawnTimer -= Time.Delta;
			if ( _spawnTimer <= 0f )
			{
				SpawnEnemy( def );
				_enemiesLeft--;
				_spawnTimer = def.SpawnRate;
			}
		}

		// Clear phase: wait until every enemy from this wave is dead (or hard-cap hit)
		bool allDead = _waveEnemies.All( go => !go.IsValid() );
		if ( (_enemiesLeft <= 0 && allDead) || timedOut )
		{
			EndWave();
		}
	}

	// ── Wave lifecycle helpers ─────────────────────────────────────────────────
	private void StartWave( WaveDef def )
	{
		_state        = SpawnState.WaveActive;
		_stateTimer   = 0f;
		_enemiesLeft  = def.EnemyCount;
		_spawnTimer   = 0f;
		_bossFired    = false;
		_lastCountdownSecond = -1;
		_waveEnemies.Clear();

		string label = WaveLabel( _waveNumber, def );
		Color  color = def.IsSwarm ? new Color( 1f, 0.4f, 0.1f )
		             : def.HasBoss  ? Color.Magenta
		             : new Color( 0.4f, 0.9f, 0.4f );

		ChatComponent.Instance?.AddMessage( "Wave", label, color );
		Log.Info( $"[EnemySpawner] {label} started (count={def.EnemyCount}, rate={def.SpawnRate}s, swarm={def.IsSwarm}, boss={def.HasBoss})" );
	}

	private void EndWave()
	{
		// Check if we just completed the tier's final wave — transition to endless
		bool wasTierFinalWave = !_isEndlessMode && _waveNumber >= _maxWavesForTier;
		if ( wasTierFinalWave )
		{
			_isEndlessMode     = true;
			_tierJustCompleted = true;
			ChatComponent.Instance?.AddMessage( "System", "Tier complete! Endless mode activated!", new Color( 0.4f, 1f, 0.4f ) );
		}

		_state        = SpawnState.Intermission;
		_stateTimer   = IntermissionTime;
		_lastCountdownSecond = -1;

		// Announce next wave if there is one
		int next = _waveNumber + 1;
		WaveDef? nextDef = GetWaveDef( next );
		if ( nextDef.HasValue )
		{
			string nextLabel = WaveLabel( next, nextDef.Value );
			ChatComponent.Instance?.AddMessage( "Wave", $"Wave {_waveNumber} cleared. Next: {nextLabel}", new Color( 0.7f, 0.9f, 1f ) );
		}
	}

	/// <summary>Returns wave definition for any wave. For waves 31+, generates dynamically (boss every 10th).</summary>
	private WaveDef? GetWaveDef( int waveNum )
	{
		if ( waveNum >= 1 && waveNum <= Waves.Length )
			return Waves[waveNum - 1];
		if ( waveNum > Waves.Length )
		{
			// Endless: scale from wave 30 pattern, boss every 10th
			bool isBoss = waveNum % 10 == 0;
			int  over   = waveNum - 30;
			int  count  = 80 + over * 4;
			float rate  = MathF.Max( 0.12f, 0.18f - over * 0.002f );
			return new WaveDef( count, rate, swarm: true, boss: isBoss );
		}
		return null;
	}

	private static string WaveLabel( int num, WaveDef def )
	{
		if ( def.IsSwarm && def.HasBoss ) return $"Wave {num} — FINAL SWARM!";
		if ( def.IsSwarm )               return $"Wave {num} — SWARM!";
		if ( def.HasBoss )               return $"Wave {num} — BOSS WAVE!";
		return                                  $"Wave {num}";
	}

	// ── Enemy spawning ────────────────────────────────────────────────────────
	private void SpawnEnemy( WaveDef def )
	{
		float angle  = (float)(_rand.NextDouble() * 360.0);
		float dist   = 380f + (float)(_rand.NextDouble() * 80f);
		var   offset = new Vector3(
			MathF.Cos( angle * MathF.PI / 180f ) * dist,
			MathF.Sin( angle * MathF.PI / 180f ) * dist,
			0f );

		var go = new GameObject( true, "Enemy" );
		go.WorldPosition = (WorldPosition + offset).WithZ( 0f );

		var   enemy     = go.Components.Create<EnemyBase>();
		enemy.Target    = GameObject;

		// Stat scaling — grows as the run progresses
		float timeScale = 1f + _runTimer / 120f;
		int   w         = _waveNumber;

		// Determine enemy type
		EnemyType type = PickType( def, timeScale );

		// Force boss for the first enemy of a boss wave
		if ( def.HasBoss && !_bossFired )
		{
			type       = EnemyType.Boss;
			_bossFired = true;
		}

		ApplyType( go, enemy, type, timeScale, w );
		_spawnedEnemies.Add( go );
		_waveEnemies.Add( go );
	}

	private enum EnemyType { Basic, Bat, Armored, Boss }

	private EnemyType PickType( WaveDef def, float timeScale )
	{
		int w = _waveNumber;

		// Swarm waves focus a specific type based on which swarm it is
		if ( def.IsSwarm )
		{
			if ( w <= 5  ) return EnemyType.Bat;
			if ( w <= 15 ) return EnemyType.Armored;
			// wave 25 / 30 — chaos: distribute evenly among all
		}

		// Normal weighted rolls per wave bracket
		float batWeight     = w <= 3 ? 0.55f : (w <= 9 ? 0.30f : 0.18f);
		float armoredWeight = w >= 4 ? (w <= 9 ? 0.25f : (w <= 19 ? 0.38f : 0.42f)) : 0f;
		float bossWeight    = def.HasBoss ? 0.10f : 0f;
		float basicWeight   = Math.Max( 0f, 1f - batWeight - armoredWeight - bossWeight );

		float roll = (float)_rand.NextDouble();

		if ( roll < bossWeight )                        return EnemyType.Boss;
		if ( roll < bossWeight + armoredWeight )        return EnemyType.Armored;
		if ( roll < bossWeight + armoredWeight + batWeight ) return EnemyType.Bat;
		return EnemyType.Basic;
	}

	private void ApplyType( GameObject go, EnemyBase enemy, EnemyType type, float timeScale, int wave )
	{
		// Exponential scaling: wave 1 = 1.0, grows as Pow(1 + WaveScaleFactor, wave - 1).
		float waveScale = MathF.Pow( 1f + WaveScaleFactor, wave - 1 );
		float dmgScale  = timeScale * waveScale;
		float speedScale = waveScale;

		switch ( type )
		{
			case EnemyType.Boss:
				// Slow, crushing hits — gives the player a window to react but punishes standing still.
				enemy.MaxHP                  = 600f * timeScale;
				enemy.Speed                  = 15.2f * speedScale;
				enemy.ContactDamage          = 40f * dmgScale;
				enemy.DamageCooldownDuration = 2.2f;
				enemy.XPValue                = 60;
				enemy.EnemyColor             = Color.Magenta;
				enemy.SizeScale              = 2f;
				go.Name                      = "Boss";
				break;

			case EnemyType.Armored:
				// Heavy, deliberate strikes — less frequent but chunky.
				enemy.MaxHP                  = 90f * timeScale;
				enemy.Speed                  = (20.8f + (float)(_rand.NextDouble() * 6f)) * speedScale;
				enemy.ContactDamage          = 22f * dmgScale;
				enemy.DamageCooldownDuration = 1.4f;
				enemy.XPValue                = 18;
				enemy.EnemyColor             = new Color( 0.5f, 0.5f, 1f );
				enemy.SizeScale              = 1.2f;
				go.Name                      = "EnemyArmored";
				break;

			case EnemyType.Bat:
				// Fast nips — dangerous in swarms because the rapid cadence adds up quickly.
				enemy.MaxHP                  = 18f * timeScale;
				enemy.Speed                  = 44f * speedScale;
				enemy.ContactDamage          = 12f * dmgScale;
				enemy.DamageCooldownDuration = 0.6f;
				enemy.XPValue                = 8;
				enemy.EnemyColor             = new Color( 0.85f, 0.45f, 0.1f );
				enemy.SizeScale              = 0.65f;
				enemy.SpritePath             = "sprites/wraith/wraithanimations.sprite";
				enemy.DieAnimation           = "die";
				enemy.DieAnimDuration        = 0.5f;
				go.Name                      = "EnemyBat";
				break;

			default: // Basic
				// Steady pressure — straightforward 1-second rhythm.
				enemy.MaxHP                  = 30f * timeScale;
				enemy.Speed                  = (20f + (float)(_rand.NextDouble() * 8f)) * speedScale;
				enemy.ContactDamage          = 14f * dmgScale;
				enemy.DamageCooldownDuration = 1.0f;
				enemy.XPValue                = 5;
				enemy.EnemyColor             = new Color( 0.85f, 0.15f, 0.15f );
				go.Name                      = "Enemy";
				break;
		}
	}

	// ── Public control ────────────────────────────────────────────────────────
	public bool IsPaused { get; private set; } = false;

	public void SetPaused( bool paused )
	{
		IsPaused = paused;
	}

	/// <summary>Call after processing tier completion. Clears the one-frame TierJustCompleted flag.</summary>
	public void ClearTierJustCompleted()
	{
		_tierJustCompleted = false;
	}

	public void StopSpawning()
	{
		_isActive = false;
		foreach ( var go in _spawnedEnemies )
		{
			if ( go.IsValid() )
				go.Destroy();
		}
		_spawnedEnemies.Clear();
	}
}
