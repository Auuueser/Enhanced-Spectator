using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using EnhancedSpectator.Logging;

namespace EnhancedSpectator.Features.SplitScreen;

/// <summary>
/// The split-screen's code is compiled ahead, a little each frame while the player is alive. Mono compiles a method
/// the first time it runs, and the split-screen first runs when the player first dies: test5 logged that frame at
/// 87 ms, 33 ms of it preparing the views (deaths after it, running the same code, did not stall).
/// Only this mod's own split-screen, chat and social types are compiled; nothing runs.
/// </summary>
internal sealed class SplitScreenWarmUp
{
    // A soft budget checked between methods; one reflection/JIT call cannot be interrupted midway.
    private const double BudgetMilliseconds = 1;
    private const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
    private readonly Queue<MethodBase> _methods = new();
    private bool _collected;
    private int _compiled;
    private double _milliseconds, _longestStep;

    internal bool Done => _collected && _methods.Count == 0;

    internal void Step()
    {
        var watch = Stopwatch.StartNew();
        if (!_collected) { _collected = true; Collect(); }
        while (_methods.Count > 0 && watch.Elapsed.TotalMilliseconds < BudgetMilliseconds)
        {
            var method = _methods.Dequeue();
            // Getting the entry point is what makes Mono compile the method (RuntimeHelpers.PrepareMethod does not).
            try { method.MethodHandle.GetFunctionPointer(); _compiled++; }
            // One naming another mod's optional type cannot compile without that mod, and never runs without it.
            catch (Exception) { }
        }
        double elapsed = watch.Elapsed.TotalMilliseconds;
        _milliseconds += elapsed; _longestStep = Math.Max(_longestStep, elapsed);
        if (Done) ModLog.Debug($"Split-screen code compiled ahead: {_compiled} methods in {_milliseconds:F0} ms over several frames "
            + $"(including collection; largest step {_longestStep:F1} ms).");
    }

    private void Collect()
    {
        Type?[] types;
        try { types = typeof(SplitScreenWarmUp).Assembly.GetTypes(); }
        catch (ReflectionTypeLoadException partial) { types = partial.Types; }
        foreach (var type in types)
        {
            if (type == null || type.ContainsGenericParameters || !Includes(type)) continue;
            foreach (var method in type.GetMethods(Declared))
                if (!method.IsAbstract && !method.ContainsGenericParameters) _methods.Enqueue(method);
            foreach (var constructor in type.GetConstructors(Declared)) _methods.Enqueue(constructor);
        }
    }

    // Nested (compiler-generated) types go with the type they are declared in.
    private static bool Includes(Type type)
    {
        while (type.DeclaringType != null) type = type.DeclaringType;
        return type.Namespace == "EnhancedSpectator.Features.SplitScreen" || type.Namespace == "EnhancedSpectator.Features.Social"
            || type.Namespace == "EnhancedSpectator.GameInterop"
                && (type.Name.Contains("SplitScreen") || type.Name.Contains("Chat") || type.Name.Contains("Avatar") || type.Name.Contains("Roster"));
    }
}
