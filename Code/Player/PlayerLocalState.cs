/// <summary>
/// Local-only gameplay state: HP, speed, damage, etc.
/// Never synced to the network — only the owning client uses this.
/// </summary>
public sealed class PlayerLocalState : Component
{
	public static PlayerLocalState LocalInstance { get; private set; }
	protected override void OnStart() { LocalInstance = this; }
	protected override void OnDestroy() { if ( LocalInstance == this ) LocalInstance = null; }

	public float HP { get; set; } = 100f;
	public float MaxHP { get; set; } = 100f;
	public float Speed { get; set; } = 80f;
	public float Damage { get; set; } = 10f;
	public float Area { get; set; } = 1f;
	public float CooldownMultiplier { get; set; } = 1f;
	public float MagnetRadius { get; set; } = 20f;
	public float Luck { get; set; } = 0f;
	public float Armor { get; set; } = 0f;
	public bool HasRevival { get; set; } = false;

	// ── New tome stats ─────────────────────────────────────────────────────
	/// <summary>Current shield HP. Absorbs damage before HP drains.</summary>
	public float Shield { get; set; } = 0f;
	/// <summary>Maximum shield HP granted by Shield Tomes.</summary>
	public float MaxShield { get; set; } = 0f;
	/// <summary>HP healed per minute passively (from Regen Tome).</summary>
	public float RegenPerMinute { get; set; } = 0f;
	/// <summary>Multiplier applied to all projectile launch speeds.</summary>
	public float ProjectileSpeedMultiplier { get; set; } = 1f;
	/// <summary>Critical hit chance (0 = 0%, 1 = 100%). Values above 1 cause Overcrit.</summary>
	public float CritChance { get; set; } = 0.05f;
	/// <summary>Base crit damage multiplier (default 1.5×, i.e. +50% damage on crit).</summary>
	public float CritMultiplier { get; set; } = 1.5f;
	/// <summary>Enemy knockback distance multiplier.</summary>
	public float Knockback { get; set; } = 1f;
	/// <summary>Multiplier on all gold gained.</summary>
	public float GoldMultiplier { get; set; } = 1f;
	/// <summary>Bonus projectiles added to multi-projectile attacks.</summary>
	public int ProjectileCount { get; set; } = 0;
	/// <summary>Multiplier on all XP gained.</summary>
	public float XPMultiplier { get; set; } = 1f;

	// ── Megabonk-style shrine stats ───────────────────────────────────────────
	/// <summary>Chance to evade incoming damage (0 = 0%, 1 = 100%).</summary>
	public float Evasion { get; set; } = 0f;
	/// <summary>Fraction of damage dealt that heals the player (0 = 0%, 0.1 = 10% lifesteal).</summary>
	public float Lifesteal { get; set; } = 0f;
	/// <summary>Multiplier on ability/effect duration (e.g. BurnZone, buffs).</summary>
	public float DurationMultiplier { get; set; } = 1f;
	/// <summary>Base dash cooldown in seconds. Reduced by DashCooldownUp upgrades.</summary>
	public float DashCooldownBase { get; set; } = 1.5f;
	/// <summary>Multiplier on dash cooldown (1 = base, 0.8 = 20% faster dash).</summary>
	public float DashCooldownMultiplier { get; set; } = 1f;
	/// <summary>Multiplier on silver/premium currency (for future use).</summary>
	public float SilverMultiplier { get; set; } = 1f;

	private bool _revivalUsed = false;

	// Warding Pendant — brief invincibility after taking a hit
	private bool _hasPendant = false;
	private float _iFrameTimer = 0f;
	private const float PendantIFrameDuration = 1.5f;

	// Shield regen — starts regenerating after 3s of no damage
	private float _shieldRegenTimer = 0f;
	private const float ShieldRegenDelay = 3f;
	private const float ShieldRegenRate = 0.15f; // fraction of MaxShield per second

	// No-damage streak tracking for quest progress
	private float _noDamageTimer = 0f;
	/// <summary>Longest consecutive seconds without taking damage this run (for NoDamageSeconds quests).</summary>
	public int LongestNoDamageSeconds { get; private set; } = 0;

	/// <summary>Counts down from 0.25s after taking HP damage. Used by PlayerController and GameHUD for hit flash.</summary>
	public float HitFlashTimer { get; private set; } = 0f;

	// Merchant's Badge — makes the first chest per run free
	private bool _hasMerchantBadge = false;
	private bool _firstChestFreed = false;
	/// <summary>Read-only check: true when the next chest would be free (Merchant's Badge, first chest). Use for UI display.</summary>
	public bool IsNextChestFree => _hasMerchantBadge && !_firstChestFreed;
	/// <summary>True when the next chest should cost 0 coins (Merchant's Badge first-chest effect). Consumes the free chest when returning true.</summary>
	public bool ShouldNextChestBeFree()
	{
		if ( _hasMerchantBadge && !_firstChestFreed )
		{
			_firstChestFreed = true;
			return true;
		}
		return false;
	}

	public float HPPercent => MaxHP > 0f ? HP / MaxHP : 0f;
	public bool IsDead => HP <= 0f;

