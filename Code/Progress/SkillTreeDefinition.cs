using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Categories for skill tree nodes.
/// </summary>
public enum SkillCategory
{
	Offense,
	Defense,
	Utility,
	Economy
}

/// <summary>
/// Defines a single skill node in the persistent skill tree.
/// Matches the definition-driven pattern used by UnlockDefinition and QuestDefinition.
/// </summary>
public class SkillNodeDefinition
{
	public string Id { get; init; }
	public string Name { get; init; }
	public string Description { get; init; }
	public SkillCategory Category { get; init; }
	public int MaxLevel { get; init; }
	/// <summary>Base essence cost for level 1.</summary>
	public int BaseCost { get; init; }
	/// <summary>Additional cost per current level (total = BaseCost + currentLevel * CostPerLevel).</summary>
	public int CostPerLevel { get; init; }
	/// <summary>Stat bonus per level (interpretation depends on StatType).</summary>
	public float ValuePerLevel { get; init; }
	/// <summary>Which stat this skill modifies (reuses UpgradeDefinition.UpgradeType).</summary>
	public UpgradeDefinition.UpgradeType StatType { get; init; }
	/// <summary>Skill IDs that must have at least 1 level before this skill can be upgraded.</summary>
	public string[] Prerequisites { get; init; } = System.Array.Empty<string>();
	/// <summary>Short stat suffix for display (e.g. "%", "/min", " flat").</summary>
	public string StatSuffix { get; init; } = "%";

	/// <summary>Cost to upgrade from currentLevel to currentLevel+1.</summary>
	public int GetCost( int currentLevel ) => BaseCost + currentLevel * CostPerLevel;

	/// <summary>Total bonus at a given level.</summary>
	public float GetTotalValue( int level ) => level * ValuePerLevel;

	// ── All 21 skill nodes ──────────────────────────────────────────────────

