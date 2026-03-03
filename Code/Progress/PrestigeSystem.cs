/// <summary>
/// Handles prestige resets for the skill tree. Prestiging clears all skill levels
/// but grants a permanent multiplicative bonus to all skill tree bonuses.
/// </summary>
public static class PrestigeSystem
{
	/// <summary>Minimum total essence spent on skills required to prestige.</summary>
	public const int MinEssenceSpentToPrestige = 500;

	/// <summary>Maximum prestige level.</summary>
	public const int MaxPrestigeLevel = 10;

	/// <summary>Bonus multiplier per prestige level (5%).</summary>
	private const float BonusPerLevel = 0.05f;

	/// <summary>Prestige titles by level (0-10).</summary>
	private static readonly string[] Titles = new[]
	{
		"Novice",       // 0
		"Apprentice",   // 1
		"Journeyman",   // 2
		"Adept",        // 3
		"Expert",       // 4
		"Veteran",      // 5
		"Master",       // 6
		"Grandmaster",  // 7
		"Champion",     // 8
		"Ascendant",    // 9
		"Transcendent", // 10
	};

	/// <summary>Returns the current prestige level.</summary>
	public static int GetLevel() => PlayerProgress.Data.PrestigeLevel;

	/// <summary>Returns the title for the current prestige level.</summary>
	public static string GetTitle()
	{
		int level = GetLevel();
		return level >= 0 && level < Titles.Length ? Titles[level] : Titles[0];
	}

	/// <summary>Returns the title for a given prestige level.</summary>
	public static string GetTitle( int level )
	{
		return level >= 0 && level < Titles.Length ? Titles[level] : Titles[0];
	}

	/// <summary>
	/// Returns the multiplicative bonus applied to all skill tree bonuses.
	/// e.g. prestige 2 = 1.10 (10% bonus).
	/// </summary>
	public static float GetBonusMultiplier()
	{
		return 1f + (PlayerProgress.Data.PrestigeLevel * BonusPerLevel);
	}

	/// <summary>True when the player meets all prestige requirements.</summary>
	public static bool CanPrestige()
	{
		if ( PlayerProgress.Data.PrestigeLevel >= MaxPrestigeLevel ) return false;
		return PlayerProgress.Data.TotalSoulEssenceSpent >= MinEssenceSpentToPrestige;
	}

	/// <summary>
	/// Performs a prestige reset: clears skill levels, increments prestige, keeps unspent essence.
	/// Returns true on success.
	/// </summary>
	public static bool TryPrestige()
	{
		if ( !CanPrestige() ) return false;

		PlayerProgress.Data.SkillLevels.Clear();
		PlayerProgress.Data.PrestigeLevel++;
		// Unspent essence is kept. TotalSoulEssenceSpent is NOT reset (lifetime stat).

		PlayerProgress.Save();
		return true;
	}
}
