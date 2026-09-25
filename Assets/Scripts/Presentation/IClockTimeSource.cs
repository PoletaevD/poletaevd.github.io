using System;

namespace Clock.Presentation
{
    public interface IClockTimeSource
    {
        bool IsReady { get; }
        DateTime Now { get; }
    }
}
