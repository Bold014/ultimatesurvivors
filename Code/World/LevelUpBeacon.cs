/// <summary>
/// A Charge Shrine-style world object. Stand within range for 4 seconds to fully charge it,
/// then receive a free upgrade selection via UpgradeSystem. Leaving the radius drains the charge
/// at the same rate. Pulses visually while charging.
/// </summary>
public sealed class LevelUpBeacon : Component
{
	public float ChargePercent { get; private set; } = 0f;
	public bool IsActive { get; private set; } = true;
	public bool IsPlayerNearby { get; private set; } = false;

	private const float ChargeRadius = 65f;
	private const float ChargeTime   = 4f;

	private SpriteRenderer       _spriteRenderer;
	private float                _pulseTimer = 0f;

	private CircleRingRenderer   _ring;

	private static readonly Color BaseColor    = new Color( 0.3f, 0.5f, 1f );
	private static readonly Color ChargingColor = new Color( 0.6f, 0.8f, 1f );

	static SoundEvent ChargeSound = new( "sounds/beacon_charge.mp3" );
	private SoundHandle _chargeHandle;
	private bool _wasPlayerNearby = false;

	protected override void OnStart()
	{
		var spriteGo = new GameObject( true, "ShrineSprite" );
		spriteGo.SetParent( GameObject );
		spriteGo.LocalPosition = new Vector3( 0f, 0f, 2f );
		spriteGo.LocalScale    = new Vector3( 5f, 5f, 5f );

		var sprite = ResourceLibrary.Get<Sprite>( "sprites/shrine.sprite" );
		_spriteRenderer = spriteGo.Components.Create<SpriteRenderer>();
		_spriteRenderer.Sprite      = sprite;
		_spriteRenderer.PlaybackSpeed = 1f;
		_spriteRenderer.TextureFilter = Sandbox.Rendering.FilterMode.Point;
		_spriteRenderer.Color = BaseColor;

		// Collision half-extent matches the hardcoded 15-unit AABB in PlayerController
		var collider = Components.Create<BoxCollider>();
		collider.Scale = new Vector3( 30f, 30f, 30f );

		_ring = Components.GetOrCreate<CircleRingRenderer>();
		_ring.Radius = ChargeRadius;
		_ring.Tint   = new Color( 0.3f, 0.5f, 1f, 1f );
	}

	protected override void OnUpdate()
	{
		if ( !IsActive ) return;

		var player = Scene.GetAllComponents<PlayerLocalState>().FirstOrDefault();
		if ( player == null ) return;

		// Don't charge while upgrade panel is open
		var upgradeSystem = Scene.GetAllComponents<UpgradeSystem>().FirstOrDefault();
		if ( upgradeSystem?.IsShowingUpgrades == true ) return;

		float dist = (player.WorldPosition - WorldPosition).WithZ( 0f ).Length;
		IsPlayerNearby = dist < ChargeRadius;

		if ( IsPlayerNearby && !_wasPlayerNearby )
		{
			// Player just entered — start charge sound
			try { _chargeHandle = Sound.Play( ChargeSound ); } catch { }
		}
		else if ( !IsPlayerNearby && _wasPlayerNearby )
		{
			// Player just left — stop and reset charge sound
			try { _chargeHandle.Stop(); } catch { }
		}
		_wasPlayerNearby = IsPlayerNearby;

		float chargeRate = 1f / ChargeTime;

		if ( IsPlayerNearby )
		{
			ChargePercent = MathF.Min( 1f, ChargePercent + chargeRate * Time.Delta );
		}
		else
		{
			ChargePercent = MathF.Max( 0f, ChargePercent - chargeRate * Time.Delta );
		}

		UpdateVisuals();

		if ( ChargePercent >= 1f )
		{
			Activate( player.GameObject );
		}
	}

	private void Activate( GameObject playerGo )
	{
		IsActive = false;
		IsPlayerNearby = false;

		try { _chargeHandle.Stop(); } catch { }

		var upgradeSystem = playerGo.Components.Get<UpgradeSystem>();
		upgradeSystem?.TriggerShrineReward();

		GameObject.Destroy();
	}

	protected override void OnDestroy()
	{
		try { _chargeHandle.Stop(); } catch { }
	}

	private void UpdateVisuals()
	{
		if ( _spriteRenderer == null ) return;

		if ( IsPlayerNearby )
		{
			_pulseTimer += Time.Delta * 6f;
			float pulse = (MathF.Sin( _pulseTimer ) + 1f) * 0.5f;
			_spriteRenderer.Color = Color.Lerp( BaseColor, ChargingColor, 0.4f + pulse * 0.6f );

			if ( _ring != null )
			{
				float r = 0.3f + ChargePercent * 0.5f;
				float g = 0.5f + ChargePercent * 0.3f;
				_ring.Tint = new Color( r, g, 1f, 1f );
			}
		}
		else
		{
			_spriteRenderer.Color = BaseColor;
			_pulseTimer = 0f;

			if ( _ring != null )
				_ring.Tint = new Color( 0.3f, 0.5f, 1f, 1f );
		}
	}
}
