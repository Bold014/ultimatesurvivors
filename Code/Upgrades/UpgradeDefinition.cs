/// <summary>
/// Defines a single upgrade/item choice that can appear in level-up, shrine, or chest reward screens.
/// </summary>
public class UpgradeDefinition
{
	private const float MinCooldownMultiplier = 0.35f;
	private const float MinDashCooldownMultiplier = 0.40f;
	private const float MaxEvasion = 0.55f;
	private const float MaxLifesteal = 0.35f;
	private const float MaxCritChance = 1.5f;
	private const float MaxGoldMultiplier = 2.5f;
	private const float MaxXPMultiplier = 2.5f;
	private const float MaxSilverMultiplier = 2.5f;
	private const float MaxLuck = 15f;
	private const float MaxArmor = 40f;
	private const int MaxProjectileCount = 4;

	public string Name        { get; init; }
	public string Description { get; init; }
	public UpgradeType Type   { get; init; }
	public float Value        { get; init; }
	/// <summary>Display name of the weapon (for WeaponUnlock / WeaponLevelUp types).</summary>
	public string WeaponName  { get; init; } = null;
	/// <summary>True when this is a first-time weapon unlock (shows "NEW" badge).</summary>
	public bool IsNewWeapon   { get; init; } = false;
	/// <summary>Rarity tier — used by shrine choices for colour-coding.</summary>
	public UpgradeRarity Rarity { get; init; } = UpgradeRarity.Common;
	/// <summary>How many times this tome can be levelled up. 0 = not a tome (no level cap).</summary>
	public int MaxLevel { get; init; } = 0;
	/// <summary>The level that WILL be reached if this choice is selected. Set dynamically by UpgradeSystem.</summary>
	public int NextLevel { get; init; } = 0;
	/// <summary>Human-readable "current → new" stat line, e.g. "Damage: 10.0 → 12.0". Set dynamically by UpgradeSystem.</summary>
	public string StatPreview { get; init; } = null;
	/// <summary>If set, this entry only appears in the pool when the matching UnlockDefinition ID is owned. Null = always available.</summary>
	public string UnlockId    { get; init; } = null;
	/// <summary>For PlayerEffect type items: the UnlockDefinition ID passed to PlayerLocalState.ApplyItemById().</summary>
	public string ItemId      { get; init; } = null;

	public enum UpgradeType
	{
		SpeedUp, HPUp, MaxHPUp, DamageUp, AreaUp,
		CooldownDown, MagnetUp, LuckUp, ArmorUp, Revival,
		WeaponUnlock, WeaponLevelUp,
		/// <summary>Special item effect — delegates to PlayerLocalState.ApplyItemById(ItemId).</summary>
		PlayerEffect,
		/// <summary>Increases shield capacity. Shield absorbs damage before HP.</summary>
		ShieldUp,
		/// <summary>Increases passive HP regeneration per minute.</summary>
		RegenUp,
		/// <summary>Increases the speed multiplier of all projectiles.</summary>
		ProjectileSpeedUp,
		/// <summary>Increases critical hit chance (0–1+ range; above 1 is Overcrit).</summary>
		CritChanceUp,
		/// <summary>Increases enemy knockback distance multiplier.</summary>
		KnockbackUp,
		/// <summary>Increases gold gained from all sources.</summary>
		GoldMultiplierUp,
		/// <summary>Adds bonus projectile count to multi-projectile attacks.</summary>
		ProjectileCountUp,
		/// <summary>Increases XP gained from all sources.</summary>
		XPMultiplierUp,
		// ── Megabonk shrine upgrades ───────────────────────────────────────────
		/// <summary>Increases critical hit damage multiplier.</summary>
		CritDamageUp,
		/// <summary>Chance to evade incoming damage.</summary>
		EvasionUp,
		/// <summary>Heal a fraction of damage dealt.</summary>
		LifestealUp,
		/// <summary>Increases silver/premium currency gain.</summary>
		SilverMultiplierUp,
		/// <summary>Increases ability/effect duration.</summary>
		DurationMultiplierUp,
		/// <summary>Reduces dash cooldown.</summary>
		DashCooldownUp,
	}

