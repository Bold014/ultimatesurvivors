using System.Collections.Generic;

public enum UnlockCategory
{
	Characters,
	Weapons,
	Tomes,
	Items
}

public class UnlockDefinition
{
	public string Id { get; init; }
	public string Name { get; init; }
	public string Description { get; init; }
	public UnlockCategory Category { get; init; }
	public int CoinCost { get; init; }
	/// <summary>Quest ID that must be claimed before this unlock can be purchased. Null = no requirement.</summary>
	public string PrerequisiteQuestId { get; init; }
	public bool IsFree => CoinCost == 0;

	public static readonly List<UnlockDefinition> All = new()
	{
		// ── Characters ─────────────────────────────────────────────────────────
		new() { Id = "char_archer",      Name = "Archer",      Category = UnlockCategory.Characters, CoinCost = 0,   PrerequisiteQuestId = null,           Description = "A nimble ranger. Fast and precise." },
		new() { Id = "char_warrior",     Name = "Warrior",     Category = UnlockCategory.Characters, CoinCost = 200, PrerequisiteQuestId = "complete_1",   Description = "A resilient fighter with high HP." },
		new() { Id = "char_mage",        Name = "Mage",        Category = UnlockCategory.Characters, CoinCost = 300, PrerequisiteQuestId = "survive_5",    Description = "A powerful spellcaster. High damage, low health." },
		new() { Id = "char_knight",      Name = "Knight",      Category = UnlockCategory.Characters, CoinCost = 150, PrerequisiteQuestId = "kill_100",     Description = "An armoured melee fighter. High HP, slow but deadly." },
		new() { Id = "char_templar",     Name = "Templar",     Category = UnlockCategory.Characters, CoinCost = 250, PrerequisiteQuestId = "kills_magicwand_500", Description = "A swift lightning striker. Chains bolts between enemies." },
		new() { Id = "char_druid",       Name = "Druid",       Category = UnlockCategory.Characters, CoinCost = 300, PrerequisiteQuestId = "kills_bow_500",Description = "A nature guardian. Orbiting shards defend and destroy." },
		new() { Id = "char_pyromancer",  Name = "Pyromancer",  Category = UnlockCategory.Characters, CoinCost = 350, PrerequisiteQuestId = "survive_15",   Description = "A glass-cannon fire mage. Highest damage, lowest HP." },

		// ── Weapons ────────────────────────────────────────────────────────────
		new() { Id = "weapon_magicwand",     Name = "Magic Wand",     Category = UnlockCategory.Weapons, CoinCost = 0,   PrerequisiteQuestId = null,               Description = "Fires seeking projectiles at the nearest enemy." },
		new() { Id = "weapon_axe",           Name = "Throwing Axe",   Category = UnlockCategory.Weapons, CoinCost = 150, PrerequisiteQuestId = "kills_sword_300",  Description = "Hurls spinning axes that arc through enemies." },
		new() { Id = "weapon_aura",          Name = "Aura Blast",     Category = UnlockCategory.Weapons, CoinCost = 200, PrerequisiteQuestId = "complete_1",       Description = "Emits a damage pulse around the player." },
		new() { Id = "weapon_stormrod",      Name = "Storm Rod",      Category = UnlockCategory.Weapons, CoinCost = 200, PrerequisiteQuestId = "kills_magicwand_500", Description = "Fires lightning bolts at up to 3 nearby enemies at once." },
		new() { Id = "weapon_orbitalshards", Name = "Orbital Shards", Category = UnlockCategory.Weapons, CoinCost = 250, PrerequisiteQuestId = "kills_bow_500",    Description = "Rock shards orbit you, crushing enemies they pass through." },
		new() { Id = "weapon_embertrail",    Name = "Ember Trail",    Category = UnlockCategory.Weapons, CoinCost = 200, PrerequisiteQuestId = "kills_aura_300",   Description = "Drops lingering fire zones that burn enemies standing in them." },

		// ── Tomes ──────────────────────────────────────────────────────────────
		// CoinCost = 0 means automatically unlocked — no purchase required.
		new() { Id = "tome_agility",         Name = "Agility Tome",          Category = UnlockCategory.Tomes, CoinCost = 0,   PrerequisiteQuestId = null,              Description = "Increases movement speed." },
		new() { Id = "tome_size",            Name = "Size Tome",             Category = UnlockCategory.Tomes, CoinCost = 0,   PrerequisiteQuestId = null,              Description = "Increases the size of your attacks, projectiles, and explosions." },
		new() { Id = "tome_shield",          Name = "Shield Tome",           Category = UnlockCategory.Tomes, CoinCost = 0,   PrerequisiteQuestId = null,              Description = "Increases shield. Shield absorbs damage before HP and regenerates when out of combat." },
		new() { Id = "tome_regen",           Name = "Regen Tome",            Category = UnlockCategory.Tomes, CoinCost = 0,   PrerequisiteQuestId = null,              Description = "Increases the amount of HP you regenerate per minute." },
		new() { Id = "tome_lifesteal",       Name = "Lifesteal Tome",        Category = UnlockCategory.Tomes, CoinCost = 0,   PrerequisiteQuestId = null,              Description = "Heal a percentage of damage dealt to enemies." },
		new() { Id = "tome_projectilespeed", Name = "Projectile Speed Tome", Category = UnlockCategory.Tomes, CoinCost = 0,   PrerequisiteQuestId = null,              Description = "Increases the speed of projectiles." },
		new() { Id = "tome_hp",              Name = "HP Tome",               Category = UnlockCategory.Tomes, CoinCost = 0,   PrerequisiteQuestId = null,              Description = "Increases max HP." },
		new() { Id = "tome_golden",          Name = "Golden Tome",           Category = UnlockCategory.Tomes, CoinCost = 0,   PrerequisiteQuestId = null,              Description = "Gain more gold from all sources." },
		new() { Id = "tome_damage",          Name = "Damage Tome",           Category = UnlockCategory.Tomes, CoinCost = 0,   PrerequisiteQuestId = null,              Description = "Increases the damage of your attacks." },
		new() { Id = "tome_cooldown",        Name = "Cooldown Tome",         Category = UnlockCategory.Tomes, CoinCost = 0,   PrerequisiteQuestId = null,              Description = "Increases your attack speed." },
		new() { Id = "tome_precision",       Name = "Precision Tome",        Category = UnlockCategory.Tomes, CoinCost = 150, PrerequisiteQuestId = "level_10",        Description = "Increases critical hit chance. Reach over 100% to Overcrit for even more damage." },
		new() { Id = "tome_knockback",       Name = "Knockback Tome",        Category = UnlockCategory.Tomes, CoinCost = 150, PrerequisiteQuestId = "kill_500",        Description = "Increases knockback, pushing enemies further when you hit them." },
		new() { Id = "tome_quantity",        Name = "Quantity Tome",         Category = UnlockCategory.Tomes, CoinCost = 200, PrerequisiteQuestId = "projectiles_5000",Description = "Increases the number of attacks and projectiles." },
		new() { Id = "tome_xp",             Name = "XP Tome",               Category = UnlockCategory.Tomes, CoinCost = 150, PrerequisiteQuestId = "complete_5",      Description = "Increases XP gained from all sources." },
		new() { Id = "tome_duration",       Name = "Duration Tome",          Category = UnlockCategory.Tomes, CoinCost = 9,   PrerequisiteQuestId = "axe_level_10",   Description = "Increases the duration of attacks and projectiles." },

		// ── Items ──────────────────────────────────────────────────────────────
		// Unlocking an item adds it to the chest reward pool so it can appear when you open a chest.
		new() { Id = "item_revival",      Name = "Revival Stone",    Category = UnlockCategory.Items, CoinCost = 250, PrerequisiteQuestId = "survive_15",  Description = "Can appear in chest rewards. Survive one lethal hit with 30% HP." },
		new() { Id = "item_luckcharm",    Name = "Luck Charm",       Category = UnlockCategory.Items, CoinCost = 200, PrerequisiteQuestId = "complete_1",  Description = "Can appear in chest rewards. +2 Luck, improving upgrade rarity." },
		new() { Id = "item_pendant",      Name = "Warding Pendant",  Category = UnlockCategory.Items, CoinCost = 300, PrerequisiteQuestId = "nodamage_60", Description = "Can appear in chest rewards. Brief invincibility after taking damage." },
		new() { Id = "item_ironrations",  Name = "Iron Rations",     Category = UnlockCategory.Items, CoinCost = 250, PrerequisiteQuestId = "kill_100",    Description = "Can appear in chest rewards. +25% maximum HP." },
		new() { Id = "item_cursedamulet", Name = "Cursed Amulet",    Category = UnlockCategory.Items, CoinCost = 200, PrerequisiteQuestId = "deaths_10",   Description = "Can appear in chest rewards. +10% damage per 10 deaths (max +30%)." },
		new() { Id = "item_merchantbadge",Name = "Merchant's Badge", Category = UnlockCategory.Items, CoinCost = 150, PrerequisiteQuestId = "chests_10",   Description = "Can appear in chest rewards. Your next chest costs nothing." },
	};

	public static List<UnlockDefinition> ForCategory( UnlockCategory cat ) =>
		All.FindAll( u => u.Category == cat );
}
