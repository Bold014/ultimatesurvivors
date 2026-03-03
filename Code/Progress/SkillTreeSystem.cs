using System.Linq;

/// <summary>
/// Static class managing skill tree state: upgrading nodes, reading levels, applying bonuses.
/// All state is stored in PlayerProgress.Data.SkillLevels and SoulEssence.
/// </summary>
public static class SkillTreeSystem
{
	/// <summary>Returns the current level of a skill (0 if not yet upgraded).</summary>
	public static int GetLevel( string skillId )
	{
		return PlayerProgress.Data.SkillLevels.TryGetValue( skillId, out var lvl ) ? lvl : 0;
	}

	/// <summary>Returns the essence cost to upgrade this skill from its current level.</summary>
	public static int GetUpgradeCost( string skillId )
	{
		var def = SkillNodeDefinition.GetById( skillId );
		if ( def == null ) return int.MaxValue;
		return def.GetCost( GetLevel( skillId ) );
	}

	/// <summary>True when the skill can be upgraded (not maxed, enough essence, prereqs met).</summary>
	public static bool CanUpgrade( string skillId )
	{
		var def = SkillNodeDefinition.GetById( skillId );
		if ( def == null ) return false;

		int currentLevel = GetLevel( skillId );
		if ( currentLevel >= def.MaxLevel ) return false;

		int cost = def.GetCost( currentLevel );
		if ( PlayerProgress.Data.SoulEssence < cost ) return false;

		// Check prerequisites
		foreach ( var prereqId in def.Prerequisites )
		{
			if ( GetLevel( prereqId ) < 1 ) return false;
		}

		return true;
	}

	/// <summary>Spends essence and increments the skill level. Returns true on success.</summary>
	public static bool TryUpgrade( string skillId )
	{
		if ( !CanUpgrade( skillId ) ) return false;

		var def = SkillNodeDefinition.GetById( skillId );
		int currentLevel = GetLevel( skillId );
		int cost = def.GetCost( currentLevel );

		PlayerProgress.Data.SoulEssence -= cost;
		PlayerProgress.Data.TotalSoulEssenceSpent += cost;
		PlayerProgress.Data.SkillLevels[skillId] = currentLevel + 1;
		PlayerProgress.Save();
		return true;
	}

	/// <summary>Sum of all essence ever spent on skill upgrades (used for prestige threshold).</summary>
	public static int GetTotalSkillPointsSpent()
	{
		return PlayerProgress.Data.TotalSoulEssenceSpent;
	}

	/// <summary>
	/// Applies all skill tree bonuses to the player state at run start.
	/// Uses the prestige multiplier to scale all bonuses.
	/// Mirrors the stat application logic from UpgradeDefinition.Apply().
	/// </summary>
	public static void ApplySkillBonuses( PlayerLocalState state )
	{
		if ( state == null ) return;

		float prestigeMult = PrestigeSystem.GetBonusMultiplier();

		foreach ( var def in SkillNodeDefinition.All )
		{
			int level = GetLevel( def.Id );
			if ( level <= 0 ) continue;

			float totalValue = def.GetTotalValue( level ) * prestigeMult;

			switch ( def.StatType )
			{
				case UpgradeDefinition.UpgradeType.DamageUp:
					state.Damage *= 1f + totalValue;
					break;
				case UpgradeDefinition.UpgradeType.CritChanceUp:
					state.CritChance += totalValue;
					break;
				case UpgradeDefinition.UpgradeType.CritDamageUp:
					state.CritMultiplier += totalValue;
					break;
				case UpgradeDefinition.UpgradeType.CooldownDown:
					state.CooldownMultiplier *= 1f - totalValue;
					break;
				case UpgradeDefinition.UpgradeType.ProjectileSpeedUp:
					state.ProjectileSpeedMultiplier *= 1f + totalValue;
					break;
				case UpgradeDefinition.UpgradeType.AreaUp:
					state.Area *= 1f + totalValue;
					break;
				case UpgradeDefinition.UpgradeType.KnockbackUp:
					state.Knockback += totalValue;
					break;
				case UpgradeDefinition.UpgradeType.MaxHPUp:
					var hpBonus = state.MaxHP * totalValue;
					state.MaxHP += hpBonus;
					state.HP += hpBonus;
					break;
				case UpgradeDefinition.UpgradeType.ArmorUp:
					state.Armor += totalValue;
					break;
				case UpgradeDefinition.UpgradeType.ShieldUp:
					state.MaxShield += totalValue;
					state.Shield += totalValue;
					break;
				case UpgradeDefinition.UpgradeType.RegenUp:
					state.RegenPerMinute += totalValue;
					break;
				case UpgradeDefinition.UpgradeType.EvasionUp:
					state.Evasion += totalValue;
					break;
				case UpgradeDefinition.UpgradeType.LifestealUp:
					state.Lifesteal += totalValue;
					break;
				case UpgradeDefinition.UpgradeType.SpeedUp:
					state.Speed *= 1f + totalValue;
					break;
				case UpgradeDefinition.UpgradeType.MagnetUp:
					state.MagnetRadius *= 1f + totalValue;
					break;
				case UpgradeDefinition.UpgradeType.LuckUp:
					state.Luck += totalValue;
					break;
				case UpgradeDefinition.UpgradeType.DashCooldownUp:
					state.DashCooldownMultiplier *= 1f - totalValue;
					break;
				case UpgradeDefinition.UpgradeType.DurationMultiplierUp:
					state.DurationMultiplier *= 1f + totalValue;
					break;
				case UpgradeDefinition.UpgradeType.GoldMultiplierUp:
					state.GoldMultiplier *= 1f + totalValue;
					break;
				case UpgradeDefinition.UpgradeType.XPMultiplierUp:
					state.XPMultiplier *= 1f + totalValue;
					break;
			}
		}
	}
}
