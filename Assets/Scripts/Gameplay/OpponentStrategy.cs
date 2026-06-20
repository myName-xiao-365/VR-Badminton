namespace VRBadminton.Gameplay
{
    public static class OpponentStrategy
    {
        public static OpponentDecision Choose(
            int difficultyLevel,
            float opponentStamina,
            bool canSmash,
            bool fromFrontCourt,
            float opponentSmashChance,
            float random01)
        {
            return OpponentDecision.Choose(
                difficultyLevel,
                opponentStamina,
                canSmash,
                fromFrontCourt,
                opponentSmashChance,
                random01);
        }
    }
}