	public void Initialize( CharacterDefinition def )
	{
		MaxHP = def.BaseHP;
		HP = def.BaseHP;
		Speed = def.BaseSpeed;
		Damage = def.BaseDamage;
		Area = def.BaseArea;
		CooldownMultiplier = 1f;
		MagnetRadius = 16f;
		Luck = 0f;
		Armor = 0f;
		HasRevival = false;
		_revivalUsed = false;
		_hasPendant = false;
		_iFrameTimer = 0f;
		_noDamageTimer = 0f;
		LongestNoDamageSeconds = 0;
		_firstChestFreed = false;
		_hasMerchantBadge = false;
		Shield = 0f;
		MaxShield = 0f;
		RegenPerMinute = 0f;
		ProjectileSpeedMultiplier = 1f;
		CritChance = 0.05f;
		CritMultiplier = 1.5f;
		Knockback = 1f;
		GoldMultiplier = 1f;
		ProjectileCount = 0;
		XPMultiplier = 1f;
		Evasion = 0f;
		Lifesteal = 0f;
		DurationMultiplier = 1f;
		DashCooldownMultiplier = 1f;
		SilverMultiplier = 1f;
		_shieldRegenTimer = 0f;
	}

	protected override void OnUpdate()
	{
		if ( _iFrameTimer > 0f )
			_iFrameTimer -= Time.Delta;

		if ( HitFlashTimer > 0f )
			HitFlashTimer -= Time.Delta;

		if ( !IsDead )
		{
			_noDamageTimer += Time.Delta;
			int seconds = (int)_noDamageTimer;
			if ( seconds > LongestNoDamageSeconds )
				LongestNoDamageSeconds = seconds;

			// Passive HP regen
			if ( RegenPerMinute > 0f )
				Heal( RegenPerMinute / 60f * Time.Delta );

			// Shield regen after ShieldRegenDelay seconds of no damage
			if ( MaxShield > 0f && Shield < MaxShield )
			{
				_shieldRegenTimer -= Time.Delta;
				if ( _shieldRegenTimer <= 0f )
					Shield = Math.Min( Shield + MaxShield * ShieldRegenRate * Time.Delta, MaxShield );
			}
		}
	}

	/// <summary>Returns true if the player should die (health hit 0 with no revival).</summary>
	public bool TakeDamage( float amount )
	{
		if ( ChallengeRuntime.HasModifier( ChallengeModifierType.OneHitDeath ) )
			amount = Math.Max( amount, MaxHP + MaxShield + 1f );

		// Warding Pendant invincibility frames
		if ( _iFrameTimer > 0f )
			return false;

		// Evasion: chance to completely avoid damage
		if ( Evasion > 0f && System.Random.Shared.NextSingle() < Evasion )
			return false;

		var actualDamage = Math.Max( 1f, amount - Armor );

		// Reset no-damage streak and shield regen delay on any actual hit
		_noDamageTimer = 0f;
		_shieldRegenTimer = ShieldRegenDelay;

		// Shield absorbs damage first
		if ( Shield > 0f )
		{
			if ( Shield >= actualDamage )
			{
				Shield -= actualDamage;
				// Start iframes if pendant is equipped even when only shield is hit
				if ( _hasPendant )
					_iFrameTimer = PendantIFrameDuration;
				return false;
			}

			actualDamage -= Shield;
			Shield = 0f;
		}

		HP -= actualDamage;

		HitFlashTimer = 0.25f;
		DamageIndicatorWorld.SpawnPlayerDamage( this.GameObject, new Vector3( 0f, 0f, 14f ), actualDamage );

		// Start iframes after taking damage if pendant is equipped
		if ( _hasPendant )
			_iFrameTimer = PendantIFrameDuration;

		if ( HP <= 0f )
		{
			if ( HasRevival && !_revivalUsed )
			{
				_revivalUsed = true;
				HP = MaxHP * 0.3f;
				return false;
			}

			HP = 0f;
			return true;
		}

		return false;
	}

	public void Heal( float amount )
	{
		HP = Math.Min( HP + amount, MaxHP );
	}

	/// <summary>
	/// Applies the effect of a single item mid-run by its unlock ID (e.g. from a chest reward).
	/// </summary>
	public void ApplyItemById( string id )
	{
		switch ( id )
		{
			case "item_pendant":
				_hasPendant = true;
				break;
			case "item_cursedamulet":
				ApplyCursedAmuletBonus();
				break;
			case "item_merchantbadge":
				_hasMerchantBadge = true;
				break;
		}
	}

	/// <summary>Legacy overload — delegates to ApplyItemById.</summary>
	public void ApplyItem( UnlockDefinition def ) => ApplyItemById( def.Id );

	private void ApplyCursedAmuletBonus()
	{
		// +10% damage per 10 deaths, capped at +30%
		int deathTiers = System.Math.Min( 3, PlayerProgress.Data.TotalDeaths / 10 );
		if ( deathTiers > 0 )
			Damage *= 1f + deathTiers * 0.10f;
	}

}
