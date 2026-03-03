/// <summary>
/// Ground pickup dropped by enemies. Flies to the player when attracted and instantly
/// vacuums all XP gems on screen toward the player.
/// </summary>
public sealed class MagnetPickup : Component
{
	public GameObject PlayerObject { get; set; }

	private float _lifetime = 25f;
	private bool _attracted = false;
	private const float MoveSpeed = 220f;
	private const float PickupSize = 6f;

	protected override void OnStart()
	{
		WorldPosition = WorldPosition.WithZ( 4f );

		var renderer = Components.Create<SpriteRenderer>();
		renderer.Sprite = ResourceLibrary.Get<Sprite>( "ui/pickups/magnet.sprite" );
		renderer.Size = new Vector2( PickupSize, PickupSize );
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

		if ( dist < playerState.MagnetRadius )
			_attracted = true;

		if ( _lifetime < 3f )
			_attracted = true;

		if ( _attracted )
		{
			if ( dist > 1f )
				WorldPosition += toPlayer.Normal * MoveSpeed * Time.Delta;
			WorldPosition = WorldPosition.WithZ( 4f );

			if ( dist < 3f )
				Collect();
		}
	}

	private void Collect()
	{
		foreach ( var gem in Scene.GetAllComponents<XPGem>() )
			gem.ForceAttract();

		try
		{
			Sound.Play( "sounds/xp_pickup.mp3" );
		}
		catch { }
		GameObject.Destroy();
	}
}
