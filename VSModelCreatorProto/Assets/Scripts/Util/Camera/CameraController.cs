using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace VSMC
{
    /// <summary>
    /// This should be attached to the camera anchor. The camera itself will rotate around this.
    /// This is a very primitive camera controller. It has no smoothing or other such QOL features.
    /// </summary>
    public class CameraController : ISceneRaycaster
    {

        enum CameraMode
        {
            Orbital = 0,
            Free = 1
        }

        [Header("Unity References")]
        public GameObject cameraChild;
        public GameObject pivotChild;
        public GameObject cameraCompass;
        public RawImage sceneViewRawImage;

        [Header("Button References")]
        public TMP_Text cameraModeButtonText;
        public TMP_Text uiControlledSpeedButtonText;

        float rotX = 30;
        float rotY = 30;

        Vector3 cameraAnchorPos = new Vector3(0.5f, 0.5f, 0.5f);
        float distFromAnchor = 2;

        [Header("Orbital Settings")]
        public Vector2 minMaxRotX;
        public Vector2 minMaxDistance;

        [Header("Speeds")]
        public float movementSpeed = 0.1f;
        public float rotationSpeed = 0.5f;
        public float zoomSpeed = 0.2f;
        public float keyboardZoomModifier = 0.05f;

        public float uiControlledSpeedMultiplier = 1f;
        public float shiftControlledSpeedMultiplier = 5f;

        [Header("SpaceMouse / NDOF Settings")]
        [Tooltip("Overall multiplier applied on top of translation/rotation speed below and the existing shift/UI speed multipliers.")]
        public float ndofMasterSensitivity = 4f;
        [Tooltip("World units/second of pan at distFromAnchor == 1 (Orbital mode); scales with current view distance, see ApplyNDOFMotion.")]
        public float ndofPanSpeed = 1.2f;
        [Tooltip("Units/second for dolly (Orbital: distFromAnchor: Free: world-space movement).")]
        public float ndofDollySpeed = 3.5f;
        [Tooltip("Degrees/second of orbit rotation at full puck deflection.")]
        public float ndofRotationSpeed = 140f;

        public ModelEditor editor;
        InputAction mousePosAction;

        bool lmbDown = false;
        bool hasMovedSinceLmbDown = false;
        Vector2 storedLmbOnDown = Vector2.zero;
        bool rmbDown = false;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            mousePosAction = InputSystem.actions.FindAction("Look");
            uiControlledSpeedButtonText.text = uiControlledSpeedMultiplier.ToString("0.##") + "x Camera Speed";
            cameraModeButtonText.text = "Camera Mode: " + CurrentCameraMode.ToString();
        }

        // Update is called once per frame
        void Update()
        {
            DoMouseUpdates();
            DoKeyboardUpdates();
            gameObject.transform.localPosition = cameraAnchorPos;
            gameObject.transform.localEulerAngles = new Vector3(rotX, rotY, 0);

            cameraChild.transform.localPosition = new Vector3(0, 0, CurrentCameraMode == CameraMode.Orbital ? -distFromAnchor : 0);
            cameraCompass.transform.localEulerAngles = new Vector3(0, 0, rotY);
            if (pivotChild != null)
            {
                pivotChild.SetActive(CurrentCameraMode == CameraMode.Orbital && distFromAnchor > 0.1f); //disable pivot at close distance
                pivotChild.transform.rotation = Quaternion.identity;
            }
        }

        public override bool OnSceneViewMouseDown(Vector2 mouseClickScenePositionForCamera, PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left)
            {
                hasMovedSinceLmbDown = false;
                storedLmbOnDown = Input.mousePosition;
                lmbDown = true;
                return false;
            }
            else if (data.button == PointerEventData.InputButton.Right) rmbDown = true;
            return true;
        }


        public override bool OnSceneViewMouseUp(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left)
            {
                //This may be a useless check, but I'd like to add it just in case.
                if (lmbDown)
                {
                    lmbDown = false;
                    //if the user has moved the camera, make sure the object selection does not kick in.
                    return hasMovedSinceLmbDown;
                }
            }
            else if (data.button == PointerEventData.InputButton.Right)
            {
                rmbDown = false;
                return true;
            }
            return false;
        }

        public override bool OnSceneViewMouseScroll(PointerEventData data)
        {
            if (CurrentCameraMode == CameraMode.Orbital)
            {
                distFromAnchor -= data.scrollDelta.y * zoomSpeed * GetTotalSpeedMultiplier();
                distFromAnchor = Mathf.Clamp(distFromAnchor, minMaxDistance.x, minMaxDistance.y);
            }
            else
            {
                cameraAnchorPos += data.scrollDelta.y * zoomSpeed * (cameraChild.transform.forward) * GetTotalSpeedMultiplier();
            }
            return true;
        }

        void DoMouseUpdates()
        {
            Vector2 mouseMovement = mousePosAction.ReadValue<Vector2>();
            if (lmbDown)
            {
                //Using 'transform.right' and up here allow us to move the camera anchor in reference to the camera's angle.
                if (mouseMovement.sqrMagnitude >= Mathf.Epsilon)
                {
                    
                    cameraAnchorPos -= cameraChild.transform.right * mouseMovement.x * movementSpeed * GetTotalSpeedMultiplier();
                    cameraAnchorPos -= cameraChild.transform.up * mouseMovement.y * movementSpeed * GetTotalSpeedMultiplier();
                    if (!storedLmbOnDown.IsNearlyEqual(Input.mousePosition, 3)) //Allow a tiny amount of movement before blocking further interaction.
                    {
                        hasMovedSinceLmbDown = true;
                    }
                }
            }

            if (rmbDown)
            {
                rotY = (rotY + (mouseMovement.x * rotationSpeed * GetTotalSpeedMultiplier())) % 360;
                rotX -= (mouseMovement.y * rotationSpeed * GetTotalSpeedMultiplier());
            }

            rotX = Mathf.Clamp(rotX, minMaxRotX.x, minMaxRotX.y);
        }

        public void DoKeyboardUpdates()
        {
            float scrollValue = Input.GetAxis("keyboardZoom");
            if (CurrentCameraMode == CameraMode.Orbital)
            {
                distFromAnchor -= scrollValue * zoomSpeed * keyboardZoomModifier * GetTotalSpeedMultiplier();
                distFromAnchor = Mathf.Clamp(distFromAnchor, minMaxDistance.x, minMaxDistance.y);
                if (Input.GetKeyDown(KeyCode.F) && (EventSystem.current.currentSelectedGameObject?.GetComponent<TMP_InputField>() == null))
                {
                    FocusOnSelected();
                }
            }
            else
            {
                cameraAnchorPos += scrollValue * zoomSpeed * keyboardZoomModifier * (cameraChild.transform.forward) * GetTotalSpeedMultiplier();
            }
        }
        
        //Drives the same pivot/rotation/distance fields mouse navigation uses, so the two stay
        //interchangeable rather than fighting over separate camera state. All three axis groups apply
        //unconditionally in one call, which is what makes simultaneous pan+orbit+dolly work.
        //
        //Rotation mirrors RMB-drag orbit (turntable-style pitch/yaw) since that's the only rotation
        //representation this camera has - no roll field exists for mouse orbit to set or clear, so giving
        //the SpaceMouse one would let it apply a rotation the mouse could never undo. rotation.z is left
        //unused for that reason.
        //
        //Pan scales with distFromAnchor (Orbital only) so a fixed puck deflection covers more world space
        //when zoomed out and stays fine zoomed in, reusing the existing distance value instead of adding
        //a separate zoom-scale concept.
        //
        //Dolly only ever changes distFromAnchor (Orbital) or moves along view-forward (Free), same as the
        //scroll wheel already does, so it never touches the pivot.
        public void ApplyNDOFMotion(Vector3 translation, Vector3 rotation, float deltaTime)
        {
            if (deltaTime <= 0f) return;
            float speedMul = GetTotalSpeedMultiplier() * ndofMasterSensitivity;

            if (rotation.x != 0f || rotation.y != 0f)
            {
                rotY = (rotY + rotation.y * ndofRotationSpeed * speedMul * deltaTime) % 360;
                rotX = Mathf.Clamp(rotX - rotation.x * ndofRotationSpeed * speedMul * deltaTime, minMaxRotX.x, minMaxRotX.y);
            }

            if (translation.x != 0f || translation.y != 0f)
            {
                float panDistanceScale = CurrentCameraMode == CameraMode.Orbital ? Mathf.Max(distFromAnchor, 0.05f) : 1f;
                cameraAnchorPos -= cameraChild.transform.right * translation.x * ndofPanSpeed * panDistanceScale * speedMul * deltaTime;
                cameraAnchorPos -= cameraChild.transform.up * translation.y * ndofPanSpeed * panDistanceScale * speedMul * deltaTime;
            }

            if (translation.z != 0f)
            {
                if (CurrentCameraMode == CameraMode.Orbital)
                {
                    distFromAnchor = Mathf.Clamp(distFromAnchor - translation.z * ndofDollySpeed * speedMul * deltaTime, minMaxDistance.x, minMaxDistance.y);
                }
                else
                {
                    cameraAnchorPos += translation.z * ndofDollySpeed * speedMul * (cameraChild.transform.forward) * deltaTime;
                }
            }
        }

        public void FocusOnSelected()
        {
            if (ObjectSelector.main.IsAnySelected())
            {
                cameraAnchorPos = ObjectSelector.main.GetCurrentlySelected().transform.position;
            }
        }

        public void SwapCameraType()
        {
            if (CurrentCameraMode == CameraMode.Orbital)
            {
                CurrentCameraMode = CameraMode.Free;
                cameraAnchorPos -= (cameraChild.transform.forward) * distFromAnchor;
            }
            else
            {
                CurrentCameraMode = CameraMode.Orbital;
                cameraAnchorPos += (cameraChild.transform.forward) * distFromAnchor;
            }
            InfoLogger.main.LogText("Camera mode has been changed to " + CurrentCameraMode.ToString());
            cameraModeButtonText.text = "Camera Mode: "+CurrentCameraMode.ToString();
        }

        public void ResetCamera()
        {
            rotX = 0;
            rotY = 0;
            distFromAnchor = 10;
            cameraAnchorPos = Vector3.zero;
        }

        CameraMode CurrentCameraMode
        {
            get
            {
                return (CameraMode)ProgramPreferences.CurrentCameraMode.GetValue();
            }
            set
            {
                ProgramPreferences.CurrentCameraMode.SetValue((int)value);
            }
        }

        public void UIControlledSpeedButtonPressed()
        {
            uiControlledSpeedMultiplier += Input.GetKey(KeyCode.LeftShift) ? -0.25f : 0.25f;
            if (uiControlledSpeedMultiplier > 2) uiControlledSpeedMultiplier = 0;
            else if (uiControlledSpeedMultiplier < 0) uiControlledSpeedMultiplier = 2;
            uiControlledSpeedButtonText.text = uiControlledSpeedMultiplier.ToString("0.##") + "x Camera Speed";
        }

        public float GetShiftHeldSpeedMultiplier()
        {
            return Input.GetKey(KeyCode.LeftShift) ? shiftControlledSpeedMultiplier : 1f;
        }

        public float GetTotalSpeedMultiplier()
        {
            return GetShiftHeldSpeedMultiplier() * uiControlledSpeedMultiplier;
        }

    }
}