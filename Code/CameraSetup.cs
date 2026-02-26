/// <summary>
/// Rotates the scene camera to look straight down for top-down 2D view.
/// Attached to the Camera scene object. The player spawns their own higher-priority camera.
/// </summary>
public sealed class CameraSetup : Component
{
	protected override void OnStart()
	{
		WorldRotation = Rotation.From( new Angles( 90f, 0f, 0f ) );
	}
}
