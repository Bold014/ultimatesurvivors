/// <summary>
/// Endless wave-based spawner.
/// 3 intensity surges at 2:30, 5:00, 7:30 ramp up wave difficulty, then waves continue forever.
/// Lives on the player GameObject.
/// </summary>
public sealed class EnemySpawner : Component
{
	public static EnemySpawner LocalInstance { get; private set; }

	// ── Public API ────────────────────────────────────────────────────────────
	public float RunTime       => _runTimer;
	public float TimeRemaining => MathF.Max( 0f, RunDuration - _runTimer );
	public bool  IsActive      => _isActive;
	public int   WaveNumber    => _waveNumber;
	/// <summary>True when we're past 10 min and only the final boss remains (or is dead).</summary>
	public bool  IsFinalBossPhase => _finalBossPhase;
	/// <summary>True for one frame when the final boss is defeated. GameManager uses this for tier completion.</summary>
	public bool  TierJustCompleted => _tierJustCompleted;

	/// <summary>Current intensity phase (0–3). Increments at 2:30, 5:00, 7:30.</summary>
	public int IntensityPhase => _intensityPhase;

	private const float RunDuration = 600f;  // 10 minutes
	private static readonly float[] IntensityPhaseTimes = { 150f, 300f, 450f };  // 2:30, 5:00, 7:30

	// ── Wave definitions (10 waves for 10-min run) ─────────────────────────────
	private readonly struct WaveDef
	{
		public readonly int   EnemyCount;
		public readonly float SpawnRate;

		public WaveDef( int count, float rate ) { EnemyCount = count; SpawnRate = rate; }
	}

	private static readonly WaveDef[] Waves = new WaveDef[]
	{
		new( 18, 0.55f ), new( 22, 0.5f ), new( 26, 0.45f ), new( 30, 0.42f ), new( 34, 0.38f ),
		new( 38, 0.35f ), new( 42, 0.32f ), new( 46, 0.3f ),  new( 50, 0.28f ), new( 55, 0.25f ),
	};

	private const float IntermissionTime = 4f;
	private const float TimeScaleFactor  = 0.035f;

	// ── State ─────────────────────────────────────────────────────────────────
	private enum SpawnState { Intermission, WaveActive }

	private SpawnState _state           = SpawnState.Intermission;
	private float     _stateTimer      = 3f;   // 3s grace before wave 1
	private int       _waveNumber      = 0;
	private int       _enemiesLeft     = 0;
	private float     _spawnTimer      = 0f;
	private float     _runTimer        = 0f;
	private int       _intensityPhase  = 0;
	private float     _effectiveSpawnRate = 0f;
	private bool      _finalBossPhase   = false;
	private bool      _tierJustCompleted = false;
	private bool      _isActive        = false;
	private Random    _rand;
	private readonly List<GameObject> _spawnedEnemies = new();
	private readonly List<GameObject> _waveEnemies    = new();
	private GameObject _finalBossObject;
	private int       _lastCountdownSecond = -1;

	protected override void OnStart()
	{
		LocalInstance = this;
		int seed = Connection.Local != null
			? Connection.Local.SteamId.GetHashCode()
			: (int)(Time.Now * 1000);
		_rand    = new Random( seed );
		_isActive = true;
	}

	protected override void OnDestroy()
	{
		if ( LocalInstance == this ) LocalInstance = null;
	}

