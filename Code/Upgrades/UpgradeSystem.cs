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
		EnsureInitialized();
		if ( _rand == null ) return;

		CurrentSource      = source;
		IsShowingUpgrades  = true;
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

		// Add tomes that are unlocked and not yet maxed, stamping the level and stat preview
		foreach ( var tome in UpgradeDefinition.TomePool )
		{
			if ( tome.UnlockId != null && !PlayerProgress.IsUnlocked( tome.UnlockId ) ) continue;
			int currentLevel = _tomes?.GetLevel( tome.Name ) ?? 0;
			if ( currentLevel >= tome.MaxLevel ) continue;

			pool.Add( new UpgradeDefinition
			{
				Name        = tome.Name,
				Description = tome.Description,
				Type        = tome.Type,
				Value       = tome.Value,
				MaxLevel    = tome.MaxLevel,
				NextLevel   = currentLevel + 1,
				StatPreview = BuildTomeStatPreview( tome, _state ),
			} );
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

	/// <summary>Shrine pool: one per rarity tier, drawn from ShrinePool.</summary>
	private IReadOnlyList<UpgradeDefinition> BuildShrineChoices()
	{
		var commons   = UpgradeDefinition.ShrinePool.Where( x => x.Rarity == UpgradeDefinition.UpgradeRarity.Common ).ToList();
		var uncommons = UpgradeDefinition.ShrinePool.Where( x => x.Rarity == UpgradeDefinition.UpgradeRarity.Uncommon ).ToList();
		var rares     = UpgradeDefinition.ShrinePool.Where( x => x.Rarity == UpgradeDefinition.UpgradeRarity.Rare ).ToList();

		// Pick one of each rarity for a balanced spread; fallback to full pool if a tier is exhausted
		var choices = new List<UpgradeDefinition>
		{
			commons.Count   > 0 ? commons[_rand.Next( commons.Count )]     : null,
			uncommons.Count > 0 ? uncommons[_rand.Next( uncommons.Count )] : null,
			rares.Count     > 0 ? rares[_rand.Next( rares.Count )]         : null,
		};

		return choices.Where( c => c != null ).ToList().AsReadOnly();
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

	/// <summary>Builds a "current → new" stat preview string for a tome choice.</summary>
	private static string BuildTomeStatPreview( UpgradeDefinition tome, PlayerLocalState s )
	{
		if ( s == null ) return null;

		switch ( tome.Type )
		{
			case UpgradeDefinition.UpgradeType.SpeedUp:
				return $"Speed: {s.Speed:F0} → {s.Speed * (1f + tome.Value):F0}";
			case UpgradeDefinition.UpgradeType.DamageUp:
				return $"Damage: {s.Damage:F1} → {s.Damage * (1f + tome.Value):F1}";
			case UpgradeDefinition.UpgradeType.MaxHPUp:
				return $"Max HP: {s.MaxHP:F0} → {s.MaxHP * (1f + tome.Value):F0}";
			case UpgradeDefinition.UpgradeType.AreaUp:
				return $"Area: {s.Area:F2}× → {s.Area * (1f + tome.Value):F2}×";
			case UpgradeDefinition.UpgradeType.CooldownDown:
				return $"Cooldown: {s.CooldownMultiplier:F2}× → {s.CooldownMultiplier * (1f - tome.Value):F2}×";
			case UpgradeDefinition.UpgradeType.MagnetUp:
				return $"Magnet: {s.MagnetRadius:F0} → {s.MagnetRadius * (1f + tome.Value):F0}";
			case UpgradeDefinition.UpgradeType.LuckUp:
				return $"Luck: {(int)s.Luck} → {(int)(s.Luck + tome.Value)}";
			case UpgradeDefinition.UpgradeType.ArmorUp:
				return $"Armor: {s.Armor:F0} → {s.Armor + tome.Value:F0}";
			case UpgradeDefinition.UpgradeType.ShieldUp:
				return $"Max Shield: {s.MaxShield:F0} → {s.MaxShield + tome.Value:F0}";
			case UpgradeDefinition.UpgradeType.RegenUp:
				return $"HP Regen: {s.RegenPerMinute:F0}/min → {s.RegenPerMinute + tome.Value:F0}/min";
			case UpgradeDefinition.UpgradeType.ProjectileSpeedUp:
				return $"Proj Speed: {s.ProjectileSpeedMultiplier:F2}× → {s.ProjectileSpeedMultiplier * (1f + tome.Value):F2}×";
			case UpgradeDefinition.UpgradeType.CritChanceUp:
				return $"Crit: {s.CritChance * 100f:F0}% → {(s.CritChance + tome.Value) * 100f:F0}%";
			case UpgradeDefinition.UpgradeType.KnockbackUp:
				return $"Knockback: {s.Knockback:F2}× → {s.Knockback + tome.Value:F2}×";
			case UpgradeDefinition.UpgradeType.GoldMultiplierUp:
				return $"Gold: {s.GoldMultiplier:F2}× → {s.GoldMultiplier * (1f + tome.Value):F2}×";
			case UpgradeDefinition.UpgradeType.ProjectileCountUp:
				return $"Projectiles: +{s.ProjectileCount} → +{s.ProjectileCount + (int)tome.Value}";
			case UpgradeDefinition.UpgradeType.XPMultiplierUp:
				return $"XP: {s.XPMultiplier:F2}× → {s.XPMultiplier * (1f + tome.Value):F2}×";
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