	public enum UpgradeRarity { Common, Uncommon, Rare }

	/// <summary>Applies this upgrade to the player. Weapons need the optional parameter.</summary>
	public void Apply( PlayerLocalState state, PlayerWeapons weapons = null )
	{
		switch ( Type )
		{
			case UpgradeType.WeaponUnlock:
				weapons?.AddWeaponByDisplayName( WeaponName );
				break;
			case UpgradeType.WeaponLevelUp:
				weapons?.LevelUpWeapon( WeaponName );
				break;
			case UpgradeType.SpeedUp:
				state.Speed *= 1f + Value;
				break;
			case UpgradeType.HPUp:
				state.Heal( state.MaxHP * Value );
				break;
			case UpgradeType.MaxHPUp:
				var bonus = state.MaxHP * Value;
				state.MaxHP += bonus;
				state.Heal( bonus );
				break;
			case UpgradeType.DamageUp:
				state.Damage *= 1f + Value;
				break;
			case UpgradeType.AreaUp:
				state.Area *= 1f + Value;
				break;
			case UpgradeType.CooldownDown:
				state.CooldownMultiplier = Math.Max( MinCooldownMultiplier, state.CooldownMultiplier * (1f - Value) );
				break;
			case UpgradeType.MagnetUp:
				state.MagnetRadius *= 1f + Value;
				break;
			case UpgradeType.LuckUp:
				state.Luck = Math.Min( MaxLuck, state.Luck + Value );
				break;
			case UpgradeType.ArmorUp:
				state.Armor = Math.Min( MaxArmor, state.Armor + Value );
				break;
		case UpgradeType.Revival:
			state.HasRevival = true;
			break;
		case UpgradeType.PlayerEffect:
			state?.ApplyItemById( ItemId );
			break;
		case UpgradeType.ShieldUp:
			state.MaxShield += Value;
			state.Shield = Math.Min( state.Shield + Value, state.MaxShield );
			break;
		case UpgradeType.RegenUp:
			state.RegenPerMinute += Value;
			break;
		case UpgradeType.ProjectileSpeedUp:
			state.ProjectileSpeedMultiplier *= 1f + Value;
			break;
		case UpgradeType.CritChanceUp:
			state.CritChance = Math.Min( MaxCritChance, state.CritChance + Value );
			break;
		case UpgradeType.KnockbackUp:
			state.Knockback += Value;
			break;
		case UpgradeType.GoldMultiplierUp:
			state.GoldMultiplier = Math.Min( MaxGoldMultiplier, state.GoldMultiplier * (1f + Value) );
			break;
		case UpgradeType.ProjectileCountUp:
			state.ProjectileCount = Math.Min( MaxProjectileCount, state.ProjectileCount + (int)Value );
			break;
		case UpgradeType.XPMultiplierUp:
			state.XPMultiplier = Math.Min( MaxXPMultiplier, state.XPMultiplier * (1f + Value) );
			break;
		case UpgradeType.CritDamageUp:
			state.CritMultiplier += Value;
			break;
		case UpgradeType.EvasionUp:
			state.Evasion = Math.Min( MaxEvasion, state.Evasion + Value );
			break;
		case UpgradeType.LifestealUp:
			state.Lifesteal = Math.Min( MaxLifesteal, state.Lifesteal + Value );
			break;
		case UpgradeType.SilverMultiplierUp:
			state.SilverMultiplier = Math.Min( MaxSilverMultiplier, state.SilverMultiplier * (1f + Value) );
			break;
		case UpgradeType.DurationMultiplierUp:
			state.DurationMultiplier *= 1f + Value;
			break;
		case UpgradeType.DashCooldownUp:
			state.DashCooldownMultiplier = Math.Max( MinDashCooldownMultiplier, state.DashCooldownMultiplier * (1f - Value) );
			break;
		}
	}

