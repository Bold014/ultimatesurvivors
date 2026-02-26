/// <summary>
/// Swings a wide arc in front of the player, hitting all enemies inside it.
/// Level 2+: increased damage. Level 3+: double slash (second hit after 0.2s).
/// Level 4+: wider arc and longer range. Level 5+: knockback on every hit.
/// </summary>
public sealed class SwordWeapon : WeaponBase
{
	private bool _pendingSecondSlash = false;
	private float _secondSlashTimer = 0f;
	private Vector3 _pendingSlashDir;

	protected override void OnStart()
	{
		base.OnStart();
		BaseCooldown = 2.0f;
	}

	protected override void OnFire()
	{
		var target = FindNearestEnemy();
		var dir = target != null
			? (target.WorldPosition - WorldPosition).WithZ( 0f ).Normal
			: Vector3.Forward;

		PerformSlash( dir );

		if ( WeaponLevel >= 3 )
		{
			_pendingSlashDir = dir;
			_pendingSecondSlash = true;
			_secondSlashTimer = 0.2f;
		}
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if ( _pendingSecondSlash )
		{
			_secondSlashTimer -= Time.Delta;
			if ( _secondSlashTimer <= 0f )
			{
				PerformSlash( _pendingSlashDir );
				_pendingSecondSlash = false;
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

			enemy.TakeDamage( damage, WeaponId );

			if ( WeaponLevel >= 5 )
				enemy.WorldPosition += toEnemy.Normal * 40f;
		}
	}

	private void SpawnSlashVisual( Vector3 direction, float arcDeg, float range )
	{
		var go = new GameObject( true, "SwordSlash" );
		go.WorldPosition = WorldPosition + direction * (range * 0.5f);
		go.WorldRotation = Rotation.LookAt( direction, Vector3.Up );

		var r = go.Components.Create<ModelRenderer>();
		r.Model = Model.Load( "models/dev/box.vmdl" );
		r.Tint = new Color( 1f, 0.92f, 0.25f, 0.65f );

		float widthScale = (arcDeg / 180f) * (range / 50f) / 5f;
		float lengthScale = (range / 50f) / 5f;
		go.WorldScale = new Vector3( lengthScale, widthScale, 0.02f );

		var effect = go.Components.Create<SwordSlashEffect>();
		effect.Lifetime = 0.2f;
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
/// Short-lived visual effect for a sword slash. Fades out and self-destructs.
/// </summary>
public sealed class SwordSlashEffect : Component
{
	public float Lifetime { get; set; } = 0.2f;

	private float _timer;
	private ModelRenderer _renderer;

	protected override void OnStart()
	{
		_timer = Lifetime;
		_renderer = Components.Get<ModelRenderer>();
	}

	protected override void OnUpdate()
	{
		_timer -= Time.Delta;

		if ( _renderer != null )
		{
			float alpha = Math.Max( 0f, _timer / Lifetime ) * 0.65f;
			_renderer.Tint = _renderer.Tint.WithAlpha( alpha );
		}

		if ( _timer <= 0f )
			GameObject.Destroy();
	}
}
