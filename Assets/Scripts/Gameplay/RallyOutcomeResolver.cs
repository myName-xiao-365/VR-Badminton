namespace VRBadminton.Gameplay
{
    public static class RallyOutcomeResolver
    {
        public static MatchState ApplyRallyWinner(
            MatchState currentState,
            int rallyWinner,
            MatchRules rules)
        {
            currentState.ApplyRallyWinner(rallyWinner, rules);
            return currentState;
        }
    }
}
