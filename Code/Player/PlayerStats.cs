using System.Collections.Generic;

/// <summary>
/// Player stats — plain (non-[Sync]) properties so they are always writable.
/// </summary>
public sealed class PlayerStats : Component
{
	public string PlayerName    { get; set; } = "Player";
	public string CharacterName { get; set; } = "Archer";
	public int    Kills         { get; set; } = 0;
	public int    Level         { get; set; } = 1;
	public float  Score         { get; set; } = 0f;
	public float  TimeAlive     { get; set; } = 0f;
	public bool   IsAlive       { get; set; } = true;

	/// <summary>Per-weapon kill counts accumulated this run. Reported in RunResult at run end.</summary>
	public Dictionary<string, int> KillsByWeapon { get; } = new();

	/// <summary>Total projectiles fired this run (for Quantity Tome unlock quest).</summary>
	public int ProjectilesFired { get; set; } = 0;

	private float _localTime    = 0f;
	private bool  _timerRunning = true;
	private float _debugLogTimer = 0f;

	protected override void OnStart()
	{
		Log.Info( $"[PlayerStats] OnStart — IsAlive={IsAlive}, _timerRunning={_timerRunning}, GameObject={GameObject?.Name}" );
	}

	protected override void OnUpdate()
	{
		if ( !_timerRunning ) return;
		if ( UpgradeSystem.LocalInstance?.IsShowingUpgrades == true ) return;

		_localTime     += Time.Delta;
		TimeAlive       = _localTime;

		_debugLogTimer += Time.Delta;
		if ( _debugLogTimer >= 5f )
		{
			_debugLogTimer = 0f;
			Log.Info( $"[PlayerStats] OnUpdate running — TimeAlive={TimeAlive:F1}s, Kills={Kills}, Level={Level}" );
		}
	}

	/// <param name="weaponId">Display name of the weapon (matches QuestDefinition.WeaponName), or null if unknown.</param>
	public void AddKill( string weaponId = null, int scoreValue = 5 )
	{
		Kills++;
		Score += scoreValue;

		if ( weaponId != null )
		{
			KillsByWeapon.TryGetValue( weaponId, out var prev );
			KillsByWeapon[weaponId] = prev + 1;
		}
	}

	public void Die()
	{
		Log.Info( "[PlayerStats] Die() called — stopping timer" );
		IsAlive       = false;
		_timerRunning = false;

		// Increment persistent death counter immediately so it's available for quest sync
		PlayerProgress.Data.TotalDeaths++;
	}
}
