/// <summary>
/// Megabonk-style spawner: 10-minute survival, continuous enemy spawning.
/// 3 mini-bosses at 2:30, 5:00, 7:30. Final boss at 10:00.
/// Beat the final boss to win. Lives on the player GameObject.
/// </summary>
public sealed class EnemySpawner : Component
{
	// ── Public API ────────────────────────────────────────────────────────────
	public float RunTime       => _runTimer;
	public float TimeRemaining => MathF.Max( 0f, RunDuration - _runTimer );
	public bool  IsActive      => _isActive;
	/// <summary>True when we're past 10 min and only the final boss remains (or is dead).</summary>
	public bool  IsFinalBossPhase => _finalBossPhase;
	/// <summary>True for one frame when the final boss is defeated. GameManager uses this for tier completion.</summary>
	public bool  TierJustCompleted => _tierJustCompleted;

	/// <summary>Number of mini-bosses spawned so far (0–3).</summary>
	public int MiniBossesSpawned => _miniBossesSpawned;

	private const float RunDuration = 600f;  // 10 minutes
	private static readonly float[] MiniBossTimes = { 150f, 300f, 450f };  // 2:30, 5:00, 7:30

	// ── Spawn tuning: high density for fast leveling ────────────────────────
	private const float SpawnIntervalStart = 0.45f;   // spawn every 0.45s at start
	private const float SpawnIntervalEnd   = 0.18f;   // down to 0.18s by 10 min
	private const float TimeScaleFactor    = 0.04f;   // enemy stat growth over time

	// ── State ─────────────────────────────────────────────────────────────────
	private float  _runTimer         = 0f;
	private float  _spawnTimer       = 0f;
	private int    _miniBossesSpawned = 0;
	private bool   _finalBossSpawned = false;
	private bool   _finalBossPhase   = false;
	private bool   _tierJustCompleted = false;
	private bool   _isActive         = false;
	private Random _rand;
	private readonly List<GameObject> _spawnedEnemies = new();
	private GameObject _finalBossObject;

	protected override void OnStart()
	{
		int seed = Connection.Local != null
			? Connection.Local.SteamId.GetHashCode()
			: (int)(Time.Now * 1000);
		_rand     = new Random( seed );
		_isActive = true;
		_spawnTimer = 0f;
	}

	protected override void OnUpdate()
	{
		if ( !_isActive || IsPaused ) return;

		_runTimer   += Time.Delta;
		_spawnTimer -= Time.Delta;

		// Check if final boss was killed (object destroyed)
		if ( _finalBossObject != null && !_finalBossObject.IsValid() )
		{
			_finalBossObject = null;
			_tierJustCompleted = true;
			ChatComponent.Instance?.AddMessage( "System", "FINAL BOSS DEFEATED! Victory!", new Color( 0.4f, 1f, 0.4f ) );
		}

		// Past 10 min: only final boss phase (no more regular spawns)
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

		// Spawn mini-bosses at scheduled times
		for ( int i = _miniBossesSpawned; i < MiniBossTimes.Length; i++ )
		{
			if ( _runTimer >= MiniBossTimes[i] )
			{
				_miniBossesSpawned = i + 1;
				SpawnMiniBoss();
				ChatComponent.Instance?.AddMessage( "Boss", $"Mini-boss {_miniBossesSpawned}/3!", Color.Magenta );
			}
		}

		// Continuous enemy spawning
		float interval = SpawnIntervalStart + (SpawnIntervalEnd - SpawnIntervalStart) * (_runTimer / RunDuration);
		if ( _spawnTimer <= 0f )
		{
			SpawnEnemy( isBoss: false );
			_spawnTimer = interval;
		}
	}

	private void SpawnFinalBoss()
	{
		ChatComponent.Instance?.AddMessage( "Boss", "FINAL BOSS INCOMING!", new Color( 1f, 0.2f, 0.2f ) );
		_finalBossObject = SpawnBoss( isFinalBoss: true );
	}

	private void SpawnMiniBoss()
	{
		SpawnBoss( isFinalBoss: false );
	}

	private GameObject SpawnBoss( bool isFinalBoss )
	{
		var (go, enemy) = CreateEnemyAtRandomOffset();
		float timeScale = 1f + _runTimer / 120f;
		float phaseScale = isFinalBoss ? 1.6f : (1f + _miniBossesSpawned * 0.2f);

		if ( isFinalBoss )
		{
			enemy.MaxHP                  = 1200f * timeScale * phaseScale;
			enemy.Speed                  = 18f;
			enemy.ContactDamage          = 55f * timeScale;
			enemy.DamageCooldownDuration  = 2.0f;
			enemy.XPValue                = 120;
			enemy.EnemyColor             = new Color( 0.9f, 0.1f, 0.1f );
			enemy.SizeScale              = 2.5f;
			go.Name                      = "FinalBoss";
		}
		else
		{
			enemy.MaxHP                  = 400f * timeScale * phaseScale;
			enemy.Speed                  = 16f;
			enemy.ContactDamage          = 35f * timeScale;
			enemy.DamageCooldownDuration  = 2.2f;
			enemy.XPValue                = 80;
			enemy.EnemyColor             = Color.Magenta;
			enemy.SizeScale              = 1.8f;
			go.Name                      = "MiniBoss";
		}

		_spawnedEnemies.Add( go );
		return go;
	}

	private void SpawnEnemy( bool isBoss )
	{
		var (go, enemy) = CreateEnemyAtRandomOffset();
		float timeScale = 1f + _runTimer / 120f;
		float phaseScale = MathF.Pow( 1f + TimeScaleFactor, _miniBossesSpawned + 1 );

		EnemyType type = PickType();
		ApplyType( go, enemy, type, timeScale, phaseScale );
		_spawnedEnemies.Add( go );
	}

	private (GameObject go, EnemyBase enemy) CreateEnemyAtRandomOffset()
	{
		float angle = (float)(_rand.NextDouble() * 360.0);
		float dist  = 380f + (float)(_rand.NextDouble() * 80f);
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
				enemy.SpritePath             = "sprites/orc/orcanimations.sprite";
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
