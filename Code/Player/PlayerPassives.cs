/// <summary>
/// Tracks passive upgrades chosen during level-ups for the current run.
/// Used by the HUD to display filled tome/passive slots.
/// </summary>
public sealed class PlayerPassives : Component
{
	public const int MaxSlots = 4;

	private readonly List<string> _passives = new();

	public IReadOnlyList<string> Passives => _passives.AsReadOnly();

	/// <summary>True when all tome slots are filled (4/4). When full, level-up only offers upgrades to existing tomes.</summary>
	public bool IsFull => _passives.Count >= MaxSlots;

	public void AddPassive( string name )
	{
		if ( _passives.Count < MaxSlots )
			_passives.Add( name );
	}

	public void Clear()
	{
		_passives.Clear();
	}
}
