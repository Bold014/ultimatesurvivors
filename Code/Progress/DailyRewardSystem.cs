/// <summary>
/// Handles daily login rewards with a 7-day rotating cycle and streak tracking.
/// Rewards include Soul Essence (skill tree currency) and bonus coins.
/// </summary>
public static class DailyRewardSystem
{
	public struct DailyReward
	{
		public int Essence;
		public int Coins;
		public int Day; // 1-7
	}

	/// <summary>7-day rotating reward table. Index 0 = day 1.</summary>
	private static readonly DailyReward[] RewardTable = new DailyReward[]
	{
		new() { Essence = 10, Coins = 0,  Day = 1 },
		new() { Essence = 10, Coins = 5,  Day = 2 },
		new() { Essence = 15, Coins = 5,  Day = 3 },
		new() { Essence = 15, Coins = 10, Day = 4 },
		new() { Essence = 20, Coins = 10, Day = 5 },
		new() { Essence = 25, Coins = 15, Day = 6 },
		new() { Essence = 40, Coins = 25, Day = 7 },
	};

	/// <summary>+5 essence per completed week, capped at week 4 (+15 max).</summary>
	private const int StreakBonusPerWeek = 5;
	private const int MaxStreakWeeks = 3; // weeks 1-3 completed = +5, +10, +15

	/// <summary>Returns the base reward for a given day (1-7) without streak bonuses.</summary>
	public static DailyReward GetDayReward( int day )
	{
		int index = System.Math.Clamp( day - 1, 0, RewardTable.Length - 1 );
		return RewardTable[index];
	}

	/// <summary>True when the player can claim today's daily reward.</summary>
	public static bool CanClaim()
	{
		var today = System.DateTime.UtcNow.ToString( "yyyy-MM-dd" );
		return PlayerProgress.Data.LastDailyClaimDate != today;
	}

	/// <summary>Returns what the current day's reward would be (without claiming).</summary>
	public static DailyReward PeekReward()
	{
		int streak = GetNextStreak();
		int dayIndex = (streak - 1) % 7;
		var reward = RewardTable[dayIndex];

		// Add streak bonus for completed weeks
		int completedWeeks = System.Math.Min( MaxStreakWeeks, (streak - 1) / 7 );
		reward.Essence += completedWeeks * StreakBonusPerWeek;

		return reward;
	}

	/// <summary>Returns the current streak value (what it will be after claiming).</summary>
	public static int GetNextStreak()
	{
		var data = PlayerProgress.Data;
		if ( string.IsNullOrEmpty( data.LastDailyClaimDate ) )
			return 1;

		if ( !System.DateTime.TryParse( data.LastDailyClaimDate, out var lastDate ) )
			return 1;

		var today = System.DateTime.UtcNow.Date;
		var daysSince = (today - lastDate.Date).Days;

		if ( daysSince == 1 )
			return data.DailyStreak + 1; // consecutive day
		if ( daysSince == 0 )
			return data.DailyStreak; // already claimed today
		return 1; // missed a day, reset
	}

	/// <summary>Returns the current streak day (1-7 cycle position) for display.</summary>
	public static int GetCurrentStreakDay()
	{
		var data = PlayerProgress.Data;
		if ( data.DailyStreak <= 0 ) return 0;
		return ((data.DailyStreak - 1) % 7) + 1;
	}

	/// <summary>
	/// Attempts to claim the daily reward. Returns true on success.
	/// </summary>
	public static bool TryClaim( out DailyReward reward )
	{
		reward = default;
		if ( !CanClaim() ) return false;

		int streak = GetNextStreak();
		int dayIndex = (streak - 1) % 7;
		reward = RewardTable[dayIndex];

		// Add streak bonus
		int completedWeeks = System.Math.Min( MaxStreakWeeks, (streak - 1) / 7 );
		reward.Essence += completedWeeks * StreakBonusPerWeek;

		var data = PlayerProgress.Data;
		data.LastDailyClaimDate = System.DateTime.UtcNow.ToString( "yyyy-MM-dd" );
		data.DailyStreak = streak;
		data.TotalDailyClaims++;
		data.SoulEssence += reward.Essence;
		data.Coins += reward.Coins;

		PlayerProgress.Save();
		return true;
	}
}
