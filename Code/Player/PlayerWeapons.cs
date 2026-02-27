/// <summary>
/// Manages the local player's list of auto-attack weapons.
/// Only runs on the owning client.
/// </summary>
public sealed class PlayerWeapons : Component
{
	public static PlayerWeapons LocalInstance { get; private set; }

	public const int MaxWeapons = 4;

	private readonly List<WeaponBase> _weapons = new();

	public bool IsPaused { get; private set; } = false;
	public IReadOnlyList<WeaponBase> Weapons => _weapons.AsReadOnly();
	public bool IsFull => _weapons.Count >= MaxWeapons;

	protected override void OnStart()
	{
		LocalInstance = this;
		if ( IsProxy ) return;
	}

	protected override void OnDestroy()
	{
		if ( LocalInstance == this ) LocalInstance = null;
	}

	/// <summary>Returns true if the player already owns the weapon with this display name.</summary>
	public bool HasWeapon( string displayName )
		=> _weapons.Any( w => w.WeaponDisplayName == displayName );

	/// <summary>Increments the level of an already-owned weapon by display name.</summary>
	public void LevelUpWeapon( string displayName )
	{
		var weapon = _weapons.FirstOrDefault( w => w.WeaponDisplayName == displayName );
		if ( weapon != null )
			weapon.WeaponLevel++;
	}

	/// <summary>Adds a weapon by its display name (converts to internal name for AddWeaponByName).</summary>
	public void AddWeaponByDisplayName( string displayName )
	{
		string internalName = displayName switch
		{
			"Magic Wand"     => "MagicWand",
			"Bow"            => "Bow",
			"Axe"            => "Axe",
			"Aura"           => "Aura",
			"Storm Rod"      => "StormRod",
			"Orbital Shards" => "OrbitalShards",
			"Ember Trail"    => "EmberTrail",
			"Sword"          => "Sword",
			_                => displayName
		};
		AddWeaponByName( internalName );
	}

	public void AddWeaponByName( string weaponName )
	{
		if ( IsProxy ) return;

		WeaponBase weapon = weaponName switch
		{
			"MagicWand"     => Components.Create<MagicWand>(),
			"Bow"           => Components.Create<BowWeapon>(),
			"Axe"           => Components.Create<AxeWeapon>(),
			"Aura"          => Components.Create<AuraWeapon>(),
			"StormRod"      => Components.Create<StormRodWeapon>(),
			"OrbitalShards" => Components.Create<OrbitalShardsWeapon>(),
			"EmberTrail"    => Components.Create<EmberTrailWeapon>(),
			"Sword"         => Components.Create<SwordWeapon>(),
			_               => Components.Create<MagicWand>()
		};

		_weapons.Add( weapon );
	}

	public void SetPaused( bool paused )
	{
		IsPaused = paused;
		foreach ( var w in _weapons )
			w.Enabled = !paused;
	}
}
