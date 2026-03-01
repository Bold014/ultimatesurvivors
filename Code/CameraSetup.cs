/// <summary>
/// Rotates the scene camera to look straight down for top-down 2D view.
/// Attached to the Camera scene object. The player spawns their own higher-priority camera.
/// </summary>
public sealed class CameraSetup : Component
{
	[Property] public float CameraYaw { get; set; } = 90f;

	protected override void OnStart()
	{
		WorldRotation = Rotation.From( new Angles( 90f, CameraYaw, 0f ) );
	}
}
