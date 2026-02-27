using SpriteTools;
using System.Collections.Generic;

/// <summary>
/// Swings a wide arc in the player's last-pressed movement direction, hitting all enemies inside it.
/// Level 2+: increased damage. Level 3+: double slash (strikes twice per attack).
/// Level 4+: wider arc and longer range. Level 5+: knockback on every hit.
/// Quantity Tome (ProjectileCount) adds extra slashes, each staggered 0.15s apart.
/// </summary>
public sealed class SwordWeapon : WeaponBase
{
	private readonly List<(float TimeLeft, Vector3 Dir)> _pendingSlashes = new();

	protected override void OnStart()
	{
		base.OnStart();
		BaseCooldown = 2.0f;
	}

	protected override void OnFire()
	{
		var controller = Components.Get<PlayerController>();
		var dir = (controller != null) ? controller.LastMoveDirection : Vector3.Forward;

		int baseCount = WeaponLevel >= 3 ? 2 : 1;
		int totalCount = baseCount + (_state?.ProjectileCount ?? 0);

		PerformSlash( dir );

		for ( int i = 1; i < totalCount; i++ )
			_pendingSlashes.Add( (i * 0.15f, dir) );
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		for ( int i = _pendingSlashes.Count - 1; i >= 0; i-- )
		{
			var (timeLeft, dir) = _pendingSlashes[i];
			timeLeft -= Time.Delta;

			if ( timeLeft <= 0f )
			{
				PerformSlash( dir );
				_pendingSlashes.RemoveAt( i );
			}
			else
			{
				_pendingSlashes[i] = (timeLeft, dir);
			}
		}
	}

	private void PerformSlash( Vector3 direction )
	{
		if ( _state == null ) return;

		float arcDeg = GetArcDegrees();
		float range = GetRange() * (_state?.Area ?? 1f);
		float damage = _state.Damage * GetDamageMultiplier();

		SpawnSlashVisual( direction, arcDeg, range );

		foreach ( var enemy in Scene.GetAllComponents<EnemyBase>() )
		{
			var toEnemy = (enemy.WorldPosition - WorldPosition).WithZ( 0f );
			if ( toEnemy.LengthSquared > range * range ) continue;
			if ( toEnemy.LengthSquared < 0.01f ) continue;

			float angle = Vector3.GetAngle( direction, toEnemy.Normal );
			if ( angle > arcDeg * 0.5f ) continue;

			float knockbackMult = WeaponLevel >= 5 ? 3f : 1f;
			enemy.TakeDamage( damage, WeaponId, WorldPosition, knockbackMult );
		}
	}

	private void SpawnSlashVisual( Vector3 direction, float arcDeg, float range )
	{
		// Animation is 2 frames @ 4 fps = 0.5s total
		const float animDuration = 0.5f;
		float lifetime = animDuration * (_state?.DurationMultiplier ?? 1f);

		var go = new GameObject( true, "SwordSlash" );
		go.WorldPosition = WorldPosition + direction * (range * 0.4f);

		// Lay flat on the XY ground plane oriented along the attack direction.
		// Pitch 90° = flat, then yaw from the direction vector.
		var yaw = MathF.Atan2( direction.y, direction.x ) * (180f / MathF.PI);
		go.WorldRotation = Rotation.From( new Angles( 90f, yaw, 0f ) );

		// Scale to visually fill the hit cone:
		//   X = depth along attack direction  (≈ range)
		//   Y = width across the arc           (2 * range * sin(arcDeg/2))
		float halfArcRad = arcDeg * 0.5f * (MathF.PI / 180f);
		float arcWidth = 2f * range * MathF.Sin( halfArcRad );
		// Sprite images are small (the SpriteComponent pixel-scale maps 1 px → 1 world unit at scale 1).
		// Divide by a base resolution assumption (64 px) to normalise, then multiply by desired world size.
		const float basePixels = 64f;
		go.WorldScale = new Vector3( range / basePixels, arcWidth / basePixels, 1f );

		var spriteRes = ResourceLibrary.Get<SpriteResource>( "ui/weapons/sword/swordeffect.spr" );
		if ( spriteRes != null )
		{
			var sc = go.Components.Create<SpriteComponent>();
			sc.Sprite = spriteRes;
			sc.UsePixelScale = true;
			sc.PlayAnimation( "slash" );
		}

		var effect = go.Components.Create<SwordSlashEffect>();
		effect.Lifetime = lifetime;
	}

	private float GetArcDegrees() => WeaponLevel switch
	{
		>= 4 => 150f,
		>= 3 => 130f,
		_    => 120f,
	};

	private float GetRange() => WeaponLevel switch
	{
		>= 4 => 130f,
		>= 3 => 110f,
		_    => 100f,
	};

	private float GetDamageMultiplier() => WeaponLevel switch
	{
		>= 5 => 2.5f,
		>= 4 => 2.3f,
		>= 3 => 2.0f,
		>= 2 => 2.0f,
		_    => 1.8f,
	};

	public override string GetUpgradeDescription( int nextLevel ) => nextLevel switch
	{
		2 => "Damage: +11%",
		3 => "Double slash — strikes twice per attack",
		4 => "Wider arc (150°), longer range, Damage: +15%",
		5 => "Knockback — enemies are pushed away on hit",
		_ => $"Level {nextLevel}: improved stats",
	};
}

/// <summary>
/// Short-lived effect on a sword slash visual. Waits for the sprite animation to finish, then destroys itself.
/// </summary>
public sealed class SwordSlashEffect : Component
{
	public float Lifetime { get; set; } = 0.5f;

	private float _timer;

	protected override void OnStart()
	{
		_timer = Lifetime;
	}

	protected override void OnUpdate()
	{
		_timer -= Time.Delta;
		if ( _timer <= 0f )
			GameObject.Destroy();
	}
}
