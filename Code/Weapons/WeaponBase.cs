/// <summary>
/// Abstract base for all auto-attack weapons.
/// Weapons are components on the player GameObject. Only the owner runs weapon logic.
/// </summary>
public abstract class WeaponBase : Component
{
	public const int MaxWeaponLevel = 5;

	[Property] public int WeaponLevel { get; set; } = 1;
	[Property] public float BaseCooldown { get; set; } = 1.5f;

	protected float _cooldownTimer = 0f;
	protected PlayerLocalState _state;

	protected override void OnStart()
	{
		_state = Components.Get<PlayerLocalState>();
	}

	protected override void OnUpdate()
	{
		if ( _state == null ) return;

		_cooldownTimer -= Time.Delta;
		if ( _cooldownTimer <= 0f )
		{
			_cooldownTimer = BaseCooldown * _state.CooldownMultiplier;
			OnFire();
		}
	}

	protected abstract void OnFire();

	protected EnemyBase FindNearestEnemy()
	{
		var myPos = WorldPosition;
		return Scene.GetAllComponents<EnemyBase>()
			.OrderBy( e => (e.WorldPosition - myPos).LengthSquared )
			.FirstOrDefault();
	}

	public string WeaponDisplayName => GetType().Name switch
	{
		"MagicWand"            => "Magic Wand",
		"BowWeapon"            => "Bow",
		"AxeWeapon"            => "Axe",
		"AuraWeapon"           => "Aura",
		"StormRodWeapon"       => "Storm Rod",
		"OrbitalShardsWeapon"  => "Orbital Shards",
		"EmberTrailWeapon"     => "Ember Trail",
		"SwordWeapon"          => "Sword",
		_ => GetType().Name
	};

	/// <summary>
	/// Short identifier used for kill-tracking (matches QuestDefinition.WeaponName values).
	/// Override in subclasses is not needed — this resolves from the type name.
	/// </summary>
	public string WeaponId => GetType().Name switch
	{
		"MagicWand"            => "Magic Wand",
		"BowWeapon"            => "Bow",
		"AxeWeapon"            => "Axe",
		"AuraWeapon"           => "Aura",
		"StormRodWeapon"       => "Storm Rod",
		"OrbitalShardsWeapon"  => "Orbital Shards",
		"EmberTrailWeapon"     => "Ember Trail",
		"SwordWeapon"          => "Sword",
		_ => GetType().Name
	};

	/// <summary>Returns a human-readable description of what upgrading to nextLevel does.</summary>
	public virtual string GetUpgradeDescription( int nextLevel ) => $"Level {nextLevel}: improved stats";
}
