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
	/// <summary>Number of level-ups queued while a panel was already open.</summary>
	private int              _pendingLevelUps = 0;

	private HashSet<string> _banishedNames = new();
	public int BanishesRemaining { get; private set; } = 1;
	public int SkipsRemaining { get; private set; } = 1;
	public int RefreshesRemaining { get; private set; } = 2;
	public const int RefreshCost = 25;
	public bool IsBanishMode { get; private set; } = false;

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

	/// <summary>Called by PlayerXP on level-up. Shows Tome + Weapon choices, or queues if a panel is already open.</summary>
	public void ShowUpgrades()
	{
		if ( IsShowingUpgrades )
		{
			_pendingLevelUps++;
			return;
		}
		Open( RewardSource.LevelUp );
	}

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

		if ( IsBanishMode )
		{
			BanishUpgrade( index );
			return;
		}

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

	/// <summary>Enter banish mode — next card click will banish that upgrade.</summary>
	public void EnterBanishMode()
	{
		if ( BanishesRemaining <= 0 || CurrentSource != RewardSource.LevelUp ) return;
		IsBanishMode = true;
	}

	/// <summary>Cancel banish mode without banishing anything.</summary>
	public void CancelBanishMode()
	{
		IsBanishMode = false;
	}

	/// <summary>Banish the upgrade at `index` — permanently remove it from future level-up pools this run, then re-roll choices.</summary>
	public void BanishUpgrade( int index )
	{
		if ( CurrentChoices == null || index < 0 || index >= CurrentChoices.Count ) return;
		EnsureInitialized();

		var chosen = CurrentChoices[index];
		_banishedNames.Add( chosen.Name );
		BanishesRemaining--;
		IsBanishMode = false;

		CurrentChoices = BuildLevelUpChoices();
	}

	/// <summary>Skip this level-up entirely — forfeit the upgrade pick.</summary>
	public void SkipLevelUp()
	{
		if ( SkipsRemaining <= 0 || CurrentSource != RewardSource.LevelUp ) return;
		SkipsRemaining--;
		ClosePanel();
	}

	/// <summary>Re-roll all 3 level-up choices for a coin cost.</summary>
	public void RefreshChoices()
	{
		if ( RefreshesRemaining <= 0 || CurrentSource != RewardSource.LevelUp ) return;
		var coins = PlayerCoins.LocalInstance;
		if ( coins == null || !coins.CanAfford( RefreshCost ) ) return;
		coins.SpendCoins( RefreshCost );
		RefreshesRemaining--;

		CurrentChoices = BuildLevelUpChoices();
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
		IsBanishMode      = false;

		// If level-ups were queued while the panel was open, show the next one immediately
		// without unpausing — weapons and spawner stay frozen until all selections are done.
		if ( _pendingLevelUps > 0 )
		{
			_pendingLevelUps--;
			Open( RewardSource.LevelUp );
			return;
		}

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
			if ( _banishedNames.Contains( tome.Name ) ) continue;
			// When slots full, only show tomes we already own (currentLevel > 0)
			if ( tomeSlotsFull && currentLevel == 0 ) continue;

			var (rolledValue, description, rarity) = RollTomeValue( tome, luck );
			var choice = new UpgradeDefinition
			{
				Name        = tome.Name,
				Description = description,
				Type        = tome.Type,
				Value       = rolledValue,
				MaxLevel    = tome.MaxLevel,
				NextLevel   = currentLevel + 1,
				StatPreview = BuildTomeStatPreview( tome.Type, rolledValue, _state ),
				Rarity      = rarity,
			};
			pool.Add( choice );
		}

		// Add weapon options: level-up if owned (and not maxed), unlock if new and slots remain
		bool weaponSlotsFull = _weapons?.IsFull ?? false;
		foreach ( var name in UpgradeDefinition.AllWeaponDisplayNames )
		{
			if ( UpgradeDefinition.WeaponUnlockIds.TryGetValue( name, out var weaponUnlockId )
				&& !PlayerProgress.IsUnlocked( weaponUnlockId ) )
				continue;
			if ( _banishedNames.Contains( name ) ) continue;
			if ( _weapons != null && _weapons.HasWeapon( name ) )
			{
				var existing = _weapons.Weapons.FirstOrDefault( w => w.WeaponDisplayName == name );
				int curLevel = existing?.WeaponLevel ?? 1;
				if ( curLevel >= WeaponBase.MaxWeaponLevel ) continue;

				var wRarity = RollLevelUpRarity( luck );
				float bonus = wRarity switch
				{
					UpgradeDefinition.UpgradeRarity.Uncommon  => 0.05f,
					UpgradeDefinition.UpgradeRarity.Rare      => 0.12f,
					UpgradeDefinition.UpgradeRarity.Legendary => 0.22f,
					_                                         => 0f,
				};
				string baseDesc = existing?.GetUpgradeDescription( curLevel + 1 ) ?? $"LVL {curLevel} → LVL {curLevel + 1}";
				string fullDesc = bonus > 0 ? $"{baseDesc} / +{(int)(bonus * 100)}% Dmg" : baseDesc;

				pool.Add( new UpgradeDefinition
				{
					Name        = name,
					Description = fullDesc,
					Type        = UpgradeDefinition.UpgradeType.WeaponLevelUp,
					WeaponName  = name,
					IsNewWeapon = false,
					NextLevel   = curLevel + 1,
					Rarity      = wRarity,
					Value       = bonus,
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

			if ( pool.Count == 0 )
			{
				// Fallback to any remaining shrine option to preserve 3 unique offers.
				pool = commons.Count > 0 ? commons : uncommons.Count > 0 ? uncommons : rares;
			}

			if ( pool.Count > 0 )
			{
				int idx = _rand.Next( pool.Count );
				var picked = pool[idx];
				choices.Add( picked );
				pool.RemoveAt( idx ); // no duplicates in one shrine reward
			}
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

	/// <summary>Rolls a random tome value with variance; Luck shifts toward better rarity.</summary>
	private (float value, string description, UpgradeDefinition.UpgradeRarity rarity) RollTomeValue(
		UpgradeDefinition tome, float luck )
	{
		float baseVal = tome.Value;
		if ( tome.Type == UpgradeDefinition.UpgradeType.ProjectileCountUp )
			return (baseVal, $"+{(int)baseVal} projectile to all attacks", UpgradeDefinition.UpgradeRarity.Common);

		var rarity  = RollLevelUpRarity( luck );
		float value = baseVal * RarityValueMultiplier( rarity );
		return (value, BuildTomeDescription( tome.Type, value ), rarity);
	}

	/// <summary>Rolls a level-up rarity tier. Luck shifts weight toward better tiers.</summary>
	private UpgradeDefinition.UpgradeRarity RollLevelUpRarity( float luck )
	{
		float legendary = Math.Min( 0.20f, 0.03f + luck * 0.015f );
		float rare      = Math.Min( 0.35f, 0.10f + luck * 0.016f );
		float uncommon  = Math.Min( 0.40f, 0.27f + luck * 0.006f );
		float common    = Math.Max( 0.05f, 1f - legendary - rare - uncommon );

		float r = (float)_rand.NextDouble();
		if ( r < legendary )                    return UpgradeDefinition.UpgradeRarity.Legendary;
		if ( r < legendary + rare )             return UpgradeDefinition.UpgradeRarity.Rare;
		if ( r < legendary + rare + uncommon )  return UpgradeDefinition.UpgradeRarity.Uncommon;
		return UpgradeDefinition.UpgradeRarity.Common;
	}

	/// <summary>Value multiplier applied to tome base stat per rarity tier.</summary>
	private static float RarityValueMultiplier( UpgradeDefinition.UpgradeRarity rarity ) => rarity switch
	{
		UpgradeDefinition.UpgradeRarity.Uncommon  => 1.25f,
		UpgradeDefinition.UpgradeRarity.Rare      => 1.55f,
		UpgradeDefinition.UpgradeRarity.Legendary => 2.00f,
		_                                         => 1.00f,
	};

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
