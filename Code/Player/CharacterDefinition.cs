/// <summary>
/// Immutable data describing a playable character's base stats and starting weapon.
/// </summary>
public class CharacterDefinition
{
	public string Name { get; init; }
	public string Description { get; init; }
	public float BaseHP { get; init; }
	public float BaseSpeed { get; init; }
	public float BaseDamage { get; init; }
	public float BaseArea { get; init; }
	public string StartingWeapon { get; init; }

	public static CharacterDefinition Archer { get; } = new()
	{
		Name = "Archer",
		Description = "Fast ranger with piercing projectiles. +20% projectile speed.",
		BaseHP = 100f,
		BaseSpeed = 92.5f,
		BaseDamage = 12f,
		BaseArea = 1f,
		StartingWeapon = "Bow"
	};

	public static CharacterDefinition Warrior { get; } = new()
	{
		Name = "Warrior",
		Description = "Tanky fighter with area attacks. +30% max HP and area.",
		BaseHP = 140f,
		BaseSpeed = 70f,
		BaseDamage = 10f,
		BaseArea = 1.15f,
		StartingWeapon = "Aura"
	};

	public static CharacterDefinition Mage { get; } = new()
	{
		Name = "Mage",
		Description = "Powerful spellcaster. +15% area, high damage.",
		BaseHP = 80f,
		BaseSpeed = 82.5f,
		BaseDamage = 16f,
		BaseArea = 1.15f,
		StartingWeapon = "MagicWand"
	};

	public static CharacterDefinition Knight { get; } = new()
	{
		Name = "Knight",
		Description = "Armoured melee fighter. High HP, close-range sword strikes.",
		BaseHP = 160f,
		BaseSpeed = 62.5f,
		BaseDamage = 14f,
		BaseArea = 1f,
		StartingWeapon = "Sword"
	};

	public static CharacterDefinition Templar { get; } = new()
	{
		Name = "Templar",
		Description = "Swift lightning striker. Chains bolts between multiple enemies.",
		BaseHP = 95f,
		BaseSpeed = 87.5f,
		BaseDamage = 13f,
		BaseArea = 1f,
		StartingWeapon = "StormRod"
	};

	public static CharacterDefinition Druid { get; } = new()
	{
		Name = "Druid",
		Description = "Nature guardian. Orbiting shards shield and damage enemies.",
		BaseHP = 115f,
		BaseSpeed = 75f,
		BaseDamage = 11f,
		BaseArea = 1.1f,
		StartingWeapon = "OrbitalShards"
	};

	public static CharacterDefinition Pyromancer { get; } = new()
	{
		Name = "Pyromancer",
		Description = "Glass-cannon fire mage. Scorches the ground with ember trails.",
		BaseHP = 75f,
		BaseSpeed = 82.5f,
		BaseDamage = 18f,
		BaseArea = 1f,
		StartingWeapon = "EmberTrail"
	};

	public static IReadOnlyList<CharacterDefinition> All { get; } = new[]
	{
		Archer, Warrior, Mage, Knight, Templar, Druid, Pyromancer
	};

	public static CharacterDefinition GetByName( string name ) =>
		All.FirstOrDefault( c => c.Name == name ) ?? Archer;
}