	// ── All weapon display names offered through the level-up screen ──────────

	public static readonly IReadOnlyList<string> AllWeaponDisplayNames = new[]
	{
		"Magic Wand", "Bow", "Axe", "Aura", "Storm Rod", "Orbital Shards", "Ember Trail", "Sword"
	};

	/// <summary>
	/// Maps a weapon display name to its UnlockDefinition ID.
	/// Weapons absent from this dictionary are always available (no unlock required).
	/// </summary>
	public static readonly IReadOnlyDictionary<string, string> WeaponUnlockIds =
		new Dictionary<string, string>
		{
			{ "Magic Wand",     "weapon_magicwand"     },
			{ "Sword",          "weapon_sword"         },
			{ "Bow",            "weapon_bow"           },
			{ "Axe",            "weapon_axe"           },
			{ "Aura",           "weapon_aura"          },
			{ "Storm Rod",      "weapon_stormrod"      },
			{ "Orbital Shards", "weapon_orbitalshards" },
			{ "Ember Trail",    "weapon_embertrail"    },
		};

	// ── Level-up: TOME pool ───────────────────────────────────────────────────
	// Shown alongside weapon choices when the player gains a level from XP.
	// Tomes with no UnlockId are always available. Others require a meta-shop unlock.

	public static IReadOnlyList<UpgradeDefinition> TomePool { get; } = new UpgradeDefinition[]
	{
		// ── Auto-unlocked tomes (always in the pool) ─────────────────────────
		new() { Name = "Agility Tome",          Description = "+8% movement speed",               Type = UpgradeType.SpeedUp,           Value = 0.08f, MaxLevel = 10 },
		new() { Name = "Size Tome",             Description = "+6% attack size",                 Type = UpgradeType.AreaUp,            Value = 0.06f, MaxLevel = 10 },
		new() { Name = "Shield Tome",           Description = "+8 shield capacity",               Type = UpgradeType.ShieldUp,          Value = 8f,    MaxLevel = 10 },
		new() { Name = "Regen Tome",            Description = "+2 HP regen per minute",           Type = UpgradeType.RegenUp,           Value = 2f,    MaxLevel = 10 },
		new() { Name = "Lifesteal Tome",       Description = "+2% lifesteal (heal % of damage dealt)", Type = UpgradeType.LifestealUp, Value = 0.02f, MaxLevel = 10 },
		new() { Name = "Projectile Speed Tome", Description = "+8% projectile speed",             Type = UpgradeType.ProjectileSpeedUp, Value = 0.08f, MaxLevel = 10 },
		new() { Name = "HP Tome",               Description = "+8% max HP",                        Type = UpgradeType.MaxHPUp,           Value = 0.08f, MaxLevel = 10 },
		new() { Name = "Golden Tome",           Description = "+8% gold from all sources",        Type = UpgradeType.GoldMultiplierUp,  Value = 0.08f, MaxLevel = 10 },
		new() { Name = "Damage Tome",           Description = "+5% damage",                        Type = UpgradeType.DamageUp,          Value = 0.05f, MaxLevel = 10 },
		new() { Name = "Cooldown Tome",         Description = "+2% attack speed",                  Type = UpgradeType.CooldownDown,      Value = 0.02f, MaxLevel = 10 },

		// ── Locked tomes (require meta-shop unlock) ──────────────────────────
		new() { Name = "Precision Tome",        Description = "+4% critical hit chance",          Type = UpgradeType.CritChanceUp,      Value = 0.04f, MaxLevel = 10, UnlockId = "tome_precision" },
		new() { Name = "Knockback Tome",        Description = "+6% enemy knockback distance",   Type = UpgradeType.KnockbackUp,       Value = 0.06f, MaxLevel = 10, UnlockId = "tome_knockback" },
		new() { Name = "Quantity Tome",         Description = "+1 projectile to all attacks",     Type = UpgradeType.ProjectileCountUp, Value = 1f,    MaxLevel = 4, UnlockId = "tome_quantity"  },
		new() { Name = "XP Tome",               Description = "+6% XP from all sources",           Type = UpgradeType.XPMultiplierUp,    Value = 0.06f, MaxLevel = 10, UnlockId = "tome_xp"        },
		new() { Name = "Duration Tome",         Description = "+8% attack and projectile duration", Type = UpgradeType.DurationMultiplierUp, Value = 0.08f, MaxLevel = 10, UnlockId = "tome_duration" },
	};

