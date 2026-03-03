/// <summary>
/// Ground pickup dropped by enemies. Flies to the player when attracted and restores Shield.
/// Only meaningful when the player has MaxShield &gt; 0 (enforced at spawn time in EnemyBase).
/// </summary>
public sealed class ShieldPickup : Component
{
	public float ShieldAmount { get; set; } = 20f;
	public GameObject PlayerObject { get; set; }

	private float _lifetime = 25f;
	private bool _attracted = false;
	private const float MoveSpeed = 220f;
	private const float PickupSize = 6f;

	protected override void OnStart()
	{
		WorldPosition = WorldPosition.WithZ( 4f );

		var renderer = Components.Create<SpriteRenderer>();
		renderer.Sprite = ResourceLibrary.Get<Sprite>( "ui/pickups/shield.sprite" );
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
				Collect( playerState );
		}
	}

	private void Collect( PlayerLocalState playerState )
	{
		playerState.Shield = Math.Min( playerState.Shield + ShieldAmount, playerState.MaxShield );
		try
		{
			Sound.Play( "sounds/xp_pickup.mp3" );
		}
		catch { }
		GameObject.Destroy();
	}
}
