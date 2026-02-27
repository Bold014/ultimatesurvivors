/// <summary>
/// Owner-only upgrade manager.
/// • Level-up (ShowUpgrades): 3 choices of TOMES + WEAPONS
/// • Shrine (TriggerShrineReward): 3 stat boosts with Common/Uncommon/Rare rarity + Ignore Offers
/// • Chest (TriggerChestReward): 3 in-run ITEMS
/// </summary>
public sealed class UpgradeSystem : Component
{
	public enum RewardSource { LevelUp, Shrine, Chest }

	public static UpgradeSystem LocalInstance { get; private set; }

	public bool IsShowingUpgrades { get; private set; } = false;
	public RewardSource CurrentSource { get; private set; } = RewardSource.LevelUp;
	/// <summary>True for Shrine rewards — shows an "Ignore Offers" button.</summary>
	public bool CanIgnore { get; private set; } = false;
	public IReadOnlyList<UpgradeDefinition> CurrentChoices { get; private set; }

	private PlayerLocalState _state;
	private PlayerWeapons    _weapons;
	private PlayerPassives   _passives;
	private PlayerTomes      _tomes;
	private EnemySpawner     _spawner;
	private Random           _rand;
	private bool             _initialized = false;

	protected override void OnStart()
	{
		LocalInstance = this;
		Log.Info( "[UpgradeSystem] OnStart — LocalInstance set." );
	}

	private void EnsureInitialized()
	{
		if ( _initialized ) return;
		_initialized = true;
		_state    = Components.Get<PlayerLocalState>();
		_weapons  = Components.Get<PlayerWeapons>();
		_passives = Components.Get<PlayerPassives>();
		_tomes    = Components.Get<PlayerTomes>();
		_spawner  = Components.Get<EnemySpawner>();
		int seed  = Connection.Local != null ? Connection.Local.SteamId.GetHashCode() ^ 0x12345 : 12345;
		_rand     = new Random( seed );
	}

	// ── Public entry points ───────────────────────────────────────────────────

	/// <summary>Called by PlayerXP on level-up. Shows Tome + Weapon choices.</summary>
	public void ShowUpgrades() => Open( RewardSource.LevelUp );

	/// <summary>Called by LevelUpBeacon (Charge Shrine) when fully charged.</summary>
	public void TriggerShrineReward() => Open( RewardSource.Shrine );

	/// <summary>Called by Chest when the player pays and opens it.</summary>
	public void TriggerChestReward() => Open( RewardSource.Chest );

	/// <summary>Legacy alias kept for any callers using the old name.</summary>
	public void TriggerUpgradeSelection() => ShowUpgrades();

	// ── Player selects a card ─────────────────────────────────────────────────

	public void SelectUpgrade( int index )
	{
		if ( !IsShowingUpgrades || CurrentChoices == null ) return;
		if ( index < 0 || index >= CurrentChoices.Count ) return;

		EnsureInitialized();

		var chosen = CurrentChoices[index];
		bool isTome   = chosen.MaxLevel > 0;
		bool isWeapon = chosen.Type == UpgradeDefinition.UpgradeType.WeaponUnlock
		             || chosen.Type == UpgradeDefinition.UpgradeType.WeaponLevelUp;

		// Increment tome level BEFORE applying so GetLevel reflects the new state
		bool isFirstTomePick = isTome && (_tomes?.GetLevel( chosen.Name ) ?? 0) == 0;
		if ( isTome )
			_tomes?.LevelUp( chosen.Name );

		chosen.Apply( _state, _weapons );

		// Add to tome HUD slots: only tomes, and only on first pick
		if ( isTome && isFirstTomePick )
			_passives?.AddPassive( chosen.Name );

		ClosePanel();
	}

	/// <summary>Called by UpgradePanel "Ignore Offers" button (shrine only).</summary>
	public void SelectIgnore()
	{
		if ( !CanIgnore ) return;
		ClosePanel();
	}

	// ── Internal ──────────────────────────────────────────────────────────────

	private void Open( RewardSource source )
	{
		Log.Info( $"[UpgradeSystem] Open called — source={source}" );
		EnsureInitialized();
		if ( _rand == null )
		{
			Log.Error( "[UpgradeSystem] Open aborted — _rand is null after EnsureInitialized!" );
			return;
		}

		CurrentSource      = source;
		IsShowingUpgrades  = true;
		Log.Info( $"[UpgradeSystem] IsShowingUpgrades=true, building choices..." );
		CanIgnore          = source == RewardSource.Shrine;
		_weapons?.SetPaused( true );
		_spawner?.SetPaused( true );

		CurrentChoices = source switch
		{
			RewardSource.LevelUp => BuildLevelUpChoices(),
			RewardSource.Shrine  => BuildShrineChoices(),
			RewardSource.Chest   => BuildChestChoices(),
			_                    => BuildLevelUpChoices()
		};
		Log.Info( $"[UpgradeSystem] CurrentChoices.Count={CurrentChoices?.Count ?? -1}" );
	}

