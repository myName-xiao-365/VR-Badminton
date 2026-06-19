using System;

namespace VRBadminton.Input
{
    public interface IBadmintonInputSource : IDisposable
    {
        string Name { get; }
        BadmintonInputSnapshot Snapshot { get; }
        void Start();
        void Stop();
        void Tick(BadmintonInputContext context);
    }
}
