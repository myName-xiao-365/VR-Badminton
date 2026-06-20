namespace VRBadminton.Gameplay
{
    public enum OpponentShotKind
    {
        Net,
        Drop,
        Lift,
        Clear,
        Smash
    }

    public readonly struct OpponentDecision
    {
        public OpponentDecision(OpponentShotKind shot)
        {
            Shot = shot;
        }

        public OpponentShotKind Shot { get; }

        public static OpponentDecision Choose(
            int difficultyLevel,
            float opponentStamina,
            bool canSmash,
            bool fromFrontCourt,
            float opponentSmashChance,
            float random01)
        {
            if (difficultyLevel == 0)
            {
                return new OpponentDecision(random01 < 0.5f
                    ? OpponentShotKind.Clear
                    : OpponentShotKind.Drop);
            }

            if (canSmash && opponentStamina >= 10f && random01 < opponentSmashChance)
            {
                return new OpponentDecision(OpponentShotKind.Smash);
            }

            if (fromFrontCourt && opponentStamina >= 3f)
            {
                return new OpponentDecision(random01 < 0.58f
                    ? OpponentShotKind.Lift
                    : OpponentShotKind.Net);
            }

            if (opponentStamina >= 5f && random01 < 0.34f)
            {
                return new OpponentDecision(OpponentShotKind.Clear);
            }

            if (opponentStamina >= 3f)
            {
                return new OpponentDecision(random01 < 0.5f
                    ? OpponentShotKind.Net
                    : OpponentShotKind.Drop);
            }

            return new OpponentDecision(OpponentShotKind.Net);
        }
    }
}
