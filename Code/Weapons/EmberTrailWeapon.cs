/// <summary>
/// Periodically drops a BurnZone at the player's position.
/// Level 3+: faster drop rate. Level 4+: drops 2 zones per fire.
/// Level 5+: drops 2 zones, fastest rate.
/// Inspired by the Flamewalker concept, renamed Ember Trail.
/// </summary>
public sealed class EmberTrailWeapon : WeaponBase
{
	private int _lastLevel = 0;

	protected override void OnStart()
	{
		base.OnStart();
		UpdateCooldown();
	}

	protected override void OnUpdate()
	{
		if ( WeaponLevel != _lastLevel )
		{
			UpdateCooldown();
			_lastLevel = WeaponLevel;
		}

		base.OnUpdate();
	}

	protected override void OnFire()
	{
		if ( _state == null ) return;

		int zoneCount = GetZoneCount() + _state.ProjectileCount;
		for ( int i = 0; i < zoneCount; i++ )
		{
			var offset = i == 0 ? Vector3.Zero : new Vector3( (i % 2 == 0 ? 1f : -1f) * 20f, 0f, 0f );
			var go = new GameObject( true, "BurnZone" );
			go.WorldPosition = (WorldPosition + offset).WithZ( 0f );
			LocalGameRunner.ParentRuntimeObject( go );

			var zone = go.Components.Create<BurnZone>();
			zone.Damage = _state.Damage * GetDamageMultiplier();
			zone.SizeScale = _state.Area;
			float baseLifetime = GetBaseLifetime();
			zone.Lifetime = baseLifetime * _state.DurationMultiplier;
			zone.PulseInterval = 0.5f;
			zone.SourceWeaponId = WeaponId;
		}
	}

	private int GetZoneCount() => WeaponLevel switch
	{
		>= 30 => 4,
		>= 10 => 3,
		>= 4  => 2,
		_     => 1,
	};

	private float GetDamageMultiplier()
	{
		int clamped = Math.Min( WeaponLevel, 5 );
		int extra = Math.Max( 0, WeaponLevel - 5 );
		return 0.4f + clamped * 0.1f + extra * 0.03f;
	}

	private float GetBaseLifetime()
	{
		int clamped = Math.Min( WeaponLevel, 5 );
		int extra = Math.Max( 0, WeaponLevel - 5 );
		return 2f + clamped * 0.4f + extra * 0.1f;
	}

	private void UpdateCooldown()
	{
		BaseCooldown = WeaponLevel switch
		{
			>= 5 => 1.0f,
			>= 3 => 1.4f,
			_    => 1.8f,
		};
	}

	public override string GetUpgradeDescription( int nextLevel ) => nextLevel switch
	{
		2 => "Damage: +11%, Lifetime: +9%",
		3 => "Damage: +11%, Drop rate: +22%",
		4 => "Drops 2 fire zones per activation",
		5 => "Damage: +11%, Drop rate: +29%",
		10 => "Drops 3 fire zones per activation",
		30 => "Drops 4 fire zones per activation",
		_ => $"Level {nextLevel}: +3% damage, +0.1s lifetime",
	};
}
