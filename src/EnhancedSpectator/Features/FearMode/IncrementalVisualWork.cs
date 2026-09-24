using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace EnhancedSpectator.Features.FearMode;

/// <summary>Cooperative main-thread budget; one Unity operation cannot be preempted.</summary>
internal sealed class IncrementalVisualWork : IDisposable
{
    private readonly IEnumerator<bool> _steps;
    internal bool Done { get; private set; }
    internal Exception? Failure { get; private set; }
    internal double TotalMilliseconds { get; private set; }
    internal double MaxStepMilliseconds { get; private set; }
    internal int Slices { get; private set; }
    internal IncrementalVisualWork(IEnumerator<bool> steps) => _steps = steps;
    internal void Advance(double budgetMilliseconds = 1.5, int maxOperations = 16)
    {
        if (Done || maxOperations <= 0) return;
        long start = Stopwatch.GetTimestamp();
        try
        {
            for (int i = 0; i < maxOperations; i++)
            {
                if (!_steps.MoveNext()) { Dispose(); break; }
                if (!_steps.Current) break;
                if (Milliseconds(start) >= budgetMilliseconds) break;
            }
        }
        catch (Exception ex) { Failure = ex; Dispose(); }
        finally
        {
            double elapsed = Milliseconds(start);
            TotalMilliseconds += elapsed; MaxStepMilliseconds = Math.Max(MaxStepMilliseconds, elapsed); Slices++;
        }
    }
    private static double Milliseconds(long start) => (Stopwatch.GetTimestamp()-start)*1000d/Stopwatch.Frequency;
    public void Dispose() { if (Done) return; Done = true; _steps.Dispose(); }
}

internal sealed class RuntimeVisualBuild : IDisposable
{
    internal readonly IncrementalVisualWork Work;
    internal RuntimeEnemyVisual? Visual;
    internal string Reason = string.Empty;
    internal RuntimeVisualBuild(Func<RuntimeVisualBuild,IEnumerator<bool>> steps) => Work = new IncrementalVisualWork(steps(this));
    internal RuntimeEnemyVisual? Take() { var result = Visual; Visual = null; return result; }
    public void Dispose() { Work.Dispose(); Visual?.Dispose(); Visual = null; }
}
