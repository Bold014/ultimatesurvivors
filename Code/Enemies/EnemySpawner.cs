/// <summary>
/// Megabonk-style spawner: 10-minute survival, wave-based enemy spawning.
/// 3 intensity phases at 2:30, 5:00, 7:30 ramp up wave difficulty. Final boss at 10:00.
/// Beat the final boss to win. Lives on the player GameObject.
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
	private const float TimeScaleFactor  = 0.04f;

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
	private bool      _finalBossSpawned = false;
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

		// Check if final boss was killed (object destroyed)
		if ( _finalBossObject != null && !_finalBossObject.IsValid() )
		{
			_finalBossObject = null;
			_tierJustCompleted = true;
			ChatComponent.Instance?.AddMessage( "System", "FINAL BOSS DEFEATED! Victory!", new Color( 0.4f, 1f, 0.4f ) );
		}

		// Past 10 min: only final boss phase (no more waves)
		if ( _runTimer >= RunDuration )
		{
			if ( !_finalBossSpawned )
			{
				_finalBossSpawned = true;
				_finalBossPhase   = true;
				SpawnFinalBoss();
			}
			return;
		}

		// Intensity phase surges at 2:30, 5:00, 7:30 (ramps wave difficulty)
		for ( int i = _intensityPhase; i < IntensityPhaseTimes.Length; i++ )
		{
			if ( _runTimer >= IntensityPhaseTimes[i] )
			{
				_intensityPhase = i + 1;
				ChatComponent.Instance?.AddMessage( "Wave", $"INTENSITY SURGE! Phase {_intensityPhase}!", Color.Magenta );
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
				ChatComponent.Instance?.AddMessage( "Wave", $"Wave {nextWave} — {secs}…", new Color( 1f, 0.85f, 0.2f ) );
			}
		}

		if ( _stateTimer > 0f ) return;

		_waveNumber++;
		WaveDef? def = GetWaveDef( _waveNumber );
		if ( !def.HasValue )
		{
			_state = SpawnState.Intermission;
			_stateTimer = 1f;
			return;
		}

		StartWave( def.Value );
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
		float countMult = 1f + _intensityPhase * 0.2f;
		float rateMult  = 1f - _intensityPhase * 0.12f;

		_state             = SpawnState.WaveActive;
		_stateTimer        = 0f;
		_enemiesLeft       = (int)(def.EnemyCount * countMult);
		_effectiveSpawnRate = def.SpawnRate * rateMult;
		_spawnTimer        = 0f;
		_lastCountdownSecond = -1;
		_waveEnemies.Clear();

		ChatComponent.Instance?.AddMessage( "Wave", $"Wave {_waveNumber}!", new Color( 0.4f, 0.9f, 0.4f ) );
	}

	private void EndWave()
	{
		_state      = SpawnState.Intermission;
		_stateTimer = IntermissionTime;
		_lastCountdownSecond = -1;

		int next = _waveNumber + 1;
		if ( next <= Waves.Length )
			ChatComponent.Instance?.AddMessage( "Wave", $"Wave {_waveNumber} cleared. Next: Wave {next}", new Color( 0.7f, 0.9f, 1f ) );
	}

	private WaveDef? GetWaveDef( int w )
	{
		if ( w >= 1 && w <= Waves.Length )
			return Waves[w - 1];
		return null;
	}

	private void SpawnFinalBoss()
	{
		ChatComponent.Instance?.AddMessage( "Boss", "FINAL BOSS INCOMING!", new Color( 1f, 0.2f, 0.2f ) );
		_finalBossObject = SpawnFinalBossEntity();
	}

	/// <summary>Dev command: spawn the dragon boss immediately for testing. Skips the 10-min wait. Spawns far (380–460 units) so it flies in from out of view.</summary>
	public void SpawnDragonBossForTesting()
	{
		if ( _finalBossObject != null && _finalBossObject.IsValid() )
		{
			ChatComponent.Instance?.AddMessage( "Dev", "Dragon boss already spawned.", Color.Yellow );
			return;
		}
		_finalBossSpawned = true;
		_finalBossPhase   = true;
		ChatComponent.Instance?.AddMessage( "Boss", "FINAL BOSS INCOMING!", new Color( 1f, 0.2f, 0.2f ) );
		_finalBossObject = SpawnFinalBossEntity();
		ChatComponent.Instance?.AddMessage( "Dev", "Dragon boss spawned — watch it fly in!", new Color( 0.4f, 1f, 0.4f ) );
	}

	private GameObject SpawnFinalBossEntity( float? spawnMinDist = null, float? spawnMaxDist = null )
	{
		var (go, enemy) = spawnMinDist.HasValue && spawnMaxDist.HasValue
			? CreateEnemyAtRandomOffset( spawnMinDist.Value, spawnMaxDist.Value )
			: CreateEnemyAtRandomOffset();
		float timeScale = 1f + _runTimer / 120f;
		float phaseScale = 1.6f;

		enemy.MaxHP                  = 1200f * timeScale * phaseScale;
		enemy.Speed                  = 18f;
		enemy.ContactDamage          = 55f * timeScale;
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
		float timeScale = 1f + _runTimer / 120f;
		float phaseScale = MathF.Pow( 1f + TimeScaleFactor, _intensityPhase + 1 );

		EnemyType type = PickType();
		ApplyType( go, enemy, type, timeScale, phaseScale );
		_spawnedEnemies.Add( go );
		_waveEnemies.Add( go );
	}

	private (GameObject go, EnemyBase enemy) CreateEnemyAtRandomOffset( float minDist = 380f, float maxDist = 460f )
	{
		float angle = (float)(_rand.NextDouble() * 360.0);
		float dist  = minDist + (float)(_rand.NextDouble() * (maxDist - minDist));
		var offset = new Vector3(
			MathF.Cos( angle * MathF.PI / 180f ) * dist,
			MathF.Sin( angle * MathF.PI / 180f ) * dist,
			0f );

		var go = new GameObject( true, "Enemy" );
		go.WorldPosition = (WorldPosition + offset).WithZ( 0f );
		var enemy = go.Components.Create<EnemyBase>();
		enemy.Target = GameObject;
		return (go, enemy);
	}

	private enum EnemyType { Basic, Bat, Armored }

	private EnemyType PickType()
	{
		float t = _runTimer / RunDuration;
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

		switch ( type )
		{
			case EnemyType.Armored:
				enemy.MaxHP                  = 90f * timeScale;
				enemy.Speed                  = (20.8f + (float)(_rand.NextDouble() * 6f)) * speedScale;
				enemy.ContactDamage          = 22f * dmgScale;
				enemy.DamageCooldownDuration  = 1.4f;
				enemy.XPValue                = 22;
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
				enemy.MaxHP                  = 18f * timeScale;
				enemy.Speed                  = 44f * speedScale;
				enemy.ContactDamage          = 12f * dmgScale;
				enemy.DamageCooldownDuration  = 0.6f;
				enemy.XPValue                = 12;
				enemy.EnemyColor             = new Color( 0.85f, 0.45f, 0.1f );
				enemy.SizeScale              = 0.65f;
				enemy.SpritePath             = "sprites/wraith/wraithanimations.sprite";
				enemy.DieAnimation           = "die";
				enemy.DieAnimDuration        = 0.5f;
				go.Name                      = "EnemyBat";
				break;

			default:
				enemy.MaxHP                  = 30f * timeScale;
				enemy.Speed                  = (20f + (float)(_rand.NextDouble() * 8f)) * speedScale;
				enemy.ContactDamage          = 14f * dmgScale;
				enemy.DamageCooldownDuration  = 1.0f;
				enemy.XPValue                = 8;
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
