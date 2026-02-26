/// <summary>
/// Tracks the player's in-run coin balance.
/// Coins are awarded instantly on enemy kill (no world pickup entity).
/// Unlike XP gems, coin awards are distance-independent.
/// </summary>
public sealed class PlayerCoins : Component
{
	public static PlayerCoins LocalInstance { get; private set; }

	public int Coins { get; private set; } = 0;

	protected override void OnStart()
	{
		LocalInstance = this;
		Coins = 0;
	}

	public void AddCoins( int amount )
	{
		Coins += amount;
	}

	public bool CanAfford( int amount ) => Coins >= amount;

	public bool SpendCoins( int amount )
	{
		if ( !CanAfford( amount ) ) return false;
		Coins -= amount;
		return true;
	}

	protected override void OnDestroy()
	{
		if ( LocalInstance == this )
			LocalInstance = null;
	}
}