	private void ClosePanel()
	{
		IsShowingUpgrades = false;
		CurrentChoices    = null;
		CanIgnore         = false;
		_weapons?.SetPaused( false );
		_spawner?.SetPaused( false );
	}

	// ── Choice builders ───────────────────────────────────────────────────────

	/// <summary>Level-up pool: mix of Tomes and Weapon choices (unlock or level-up).</summary>
	private IReadOnlyList<UpgradeDefinition> BuildLevelUpChoices()
	{
		var pool = new List<UpgradeDefinition>();
		float luck = _state?.Luck ?? 0f;

		// Add tomes: when inventory is full (4/4), only offer level-ups for tomes already in inventory
		bool tomeSlotsFull = _passives?.IsFull ?? false;
		foreach ( var tome in UpgradeDefinition.TomePool )
		{
			if ( tome.UnlockId != null && !PlayerProgress.IsUnlocked( tome.UnlockId ) ) continue;
			int currentLevel = _tomes?.GetLevel( tome.Name ) ?? 0;
			if ( currentLevel >= tome.MaxLevel ) continue;
			// When slots full, only show tomes we already own (currentLevel > 0)
			if ( tomeSlotsFull && currentLevel == 0 ) continue;

			var (rolledValue, description) = RollTomeValue( tome, luck );
			var choice = new UpgradeDefinition
			{
				Name        = tome.Name,
				Description = description,
				Type        = tome.Type,
				Value       = rolledValue,
				MaxLevel    = tome.MaxLevel,
				NextLevel   = currentLevel + 1,
				StatPreview = BuildTomeStatPreview( tome.Type, rolledValue, _state ),
			};
			pool.Add( choice );
		}

		// Add weapon options: level-up if owned (and not maxed), unlock if new and slots remain
		bool weaponSlotsFull = _weapons?.IsFull ?? false;
		foreach ( var name in UpgradeDefinition.AllWeaponDisplayNames )
		{
			if ( _weapons != null && _weapons.HasWeapon( name ) )
			{
				var existing = _weapons.Weapons.FirstOrDefault( w => w.WeaponDisplayName == name );
				int curLevel = existing?.WeaponLevel ?? 1;
				if ( curLevel >= WeaponBase.MaxWeaponLevel ) continue;

				pool.Add( new UpgradeDefinition
				{
					Name        = name,
					Description = existing?.GetUpgradeDescription( curLevel + 1 ) ?? $"LVL {curLevel} → LVL {curLevel + 1}",
					Type        = UpgradeDefinition.UpgradeType.WeaponLevelUp,
					WeaponName  = name,
					IsNewWeapon = false,
					NextLevel   = curLevel + 1,
				} );
			}
			else if ( !weaponSlotsFull )
			{
				pool.Add( new UpgradeDefinition
				{
					Name        = name,
					Description = "Unlock this weapon",
					Type        = UpgradeDefinition.UpgradeType.WeaponUnlock,
					WeaponName  = name,
					IsNewWeapon = true,
				} );
			}
		}

		return Pick( pool, 3 );
	}

