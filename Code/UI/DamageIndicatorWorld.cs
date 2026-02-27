/// <summary>
/// World-space damage indicator. Spawns a WorldPanel at the hit position and floats up.
/// Uses WorldPanel so the engine handles projection — no manual world-to-screen math.
/// </summary>
public sealed class DamageIndicatorWorld : Component
{
	private const float Lifetime = 1.25f;
	private const float FloatSpeed = 12f; // world units per second (+X = screen up)

	private Vector3 _spawnPos;
	private float _elapsed;
	private DamageIndicator _indicator;

	public static void Spawn( GameObject trackTarget, Vector3 offset, float amount, bool isCritical )
	{
		SpawnInternal( trackTarget, offset, amount, isCritical, false );
	}

	public static void SpawnHeal( GameObject trackTarget, Vector3 offset, float amount )
	{
		SpawnInternal( trackTarget, offset, amount, false, true );
	}

	private static void SpawnInternal( GameObject trackTarget, Vector3 offset, float amount, bool isCritical, bool isHeal )
	{
		var pos = (trackTarget != null && trackTarget.IsValid ? trackTarget.WorldPosition : Vector3.Zero) + offset;
		// Small Z jitter so overlapping indicators don't occlude each other
		pos += new Vector3( 0f, 0f, System.Random.Shared.NextSingle() * 6f );
		var go = new GameObject( true, "DamageIndicator" );
		go.WorldPosition = pos;
		go.WorldRotation = Rotation.From( new Angles( 90f, 0f, 0f ) );
		go.WorldScale = Vector3.One;

		var wp = go.Components.Create<Sandbox.WorldPanel>();
		wp.PanelSize = new Vector2( 420f, 90f );

		var ind = go.Components.Create<DamageIndicator>();
		ind.Damage = amount;
		ind.IsCritical = isCritical;
		ind.IsHeal = isHeal;
		ind.ManagedExternally = true;

		var tracker = go.Components.Create<DamageIndicatorWorld>();
		tracker._spawnPos = pos;
		tracker._indicator = ind;
	}

	protected override void OnUpdate()
	{
		_elapsed += Time.Delta;
		if ( _indicator != null )
			_indicator.Elapsed = _elapsed;

		// Float up from spawn position — no tracking, stays fixed in world space
		float floatUp = FloatSpeed * _elapsed;
		GameObject.WorldPosition = _spawnPos + new Vector3( floatUp, 0f, 0f );

		if ( _elapsed >= Lifetime )
			GameObject.Destroy();
	}
}
