using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Persistent player progression: coins, unlocked items, quest progress.
/// Loaded once at menu startup, saved on every mutation.
/// </summary>
public static class PlayerProgress
{
	private const string SaveFile = "player_progress.json";

	public static SaveData Data { get; private set; } = new();

	/// <summary>
	/// When true, all unlock and prerequisite checks are bypassed.
	/// Save data is never modified — real progression is unaffected.
	/// </summary>
	public static bool DevUnlockAll { get; set; } = false;

	public static void Load()
	{
		if ( FileSystem.Data.FileExists( SaveFile ) )
		{
			try
			{
				var json = FileSystem.Data.ReadAllText( SaveFile );
				Data = JsonSerializer.Deserialize<SaveData>( json ) ?? new SaveData();
			}
			catch
			{
				Data = new SaveData();
			}
		}
		else
		{
			Data = new SaveData();
		}

		// Ensure new fields exist (migration for old saves)
		Data.HighestTierCompletedByMap ??= new Dictionary<string, int>();

		// Archer and starter weapons are always unlocked
		EnsureDefaultUnlocks();
	}

	public static void Save()
	{
		var json = JsonSerializer.Serialize( Data, new JsonSerializerOptions { WriteIndented = true } );
		FileSystem.Data.WriteAllText( SaveFile, json );
	}

	// --- Unlock helpers ---

	public static bool IsUnlocked( string id )
	{
		if ( DevUnlockAll ) return true;
		var def = UnlockDefinition.All.Find( u => u.Id == id );
		if ( def == null ) return false;
		return def.IsFree || Data.UnlockedIds.Contains( id );
	}

	public static bool MeetsPrerequisite( string id )
	{
		if ( DevUnlockAll ) return true;
		var def = UnlockDefinition.All.Find( u => u.Id == id );
		if ( def?.PrerequisiteQuestId == null ) return true;
		return IsQuestClaimed( def.PrerequisiteQuestId );
	}

	public static bool TryUnlock( string id )
	{
		var def = UnlockDefinition.All.Find( u => u.Id == id );
		if ( def == null || IsUnlocked( id ) ) return false;
		if ( !MeetsPrerequisite( id ) ) return false;
		if ( Data.Coins < def.CoinCost ) return false;

		Data.Coins -= def.CoinCost;
		Data.UnlockedIds.Add( id );
		Save();
		return true;
	}

	// --- Tier unlock helpers ---

	/// <summary>Returns the highest tier completed for a map. Tier 2 requires >= 1, Tier 3 requires >= 2.</summary>
	public static int GetHighestTierCompleted( string mapId )
	{
		return Data.HighestTierCompletedByMap.TryGetValue( mapId ?? "", out var t ) ? t : 0;
	}

	// --- Quest helpers ---

	public static int GetQuestProgress( string questId )
	{
		return Data.QuestProgress.TryGetValue( questId, out var val ) ? val : 0;
	}

	public static bool IsQuestClaimed( string questId )
	{
		return Data.QuestClaimed.TryGetValue( questId, out var claimed ) && claimed;
	}

	public static bool CanClaimQuest( string questId )
	{
		var def = QuestDefinition.All.Find( q => q.Id == questId );
		if ( def == null || IsQuestClaimed( questId ) ) return false;
		return GetQuestProgress( questId ) >= def.Target;
	}

	public static bool TryClaimQuest( string questId )
	{
		if ( !CanClaimQuest( questId ) ) return false;

		var def = QuestDefinition.All.Find( q => q.Id == questId );
		Data.Coins += def.CoinReward;
		Data.QuestClaimed[questId] = true;
		Save();
		return true;
	}

	// --- Called at end of each run to advance quest counters ---

	public static void RecordRunResult( RunResult result )
	{
		Data.Coins += result.GoldEarned;

		Data.TotalKills += result.Kills;
		Data.TotalRunsCompleted += result.Completed ? 1 : 0;
		Data.TotalDeaths += result.Died ? 1 : 0;
		Data.TotalChestsPurchased += result.ChestsOpenedThisRun;

		if ( result.CharacterId != null )
		{
			Data.KillsByCharacter.TryGetValue( result.CharacterId, out var prev );
			Data.KillsByCharacter[result.CharacterId] = prev + result.Kills;
		}

		// Merge per-weapon kills
		foreach ( var kv in result.KillsByWeapon )
		{
			Data.KillsByWeapon.TryGetValue( kv.Key, out var prevWk );
			Data.KillsByWeapon[kv.Key] = prevWk + kv.Value;
		}

		if ( result.MaxLevel > Data.HighestLevelReached )
			Data.HighestLevelReached = result.MaxLevel;

		if ( result.SurviveMinutes > Data.LongestSurvivalMinutes )
			Data.LongestSurvivalMinutes = result.SurviveMinutes;

		if ( result.NoDamageSeconds > Data.LongestNoDamageSeconds )
			Data.LongestNoDamageSeconds = result.NoDamageSeconds;

		Data.TotalProjectilesFired += result.ProjectilesFiredThisRun;

		// Track max tome levels ever reached (across runs)
		foreach ( var kv in result.TomeLevels )
		{
			Data.MaxTomeLevelsByTome.TryGetValue( kv.Key, out var prevTl );
			if ( kv.Value > prevTl )
				Data.MaxTomeLevelsByTome[kv.Key] = kv.Value;
		}

		// Track max weapon levels ever reached (across runs)
		foreach ( var kv in result.WeaponLevels )
		{
			Data.MaxWeaponLevelsByWeapon.TryGetValue( kv.Key, out var prevWl );
			if ( kv.Value > prevWl )
				Data.MaxWeaponLevelsByWeapon[kv.Key] = kv.Value;
		}

		// Track tier completion for map selection unlocks
		if ( result.Completed && !string.IsNullOrEmpty( result.MapId ) && result.TierCompleted >= 1 )
		{
			Data.HighestTierCompletedByMap.TryGetValue( result.MapId, out var prev );
			if ( result.TierCompleted > prev )
				Data.HighestTierCompletedByMap[result.MapId] = result.TierCompleted;
		}

		// Sync quest progress from cumulative stats
		SyncQuestProgress();
		Save();
		SyncStatsToBackend();
	}

