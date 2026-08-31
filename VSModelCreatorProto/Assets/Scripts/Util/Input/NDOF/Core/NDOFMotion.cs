using UnityEngine;

namespace VSMC.NDOF
{
    //So a consumer can tell "just started" from "still going" from "just released" without tracking magnitude itself.
    public enum NDOFMotionState
    {
        Starting,
        InProgress,
        Finishing
    }

    //Boundary between the input backend (INDOFDevice) and viewport navigation - keeps camera code
    //free of HID/raw-input knowledge and keeps input code free of camera knowledge.
    public readonly struct NDOFMotion
    {
        //x = right, y = up, z = forward.
        public readonly Vector3 Translation;

        //x = pitch, y = yaw, z = roll.
        public readonly Vector3 Rotation;

        public readonly float DeltaTime;

        public readonly NDOFMotionState State;

        public NDOFMotion(Vector3 translation, Vector3 rotation, float deltaTime, NDOFMotionState state)
        {
            Translation = translation;
            Rotation = rotation;
            DeltaTime = deltaTime;
            State = state;
        }

        public bool IsZero => Translation == Vector3.zero && Rotation == Vector3.zero;
    }
}