	protected override void OnUpdate()
	{
		if ( !_isActive || IsPaused ) return;

		_runTimer += Time.Delta;
		_stateTimer -= Time.Delta;

		// Hard transition to the final boss exactly when the run timer expires.
		if ( !_finalBossPhase && _runTimer >= RunDuration )
		{
			EnterFinalBossPhase();
			return;
		}

		// Check if final boss was killed (object destroyed)
		if ( _finalBossObject != null && !_finalBossObject.IsValid() )
		{
			_finalBossObject = null;
			_tierJustCompleted = true;
			GameNotification.Show( "FINAL BOSS DEFEATED! Victory!", new Color( 0.4f, 1f, 0.4f ), 5f );
		}

		// Intensity phase surges at 2:30, 5:00, 7:30 (ramps wave difficulty)
		for ( int i = _intensityPhase; i < IntensityPhaseTimes.Length; i++ )
		{
			if ( _runTimer >= IntensityPhaseTimes[i] )
			{
				_intensityPhase = i + 1;
				GameNotification.Show( $"INTENSITY SURGE! Phase {_intensityPhase}!", Color.Magenta );
			}
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

	private void TickIntermission()
	{
		int nextWave = _waveNumber + 1;
		if ( nextWave <= Waves.Length )
		{
			int secs = (int)Math.Ceiling( _stateTimer );
			if ( secs <= 3 && secs > 0 && secs != _lastCountdownSecond )
			{
				_lastCountdownSecond = secs;
				GameNotification.Show( $"Wave {nextWave} in {secs}…", new Color( 1f, 0.85f, 0.2f ), 1.2f );
			}
		}

		if ( _stateTimer > 0f ) return;

		_waveNumber = nextWave;
		WaveDef? def = GetWaveDef( _waveNumber );
		StartWave( def ?? Waves[^1] );
	}

	private void TickWaveActive()
	{
		WaveDef def = GetWaveDef( _waveNumber ) ?? Waves[^1];

		if ( _enemiesLeft > 0 )
		{
			_spawnTimer -= Time.Delta;
			if ( _spawnTimer <= 0f )
			{
				SpawnEnemy( def );
				_enemiesLeft--;
				_spawnTimer = _effectiveSpawnRate;
			}
		}

		bool allDead = _waveEnemies.All( go => !go.IsValid() );
		if ( _enemiesLeft <= 0 && allDead )
			EndWave();
	}

	private void StartWave( WaveDef def )
	{
		float countMult = 1f + _intensityPhase * 0.24f;
		countMult *= ChallengeRuntime.GetCombinedMultiplier( ChallengeModifierType.EnemyAmountMultiplier );
		float rateMult  = MathF.Max( 0.55f, 1f - _intensityPhase * 0.14f );

		_state             = SpawnState.WaveActive;
		_stateTimer        = 0f;
		_enemiesLeft       = (int)(def.EnemyCount * countMult);
		_effectiveSpawnRate = def.SpawnRate * rateMult;
		_spawnTimer        = 0f;
		_lastCountdownSecond = -1;
		_waveEnemies.Clear();

		GameNotification.Show( $"Wave {_waveNumber}!", new Color( 0.4f, 0.9f, 0.4f ) );
	}

	private void EndWave()
	{
		_state      = SpawnState.Intermission;
		_stateTimer = IntermissionTime;
		_lastCountdownSecond = -1;

		int next = _waveNumber + 1;
		if ( next <= Waves.Length )
			GameNotification.Show( $"Wave {_waveNumber} cleared!", new Color( 0.7f, 0.9f, 1f ) );
	}

	private WaveDef? GetWaveDef( int w )
	{
		if ( w >= 1 && w <= Waves.Length )
			return Waves[w - 1];

		if ( w > Waves.Length )
		{
			int extraWaves = w - Waves.Length;
			var last = Waves[^1];
			int count = last.EnemyCount + (extraWaves * 9);
			float rate = MathF.Max( 0.10f, last.SpawnRate - (extraWaves * 0.014f) );
			return new WaveDef( count, rate );
		}

		return null;
	}

	private void SpawnFinalBoss()
	{
		GameNotification.Show( "FINAL BOSS INCOMING!", new Color( 1f, 0.2f, 0.2f ), 5f );
		_finalBossObject = SpawnFinalBossEntity();
	}

	private void EnterFinalBossPhase()
	{
		_finalBossPhase = true;

		// Stop all normal wave spawning and clear remaining non-boss enemies.
		_state = SpawnState.Intermission;
		_stateTimer = 0f;
		_enemiesLeft = 0;
		_spawnTimer = 0f;
		_lastCountdownSecond = -1;

		foreach ( var go in _spawnedEnemies )
		{
			if ( go.IsValid() )
				go.Destroy();
		}
		_spawnedEnemies.Clear();
		_waveEnemies.Clear();

		SpawnFinalBoss();
	}

	/// <summary>Dev command: spawn the dragon boss immediately for testing. Skips the 10-min wait. Spawns far (380–460 units) so it flies in from out of view.</summary>
	public void SpawnDragonBossForTesting()
	{
		if ( _finalBossObject != null && _finalBossObject.IsValid() )
		{
			GameNotification.Show( "Dragon boss already spawned.", Color.Yellow, 2f );
			return;
		}
		_finalBossPhase   = true;
		GameNotification.Show( "FINAL BOSS INCOMING!", new Color( 1f, 0.2f, 0.2f ), 5f );
		_finalBossObject = SpawnFinalBossEntity();
		GameNotification.Show( "Dragon boss spawned — watch it fly in!", new Color( 0.4f, 1f, 0.4f ), 3f );
	}

	private GameObject SpawnFinalBossEntity( float? spawnMinDist = null, float? spawnMaxDist = null )
	{
		var (go, enemy) = spawnMinDist.HasValue && spawnMaxDist.HasValue
			? CreateEnemyAtRandomOffset( spawnMinDist.Value, spawnMaxDist.Value )
			: CreateEnemyAtRandomOffset();
		float timeScale = 1f + _runTimer / 300f;
		float phaseScale = 1.25f;
		float hpMult = ChallengeRuntime.GetCombinedMultiplier( ChallengeModifierType.EnemyHpMultiplier );
		float dmgMult = ChallengeRuntime.GetCombinedMultiplier( ChallengeModifierType.EnemyDamageMultiplier );
		float speedMult = ChallengeRuntime.GetCombinedMultiplier( ChallengeModifierType.EnemySpeedMultiplier );

		enemy.MaxHP                  = 1400f * timeScale * phaseScale * hpMult;
		enemy.Speed                  = 18f * speedMult;
		enemy.ContactDamage          = 38f * timeScale * dmgMult;
		enemy.DamageCooldownDuration  = 2.0f;
		enemy.XPValue                = 120;
		enemy.EnemyColor             = new Color( 0.9f, 0.1f, 0.1f );
		enemy.SizeScale              = 6f;
		enemy.CollisionScale         = 0.3f; // Sprite-based; default formula overestimates hitbox vs visual
		enemy.SpritePath             = "sprites/Dragon/dragonanimations.sprite";
		enemy.DieAnimation           = "dragondeath";
		enemy.DieAnimDuration        = 1.5f;
		enemy.DisableMovement        = true;
		go.Name                      = "FinalBoss";

		go.Components.Create<DragonBoss>();

		_spawnedEnemies.Add( go );
		return go;
	}

	private void SpawnEnemy( WaveDef def )
	{
		var (go, enemy) = CreateEnemyAtRandomOffset();
		float timeScale = 1f + _runTimer / 300f;
		float phaseScale = MathF.Pow( 1f + TimeScaleFactor, _intensityPhase + 1 );

		EnemyType type = PickType();
		ApplyType( go, enemy, type, timeScale, phaseScale );
		_spawnedEnemies.Add( go );
		_waveEnemies.Add( go );
	}

	private (GameObject go, EnemyBase enemy) CreateEnemyAtRandomOffset( float minDist = 200f, float maxDist = 280f )
	{
		float angle = (float)(_rand.NextDouble() * 360.0);
		float dist  = minDist + (float)(_rand.NextDouble() * (maxDist - minDist));
		var offset = new Vector3(
			MathF.Cos( angle * MathF.PI / 180f ) * dist,
			MathF.Sin( angle * MathF.PI / 180f ) * dist,
			0f );

		var go = new GameObject( true, "Enemy" );
		go.WorldPosition = (WorldPosition + offset).WithZ( 0f );
		LocalGameRunner.ParentRuntimeObject( go );
		var enemy = go.Components.Create<EnemyBase>();
		enemy.Target = GameObject;
		return (go, enemy);
	}

	private enum EnemyType { Basic, Bat, Armored }

	private EnemyType PickType()
	{
		float t = Math.Clamp( _runTimer / RunDuration, 0f, 1f );
		float batWeight     = 0.5f + (0.2f - 0.5f) * t;
		float armoredWeight = 0f + (0.45f - 0f) * t;
		float basicWeight   = 1f - batWeight - armoredWeight;

		float roll = (float)_rand.NextDouble();
		if ( roll < batWeight )     return EnemyType.Bat;
		if ( roll < batWeight + armoredWeight ) return EnemyType.Armored;
		return EnemyType.Basic;
	}

	private void ApplyType( GameObject go, EnemyBase enemy, EnemyType type, float timeScale, float phaseScale )
	{
		float dmgScale  = timeScale * phaseScale;
		float speedScale = phaseScale;
		float hpMult = ChallengeRuntime.GetCombinedMultiplier( ChallengeModifierType.EnemyHpMultiplier );
		float dmgMult = ChallengeRuntime.GetCombinedMultiplier( ChallengeModifierType.EnemyDamageMultiplier );
		float speedMult = ChallengeRuntime.GetCombinedMultiplier( ChallengeModifierType.EnemySpeedMultiplier );

		switch ( type )
		{
			case EnemyType.Armored:
				enemy.MaxHP                  = 100f * timeScale * hpMult;
				enemy.Speed                  = (20.8f + (float)(_rand.NextDouble() * 6f)) * speedScale * speedMult;
				enemy.ContactDamage          = 16f * dmgScale * dmgMult;
				enemy.DamageCooldownDuration  = 1.4f;
				enemy.XPValue                = 38;
				enemy.EnemyColor             = new Color( 0.5f, 0.5f, 1f );
				enemy.SizeScale              = 1.2f;
				enemy.SpritePath             = "sprites/bear/bearanimations.sprite";
				enemy.DieAnimation           = "die";
				enemy.DieAnimDuration        = 0.5f;
				enemy.AttackAnimationPrefix  = "attack";
				enemy.AttackAnimDuration     = 0.5f;
				go.Name                      = "EnemyArmored";
				break;

			case EnemyType.Bat:
				enemy.MaxHP                  = 20f * timeScale * hpMult;
				enemy.Speed                  = 44f * speedScale * speedMult;
				enemy.ContactDamage          = 8f * dmgScale * dmgMult;
				enemy.DamageCooldownDuration  = 0.6f;
				enemy.XPValue                = 20;
				enemy.EnemyColor             = new Color( 0.85f, 0.45f, 0.1f );
				enemy.SizeScale              = 0.65f;
				enemy.SpritePath             = "sprites/wraith/wraithanimations.sprite";
				enemy.DieAnimation           = "die";
				enemy.DieAnimDuration        = 0.5f;
				go.Name                      = "EnemyBat";
				break;

			default:
				enemy.MaxHP                  = 34f * timeScale * hpMult;
				enemy.Speed                  = (20f + (float)(_rand.NextDouble() * 8f)) * speedScale * speedMult;
				enemy.ContactDamage          = 10f * dmgScale * dmgMult;
				enemy.DamageCooldownDuration  = 1.0f;
				enemy.XPValue                = 14;
				enemy.EnemyColor             = new Color( 0.85f, 0.15f, 0.15f );
				enemy.SpritePath             = "sprites/orcanimations.sprite";
				enemy.DieAnimation           = "die";
				enemy.DieAnimDuration        = 0.5f;
				enemy.AttackAnimationPrefix  = "attack";
				enemy.AttackAnimDuration     = 0.5f;
				go.Name                      = "Enemy";
				break;
		}
	}

	// ── Public control ────────────────────────────────────────────────────────
	public bool IsPaused { get; private set; } = false;

	public void SetPaused( bool paused ) => IsPaused = paused;

	public void ClearTierJustCompleted() => _tierJustCompleted = false;

	public void StopSpawning()
	{
		_isActive = false;
		foreach ( var go in _spawnedEnemies )
		{
			if ( go.IsValid() )
				go.Destroy();
		}
		_spawnedEnemies.Clear();
		_finalBossObject = null;
	}
}
