using UnityEngine;

namespace VSMC.NDOF
{
    //No Windows/HID dependency here on purpose, so this stays unit-testable and reusable by a non-Windows backend later.
    public static class NDOFNormalizer
    {
        //Nominal 3Dconnexion full-deflection raw range (also what Blender's GHOST_NDOFManager assumes).
        //Output is clamped either way, so a device that differs slightly just saturates a bit early/late,
        //not wrong.
        public const float NominalRawRange = 350f;

        public static float NormalizeAndDeadZone(float raw, float deadZone)
        {
            float normalized = Mathf.Clamp(raw / NominalRawRange, -1f, 1f);
            return ApplyDeadZone(normalized, deadZone);
        }

        //Rescales the surviving range back out to [-1, 1] past the dead zone, so there's no jump right at the boundary.
        public static float ApplyDeadZone(float normalized, float deadZone)
        {
            deadZone = Mathf.Clamp01(deadZone);
            float magnitude = Mathf.Abs(normalized);
            if (magnitude <= deadZone) return 0f;
            if (deadZone >= 1f) return 0f;

            float rescaled = (magnitude - deadZone) / (1f - deadZone);
            return Mathf.Sign(normalized) * Mathf.Clamp01(rescaled);
        }
    }
}
