using System.Collections.Generic;

public enum QuestGoalType
{
	TotalKills,
	SurviveMinutes,
	ReachLevel,
	KillsAsCharacter,
	CompleteRuns,
	/// <summary>Kill X enemies using a specific weapon (matched by WeaponName).</summary>
	KillsWithWeapon,
	/// <summary>Upgrade a specific tome to level X or higher in a single run.</summary>
	TomeReachLevel,
	/// <summary>Die X times total across all runs.</summary>
	TotalDeaths,
	/// <summary>Survive X consecutive seconds without taking damage in a single run.</summary>
	NoDamageSeconds,
	/// <summary>Purchase X chests total across all runs.</summary>
	ChestsPurchased,
	/// <summary>Fire X projectiles total across all runs.</summary>
	ProjectilesFired,
	/// <summary>Upgrade a specific weapon to level X or higher in a single run.</summary>
	WeaponReachLevel
}

public class QuestDefinition
{
	public string Id { get; init; }
	public string Description { get; init; }
	public QuestGoalType GoalType { get; init; }
	/// <summary>For KillsAsCharacter quests, which character ID this applies to.</summary>
	public string CharacterId { get; init; }
	/// <summary>For KillsWithWeapon quests, the weapon display name (e.g. "Sword", "Magic Wand").</summary>
	public string WeaponName { get; init; }
	/// <summary>For TomeReachLevel quests, the tome internal name (e.g. "Speed", "Damage").</summary>
	public string TomeName { get; init; }
	public int Target { get; init; }
	public int CoinReward { get; init; }

