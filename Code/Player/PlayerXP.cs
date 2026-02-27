/// <summary>
/// Tracks XP and current level for the local player.
/// On level-up, notifies UpgradeSystem to show choices.
/// </summary>
public sealed class PlayerXP : Component
{
	static SoundEvent LevelUpSound = new( "sounds/level_up.mp3" );

	public int CurrentXP     { get; private set; } = 0;
	public int XPToNextLevel { get; private set; } = 25;  // Level 1→2: fast. Gentler curve for 10-min runs.
	public int Level         { get; private set; } = 1;

	/// <summary>XP required to reach next level. Early levels are fast; later levels scale exponentially.</summary>
	private static int GetXPForLevel( int level ) => (int)(20 * System.Math.Pow( level, 1.18 ));

	public float XPPercent => XPToNextLevel > 0 ? (float)CurrentXP / XPToNextLevel : 0f;

	private UpgradeSystem   _upgradeSystem;
	private PlayerStats     _stats;
	private PlayerLocalState _state;
	private bool            _initialized = false;

	protected override void OnStart() { PlayerXP.LocalInstance = this; }
	protected override void OnDestroy() { if ( LocalInstance == this ) LocalInstance = null; }
	public static PlayerXP LocalInstance { get; private set; }

	private void EnsureInitialized()
	{
		if ( _initialized ) return;
		_initialized   = true;
		_upgradeSystem = Components.Get<UpgradeSystem>();
		_stats         = Components.Get<PlayerStats>();
		_state         = Components.Get<PlayerLocalState>();
	}

	private bool _loggedFirstXP = false;
	public void AddXP( int amount )
	{
		EnsureInitialized();
		float mult = _state?.XPMultiplier ?? 1f;
		int finalAmount = Math.Max( 1, (int)Math.Ceiling( amount * mult ) );

		if ( !_loggedFirstXP )
		{
			_loggedFirstXP = true;
			Log.Info( $"[PlayerXP] First XP received: +{finalAmount} (raw={amount})" );
		}

		CurrentXP += finalAmount;
		while ( CurrentXP >= XPToNextLevel )
		{
			CurrentXP -= XPToNextLevel;
			LevelUp();
		}
	}

	private void LevelUp()
	{
		EnsureInitialized();
		try { Sound.Play( LevelUpSound ); } catch { }
		Level++;
		XPToNextLevel = GetXPForLevel( Level );

		if ( _stats != null )
			_stats.Level = Level;

		Log.Info( $"[PlayerXP] LevelUp! Level={Level}. _upgradeSystem={((_upgradeSystem != null) ? "SET" : "NULL")}. UpgradeSystem.LocalInstance={((UpgradeSystem.LocalInstance != null) ? "SET" : "NULL")}" );
		_upgradeSystem?.ShowUpgrades();
	}
}