	// ── Shrine: stat boost pool (Megabonk-style, Luck scales rarity) ────────────
	// Shown when a Charge Shrine (LevelUpBeacon) is fully charged.
	// Higher Luck = better bonuses (more Uncommon/Rare offers).
	// Includes an implicit "Ignore Offers" option handled by UpgradeSystem.CanIgnore.

	public static IReadOnlyList<UpgradeDefinition> ShrinePool { get; } = new UpgradeDefinition[]
	{
		// ═══ OFFENSE ═══
		// Damage %
		new() { Name = "Gain 6% Damage",    Description = "+6% weapon damage",       Type = UpgradeType.DamageUp,     Value = 0.06f, Rarity = UpgradeRarity.Common   },
		new() { Name = "Gain 10% Damage",   Description = "+10% weapon damage",     Type = UpgradeType.DamageUp,     Value = 0.10f, Rarity = UpgradeRarity.Uncommon },
		new() { Name = "Gain 14% Damage",    Description = "+14% weapon damage",      Type = UpgradeType.DamageUp,     Value = 0.14f, Rarity = UpgradeRarity.Rare     },
		// Attack Speed / Cooldown
		new() { Name = "Gain 4% Cooldown",   Description = "-4% weapon cooldown",     Type = UpgradeType.CooldownDown, Value = 0.04f, Rarity = UpgradeRarity.Common   },
		new() { Name = "Gain 7% Cooldown",  Description = "-7% weapon cooldown",   Type = UpgradeType.CooldownDown, Value = 0.07f, Rarity = UpgradeRarity.Uncommon },
		new() { Name = "Gain 9% Cooldown",  Description = "-9% weapon cooldown",     Type = UpgradeType.CooldownDown, Value = 0.09f, Rarity = UpgradeRarity.Rare     },
		// Critical Chance
		new() { Name = "Gain 5% Crit Chance", Description = "+5% critical hit chance", Type = UpgradeType.CritChanceUp, Value = 0.05f, Rarity = UpgradeRarity.Common   },
		new() { Name = "Gain 8% Crit Chance", Description = "+8% critical hit chance", Type = UpgradeType.CritChanceUp, Value = 0.08f, Rarity = UpgradeRarity.Uncommon },
		new() { Name = "Gain 12% Crit Chance", Description = "+12% critical hit chance", Type = UpgradeType.CritChanceUp, Value = 0.12f, Rarity = UpgradeRarity.Rare     },
		// Critical Damage
		new() { Name = "Gain 25% Crit Damage", Description = "+25% crit damage", Type = UpgradeType.CritDamageUp, Value = 0.30f, Rarity = UpgradeRarity.Common   },
		new() { Name = "Gain 50% Crit Damage", Description = "+50% crit damage", Type = UpgradeType.CritDamageUp, Value = 0.55f, Rarity = UpgradeRarity.Uncommon },
		new() { Name = "Gain 75% Crit Damage", Description = "+75% crit damage", Type = UpgradeType.CritDamageUp, Value = 0.80f, Rarity = UpgradeRarity.Rare     },
		// Projectile Speed
		new() { Name = "Gain 10% Proj Speed", Description = "+10% projectile speed", Type = UpgradeType.ProjectileSpeedUp, Value = 0.10f, Rarity = UpgradeRarity.Common   },
		new() { Name = "Gain 15% Proj Speed", Description = "+15% projectile speed", Type = UpgradeType.ProjectileSpeedUp, Value = 0.15f, Rarity = UpgradeRarity.Uncommon },
		new() { Name = "Gain 25% Proj Speed", Description = "+25% projectile speed", Type = UpgradeType.ProjectileSpeedUp, Value = 0.25f, Rarity = UpgradeRarity.Rare     },
		// Projectile Quantity
		new() { Name = "Gain +1 Projectile",  Description = "+1 projectile to attacks", Type = UpgradeType.ProjectileCountUp, Value = 1f, Rarity = UpgradeRarity.Common   },
		new() { Name = "Gain +1 Projectile", Description = "+1 projectile to attacks", Type = UpgradeType.ProjectileCountUp, Value = 1f, Rarity = UpgradeRarity.Rare     },
		// Attack Size / Area
		new() { Name = "Gain 6% Area",       Description = "+6% attack area",         Type = UpgradeType.AreaUp,       Value = 0.06f, Rarity = UpgradeRarity.Common   },
		new() { Name = "Gain 9% Area",      Description = "+9% attack area",        Type = UpgradeType.AreaUp,       Value = 0.09f, Rarity = UpgradeRarity.Uncommon },
		new() { Name = "Gain 13% Area",      Description = "+13% attack area",         Type = UpgradeType.AreaUp,       Value = 0.13f, Rarity = UpgradeRarity.Rare     },
		// Knockback
		new() { Name = "Gain 8% Knockback", Description = "+8% enemy knockback",    Type = UpgradeType.KnockbackUp,   Value = 0.08f, Rarity = UpgradeRarity.Common   },
		new() { Name = "Gain 14% Knockback", Description = "+14% enemy knockback",    Type = UpgradeType.KnockbackUp,   Value = 0.14f, Rarity = UpgradeRarity.Uncommon },
		new() { Name = "Gain 22% Knockback", Description = "+22% enemy knockback",    Type = UpgradeType.KnockbackUp,   Value = 0.22f, Rarity = UpgradeRarity.Rare     },

		// ═══ DEFENSE ═══
		// Max HP
		new() { Name = "Gain 8% Max HP",    Description = "+8% max HP",              Type = UpgradeType.MaxHPUp,      Value = 0.08f, Rarity = UpgradeRarity.Common   },
		new() { Name = "Gain 12% Max HP",    Description = "+12% max HP",             Type = UpgradeType.MaxHPUp,      Value = 0.12f, Rarity = UpgradeRarity.Uncommon },
		new() { Name = "Gain 20% Max HP",    Description = "+20% max HP",             Type = UpgradeType.MaxHPUp,      Value = 0.20f, Rarity = UpgradeRarity.Rare     },
		// HP Regeneration
		new() { Name = "Gain 3 HP Regen",    Description = "+3 HP regen per minute", Type = UpgradeType.RegenUp,      Value = 3f,    Rarity = UpgradeRarity.Common   },
		new() { Name = "Gain 6 HP Regen",    Description = "+6 HP regen per minute", Type = UpgradeType.RegenUp,      Value = 6f,    Rarity = UpgradeRarity.Uncommon },
		new() { Name = "Gain 10 HP Regen",   Description = "+10 HP regen per minute", Type = UpgradeType.RegenUp,     Value = 10f,   Rarity = UpgradeRarity.Rare     },
		// Armor
		new() { Name = "Gain 3 Armor",       Description = "+3 flat damage reduction", Type = UpgradeType.ArmorUp,    Value = 3f,    Rarity = UpgradeRarity.Common   },
		new() { Name = "Gain 5 Armor",       Description = "+5 flat damage reduction", Type = UpgradeType.ArmorUp,    Value = 5f,    Rarity = UpgradeRarity.Uncommon },
		new() { Name = "Gain 8 Armor",       Description = "+8 flat damage reduction", Type = UpgradeType.ArmorUp,    Value = 8f,    Rarity = UpgradeRarity.Rare     },
		// Shield
		new() { Name = "Gain 15 Shield",     Description = "+15 shield capacity",    Type = UpgradeType.ShieldUp,      Value = 15f,   Rarity = UpgradeRarity.Common   },
		new() { Name = "Gain 25 Shield",     Description = "+25 shield capacity",    Type = UpgradeType.ShieldUp,      Value = 25f,   Rarity = UpgradeRarity.Uncommon },
		new() { Name = "Gain 32 Shield",     Description = "+32 shield capacity",    Type = UpgradeType.ShieldUp,      Value = 32f,   Rarity = UpgradeRarity.Rare     },
		// Evasion
		new() { Name = "Gain 5% Evasion",    Description = "+5% chance to evade damage", Type = UpgradeType.EvasionUp, Value = 0.05f, Rarity = UpgradeRarity.Common   },
		new() { Name = "Gain 8% Evasion",    Description = "+8% chance to evade damage", Type = UpgradeType.EvasionUp, Value = 0.08f, Rarity = UpgradeRarity.Uncommon },
		new() { Name = "Gain 10% Evasion",   Description = "+10% chance to evade damage", Type = UpgradeType.EvasionUp, Value = 0.10f, Rarity = UpgradeRarity.Rare     },
		// Lifesteal
		new() { Name = "Gain 3% Lifesteal",  Description = "Heal 3% of damage dealt", Type = UpgradeType.LifestealUp, Value = 0.03f, Rarity = UpgradeRarity.Common   },
		new() { Name = "Gain 5% Lifesteal",  Description = "Heal 5% of damage dealt", Type = UpgradeType.LifestealUp, Value = 0.05f, Rarity = UpgradeRarity.Uncommon },
		new() { Name = "Gain 6% Lifesteal",  Description = "Heal 6% of damage dealt",  Type = UpgradeType.LifestealUp, Value = 0.06f, Rarity = UpgradeRarity.Rare     },
		// Restore HP
		new() { Name = "Restore 15% HP",     Description = "Heal 15% of max HP",     Type = UpgradeType.HPUp,         Value = 0.15f, Rarity = UpgradeRarity.Common   },
		new() { Name = "Restore 25% HP",     Description = "Heal 25% of max HP",     Type = UpgradeType.HPUp,         Value = 0.25f, Rarity = UpgradeRarity.Uncommon },

		// ═══ UTILITY ═══
		// Movement Speed
		new() { Name = "Gain 8% Speed",      Description = "+8% movement speed",     Type = UpgradeType.SpeedUp,      Value = 0.08f, Rarity = UpgradeRarity.Common   },
		new() { Name = "Gain 12% Speed",     Description = "+12% movement speed",    Type = UpgradeType.SpeedUp,      Value = 0.12f, Rarity = UpgradeRarity.Uncommon },
		new() { Name = "Gain 20% Speed",    Description = "+20% movement speed",     Type = UpgradeType.SpeedUp,      Value = 0.20f, Rarity = UpgradeRarity.Rare     },
		// Pickup Range
		new() { Name = "Gain 15% Magnet",    Description = "+15% XP pickup radius",  Type = UpgradeType.MagnetUp,     Value = 0.15f, Rarity = UpgradeRarity.Common   },
		new() { Name = "Gain 20% Magnet",     Description = "+20% XP pickup radius",  Type = UpgradeType.MagnetUp,     Value = 0.20f, Rarity = UpgradeRarity.Uncommon },
		new() { Name = "Gain 25% Magnet",     Description = "+25% XP pickup radius",  Type = UpgradeType.MagnetUp,     Value = 0.25f, Rarity = UpgradeRarity.Rare     },
		// Duration
		new() { Name = "Gain 10% Duration", Description = "+10% ability duration", Type = UpgradeType.DurationMultiplierUp, Value = 0.10f, Rarity = UpgradeRarity.Common   },
		new() { Name = "Gain 15% Duration",   Description = "+15% ability duration",  Type = UpgradeType.DurationMultiplierUp, Value = 0.15f, Rarity = UpgradeRarity.Uncommon },
		new() { Name = "Gain 20% Duration",  Description = "+20% ability duration", Type = UpgradeType.DurationMultiplierUp, Value = 0.20f, Rarity = UpgradeRarity.Rare     },
		// Dash Cooldown
		new() { Name = "Gain 10% Dash",      Description = "-10% dash cooldown",     Type = UpgradeType.DashCooldownUp, Value = 0.10f, Rarity = UpgradeRarity.Common   },
		new() { Name = "Gain 15% Dash",      Description = "-15% dash cooldown",      Type = UpgradeType.DashCooldownUp, Value = 0.15f, Rarity = UpgradeRarity.Uncommon },
		new() { Name = "Gain 20% Dash",      Description = "-20% dash cooldown",     Type = UpgradeType.DashCooldownUp, Value = 0.20f, Rarity = UpgradeRarity.Rare     },

		// ═══ ECONOMY / SCALING ═══
		// XP Gain
		new() { Name = "Gain 10% XP",       Description = "+10% XP from all sources", Type = UpgradeType.XPMultiplierUp, Value = 0.10f, Rarity = UpgradeRarity.Common   },
		new() { Name = "Gain 15% XP",       Description = "+15% XP from all sources", Type = UpgradeType.XPMultiplierUp, Value = 0.15f, Rarity = UpgradeRarity.Uncommon },
		new() { Name = "Gain 20% XP",       Description = "+20% XP from all sources", Type = UpgradeType.XPMultiplierUp, Value = 0.20f, Rarity = UpgradeRarity.Rare     },
		// Gold Gain
		new() { Name = "Gain 15% Gold",      Description = "+15% gold from all sources", Type = UpgradeType.GoldMultiplierUp, Value = 0.15f, Rarity = UpgradeRarity.Common   },
		new() { Name = "Gain 20% Gold",     Description = "+20% gold from all sources", Type = UpgradeType.GoldMultiplierUp, Value = 0.20f, Rarity = UpgradeRarity.Uncommon },
		new() { Name = "Gain 25% Gold",      Description = "+25% gold from all sources", Type = UpgradeType.GoldMultiplierUp, Value = 0.25f, Rarity = UpgradeRarity.Rare     },
		// Silver Gain (for future premium currency)
		new() { Name = "Gain 15% Silver",   Description = "+15% silver gain",      Type = UpgradeType.SilverMultiplierUp, Value = 0.15f, Rarity = UpgradeRarity.Common   },
		new() { Name = "Gain 25% Silver",   Description = "+25% silver gain",      Type = UpgradeType.SilverMultiplierUp, Value = 0.25f, Rarity = UpgradeRarity.Uncommon },
		new() { Name = "Gain 30% Silver",   Description = "+30% silver gain",      Type = UpgradeType.SilverMultiplierUp, Value = 0.30f, Rarity = UpgradeRarity.Rare     },
		// Luck / Item Drop Rate
		new() { Name = "Gain 1 Luck",        Description = "+1 Luck — better upgrade rolls", Type = UpgradeType.LuckUp, Value = 1f, Rarity = UpgradeRarity.Common   },
		new() { Name = "Gain 1 Luck",       Description = "+1 Luck — better upgrade rolls", Type = UpgradeType.LuckUp, Value = 1f, Rarity = UpgradeRarity.Uncommon },
		new() { Name = "Gain 2 Luck",        Description = "+2 Luck — better upgrade rolls", Type = UpgradeType.LuckUp, Value = 2f, Rarity = UpgradeRarity.Rare     },
		// Revival (Rare only)
		new() { Name = "Revival",            Description = "Survive one lethal hit", Type = UpgradeType.Revival,      Value = 0f,    Rarity = UpgradeRarity.Rare     },
	};

