/// <summary>
/// Tracks how many times each tome has been levelled up during the current run.
/// Tomes cap at their MaxLevel — once maxed they no longer appear in level-up choices.
/// </summary>
public sealed class PlayerTomes : Component
{
	public static PlayerTomes LocalInstance { get; private set; }

	private readonly Dictionary<string, int> _levels = new();

	/// <summary>Returns the current level of a tome (0 = never picked).</summary>
	public int GetLevel( string tomeName )
		=> _levels.TryGetValue( tomeName, out var lvl ) ? lvl : 0;

	/// <summary>True when the tome has reached its maximum level and should no longer appear.</summary>
	public bool IsMaxed( string tomeName, int maxLevel )
		=> GetLevel( tomeName ) >= maxLevel;

	/// <summary>Increments the tome's level by one. Called by UpgradeSystem on selection.</summary>
	public void LevelUp( string tomeName )
		=> _levels[tomeName] = GetLevel( tomeName ) + 1;

	/// <summary>Returns a snapshot of all tome levels this run (for RunResult reporting).</summary>
	public IReadOnlyDictionary<string, int> GetAllLevels() => _levels;

	protected override void OnStart()
	{
		LocalInstance = this;
		_levels.Clear();
	}

	protected override void OnDestroy()
	{
		if ( LocalInstance == this )
			LocalInstance = null;
	}
}
