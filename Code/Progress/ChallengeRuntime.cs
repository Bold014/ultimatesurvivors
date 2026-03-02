using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Runtime challenge context for the current run.
/// Set by GameManager at run start and read by gameplay systems.
/// </summary>
public static class ChallengeRuntime
{
	public static ChallengeDefinition ActiveChallenge { get; private set; }
	public static bool IsChallengeRun => ActiveChallenge != null;

	public static void SetActive( ChallengeDefinition def ) => ActiveChallenge = def;
	public static void Clear() => ActiveChallenge = null;

	public static bool HasModifier( ChallengeModifierType type )
		=> ActiveChallenge?.Modifiers?.Any( m => m.Type == type ) == true;

	public static float GetModifier( ChallengeModifierType type, float defaultValue = 1f )
	{
		var mod = ActiveChallenge?.Modifiers?.FirstOrDefault( m => m.Type == type );
		return mod?.Value ?? defaultValue;
	}

	public static float GetCombinedMultiplier( ChallengeModifierType type )
	{
		if ( ActiveChallenge?.Modifiers == null ) return 1f;
		float result = 1f;
		foreach ( var m in ActiveChallenge.Modifiers.Where( m => m.Type == type ) )
			result *= m.Value;
		return result;
	}
}
