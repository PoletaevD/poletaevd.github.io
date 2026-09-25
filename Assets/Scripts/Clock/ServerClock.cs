using System;
using System.Diagnostics;

namespace Clock
{
    public sealed class ServerClock
    {
        private readonly Stopwatch _elapsed = new Stopwatch();
        private DateTimeOffset _referenceUtc;

        public bool IsInitialized { get; private set; }

        public DateTimeOffset UtcNow
        {
            get
            {
                if (!IsInitialized)
                {
                    throw new InvalidOperationException("Set server time before reading the clock.");
                }

                return _referenceUtc.AddTicks(_elapsed.Elapsed.Ticks);
            }
        }

        public void SetUtcTime(DateTimeOffset serverTime)
        {
            _referenceUtc = serverTime.ToUniversalTime();
            _elapsed.Restart();
            IsInitialized = true;
        }
    }
}
