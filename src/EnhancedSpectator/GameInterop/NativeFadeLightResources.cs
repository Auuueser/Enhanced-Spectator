using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

namespace EnhancedSpectator.GameInterop
{
    // A global shader binding is not a RenderGraph lifetime dependency. Retain
    // these handles in the owned pass before graph compilation, then bind them
    // during execution. Never resolve/copy GPU buffers on the CPU.
    internal readonly struct NativeFadeLightResources
    {
        private readonly ComputeBufferHandle _cluster, _offsets, _logBase;
        private static readonly int ClusterId = UnityEngine.Shader.PropertyToID("g_vLightListCluster");
        private static readonly int OffsetsId = UnityEngine.Shader.PropertyToID("g_vLayeredOffsetsBuffer");
        private static readonly int LogBaseId = UnityEngine.Shader.PropertyToID("g_logBaseBuffer");

        internal NativeFadeLightResources(ComputeBufferHandle cluster, ComputeBufferHandle offsets, ComputeBufferHandle logBase)
        { _cluster = cluster; _offsets = offsets; _logBase = logBase; }
        internal bool Available => _cluster.IsValid() && _offsets.IsValid();
        internal void Retain(RenderGraphBuilder builder)
        {
            if (_cluster.IsValid()) builder.ReadComputeBuffer(_cluster);
            if (_offsets.IsValid()) builder.ReadComputeBuffer(_offsets);
            if (_logBase.IsValid()) builder.ReadComputeBuffer(_logBase);
        }
        internal void Bind(CommandBuffer cmd)
        {
            if (_cluster.IsValid()) cmd.SetGlobalBuffer(ClusterId, _cluster);
            if (_offsets.IsValid()) cmd.SetGlobalBuffer(OffsetsId, _offsets);
            if (_logBase.IsValid()) cmd.SetGlobalBuffer(LogBaseId, _logBase);
        }
    }
}
