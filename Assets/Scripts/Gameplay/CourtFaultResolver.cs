namespace VRBadminton.Gameplay
{
    public enum CourtFaultKind
    {
        Net
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
    }
}
