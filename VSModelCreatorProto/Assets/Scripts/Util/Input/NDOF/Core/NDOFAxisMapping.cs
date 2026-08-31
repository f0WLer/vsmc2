using UnityEngine;

namespace VSMC.NDOF
{
    //Only place a sign flip or axis reorder for NDOF input should live - if a device's mapping is wrong,
    //fix it here, not in camera code.
    //
    //Device axes (raw HID): Tx, Ty, Tz translation; Rx, Ry, Rz rotation.
    //VSMC2 axes (what NDOFMotion carries): translation x=right, y=up, z=forward; rotation x=pitch, y=yaw,
    //z=roll (roll unused - see CameraController.ApplyNDOFMotion).
    //
    //Defaults below came from testing against a real device, not a spec: on that device push/pull reports
    //on raw Ty and up/down reports on raw Tz (opposite of the initial guess), and twist reports on raw Rz
    //instead of Ry. A different device may need different swap/invert values.
    public static class NDOFAxisMapping
    {
        public static bool InvertTx = false;
        public static bool InvertTy = true;
        public static bool InvertTz = false;

        public static bool InvertRx = true;
        public static bool InvertRy = true;
        public static bool InvertRz = false;

        public static bool SwapTranslationYZ = true;
        public static bool SwapRotationYZ = true;

        public static Vector3 MapTranslation(float tx, float ty, float tz)
        {
            if (SwapTranslationYZ) (ty, tz) = (tz, ty);

            if (InvertTx) tx = -tx;
            if (InvertTy) ty = -ty;
            if (InvertTz) tz = -tz;

            return new Vector3(tx, ty, tz);
        }

        public static Vector3 MapRotation(float rx, float ry, float rz)
        {
            if (SwapRotationYZ) (ry, rz) = (rz, ry);

            if (InvertRx) rx = -rx;
            if (InvertRy) ry = -ry;
            if (InvertRz) rz = -rz;

            return new Vector3(rx, ry, rz);
        }
    }
}
