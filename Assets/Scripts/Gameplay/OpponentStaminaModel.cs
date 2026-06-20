using UnityEngine;

namespace VRBadminton.Gameplay
{
    public static class OpponentStaminaModel
    {
        public static float ShotCost(OpponentShotKind shot)
        {
            switch (shot)
            {
                case OpponentShotKind.Clear:
                    return 5f;
                case OpponentShotKind.Smash:
                    return 10f;
                case OpponentShotKind.Net:
                case OpponentShotKind.Drop:
                case OpponentShotKind.Lift:
                    return 3f;
                default:
                    return 0f;
            }
        }

        public static bool CanAfford(float stamina, OpponentShotKind shot)
        {
            return stamina >= ShotCost(shot);
        }

        public static float SpendShot(float stamina, OpponentShotKind shot)
        {
            return Mathf.Max(0f, stamina - ShotCost(shot));
        }

        public static float SpendRun(
            float stamina,
            Vector3 from,
            Vector3 to,
            float staminaPerMeter)
        {
            Vector2 fromGround = new Vector2(from.x, from.z);
            Vector2 toGround = new Vector2(to.x, to.z);
            float distance = Vector2.Distance(fromGround, toGround);
            return Mathf.Max(0f, stamina - distance * staminaPerMeter);
        }
    }
}
