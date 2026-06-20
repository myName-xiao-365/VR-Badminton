using System;

namespace VRBadminton.Gameplay
{
    public readonly struct DifficultyTuning
    {
        public DifficultyTuning(
            int level,
            float opponentMaxStamina,
            float opponentSmashChance,
            float opponentSmashReceiveChance)
        {
            Level = level;
            OpponentMaxStamina = opponentMaxStamina;
            OpponentSmashChance = opponentSmashChance;
            OpponentSmashReceiveChance = opponentSmashReceiveChance;
        }

        public int Level { get; }

        public float OpponentMaxStamina { get; }

        public float OpponentSmashChance { get; }

        public float OpponentSmashReceiveChance { get; }

        public static DifficultyTuning ForLevel(int requestedLevel)
        {
            int level = Math.Max(0, Math.Min(5, requestedLevel));
            switch (level)
            {
                case 0:
                    return new DifficultyTuning(level, 100f, 0f, 0.05f);
                case 1:
                    return new DifficultyTuning(level, 50f, 0.25f, 0.2f);
                case 2:
                    return new DifficultyTuning(level, 70f, 0.5f, 0.35f);
                case 3:
                    return new DifficultyTuning(level, 100f, 0.75f, 0.5f);
                case 4:
                    return new DifficultyTuning(level, 200f, 1f, 0.75f);
                default:
                    return new DifficultyTuning(level, 500f, 1f, 1f);
            }
        }
    }
}