	public static readonly IReadOnlyList<SkillNodeDefinition> All = new SkillNodeDefinition[]
	{
		// ═══ OFFENSE (7) ═══
		new()
		{
			Id = "damage", Name = "Damage", Description = "+2% weapon damage per level",
			Category = SkillCategory.Offense, MaxLevel = 10,
			BaseCost = 8, CostPerLevel = 3, ValuePerLevel = 0.02f,
			StatType = UpgradeDefinition.UpgradeType.DamageUp, StatSuffix = "%"
		},
		new()
		{
			Id = "crit_chance", Name = "Crit Chance", Description = "+1.5% crit chance per level",
			Category = SkillCategory.Offense, MaxLevel = 8,
			BaseCost = 12, CostPerLevel = 4, ValuePerLevel = 0.015f,
			StatType = UpgradeDefinition.UpgradeType.CritChanceUp, StatSuffix = "%",
			Prerequisites = new[] { "damage" }
		},
		new()
		{
			Id = "crit_power", Name = "Crit Power", Description = "+10% crit damage per level",
			Category = SkillCategory.Offense, MaxLevel = 5,
			BaseCost = 18, CostPerLevel = 6, ValuePerLevel = 0.10f,
			StatType = UpgradeDefinition.UpgradeType.CritDamageUp, StatSuffix = "%",
			Prerequisites = new[] { "crit_chance" }
		},
		new()
		{
			Id = "attack_speed", Name = "Attack Speed", Description = "+1.5% attack speed per level",
			Category = SkillCategory.Offense, MaxLevel = 8,
			BaseCost = 10, CostPerLevel = 3, ValuePerLevel = 0.015f,
			StatType = UpgradeDefinition.UpgradeType.CooldownDown, StatSuffix = "%"
		},
		new()
		{
			Id = "proj_speed", Name = "Proj Speed", Description = "+3% projectile speed per level",
			Category = SkillCategory.Offense, MaxLevel = 5,
			BaseCost = 8, CostPerLevel = 3, ValuePerLevel = 0.03f,
			StatType = UpgradeDefinition.UpgradeType.ProjectileSpeedUp, StatSuffix = "%"
		},
		new()
		{
			Id = "area", Name = "Area", Description = "+2% attack area per level",
			Category = SkillCategory.Offense, MaxLevel = 8,
			BaseCost = 8, CostPerLevel = 3, ValuePerLevel = 0.02f,
			StatType = UpgradeDefinition.UpgradeType.AreaUp, StatSuffix = "%"
		},
		new()
		{
			Id = "knockback", Name = "Knockback", Description = "+3% knockback per level",
			Category = SkillCategory.Offense, MaxLevel = 5,
			BaseCost = 8, CostPerLevel = 3, ValuePerLevel = 0.03f,
			StatType = UpgradeDefinition.UpgradeType.KnockbackUp, StatSuffix = "%"
		},

		// ═══ DEFENSE (6) ═══
		new()
		{
			Id = "vitality", Name = "Vitality", Description = "+3% max HP per level",
			Category = SkillCategory.Defense, MaxLevel = 10,
			BaseCost = 8, CostPerLevel = 3, ValuePerLevel = 0.03f,
			StatType = UpgradeDefinition.UpgradeType.MaxHPUp, StatSuffix = "%"
		},
		new()
		{
			Id = "armor", Name = "Armor", Description = "+1 flat armor per level",
			Category = SkillCategory.Defense, MaxLevel = 8,
			BaseCost = 10, CostPerLevel = 4, ValuePerLevel = 1f,
			StatType = UpgradeDefinition.UpgradeType.ArmorUp, StatSuffix = " flat",
			Prerequisites = new[] { "vitality" }
		},
		new()
		{
			Id = "shield", Name = "Shield", Description = "+3 shield per level",
			Category = SkillCategory.Defense, MaxLevel = 8,
			BaseCost = 10, CostPerLevel = 4, ValuePerLevel = 3f,
			StatType = UpgradeDefinition.UpgradeType.ShieldUp, StatSuffix = " flat",
			Prerequisites = new[] { "vitality" }
		},
		new()
		{
			Id = "regen", Name = "Regen", Description = "+0.5 HP regen/min per level",
			Category = SkillCategory.Defense, MaxLevel = 8,
			BaseCost = 8, CostPerLevel = 3, ValuePerLevel = 0.5f,
			StatType = UpgradeDefinition.UpgradeType.RegenUp, StatSuffix = "/min"
		},
		new()
		{
			Id = "evasion", Name = "Evasion", Description = "+1% evasion per level",
			Category = SkillCategory.Defense, MaxLevel = 5,
			BaseCost = 15, CostPerLevel = 5, ValuePerLevel = 0.01f,
			StatType = UpgradeDefinition.UpgradeType.EvasionUp, StatSuffix = "%",
			Prerequisites = new[] { "armor" }
		},
		new()
		{
			Id = "lifesteal", Name = "Lifesteal", Description = "+0.5% lifesteal per level",
			Category = SkillCategory.Defense, MaxLevel = 5,
			BaseCost = 15, CostPerLevel = 5, ValuePerLevel = 0.005f,
			StatType = UpgradeDefinition.UpgradeType.LifestealUp, StatSuffix = "%",
			Prerequisites = new[] { "regen" }
		},

		// ═══ UTILITY (5) ═══
		new()
		{
			Id = "swiftness", Name = "Swiftness", Description = "+2% movement speed per level",
			Category = SkillCategory.Utility, MaxLevel = 8,
			BaseCost = 8, CostPerLevel = 3, ValuePerLevel = 0.02f,
			StatType = UpgradeDefinition.UpgradeType.SpeedUp, StatSuffix = "%"
		},
		new()
		{
			Id = "magnetism", Name = "Magnetism", Description = "+5% pickup radius per level",
			Category = SkillCategory.Utility, MaxLevel = 6,
			BaseCost = 8, CostPerLevel = 3, ValuePerLevel = 0.05f,
			StatType = UpgradeDefinition.UpgradeType.MagnetUp, StatSuffix = "%"
		},
		new()
		{
			Id = "fortune", Name = "Fortune", Description = "+0.3 Luck per level",
			Category = SkillCategory.Utility, MaxLevel = 5,
			BaseCost = 12, CostPerLevel = 4, ValuePerLevel = 0.3f,
			StatType = UpgradeDefinition.UpgradeType.LuckUp, StatSuffix = " flat",
			Prerequisites = new[] { "magnetism" }
		},
		new()
		{
			Id = "agility", Name = "Agility", Description = "-2% dash cooldown per level",
			Category = SkillCategory.Utility, MaxLevel = 5,
			BaseCost = 10, CostPerLevel = 4, ValuePerLevel = 0.02f,
			StatType = UpgradeDefinition.UpgradeType.DashCooldownUp, StatSuffix = "%",
			Prerequisites = new[] { "swiftness" }
		},
		new()
		{
			Id = "duration", Name = "Duration", Description = "+2% ability duration per level",
			Category = SkillCategory.Utility, MaxLevel = 5,
			BaseCost = 10, CostPerLevel = 3, ValuePerLevel = 0.02f,
			StatType = UpgradeDefinition.UpgradeType.DurationMultiplierUp, StatSuffix = "%"
		},

		// ═══ ECONOMY (2) ═══
		new()
		{
			Id = "gold_find", Name = "Gold Find", Description = "+3% gold from all sources per level",
			Category = SkillCategory.Economy, MaxLevel = 8,
			BaseCost = 10, CostPerLevel = 4, ValuePerLevel = 0.03f,
			StatType = UpgradeDefinition.UpgradeType.GoldMultiplierUp, StatSuffix = "%"
		},
		new()
		{
			Id = "wisdom", Name = "Wisdom", Description = "+3% XP from all sources per level",
			Category = SkillCategory.Economy, MaxLevel = 8,
			BaseCost = 10, CostPerLevel = 4, ValuePerLevel = 0.03f,
			StatType = UpgradeDefinition.UpgradeType.XPMultiplierUp, StatSuffix = "%"
		},
	};

	/// <summary>Look up a skill by ID. Returns null if not found.</summary>
	public static SkillNodeDefinition GetById( string id )
	{
		return All.FirstOrDefault( s => s.Id == id );
	}

	/// <summary>All skills in a given category.</summary>
	public static IEnumerable<SkillNodeDefinition> ForCategory( SkillCategory category )
	{
		return All.Where( s => s.Category == category );
	}
}
