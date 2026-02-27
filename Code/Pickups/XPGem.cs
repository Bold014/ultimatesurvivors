/// <summary>
/// Local-only XP gem dropped by enemies.
/// Floats in place until the player enters the magnet radius, then flies toward them.
/// Collected on close contact, adding XP via PlayerXP.
/// </summary>
public sealed class XPGem : Component
{
	static SoundEvent XpPickupSound = new( "sounds/xp_pickup.mp3" );

	[Property] public int XPValue { get; set; } = 5;
	public GameObject PlayerObject { get; set; }

	private float _lifetime = 25f;
	private bool _attracted = false;
	private const float MoveSpeed = 220f;
	private const float GemSize = 6f;

	protected override void OnStart()
	{
		var renderer = Components.Create<SpriteRenderer>();
		renderer.Sprite = ResourceLibrary.Get<Sprite>( "ui/weapons/xp.sprite" );
		renderer.Size = new Vector2( GemSize, GemSize );
		renderer.TextureFilter = Sandbox.Rendering.FilterMode.Point;
	}

	protected override void OnUpdate()
	{
		_lifetime -= Time.Delta;
		if ( _lifetime <= 0f )
		{
			GameObject.Destroy();
			return;
		}

		if ( PlayerObject == null ) return;

		var playerState = PlayerObject.Components.Get<PlayerLocalState>();
		if ( playerState == null ) return;

		var toPlayer = (PlayerObject.WorldPosition - WorldPosition).WithZ( 0f );
		float dist = toPlayer.Length;

		// Enter attract mode when within magnet radius
		if ( dist < playerState.MagnetRadius )
			_attracted = true;

		// Also attract if lifetime is nearly up
		if ( _lifetime < 3f )
			_attracted = true;

		if ( _attracted )
		{
			if ( dist > 1f )
				WorldPosition += toPlayer.Normal * MoveSpeed * Time.Delta;
			WorldPosition = WorldPosition.WithZ( 0f );

			if ( dist < 3f )
				Collect();
		}
	}

	private void Collect()
	{
		var xp = PlayerObject?.Components.Get<PlayerXP>();
		try { Sound.Play( XpPickupSound ); } catch { }
		xp?.AddXP( XPValue );
		GameObject.Destroy();
	}
}
