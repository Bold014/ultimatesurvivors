using System.Collections.Generic;
using System.Linq;

public sealed class ChallengeModifier
{
	public ChallengeModifierType Type { get; init; }
	public float Value { get; init; } = 1f;
}

public sealed class ChallengeDefinition
{
	public string Id { get; init; }
	public string Name { get; init; }
	public string Description { get; init; }
	public string MapId { get; init; }
	public int Tier { get; init; }
	public ChallengeGoalType Goal { get; init; } = ChallengeGoalType.KillAllBosses;
	public float RunCoinMultiplier { get; init; } = 1f;
	public float PermanentBonusPercent { get; init; } = 1f;
	public List<ChallengeModifier> Modifiers { get; init; } = new();

	public static List<ChallengeDefinition> ForMapTier( string mapId, int tier )
		=> All.Where( x => x.MapId == mapId && x.Tier == tier ).ToList();

	public static ChallengeDefinition GetById( string id )
		=> All.Find( x => x.Id == id );

	public static readonly List<ChallengeDefinition> All = new()
	{
		// Dark Forest — Tier 1
		new()
		{
			Id = "df_t1_fragile",
			Name = "Glass Bones",
			Description = "Any hit ends the run instantly.",
			MapId = "dark_forest",
			Tier = 1,
			Goal = ChallengeGoalType.KillAllBosses,
			RunCoinMultiplier = 1.6f,
			Modifiers = new() { new() { Type = ChallengeModifierType.OneHitDeath } }
		},
		new()
		{
			Id = "df_t1_afk",
			Name = "Statue Stance",
			Description = "Movement and jumping are disabled.",
			MapId = "dark_forest",
			Tier = 1,
			Goal = ChallengeGoalType.ReachFinalSwarm,
			RunCoinMultiplier = 1.5f,
			Modifiers = new()
			{
				new() { Type = ChallengeModifierType.NoMovement },
				new() { Type = ChallengeModifierType.NoJump }
			}
		},
		new()
		{
			Id = "df_t1_speedrunner",
			Name = "Doom Clock",
			Description = "Defeat the final boss within 6 minutes.",
			MapId = "dark_forest",
			Tier = 1,
			Goal = ChallengeGoalType.KillAllBosses,
			RunCoinMultiplier = 1.5f,
			Modifiers = new() { new() { Type = ChallengeModifierType.RunTimeLimitSeconds, Value = 360f } }
		},
		new()
		{
			Id = "df_t1_speedrunner_plus",
			Name = "Doom Clock+",
			Description = "Defeat the final boss within 4:15.",
			MapId = "dark_forest",
			Tier = 1,
			Goal = ChallengeGoalType.KillAllBosses,
			RunCoinMultiplier = 2f,
			Modifiers = new() { new() { Type = ChallengeModifierType.RunTimeLimitSeconds, Value = 255f } }
		},
		new()
		{
			Id = "df_t1_pacifist",
			Name = "Bare Hands",
			Description = "Weapons are disabled for this run.",
			MapId = "dark_forest",
			Tier = 1,
			Goal = ChallengeGoalType.KillAllBosses,
			RunCoinMultiplier = 2f,
			Modifiers = new() { new() { Type = ChallengeModifierType.NoWeapons } }
		},
		new()
		{
			Id = "df_t1_turbo",
			Name = "Rising Tide",
			Description = "Enemy pressure ramps up much faster.",
			MapId = "dark_forest",
			Tier = 1,
			Goal = ChallengeGoalType.KillAllBosses,
			RunCoinMultiplier = 1.5f,
			Modifiers = new() { new() { Type = ChallengeModifierType.EnemyAmountMultiplier, Value = 1.25f } }
		},
		new()
		{
			Id = "df_t1_oh_no",
			Name = "Blood Moon I",
			Description = "Enemies are tougher, deadlier, and quicker.",
			MapId = "dark_forest",
			Tier = 1,
			Goal = ChallengeGoalType.KillAllBosses,
			RunCoinMultiplier = 1.5f,
			Modifiers = new()
			{
				new() { Type = ChallengeModifierType.EnemyHpMultiplier, Value = 1.25f },
				new() { Type = ChallengeModifierType.EnemyDamageMultiplier, Value = 1.25f },
				new() { Type = ChallengeModifierType.EnemySpeedMultiplier, Value = 1.25f }
			}
		},
		new()
		{
			Id = "df_t1_oh_shit",
			Name = "Blood Moon II",
			Description = "A heavy stat spike for all enemies.",
			MapId = "dark_forest",
			Tier = 1,
			Goal = ChallengeGoalType.KillAllBosses,
			RunCoinMultiplier = 1.75f,
			Modifiers = new()
			{
				new() { Type = ChallengeModifierType.EnemyHpMultiplier, Value = 1.5f },
				new() { Type = ChallengeModifierType.EnemyDamageMultiplier, Value = 1.5f },
				new() { Type = ChallengeModifierType.EnemySpeedMultiplier, Value = 1.5f }
			}
		},
		new()
		{
			Id = "df_t1_wtf",
			Name = "Blood Moon III",
			Description = "Maximum enemy scaling. Survive if you can.",
			MapId = "dark_forest",
			Tier = 1,
			Goal = ChallengeGoalType.KillAllBosses,
			RunCoinMultiplier = 2f,
			Modifiers = new()
			{
				new() { Type = ChallengeModifierType.EnemyHpMultiplier, Value = 2f },
				new() { Type = ChallengeModifierType.EnemyDamageMultiplier, Value = 2f },
				new() { Type = ChallengeModifierType.EnemySpeedMultiplier, Value = 2f }
			}
		},

		// Dark Forest — Tier 2
		new() { Id = "df_t2_fragile", Name = "Glass Bones II", Description = "Any hit ends the run instantly.", MapId = "dark_forest", Tier = 2, Goal = ChallengeGoalType.KillAllBosses, RunCoinMultiplier = 1.6f, Modifiers = new() { new() { Type = ChallengeModifierType.OneHitDeath } } },
		new() { Id = "df_t2_speedrunner", Name = "Doom Clock II", Description = "Defeat the final boss within 14 minutes.", MapId = "dark_forest", Tier = 2, Goal = ChallengeGoalType.KillAllBosses, RunCoinMultiplier = 1.5f, Modifiers = new() { new() { Type = ChallengeModifierType.RunTimeLimitSeconds, Value = 840f } } },
		new() { Id = "df_t2_speedrunner_plus", Name = "Doom Clock+ II", Description = "Defeat the final boss within 12 minutes.", MapId = "dark_forest", Tier = 2, Goal = ChallengeGoalType.KillAllBosses, RunCoinMultiplier = 2f, Modifiers = new() { new() { Type = ChallengeModifierType.RunTimeLimitSeconds, Value = 720f } } },
		new() { Id = "df_t2_oh_no", Name = "Blood Moon I • Ascended", Description = "Enemies are tougher, deadlier, and quicker.", MapId = "dark_forest", Tier = 2, Goal = ChallengeGoalType.KillAllBosses, RunCoinMultiplier = 1.5f, Modifiers = new() { new() { Type = ChallengeModifierType.EnemyHpMultiplier, Value = 1.25f }, new() { Type = ChallengeModifierType.EnemyDamageMultiplier, Value = 1.25f }, new() { Type = ChallengeModifierType.EnemySpeedMultiplier, Value = 1.25f } } },
		new() { Id = "df_t2_oh_shit", Name = "Blood Moon II • Ascended", Description = "A heavy stat spike for all enemies.", MapId = "dark_forest", Tier = 2, Goal = ChallengeGoalType.KillAllBosses, RunCoinMultiplier = 1.75f, Modifiers = new() { new() { Type = ChallengeModifierType.EnemyHpMultiplier, Value = 1.5f }, new() { Type = ChallengeModifierType.EnemyDamageMultiplier, Value = 1.5f }, new() { Type = ChallengeModifierType.EnemySpeedMultiplier, Value = 1.5f } } },
		new() { Id = "df_t2_wtf", Name = "Blood Moon III • Ascended", Description = "Maximum enemy scaling. Survive if you can.", MapId = "dark_forest", Tier = 2, Goal = ChallengeGoalType.KillAllBosses, RunCoinMultiplier = 2f, Modifiers = new() { new() { Type = ChallengeModifierType.EnemyHpMultiplier, Value = 2f }, new() { Type = ChallengeModifierType.EnemyDamageMultiplier, Value = 2f }, new() { Type = ChallengeModifierType.EnemySpeedMultiplier, Value = 2f } } },

		// Dark Forest — Tier 3 (starter set)
		new() { Id = "df_t3_fragile", Name = "Glass Bones III", Description = "Any hit ends the run instantly.", MapId = "dark_forest", Tier = 3, Goal = ChallengeGoalType.KillAllBosses, RunCoinMultiplier = 1.6f, Modifiers = new() { new() { Type = ChallengeModifierType.OneHitDeath } } },
		new() { Id = "df_t3_speedrunner", Name = "Doom Clock III", Description = "Defeat the final boss within 10 minutes.", MapId = "dark_forest", Tier = 3, Goal = ChallengeGoalType.KillAllBosses, RunCoinMultiplier = 1.75f, Modifiers = new() { new() { Type = ChallengeModifierType.RunTimeLimitSeconds, Value = 600f } } },
		new() { Id = "df_t3_wtf", Name = "Blood Moon III • Nightmare", Description = "Maximum enemy scaling. Survive if you can.", MapId = "dark_forest", Tier = 3, Goal = ChallengeGoalType.KillAllBosses, RunCoinMultiplier = 2f, Modifiers = new() { new() { Type = ChallengeModifierType.EnemyHpMultiplier, Value = 2f }, new() { Type = ChallengeModifierType.EnemyDamageMultiplier, Value = 2f }, new() { Type = ChallengeModifierType.EnemySpeedMultiplier, Value = 2f } } },
	};
}