	/// <summary>Shrine pool: 3 choices. Higher Luck = better bonuses (more Uncommon/Rare offers).</summary>
	private IReadOnlyList<UpgradeDefinition> BuildShrineChoices()
	{
		var commons   = UpgradeDefinition.ShrinePool.Where( x => x.Rarity == UpgradeDefinition.UpgradeRarity.Common ).ToList();
		var uncommons = UpgradeDefinition.ShrinePool.Where( x => x.Rarity == UpgradeDefinition.UpgradeRarity.Uncommon ).ToList();
		var rares     = UpgradeDefinition.ShrinePool.Where( x => x.Rarity == UpgradeDefinition.UpgradeRarity.Rare ).ToList();

		// Luck scales rarity: base 50% Common, 35% Uncommon, 15% Rare. +4% Rare per Luck (max 50% Rare).
		float luck = _state?.Luck ?? 0f;
		float rareChance   = Math.Min( 0.50f, 0.15f + luck * 0.04f );
		float uncomChance  = 0.35f;
		float commonChance = 1f - rareChance - uncomChance;

		var choices = new List<UpgradeDefinition>();
		for ( int i = 0; i < 3; i++ )
		{
			float roll = (float)_rand.NextDouble();
			UpgradeDefinition.UpgradeRarity tier;
			if ( roll < commonChance )
				tier = UpgradeDefinition.UpgradeRarity.Common;
			else if ( roll < commonChance + uncomChance )
				tier = UpgradeDefinition.UpgradeRarity.Uncommon;
			else
				tier = UpgradeDefinition.UpgradeRarity.Rare;

			var pool = tier == UpgradeDefinition.UpgradeRarity.Common ? commons
				: tier == UpgradeDefinition.UpgradeRarity.Uncommon ? uncommons : rares;
			if ( pool.Count > 0 )
				choices.Add( pool[_rand.Next( pool.Count )] );
		}

		return choices.AsReadOnly();
	}

	/// <summary>Chest pool: 3 random items, rerolled with Luck. Unlockable items only appear when owned.</summary>
	private IReadOnlyList<UpgradeDefinition> BuildChestChoices()
	{
		var pool = UpgradeDefinition.ItemPool
			.Where( item => item.UnlockId == null || PlayerProgress.IsUnlocked( item.UnlockId ) )
			.ToList();

		int extraShuffles = (int)(_state?.Luck ?? 0);
		for ( int i = 0; i < extraShuffles; i++ )
		{
			int a = _rand.Next( pool.Count );
			int b = _rand.Next( pool.Count );
			(pool[a], pool[b]) = (pool[b], pool[a]);
		}

		return Pick( pool, 3 );
	}

	/// <summary>Rolls a random tome value with variance; Luck biases toward higher values.</summary>
	private (float value, string description) RollTomeValue( UpgradeDefinition tome, float luck )
	{
		float baseVal = tome.Value;
		// ProjectileCount (Quantity Tome) is always +1 — no variance
		if ( tome.Type == UpgradeDefinition.UpgradeType.ProjectileCountUp )
			return (baseVal, $"+{(int)baseVal} projectile to all attacks");

		// Variance: ±25% of base. Luck adds up to +20% toward max per 10 Luck (e.g. 10 Luck ≈ +10% avg).
		float variance = 0.25f;
		float minVal = baseVal * (1f - variance);
		float maxVal = baseVal * (1f + variance);
		float roll   = (float)_rand.NextDouble();
		float luckBonus = Math.Min( 0.25f, luck * 0.02f ); // +2% toward max per Luck, cap +25%
		float effectiveRoll = Math.Min( 1f, roll + luckBonus );
		float value = minVal + (maxVal - minVal) * effectiveRoll;

		string desc = BuildTomeDescription( tome.Type, value );
		return (value, desc);
	}

	/// <summary>Builds a human-readable description for a rolled tome value.</summary>
	private static string BuildTomeDescription( UpgradeDefinition.UpgradeType type, float value )
	{
		return type switch
		{
			UpgradeDefinition.UpgradeType.SpeedUp           => $"+{value * 100f:F0}% movement speed",
			UpgradeDefinition.UpgradeType.AreaUp            => $"+{value * 100f:F0}% attack size",
			UpgradeDefinition.UpgradeType.ShieldUp          => $"+{value:F0} shield capacity",
			UpgradeDefinition.UpgradeType.RegenUp           => $"+{value:F0} HP regen per minute",
			UpgradeDefinition.UpgradeType.LifestealUp        => $"+{value * 100f:F0}% lifesteal (heal % of damage dealt)",
			UpgradeDefinition.UpgradeType.ProjectileSpeedUp => $"+{value * 100f:F0}% projectile speed",
			UpgradeDefinition.UpgradeType.MaxHPUp           => $"+{value * 100f:F0}% max HP",
			UpgradeDefinition.UpgradeType.GoldMultiplierUp  => $"+{value * 100f:F0}% gold from all sources",
			UpgradeDefinition.UpgradeType.DamageUp          => $"+{value * 100f:F0}% damage",
			UpgradeDefinition.UpgradeType.CooldownDown      => $"+{value * 100f:F0}% attack speed",
			UpgradeDefinition.UpgradeType.CritChanceUp      => $"+{value * 100f:F0}% critical hit chance",
			UpgradeDefinition.UpgradeType.KnockbackUp       => $"+{value * 100f:F0}% enemy knockback distance",
			UpgradeDefinition.UpgradeType.XPMultiplierUp   => $"+{value * 100f:F0}% XP from all sources",
			UpgradeDefinition.UpgradeType.DurationMultiplierUp => $"+{value * 100f:F0}% attack and projectile duration",
			_ => $"+{value:F1}"
		};
	}