	private static void SyncStatsToBackend()
	{
		if ( Connection.Local == null ) return;
		try
		{
			Sandbox.Services.Stats.SetValue( "total_kills", Data.TotalKills );
			Sandbox.Services.Stats.SetValue( "runs_completed", Data.TotalRunsCompleted );
			Sandbox.Services.Stats.SetValue( "longest_survival", Data.LongestSurvivalMinutes );
		}
		catch { /* Stats may fail in editor or offline */ }
	}

	private static void SyncQuestProgress()
	{
		foreach ( var quest in QuestDefinition.All )
		{
			if ( IsQuestClaimed( quest.Id ) ) continue;

			int current = quest.GoalType switch
			{
				QuestGoalType.TotalKills       => Data.TotalKills,
				QuestGoalType.SurviveMinutes   => Data.LongestSurvivalMinutes,
				QuestGoalType.ReachLevel       => Data.HighestLevelReached,
				QuestGoalType.CompleteRuns     => Data.TotalRunsCompleted,
				QuestGoalType.KillsAsCharacter => quest.CharacterId != null && Data.KillsByCharacter.TryGetValue( quest.CharacterId, out var k ) ? k : 0,
				QuestGoalType.KillsWithWeapon  => quest.WeaponName != null && Data.KillsByWeapon.TryGetValue( quest.WeaponName, out var wk ) ? wk : 0,
				QuestGoalType.TomeReachLevel   => quest.TomeName != null && Data.MaxTomeLevelsByTome.TryGetValue( quest.TomeName, out var tl ) ? tl : 0,
			QuestGoalType.WeaponReachLevel => quest.WeaponName != null && Data.MaxWeaponLevelsByWeapon.TryGetValue( quest.WeaponName, out var wl ) ? wl : 0,
			QuestGoalType.TotalDeaths      => Data.TotalDeaths,
			QuestGoalType.NoDamageSeconds  => Data.LongestNoDamageSeconds,
			QuestGoalType.ChestsPurchased  => Data.TotalChestsPurchased,
			QuestGoalType.ProjectilesFired => Data.TotalProjectilesFired,
			_                              => 0
			};

			Data.QuestProgress[quest.Id] = current;
		}
	}

	private static void EnsureDefaultUnlocks()
	{
		foreach ( var def in UnlockDefinition.All )
		{
			if ( def.IsFree && !Data.UnlockedIds.Contains( def.Id ) )
				Data.UnlockedIds.Add( def.Id );
		}
	}
}

public class SaveData
{
	public int Coins { get; set; } = 0;
	public List<string> UnlockedIds { get; set; } = new();
	public Dictionary<string, int> QuestProgress { get; set; } = new();
	public Dictionary<string, bool> QuestClaimed { get; set; } = new();

	// Cumulative stats for quest tracking
	public int TotalKills { get; set; } = 0;
	public int TotalRunsCompleted { get; set; } = 0;
	public int HighestLevelReached { get; set; } = 0;
	public int LongestSurvivalMinutes { get; set; } = 0;
	public Dictionary<string, int> KillsByCharacter { get; set; } = new();

	// New stats for Megabonk-style unlock conditions
	public Dictionary<string, int> KillsByWeapon { get; set; } = new();
	public int TotalDeaths { get; set; } = 0;
	public int TotalChestsPurchased { get; set; } = 0;
	public int LongestNoDamageSeconds { get; set; } = 0;
	public int TotalProjectilesFired { get; set; } = 0;
	public Dictionary<string, int> MaxTomeLevelsByTome { get; set; } = new();
	/// <summary>Highest weapon level ever reached per weapon (e.g. "Axe" = 10).</summary>
	public Dictionary<string, int> MaxWeaponLevelsByWeapon { get; set; } = new();

	/// <summary>Highest tier completed per map. e.g. ["dark_forest"] = 2 means T1 and T2 done.</summary>
	public Dictionary<string, int> HighestTierCompletedByMap { get; set; } = new();

	public bool MusicMuted { get; set; } = false;
}

public class RunResult
{
	public int Kills { get; set; }
	public bool Completed { get; set; }
	/// <summary>True when the player died (as opposed to surviving the full run).</summary>
	public bool Died { get; set; }
	public string CharacterId { get; set; }
	public int MaxLevel { get; set; }
	public int SurviveMinutes { get; set; }

	// New fields for Megabonk-style tracking
	public Dictionary<string, int> KillsByWeapon { get; set; } = new();
	public int NoDamageSeconds { get; set; }
	public Dictionary<string, int> TomeLevels { get; set; } = new();
	/// <summary>Max weapon level reached per weapon this run (e.g. "Axe" = 10).</summary>
	public Dictionary<string, int> WeaponLevels { get; set; } = new();
	public int ChestsOpenedThisRun { get; set; }
	public int ProjectilesFiredThisRun { get; set; }

	/// <summary>Map id for tier unlock tracking (e.g. "dark_forest").</summary>
	public string MapId { get; set; }
	/// <summary>Tier that was being played (1, 2, or 3).</summary>
	public int TierCompleted { get; set; }
	/// <summary>Gold awarded for completing the run (or partial progress on death).</summary>
	public int GoldEarned { get; set; }
}
