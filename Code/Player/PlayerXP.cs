/// <summary>
/// Tracks XP and current level for the local player.
/// On level-up, notifies UpgradeSystem to show choices.
/// </summary>
public sealed class PlayerXP : Component
{
	static SoundEvent LevelUpSound = new( "sounds/level_up.mp3" );

	public int CurrentXP     { get; private set; } = 0;
	public int XPToNextLevel { get; private set; } = 100;
	public int Level         { get; private set; } = 1;

	public float XPPercent => XPToNextLevel > 0 ? (float)CurrentXP / XPToNextLevel : 0f;

	private UpgradeSystem _upgradeSystem;
	private PlayerStats   _stats;
	private bool          _initialized = false;

	protected override void OnStart()
	{
		Log.Info( $"[PlayerXP] OnStart — GameObject={GameObject?.Name}" );
	}

	private void EnsureInitialized()
	{
		if ( _initialized ) return;
		_initialized   = true;
		_upgradeSystem = Components.Get<UpgradeSystem>();
		_stats         = Components.Get<PlayerStats>();
		Log.Info( $"[PlayerXP] EnsureInitialized — upgradeSystem={_upgradeSystem != null}, stats={_stats != null}" );
	}

	public void AddXP( int amount )
	{
		EnsureInitialized();
		Log.Info( $"[PlayerXP] AddXP({amount}) — CurrentXP {CurrentXP} → {CurrentXP + amount} / {XPToNextLevel}" );

		CurrentXP += amount;
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
		XPToNextLevel = (int)(XPToNextLevel * 1.2f) + 50;

		Log.Info( $"[PlayerXP] LevelUp! New level={Level}, next XP threshold={XPToNextLevel}" );

		if ( _stats != null )
			_stats.Level = Level;

		_upgradeSystem?.ShowUpgrades();
	}
}
