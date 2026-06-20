using UnityEngine;

namespace VRBadminton.Gameplay
{
    public enum CourtFaultKind
    {
        None,
        Net,
        Out
    }

    public readonly struct CourtFaultResult
    {
        public CourtFaultResult(CourtFaultKind kind, int rallyWinner)
        {
            Kind = kind;
            RallyWinner = rallyWinner;
        }

        public CourtFaultKind Kind { get; }

        public int RallyWinner { get; }
    }

    public static class CourtFaultResolver
    {
        public static CourtFaultResult ResolveNetFault(int currentFlightHitter)
        {
            return new CourtFaultResult(
                CourtFaultKind.Net,
                currentFlightHitter == 1 ? 2 : 1);
        }

        public static CourtFaultResult ResolveLanding(
            Vector3 landing,
            float courtHalfWidth,
            float frontZ,
            float backZ,
            int currentFlightHitter)
        {
            bool outOfBounds =
                Mathf.Abs(landing.x) > courtHalfWidth ||
                landing.z < frontZ ||
                landing.z > backZ;
            if (!outOfBounds)
            {
                return new CourtFaultResult(CourtFaultKind.None, 0);
            }

            return new CourtFaultResult(
                CourtFaultKind.Out,
                currentFlightHitter == 1 ? 2 : 1);
        }
    }
}