	// ── Chest: ITEM pool ──────────────────────────────────────────────────────
	// Shown when a player opens a chest with coins.
	// Base items are always available. Unlockable items only appear once the
	// matching unlock has been purchased in the meta shop.

	public static IReadOnlyList<UpgradeDefinition> ItemPool { get; } = new UpgradeDefinition[]
	{
		// Base items — always in the chest pool
		new() { Name = "Luck Charm",           Description = "+2 Luck — better upgrade rolls",         Type = UpgradeType.LuckUp,       Value = 2f    },
		new() { Name = "Iron Skin",            Description = "+10 flat damage reduction",              Type = UpgradeType.ArmorUp,      Value = 10f   },
		new() { Name = "Berserker Brew",       Description = "+16% weapon damage",                     Type = UpgradeType.DamageUp,     Value = 0.16f },
		new() { Name = "Sorcerer's Handbook",  Description = "+12% attack area",                       Type = UpgradeType.AreaUp,       Value = 0.12f },
		new() { Name = "Swift Boots",          Description = "+15% movement speed",                    Type = UpgradeType.SpeedUp,      Value = 0.15f },
		new() { Name = "Ancient Tome",         Description = "+18% max HP",                            Type = UpgradeType.MaxHPUp,      Value = 0.18f },
		new() { Name = "Clockwork Gear",       Description = "-7% weapon cooldown",                   Type = UpgradeType.CooldownDown, Value = 0.07f },
		new() { Name = "Lodestone Ring",       Description = "+22% XP gem pickup radius",              Type = UpgradeType.MagnetUp,     Value = 0.22f },
		new() { Name = "Resurrection Charm",   Description = "Survive one lethal hit",                Type = UpgradeType.Revival,      Value = 0f    },
		new() { Name = "Fortune Crystal",      Description = "+1 Luck",                                Type = UpgradeType.LuckUp,       Value = 1f    },
		new() { Name = "Quick Heal Potion",    Description = "Restore 30% of max HP now",             Type = UpgradeType.HPUp,         Value = 0.30f },
		new() { Name = "Warlord's Banner",     Description = "+15% weapon damage",                     Type = UpgradeType.DamageUp,     Value = 0.15f },
		new() { Name = "Vampiric Charm",        Description = "+6% lifesteal (heal % of damage dealt)", Type = UpgradeType.LifestealUp, Value = 0.06f },

		// Unlockable items — only appear in chests once purchased in the meta shop
		new() { Name = "Revival Stone",    Description = "Survive one lethal hit with 30% HP",           Type = UpgradeType.Revival,      Value = 0f,    UnlockId = "item_revival"       },
		new() { Name = "Grand Luck Charm", Description = "+3 Luck — improves upgrade rarity",             Type = UpgradeType.LuckUp,       Value = 3f,    UnlockId = "item_luckcharm"     },
		new() { Name = "Iron Rations",     Description = "+25% maximum HP",                               Type = UpgradeType.MaxHPUp,      Value = 0.25f, UnlockId = "item_ironrations"   },
		new() { Name = "Warding Pendant",  Description = "Brief invincibility after taking damage",        Type = UpgradeType.PlayerEffect, ItemId = "item_pendant",      UnlockId = "item_pendant"       },
		new() { Name = "Cursed Amulet",    Description = "+10% damage per 10 deaths (max +30%)",           Type = UpgradeType.PlayerEffect, ItemId = "item_cursedamulet", UnlockId = "item_cursedamulet"  },
		new() { Name = "Merchant's Badge", Description = "Your next chest costs nothing",                  Type = UpgradeType.PlayerEffect, ItemId = "item_merchantbadge",UnlockId = "item_merchantbadge" },
	};

	// ── Legacy: kept for any code still referencing UpgradeDefinition.All ─────
	public static IReadOnlyList<UpgradeDefinition> All => TomePool;
}
