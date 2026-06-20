using System;

namespace VRBadminton.Gameplay
{
    public readonly struct MatchRules
    {
        public MatchRules(int scoreTarget, int scoreCap)
        {
            if (scoreTarget <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(scoreTarget));
            }

            if (scoreCap < scoreTarget)
            {
                throw new ArgumentOutOfRangeException(nameof(scoreCap));
            }

            ScoreTarget = scoreTarget;
            ScoreCap = scoreCap;
        }

        public int ScoreTarget { get; }

        public int ScoreCap { get; }

        public static MatchRules FifteenPoint => new MatchRules(15, 21);

        public static MatchRules TwentyOnePoint => new MatchRules(21, 30);

        public int GetWinner(int playerScore, int opponentScore)
        {
            int highestScore = Math.Max(playerScore, opponentScore);
            int lead = Math.Abs(playerScore - opponentScore);
            bool reachedCap = highestScore >= ScoreCap;
            bool wonByTwo = highestScore >= ScoreTarget && lead >= 2;
            if (!reachedCap && !wonByTwo)
            {
                return 0;
            }

            return playerScore > opponentScore ? 1 : 2;
        }

        public static float ServiceSideForScore(int score)
        {
            return score % 2 == 1 ? -1f : 1f;
        }
    }

    public struct MatchState
    {
        public int PlayerScore;
        public int OpponentScore;
        public bool PlayerServing;
        public int RallyWinner;
        public int MatchWinner;
        public bool MatchOver;

        public void ApplyRallyWinner(int rallyWinner, MatchRules rules)
        {
            RallyWinner = rallyWinner;
            if (rallyWinner == 1)
            {
                PlayerScore++;
                PlayerServing = true;
            }
            else if (rallyWinner == 2)
            {
                OpponentScore++;
                PlayerServing = false;
            }

            MatchWinner = rules.GetWinner(PlayerScore, OpponentScore);
            MatchOver = MatchWinner != 0;
        }
    }
}
