namespace EnhancedSpectator.GameInterop
{

    // HDRP lighting uses the low eight rendering-layer bits. High bits are temporary
    // filtering tags on owned clone renderers only, restored after each camera.
    internal sealed class NativeFadeSlots
    {
        internal const uint LightLayers = 0xff;
        private uint _used;
        internal uint Acquire()
        {
            for (int index = 8; index < 32; index++)
            {
                uint bit = 1u << index;
                if ((_used & bit) != 0) continue;
                _used |= bit; return bit;
            }
            return 0;
        }
        internal void Release(uint bit) => _used &= ~bit;
        internal static uint Tagged(uint original, uint bit) => (original & LightLayers) | bit;
    }
}