	public static readonly List<QuestDefinition> All = new()
	{
		// --- Kill quests ---
		new() { Id = "kill_100",  Description = "Kill 100 enemies",   GoalType = QuestGoalType.TotalKills,   Target = 100,   CoinReward = 10  },
		new() { Id = "kill_500",  Description = "Kill 500 enemies",   GoalType = QuestGoalType.TotalKills,   Target = 500,   CoinReward = 30  },
		new() { Id = "kill_2000", Description = "Kill 2,000 enemies", GoalType = QuestGoalType.TotalKills,   Target = 2000,  CoinReward = 75  },
		new() { Id = "kill_5000", Description = "Kill 5,000 enemies", GoalType = QuestGoalType.TotalKills,   Target = 5000,  CoinReward = 150 },

		// --- Survive quests ---
		new() { Id = "survive_5",  Description = "Survive for 5 minutes",  GoalType = QuestGoalType.SurviveMinutes, Target = 5,  CoinReward = 15  },
		new() { Id = "survive_15", Description = "Survive for 15 minutes", GoalType = QuestGoalType.SurviveMinutes, Target = 15, CoinReward = 50  },
		new() { Id = "survive_30", Description = "Survive for 30 minutes", GoalType = QuestGoalType.SurviveMinutes, Target = 30, CoinReward = 150 },

		// --- Level quests ---
		new() { Id = "level_5",  Description = "Reach level 5",  GoalType = QuestGoalType.ReachLevel, Target = 5,  CoinReward = 10  },
		new() { Id = "level_10", Description = "Reach level 10", GoalType = QuestGoalType.ReachLevel, Target = 10, CoinReward = 30  },
		new() { Id = "level_20", Description = "Reach level 20", GoalType = QuestGoalType.ReachLevel, Target = 20, CoinReward = 75  },

		// --- Character-specific kill quests ---
		new() { Id = "archer_kills_100",  Description = "Kill 100 enemies as the Archer",  GoalType = QuestGoalType.KillsAsCharacter, CharacterId = "char_archer",  Target = 100, CoinReward = 20 },
		new() { Id = "warrior_kills_100", Description = "Kill 100 enemies as the Warrior", GoalType = QuestGoalType.KillsAsCharacter, CharacterId = "char_warrior", Target = 100, CoinReward = 20 },
		new() { Id = "mage_kills_100",    Description = "Kill 100 enemies as the Mage",    GoalType = QuestGoalType.KillsAsCharacter, CharacterId = "char_mage",    Target = 100, CoinReward = 20 },

		// --- Complete run quests ---
		new() { Id = "complete_1",  Description = "Complete 1 run",  GoalType = QuestGoalType.CompleteRuns, Target = 1,  CoinReward = 25  },
		new() { Id = "complete_5",  Description = "Complete 5 runs",  GoalType = QuestGoalType.CompleteRuns, Target = 5,  CoinReward = 75  },
		new() { Id = "complete_10", Description = "Complete 10 runs", GoalType = QuestGoalType.CompleteRuns, Target = 10, CoinReward = 150 },

		// --- Kills with weapon quests ---
		new() { Id = "kills_sword_300",     Description = "Kill 300 enemies with the Sword",   GoalType = QuestGoalType.KillsWithWeapon, WeaponName = "Sword",      Target = 300,  CoinReward = 20 },
		new() { Id = "kills_magicwand_500", Description = "Kill 500 enemies with Magic Wand",  GoalType = QuestGoalType.KillsWithWeapon, WeaponName = "Magic Wand", Target = 500,  CoinReward = 30 },
		new() { Id = "kills_bow_500",       Description = "Kill 500 enemies with the Bow",     GoalType = QuestGoalType.KillsWithWeapon, WeaponName = "Bow",        Target = 500,  CoinReward = 30 },
		new() { Id = "kills_aura_300",           Description = "Kill 300 enemies with Aura Blast",      GoalType = QuestGoalType.KillsWithWeapon, WeaponName = "Aura",           Target = 300, CoinReward = 20 },
		new() { Id = "kills_axe_500",            Description = "Kill 500 enemies with Throwing Axe",   GoalType = QuestGoalType.KillsWithWeapon, WeaponName = "Axe",            Target = 500, CoinReward = 30 },
		new() { Id = "kills_stormrod_300",       Description = "Kill 300 enemies with Storm Rod",       GoalType = QuestGoalType.KillsWithWeapon, WeaponName = "Storm Rod",      Target = 300, CoinReward = 25 },
		new() { Id = "kills_orbitalshards_300",  Description = "Kill 300 enemies with Orbital Shards",  GoalType = QuestGoalType.KillsWithWeapon, WeaponName = "Orbital Shards", Target = 300, CoinReward = 25 },

		// --- Tome level quests ---
		new() { Id = "tome_damage_lvl3",  Description = "Upgrade Damage Tome to Level 3 in a single run",  GoalType = QuestGoalType.TomeReachLevel, TomeName = "Damage Tome",  Target = 3, CoinReward = 20 },
		new() { Id = "tome_agility_lvl5", Description = "Upgrade Agility Tome to Level 5 in a single run", GoalType = QuestGoalType.TomeReachLevel, TomeName = "Agility Tome", Target = 5, CoinReward = 30 },

		// --- Death quests ---
		new() { Id = "deaths_10", Description = "Die 10 times", GoalType = QuestGoalType.TotalDeaths, Target = 10, CoinReward = 15 },

		// --- No-damage quests ---
		new() { Id = "nodamage_60", Description = "Survive 60 consecutive seconds without taking damage", GoalType = QuestGoalType.NoDamageSeconds, Target = 60, CoinReward = 25 },

		// --- Chests purchased quests ---
		new() { Id = "chests_10", Description = "Purchase 10 chests total", GoalType = QuestGoalType.ChestsPurchased, Target = 10, CoinReward = 20 },

		// --- Projectiles fired quests ---
		new() { Id = "projectiles_5000", Description = "Fire 5,000 projectiles", GoalType = QuestGoalType.ProjectilesFired, Target = 5000, CoinReward = 25 },

		// --- Weapon level quests ---
		new() { Id = "axe_level_10", Description = "Upgrade Axe to Level 10 in a single run", GoalType = QuestGoalType.WeaponReachLevel, WeaponName = "Axe", Target = 10, CoinReward = 20 },
	};
}
