using System;

namespace VSMC.NDOF
{
    //Translation/rotation get separate "fresh" flags because SpaceMouse HID reports arrive as two
    //independent packets - a backend shouldn't claim both are current just because one of them is.
    public struct NDOFRawSample
    {
        public float Tx, Ty, Tz;
        public bool TranslationFresh;

        public float Rx, Ry, Rz;
        public bool RotationFresh;
    }

    //Keeps OS/HID specifics (Windows Raw Input now, maybe Linux later) out of anything that touches the camera.
    public interface INDOFDevice : IDisposable
    {
        bool IsAvailable { get; }

        void Poll();

        bool TryGetLatestSample(out NDOFRawSample sample);
    }
}
