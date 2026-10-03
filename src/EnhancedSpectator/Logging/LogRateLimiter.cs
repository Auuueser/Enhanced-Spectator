using System;
using System.Collections.Generic;

namespace EnhancedSpectator.Logging;

// Call-site keys keep changing player IDs and exception text from bypassing the limit.
internal sealed class LogRateLimiter
{
    private sealed class Window
    {
        internal double Started;
        internal int Written, Suppressed;
    }
    private readonly Dictionary<string, Window> _windows = new Dictionary<string, Window>();
    private readonly int _burst, _capacity;
    private readonly double _seconds;
    internal LogRateLimiter(int burst, double seconds, int capacity)
    { _burst = burst; _seconds = seconds; _capacity = capacity; }

    internal bool Allow(string key, double now, out int suppressed)
    {
        suppressed = 0;
        if (!_windows.TryGetValue(key, out var window))
        {
            if (_windows.Count >= _capacity)
            {
                string? oldest = null; double time = double.MaxValue;
                foreach (var pair in _windows)
                    if (pair.Value.Started < time) { oldest = pair.Key; time = pair.Value.Started; }
                if (oldest != null) _windows.Remove(oldest);
            }
            window = new Window { Started = now };
            _windows.Add(key, window);
        }
        if (now < window.Started || now - window.Started >= _seconds)
        {
            suppressed = window.Suppressed;
            window.Started = now; window.Written = 0; window.Suppressed = 0;
        }
        if (window.Written >= _burst)
        {
            if (window.Suppressed < int.MaxValue) window.Suppressed++;
            return false;
        }
        window.Written++;
        return true;
    }
    internal int Count => _windows.Count;
    internal void Clear() => _windows.Clear();
}
