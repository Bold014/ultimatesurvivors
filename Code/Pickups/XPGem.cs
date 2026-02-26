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

	protected override void OnStart()
	{
		var renderer = Components.Create<ModelRenderer>();
		renderer.Model = Model.Load( "models/dev/box.vmdl" );
		renderer.Tint = new Color( 0.1f, 0.9f, 0.3f );
		GameObject.WorldScale = new Vector3( 0.15f, 0.15f, 0.04f );
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

			if ( dist < 12f )
				Collect();
		}
	}

	private void Collect()
	{
		var xp = PlayerObject?.Components.Get<PlayerXP>();
		Log.Info( $"[XPGem] Collect — PlayerObject={PlayerObject?.Name ?? "NULL"}, PlayerXP found={xp != null}, XPValue={XPValue}" );
		try { Sound.Play( XpPickupSound ); } catch { }
		xp?.AddXP( XPValue );
		GameObject.Destroy();
	}
}