	/// <summary>Builds a "current → new" stat preview string for a tome choice.</summary>
	private static string BuildTomeStatPreview( UpgradeDefinition.UpgradeType type, float value, PlayerLocalState s )
	{
		if ( s == null ) return null;

		switch ( type )
		{
			case UpgradeDefinition.UpgradeType.SpeedUp:
				return $"Speed: {s.Speed:F0} → {s.Speed * (1f + value):F0}";
			case UpgradeDefinition.UpgradeType.DamageUp:
				return $"Damage: {s.Damage:F1} → {s.Damage * (1f + value):F1}";
			case UpgradeDefinition.UpgradeType.MaxHPUp:
				return $"Max HP: {s.MaxHP:F0} → {s.MaxHP * (1f + value):F0}";
			case UpgradeDefinition.UpgradeType.AreaUp:
				return $"Area: {s.Area:F2}× → {s.Area * (1f + value):F2}×";
			case UpgradeDefinition.UpgradeType.CooldownDown:
				return $"Cooldown: {s.CooldownMultiplier:F2}× → {s.CooldownMultiplier * (1f - value):F2}×";
			case UpgradeDefinition.UpgradeType.MagnetUp:
				return $"Magnet: {s.MagnetRadius:F0} → {s.MagnetRadius * (1f + value):F0}";
			case UpgradeDefinition.UpgradeType.LuckUp:
				return $"Luck: {(int)s.Luck} → {(int)(s.Luck + value)}";
			case UpgradeDefinition.UpgradeType.ArmorUp:
				return $"Armor: {s.Armor:F0} → {s.Armor + value:F0}";
			case UpgradeDefinition.UpgradeType.ShieldUp:
				return $"Max Shield: {s.MaxShield:F0} → {s.MaxShield + value:F0}";
			case UpgradeDefinition.UpgradeType.RegenUp:
				return $"HP Regen: {s.RegenPerMinute:F0}/min → {s.RegenPerMinute + value:F0}/min";
			case UpgradeDefinition.UpgradeType.LifestealUp:
				return $"Lifesteal: {s.Lifesteal * 100f:F0}% → {(s.Lifesteal + value) * 100f:F0}%";
			case UpgradeDefinition.UpgradeType.ProjectileSpeedUp:
				return $"Proj Speed: {s.ProjectileSpeedMultiplier:F2}× → {s.ProjectileSpeedMultiplier * (1f + value):F2}×";
			case UpgradeDefinition.UpgradeType.CritChanceUp:
				return $"Crit: {s.CritChance * 100f:F0}% → {(s.CritChance + value) * 100f:F0}%";
			case UpgradeDefinition.UpgradeType.KnockbackUp:
				return $"Knockback: {s.Knockback:F2}× → {s.Knockback + value:F2}×";
			case UpgradeDefinition.UpgradeType.GoldMultiplierUp:
				return $"Gold: {s.GoldMultiplier:F2}× → {s.GoldMultiplier * (1f + value):F2}×";
			case UpgradeDefinition.UpgradeType.ProjectileCountUp:
				return $"Projectiles: +{s.ProjectileCount} → +{s.ProjectileCount + (int)value}";
			case UpgradeDefinition.UpgradeType.XPMultiplierUp:
				return $"XP: {s.XPMultiplier:F2}× → {s.XPMultiplier * (1f + value):F2}×";
			case UpgradeDefinition.UpgradeType.DurationMultiplierUp:
				return $"Duration: {s.DurationMultiplier:F2}× → {s.DurationMultiplier * (1f + value):F2}×";
			default:
				return null;
		}
	}

	/// <summary>Picks `count` unique random entries from `pool`.</summary>
	private List<UpgradeDefinition> Pick( List<UpgradeDefinition> pool, int count )
	{
		var result = new List<UpgradeDefinition>();
		int needed = Math.Min( count, pool.Count );
		for ( int i = 0; i < needed; i++ )
		{
			int idx = _rand.Next( pool.Count );
			result.Add( pool[idx] );
			pool.RemoveAt( idx );
		}
		return result;
	}

	protected override void OnDestroy()
	{
		if ( LocalInstance == this )
			LocalInstance = null;
	}
}
