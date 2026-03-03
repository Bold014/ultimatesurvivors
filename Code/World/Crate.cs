/// <summary>
/// A free interactable crate. The player presses E when nearby to receive a handful of XP gems.
/// More common than chests — no coin cost.
/// </summary>
public sealed class Crate : Component
{
	static SoundEvent CrateOpenSound = new( "sounds/chest_open.mp3" );

	public bool IsOpened { get; private set; } = false;

	/// <summary>True when the local player is within interaction range.</summary>
	public bool IsPlayerNearby { get; private set; } = false;

	private const float InteractRadius = 70f;
	private const float SpriteScale    = 2f;
	private const string SpritePath    = "scenes/crate.sprite";

	private const int GemXPValue = 5;
	private const string AnimDefault = "default";

	private Random _rand;
	private SpriteRenderer _spriteRenderer;
	private BoxCollider _collider;

	protected override void OnStart()
	{
		_rand = new Random();
		SetupSprite();
	}

	protected override void OnUpdate()
	{
		if ( IsOpened ) return;

		var player = PlayerLocalState.LocalInstance;
		if ( player == null ) return;

		var playerGo = player.GameObject;
		float dist = (playerGo.WorldPosition - WorldPosition).WithZ( 0f ).Length;
		IsPlayerNearby = dist < InteractRadius;

		if ( IsPlayerNearby && Input.Pressed( "Use" ) )
		{
			Open( playerGo );
		}
	}

	private void Open( GameObject playerGo )
	{
		IsOpened      = true;
		IsPlayerNearby = false;

		_spriteRenderer?.GameObject.Destroy();
		_collider?.Destroy();

		try
		{
			var h = Sound.Play( CrateOpenSound );
			h.Position = WorldPosition;
		}
		catch { }

		int gemCount = _rand.Next( 3, 6 ); // 3 to 5 inclusive
		for ( int i = 0; i < gemCount; i++ )
		{
			var offset = new Vector3( _rand.NextSingle() * 20f - 10f, _rand.NextSingle() * 20f - 10f, 0f );
			var gemGo  = new GameObject( true, "XPGem" );
			gemGo.WorldPosition = WorldPosition.WithZ( 0f ) + offset;
			LocalGameRunner.ParentRuntimeObject( gemGo );
			var gem = gemGo.Components.Create<XPGem>();
			gem.XPValue    = GemXPValue;
			gem.PlayerObject = playerGo;
		}
	}

	private void SetupSprite()
	{
		var sprite = ResourceLibrary.Get<Sprite>( SpritePath );

		var spriteGo = new GameObject( true, "CrateSprite" );
		spriteGo.SetParent( GameObject );
		spriteGo.LocalPosition = new Vector3( 0f, 0f, 2f );
		spriteGo.LocalScale    = new Vector3( SpriteScale, SpriteScale, SpriteScale );

		_spriteRenderer = spriteGo.Components.Create<SpriteRenderer>();
		_spriteRenderer.Sprite        = sprite;
		_spriteRenderer.TextureFilter = Sandbox.Rendering.FilterMode.Point;

		// Pause on frame 0 so the closed crate is visible from the start.
		_spriteRenderer.PlayAnimation( AnimDefault );
		_spriteRenderer.PlaybackSpeed = 0f;

		// Collision so enemies and players can bump into the crate.
		_collider = Components.Create<BoxCollider>();
		_collider.Scale = new Vector3( SpriteScale * 16f, SpriteScale * 16f, SpriteScale * 16f );
	}
}
