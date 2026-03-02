/// <summary>
/// A short-lived fire zone dropped on the ground.
/// Pulses damage to enemies touching the flame sprite until it expires.
/// Created by EmberTrailWeapon.
/// </summary>
public sealed class BurnZone : Component
{
	public float Damage { get; set; } = 6f;
	public float Lifetime { get; set; } = 3f;
	public float PulseInterval { get; set; } = 0.5f;
	/// <summary>Multiplier from Size Tome / Area upgrades. Scales sprite size and hit radius.</summary>
	public float SizeScale { get; set; } = 1f;
	/// <summary>Weapon that created this zone — used to attribute kills for quest tracking.</summary>
	public string SourceWeaponId { get; set; } = null;
	/// <summary>When set, damages this GameObject (e.g. player) instead of enemies. Used by DragonBoss fire attacks.</summary>
	public GameObject DamageTarget { get; set; } = null;

	private const string FlameSpritePath = "Textures/flame/flameanimation.sprite";
	/// <summary>Base radius for damage — scaled by SizeScale. Enemies within this distance are "touching".</summary>
	private const float BaseFlameHitRadius = 18f;
	/// <summary>Duration of rise animation (4 frames @ 8 fps).</summary>
	private const float RiseDuration = 0.5f;
	/// <summary>Duration of fall animation (4 frames @ 15 fps).</summary>
	private const float FallDuration = 0.27f;

	private float _timeAlive = 0f;
	private float _pulseTimer = 0f;
	private SpriteRenderer _flameSprite;
	private string _currentAnim = "";

	protected override void OnStart()
	{
		var spriteGo = new GameObject( true, "FlameSprite" );
		spriteGo.Parent = GameObject;
		spriteGo.LocalPosition = Vector3.Zero;
		// Uniform scale; base 2.5 matches character sprite size; scales with Size Tome.
		float flameScale = 2.5f * SizeScale;
		spriteGo.LocalScale = new Vector3( flameScale, flameScale, flameScale );

		var sprite = ResourceLibrary.Get<Sprite>( FlameSpritePath );
		if ( sprite != null )
		{
			_flameSprite = spriteGo.Components.Create<SpriteRenderer>();
			_flameSprite.Sprite = sprite;
			_flameSprite.TextureFilter = Sandbox.Rendering.FilterMode.Point;
			_flameSprite.PlaybackSpeed = 1f;
			_flameSprite.PlayAnimation( "rise" );
			_currentAnim = "rise";
		}
	}

	protected override void OnUpdate()
	{
		_timeAlive += Time.Delta;
		_pulseTimer += Time.Delta;

		float fade = 1f - (_timeAlive / Lifetime);

		// Flame animation: rise → idle → fall
		if ( _flameSprite != null )
		{
			_flameSprite.Color = Color.White.WithAlpha( fade );

			string nextAnim = _timeAlive < RiseDuration
				? "rise"
				: _timeAlive < Lifetime - FallDuration
					? "idle"
					: "fall";

			if ( nextAnim != _currentAnim )
			{
				_currentAnim = nextAnim;
				_flameSprite.PlayAnimation( nextAnim );
			}
		}

		if ( _pulseTimer >= PulseInterval )
		{
			_pulseTimer = 0f;
			DamageEnemiesTouchingFlame();
		}

		if ( _timeAlive >= Lifetime )
			GameObject.Destroy();
	}

	private void DamageEnemiesTouchingFlame()
	{
		if ( DamageTarget != null && DamageTarget.IsValid )
		{
			var dist = (DamageTarget.WorldPosition - WorldPosition).WithZ( 0f ).Length;
			const float playerHalfExtent = 1f;
			if ( dist <= BaseFlameHitRadius * SizeScale + playerHalfExtent )
			{
				var playerState = DamageTarget.Components.Get<PlayerLocalState>();
				if ( playerState != null )
				{
					bool died = playerState.TakeDamage( Damage );
					if ( died )
						DamageTarget.Components.Get<PlayerStats>()?.Die();
				}
			}
		}
		else
		{
			foreach ( var enemy in Scene.GetAllComponents<EnemyBase>() )
			{
				var dist = (enemy.WorldPosition - WorldPosition).WithZ( 0f ).Length;
				if ( dist <= BaseFlameHitRadius * SizeScale + enemy.ProjectileHitRadius )
					enemy.TakeDamage( Damage, SourceWeaponId, WorldPosition );
			}
		}
	}
}
