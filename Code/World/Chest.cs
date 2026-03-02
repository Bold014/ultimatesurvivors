/// <summary>
/// A purchasable chest that spawns in the world. The player presses E when nearby
/// to spend coins and receive an upgrade selection (3 choices via UpgradeSystem).
/// Price scales exponentially with how many chests have been opened this run.
/// </summary>
public sealed class Chest : Component
{
	static SoundEvent ChestOpenSound = new( "sounds/chest_open.mp3" );

	/// <summary>Tracks how many paid chests have been opened this run (shared across all chest instances).</summary>
	public static int ChestsOpened { get; private set; } = 0;

	/// <summary>Reset at the start of each run.</summary>
	public static void ResetRunState() => ChestsOpened = 0;

	public bool IsOpened { get; private set; } = false;

	/// <summary>Coin cost to open this chest (computed from ChestsOpened when spawned).</summary>
	public int Cost { get; private set; }

	/// <summary>True when the local player is within interaction range.</summary>
	public bool IsPlayerNearby { get; private set; } = false;

	/// <summary>Wave number at the time this chest was spawned. Set by WorldObjectSpawner before OnStart.</summary>
	public int SpawnedOnWave { get; set; } = 1;

	private const float InteractRadius  = 70f;
	private const float BaseCost        = 20f;   // coins at wave 1 with no prior chests opened
	private const float WaveScaleFactor = 0.08f; // +8 % base cost per wave
	private const float ChestCostScale  = 1.28f; // multiplier per chest already opened this run

	private SpriteRenderer _spriteRenderer;

	private const float SpriteScale  = 2f;
	private const string SpritePath  = "sprites/chestanimations.sprite";
	private const string AnimOpen    = "open";

	// 4 frames (0-3) at 8 fps = 0.5 s per loop.
	// We stop at 0.4375 s (midway through frame 3) so we never cross the loop boundary back to frame 0.
	private const float OpenAnimDuration = 0.4375f;

	private float _openTimer = -1f; // >= 0 while the open animation is playing

	protected override void OnStart()
	{
		Cost = ComputeCost( ChestsOpened, SpawnedOnWave );
		float challengeCostMult = ChallengeRuntime.GetCombinedMultiplier( ChallengeModifierType.ChestCostMultiplier );
		if ( challengeCostMult != 1f )
			Cost = Math.Max( 1, (int)MathF.Round( Cost * challengeCostMult ) );

		SetupSprite();
	}

	protected override void OnUpdate()
	{
		// Advance the open animation timer and freeze on the last frame when done.
		if ( _openTimer >= 0f )
		{
			_openTimer += Time.Delta;
			if ( _openTimer >= OpenAnimDuration && _spriteRenderer != null )
			{
				_spriteRenderer.PlaybackSpeed = 0f;
				_openTimer = -1f;
			}
		}

		if ( IsOpened ) return;

		var player = Scene.GetAllComponents<PlayerLocalState>().FirstOrDefault();
		if ( player == null ) return;

		var playerGo = player.GameObject;
		float dist = (playerGo.WorldPosition - WorldPosition).WithZ( 0f ).Length;
		IsPlayerNearby = dist < InteractRadius;

		if ( IsPlayerNearby && Input.Pressed( "Use" ) )
		{
			TryOpen( playerGo );
		}
	}

	private void TryOpen( GameObject playerGo )
	{
		var coins = playerGo.Components.Get<PlayerCoins>();
		if ( coins == null ) return;

		var upgradeSystem = playerGo.Components.Get<UpgradeSystem>();
		if ( upgradeSystem == null ) return;

		// Merchant's Badge: first chest per run is free
		var playerState = playerGo.Components.Get<PlayerLocalState>();
		bool isFree = playerState != null && playerState.ShouldNextChestBeFree();

		if ( !isFree && !coins.CanAfford( Cost ) ) return;

		if ( !isFree )
			coins.SpendCoins( Cost );

		try
		{
			var h = Sound.Play( ChestOpenSound );
			h.Position = WorldPosition;
		}
		catch { }
		ChestsOpened++;
		IsOpened = true;
		IsPlayerNearby = false;

		upgradeSystem.TriggerChestReward();

		if ( _spriteRenderer != null )
		{
			_spriteRenderer.PlayAnimation( AnimOpen );
			_spriteRenderer.PlaybackSpeed = 1f;
		}
		_openTimer = 0f;
	}

	private void SetupSprite()
	{
		var sprite = ResourceLibrary.Get<Sprite>( SpritePath );

		var spriteGo = new GameObject( true, "ChestSprite" );
		spriteGo.SetParent( GameObject );
		spriteGo.LocalPosition = new Vector3( 0f, 0f, 2f );
		spriteGo.LocalScale    = new Vector3( SpriteScale, SpriteScale, SpriteScale );

		_spriteRenderer = spriteGo.Components.Create<SpriteRenderer>();
		_spriteRenderer.Sprite        = sprite;
		_spriteRenderer.TextureFilter = Sandbox.Rendering.FilterMode.Point;

		// Pause on frame 0 (closed state) until the player opens the chest.
		_spriteRenderer.PlayAnimation( AnimOpen );
		_spriteRenderer.PlaybackSpeed = 0f;

		// Collision so enemies and players can bump into the chest.
		var collider = Components.Create<BoxCollider>();
		collider.Scale = new Vector3( SpriteScale * 16f, SpriteScale * 16f, SpriteScale * 16f );
	}

	/// <summary>
	/// Cost formula:
	///   waveBase  = BaseCost * (1 + wave * WaveScaleFactor)   — grows linearly with wave
	///   finalCost = waveBase * ChestCostScale ^ opened         — extra multiplier per chest opened
	/// Example: wave 1 / 0 opens ≈ 22 c  |  wave 10 / 2 opens ≈ 58 c  |  wave 20 / 4 opens ≈ 111 c
	/// </summary>
	private static int ComputeCost( int opened, int wave )
	{
		float waveBase = BaseCost * (1f + wave * WaveScaleFactor);
		return (int)MathF.Round( waveBase * MathF.Pow( ChestCostScale, opened ) );
	}

}
