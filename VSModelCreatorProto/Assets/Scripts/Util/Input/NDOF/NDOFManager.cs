using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace VSMC.NDOF
{
    //Feeds mapped NDOFMotion straight into CameraController, the same state mouse navigation drives -
    //deliberately no separate SpaceMouse camera state to keep the two in sync.
    public class NDOFManager : MonoBehaviour
    {
        [Header("Unity References")]
        public CameraController cameraController;

        [Header("Sensitivity")]
        [Tooltip("Normalized magnitude below which an axis is treated as at rest.")]
        [Range(0f, 0.5f)] public float deadZone = 0.10f;

        bool wasActiveLastFrame = false;
        float lastSampleTime;

        INDOFDevice device;

        void OnEnable()
        {
            device = new WindowsRawInputNDOFDevice();
            lastSampleTime = Time.unscaledTime;
            if (!device.IsAvailable)
            {
                //Expected in the Editor and on machines with no device - not an error.
                Debug.Log("[NDOF] SpaceMouse backend unavailable on this platform/build - NDOF navigation disabled, mouse navigation unaffected.");
            }
        }

        void OnDisable()
        {
            device?.Dispose();
            device = null;
        }

        void Update()
        {
            if (device == null || !device.IsAvailable) return;

            device.Poll();
            float now = Time.unscaledTime;
            float deltaTime = Mathf.Max(now - lastSampleTime, 0f);
            lastSampleTime = now;

            if (!device.TryGetLatestSample(out NDOFRawSample raw)) return;

            Vector3 translation = raw.TranslationFresh
                ? NDOFAxisMapping.MapTranslation(
                    NDOFNormalizer.NormalizeAndDeadZone(raw.Tx, deadZone),
                    NDOFNormalizer.NormalizeAndDeadZone(raw.Ty, deadZone),
                    NDOFNormalizer.NormalizeAndDeadZone(raw.Tz, deadZone))
                : Vector3.zero;

            Vector3 rotation = raw.RotationFresh
                ? NDOFAxisMapping.MapRotation(
                    NDOFNormalizer.NormalizeAndDeadZone(raw.Rx, deadZone),
                    NDOFNormalizer.NormalizeAndDeadZone(raw.Ry, deadZone),
                    NDOFNormalizer.NormalizeAndDeadZone(raw.Rz, deadZone))
                : Vector3.zero;

            bool isActive = translation != Vector3.zero || rotation != Vector3.zero;

            if (!isActive && !wasActiveLastFrame)
            {
                //Avoids calling into the camera with an all-zero motion every idle frame.
                return;
            }

            NDOFMotionState state = !wasActiveLastFrame ? NDOFMotionState.Starting
                : isActive ? NDOFMotionState.InProgress
                : NDOFMotionState.Finishing;
            wasActiveLastFrame = isActive;

            if (!IsNavigationAllowedRightNow()) return;

            NDOFMotion motion = new NDOFMotion(translation, rotation, deltaTime, state);
            if (cameraController != null)
            {
                cameraController.ApplyNDOFMotion(motion.Translation, motion.Rotation, motion.DeltaTime);
            }
        }

        //VSMC2 has one 3D viewport and mouse nav isn't gated on hover/focus, so the only real risk is
        //a UI text field claiming keyboard input - same check the 'F to focus' shortcut already uses.
        static bool IsNavigationAllowedRightNow()
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null) return true;
            return selected.GetComponent<TMP_InputField>() == null;
        }
    }
}
