using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VSMC
{
    /// <summary>
    /// This controls everything to do with editing base ShapeElements properties.
    /// This includes position, rotation, scale, rotation origin, as well as creation, deletion, and renaming.
    /// UI Controls and interactions for this class are split into <see cref="ModelEditorUIElements"/>.
    /// </summary>
    public class ModelEditor : MonoBehaviour
    {
        [Header("Unity References")]
        public CameraController cameraController;
        public ObjectSelector objectSelector;
        public ElementHierarchyManager elementHierarchyManager;
        public ReparentElementOverlay reparentElementOverlay;
        public SetStepparentElementOverlay stepParentOverlay;

        [Header("UI References")]
        public ModelEditorUIElements uiElements;

        public Selectable[] onlyOnModelModeMenubarButtons;

        [Header("Resize Overlay")]
        public TMP_InputField scaleInput;
        public Toggle scaleUVsToggle;
        public Selectable[] scaleButtonsOnlyForSelection;

        enum AlignFacesState { Idle, WaitingForSource, WaitingForTarget }
        AlignFacesState alignFacesState = AlignFacesState.Idle;
        ShapeElement alignFacesSource;
        int alignFacesSourceFace;
        //The element that actually receives the translation. Equal to alignFacesSource on a normal click
        //Ctrl-clicking the source face instead points this at the source's immediate parent, so the
        //whole assembly moves together while the picked child face stays the alignment datum.
        ShapeElement alignFacesMovementRoot;

        private void Start()
        {
            objectSelector.RegisterForObjectSelectedEvent(OnObjectSelected);
            objectSelector.RegisterForObjectDeselectedEvent(OnObjectDeselcted);
            EditModeManager.RegisterForOnModeSelect(OnEditModeSelect);
            EditModeManager.RegisterForOnModeDeselect(OnEditModeDeselect);
            uiElements.HideAllUIElements();
            UndoManager.RegisterForAnyActionDoneOrUndone(OnAnyAction);
        }

        public void OnAnyAction()
        {
            if (EditModeManager.main.cEditMode != VSEditMode.Model) return;
            uiElements.RefreshSelectionValues();
        }

        private void OnObjectSelected(GameObject cSelected)
        {
            foreach (var s in scaleButtonsOnlyForSelection)
            {
                s.interactable = true;
            }
            if (EditModeManager.main.cEditMode != VSEditMode.Model) return;
            uiElements.OnElementSelected(cSelected.GetComponent<ShapeElementGameObject>());
            uiElements.ShowAllUIElements();
        }

        private void OnObjectDeselcted(GameObject deSelected)
        {
            foreach (var s in scaleButtonsOnlyForSelection)
            {
                s.interactable = false;
            }
            if (EditModeManager.main.cEditMode != VSEditMode.Model) return;
            uiElements.HideAllUIElements();
        }

        public void SetSize(EnumAxis axis, float value)
        {
            if (!objectSelector.IsAnySelected()) return;
            ShapeElement cElem = objectSelector.GetCurrentlySelected().GetComponent<ShapeElementGameObject>().element;
            TaskSetElementSize ssTask = new TaskSetElementSize(cElem, axis, value);
            ssTask.DoTask();
            UndoManager.main.CommitTask(ssTask);
        }

        public void SetPosition(EnumAxis axis, float value)
        {
            if (!objectSelector.IsAnySelected()) return;
            ShapeElement cElem = objectSelector.GetCurrentlySelected().GetComponent<ShapeElementGameObject>().element;

            TaskSetElementPosition spTask = new TaskSetElementPosition(cElem, axis, value, !Input.GetKey(KeyCode.LeftControl));
            spTask.DoTask();
            UndoManager.main.CommitTask(spTask);
        }


        public void SetRotationOrigin(EnumAxis axis, float value)
        {
            if (!objectSelector.IsAnySelected()) return;
            ShapeElement cElem = objectSelector.GetCurrentlySelected().GetComponent<ShapeElementGameObject>().element;
            
            TaskSetElementRotationOrigin soTask = new TaskSetElementRotationOrigin(cElem, axis, value);
            soTask.DoTask();
            UndoManager.main.CommitTask(soTask);
        }

        public void SetRotation(EnumAxis axis, float value)
        {
            if (!objectSelector.IsAnySelected()) return;
            ShapeElement cElem = objectSelector.GetCurrentlySelected().GetComponent<ShapeElementGameObject>().element;

            TaskSetElementRotation srTask = new TaskSetElementRotation(cElem, axis, value);
            srTask.DoTask();
            UndoManager.main.CommitTask(srTask);
        }

        void OnEditModeSelect(VSEditMode sel)
        {
            foreach (Selectable s in onlyOnModelModeMenubarButtons)
            {
                s.interactable = sel == VSEditMode.Model;
            }
            if (sel != VSEditMode.Model) return;
            ShapeLoader.main.shapeHolder.ReparentGameObjectsToNoJoints();
            if (!objectSelector.IsAnySelected())
            {
                OnObjectDeselcted(null);
            }
            else
            {
                OnObjectSelected(objectSelector.GetCurrentlySelected());
            }
        }

        void OnEditModeDeselect(VSEditMode desel)
        {
            if (desel != VSEditMode.Model) return;
            CancelAlignFaces();
        }

        void Update()
        {
            if (alignFacesState != AlignFacesState.Idle && Input.GetKeyDown(KeyCode.Escape))
            {
                CancelAlignFaces();
            }
        }

        /// <summary>
        /// Tools > Align Faces. No general "currently selected face" concept exists outside Texture mode's
        /// UV-editing UI, so this prompts for both the source and target face after invoking the command.
        /// Ctrl-clicking the source face uses it as the datum but moves the source's immediate parent
        /// instead, so the whole assembly can be aligned by one of its child faces.
        /// </summary>
        public void StartAlignFaces()
        {
            if (EditModeManager.main.cEditMode != VSEditMode.Model) return;
            CancelAlignFaces();
            alignFacesState = AlignFacesState.WaitingForSource;
            InfoLogger.main.LogText("Align Faces: select source face");
            objectSelector.BeginFacePicking(OnAlignFacesSourcePicked);
        }

        public void CancelAlignFaces()
        {
            if (alignFacesState == AlignFacesState.Idle) return;
            alignFacesState = AlignFacesState.Idle;
            alignFacesSource = null;
            alignFacesMovementRoot = null;
            objectSelector.CancelFacePicking();
            InfoLogger.main.LogText("Align Faces: cancelled");
        }

        void OnAlignFacesSourcePicked(ShapeElement elem, int faceIndex, bool ctrlHeld)
        {
            alignFacesSource = elem;
            alignFacesSourceFace = faceIndex;
            //Ctrl-click moves the parent assembly instead of just the picked element. With no parent to
            //move, there's nothing an assembly-level move could mean beyond moving the source itself.
            alignFacesMovementRoot = (ctrlHeld && elem.ParentElement != null) ? elem.ParentElement : elem;
            alignFacesState = AlignFacesState.WaitingForTarget;
            InfoLogger.main.LogText("Align Faces: select target face");
            objectSelector.BeginFacePicking(OnAlignFacesTargetPicked);
        }

        void OnAlignFacesTargetPicked(ShapeElement targetElem, int targetFace, bool ctrlHeld)
        {
            alignFacesState = AlignFacesState.Idle;

            //Target can't be the movement root itself or a descendant of it - the root dragging the
            //target along would make "align A's face to B's face" have no fixed point to solve for. A
            //target that's an ancestor of the movement root is fine; only the root moves either way.
            if (alignFacesMovementRoot.GetThisAndAllChildrenRecursively().Any(e => e.elementUID == targetElem.elementUID))
            {
                InfoLogger.main.LogText("Align Faces: target can't be the moved element or one of its children");
                return;
            }

            FacePlaneUtil.GetFacePlane(alignFacesSource, alignFacesSourceFace, out Vector3 sourcePoint, out Vector3 sourceNormal);
            FacePlaneUtil.GetFacePlane(targetElem, targetFace, out Vector3 targetPoint, out Vector3 targetNormal);

            if (!AlignFacesMath.TryComputeTranslation(sourcePoint, sourceNormal, targetPoint, targetNormal, out Vector3 worldTranslation))
            {
                InfoLogger.main.LogText("Align Faces: selected faces are not parallel");
                return;
            }

            if (AlignFacesMath.IsNegligible(worldTranslation))
            {
                InfoLogger.main.LogText("Align Faces: faces are already coplanar");
                return;
            }

            //Shifting From and RotationOrigin by the same delta d (below) turns ApplyTransform's
            //T(origin)*R*T(-origin)*T(From) into T(d) * [that] - the moved element's own rotation R
            //cancels out entirely. Only its parent chain's rotation carries into world space, so that's
            //what gets inverted here; the full model matrix would incorrectly skew the movement on a
            //rotated element.
            Matrix4x4 movementRootParentMatrix = FacePlaneUtil.GetParentModelMatrix(alignFacesMovementRoot);
            movementRootParentMatrix.SetColumn(3, new Vector4(0, 0, 0, 1));
            Vector3 localDelta = movementRootParentMatrix.inverse.MultiplyVector(worldTranslation);

            TaskAddToElementPosition task = new TaskAddToElementPosition(
                alignFacesMovementRoot, alignFacesMovementRoot.From, alignFacesMovementRoot.To, alignFacesMovementRoot.RotationOrigin,
                new double[] { localDelta.x, localDelta.y, localDelta.z }, 0, true);
            task.DoTask();
            UndoManager.main.CommitTask(task);

            objectSelector.SelectObject(alignFacesMovementRoot.gameObject.gameObject, false, false);
            InfoLogger.main.LogText("Align Faces: aligned");
        }

        public void CreateNewShapeElement()
        {
            if (EditModeManager.main.cEditMode != VSEditMode.Model) return;
            ShapeElement cElem = null;
            if (objectSelector.IsAnySelected())
            {
                cElem = objectSelector.GetCurrentlySelected().GetComponent<ShapeElementGameObject>().element;
            }

            TaskCreateNewElement cnTask = new TaskCreateNewElement(cElem);
            cnTask.DoTask();
            UndoManager.main.CommitTask(cnTask);
        }
        
        public void CreateNewFaceElement()
        {
            if (EditModeManager.main.cEditMode != VSEditMode.Model) return;
            ShapeElement cElem = null;
            if (objectSelector.IsAnySelected())
            {
                cElem = objectSelector.GetCurrentlySelected().GetComponent<ShapeElementGameObject>().element;
            }

            TaskCreateNewFace cnTask = new TaskCreateNewFace(cElem);
            cnTask.DoTask();
            UndoManager.main.CommitTask(cnTask);
        }

        public void DeleteSelectedShapeElement()
        {
            if (!objectSelector.IsAnySelected()) return;
            ShapeElement cElem = objectSelector.GetCurrentlySelected().GetComponent<ShapeElementGameObject>().element;
                
            TaskDeleteElement deTask = new TaskDeleteElement(cElem);
            deTask.DoTask();
            UndoManager.main.CommitTask(deTask);
        }

        /// <summary>
        /// Renames an element, if possible. Returns either the new name if success, or the old name if failed.
        /// </summary>
        public string RenameElement(string newName)
        {
            //Need to rename the element, but then swap all names in the shape animations too...
            if (!objectSelector.IsAnySelected()) return "";
            ShapeElement cElem = objectSelector.GetCurrentlySelected().GetComponent<ShapeElementGameObject>().element;
            TaskRenameElement renameTask = new TaskRenameElement(cElem, newName);
            renameTask.DoTask();
            UndoManager.main.CommitTask(renameTask);
            return renameTask.newName;
        }

        public void CopyElement()
        {
            if (!objectSelector.IsAnySelected()) return; 
            ShapeElement cElem = objectSelector.GetCurrentlySelected().GetComponent<ShapeElementGameObject>().element;
            TaskCopyElement copyTask = new TaskCopyElement(cElem);
            copyTask.DoTask();
            UndoManager.main.CommitTask(copyTask);
        }

        /// <summary>
        /// Mirrors the selected element and its full child subtree across the model origin on the given
        /// axis, producing an ordinary duplicate rather than a live/linked symmetry.
        /// </summary>
        public void MirrorSelectedElement(EnumAxis axis)
        {
            if (!objectSelector.IsAnySelected()) return;
            ShapeElement cElem = objectSelector.GetCurrentlySelected().GetComponent<ShapeElementGameObject>().element;
            TaskMirrorElement mirrorTask = new TaskMirrorElement(cElem, axis);
            mirrorTask.DoTask();
            UndoManager.main.CommitTask(mirrorTask);
        }

        /// <summary>
        /// Parameterless wrappers around <see cref="MirrorSelectedElement(EnumAxis)"/> for the Tools menu -
        /// every other menu button there calls a void, argument-free method, so these keep Mirror on the
        /// same UnityEvent wiring convention rather than needing an enum-argument persistent call.
        /// </summary>
        public void MirrorSelectedElementX() => MirrorSelectedElement(EnumAxis.X);
        public void MirrorSelectedElementY() => MirrorSelectedElement(EnumAxis.Y);
        public void MirrorSelectedElementZ() => MirrorSelectedElement(EnumAxis.Z);

        public void OpenReparentMenu()
        {
            if (!objectSelector.IsAnySelected()) return;
            reparentElementOverlay.OpenOverlay(objectSelector.GetCurrentlySelected().GetComponent<ShapeElementGameObject>().element);
        }

        public void ReparentElement(int elemToReparentUID, int newParentUID, bool keepElementGlobalTransform)
        {
            TaskReparentElement reTask = new TaskReparentElement(elemToReparentUID, newParentUID, keepElementGlobalTransform);
            reTask.DoTask();
            UndoManager.main.CommitTask(reTask);
        }

        public void OpenStepParentMenu()
        {
            if (!objectSelector.IsAnySelected()) return;
            stepParentOverlay.OpenOverlay(objectSelector.GetCurrentlySelected().GetComponent<ShapeElementGameObject>().element);
        }
    
        public void SetStepParentElement(int elemToSetStepparent, string stepParentCode)
        {
            ShapeElement toChange = ShapeElementRegistry.main.GetShapeElementByUID(elemToSetStepparent);
            ShapeElement t = ShapeElementRegistry.main.GetShapeElementByName(stepParentCode);
            if (t != null)
            {
                //We know that the current selected is a root elem, so we can speed up the check by just finding the topmost parent.
                while (t.ParentElement != null)
                {
                    t = t.ParentElement;
                }
                if (t == toChange)
                {
                    //No.
                    uiElements.RefreshSelectionValues();
                    return;
                }
            }
            TaskSetElementStepparent setStepparentTask = new TaskSetElementStepparent(toChange, stepParentCode);
            setStepparentTask.DoTask();
            UndoManager.main.CommitTask(setStepparentTask);
        }

        public void SetStepParentElement(string stepParentCode)
        {
            //Ugh. Need to ensure that the new step parent is not a child of the selected.
            ShapeElement t = ShapeElementRegistry.main.GetShapeElementByName(stepParentCode);
            if (t != null)
            {
                //We know that the current selected is a root elem, so we can speed up the check by just finding the topmost parent.
                while (t.ParentElement != null)
                {
                    t = t.ParentElement;
                }
                if (t == objectSelector.GetCurrentlySelected().GetComponent<ShapeElementGameObject>().element)
                {
                    //No.
                    InfoLogger.main.LogText("Cannot set step-parent as selected object has a child with the given code.");
                    uiElements.RefreshSelectionValues();
                    return;
                }
            }
            TaskSetElementStepparent setStepparentTask = new TaskSetElementStepparent(objectSelector.GetCurrentlySelected().GetComponent<ShapeElementGameObject>().element, stepParentCode);
            setStepparentTask.DoTask();
            UndoManager.main.CommitTask(setStepparentTask);
        }

        public void SetRenderPass(int value)
        {
            ShapeElement sel = objectSelector.GetCurrentlySelected().GetComponent<ShapeElementGameObject>().element;
            if (sel.RenderPass == value) return;
            TaskSetElementRenderPass setRenderPassTask = new TaskSetElementRenderPass(sel, value);
            setRenderPassTask.DoTask();
            UndoManager.main.CommitTask(setRenderPassTask);
        }

        public void GenerateSnowLayers()
        {
            if (EditModeManager.main.cEditMode != VSEditMode.Model) return;

            TaskGenerateSnowLayer genSnowTask = new TaskGenerateSnowLayer();
            genSnowTask.DoTask();
            UndoManager.main.CommitTask(genSnowTask);
        }

        public void ScaleSelected()
        {
            TaskResizeElement resize = new TaskResizeElement(null, true, 2f, false);
            resize.DoTask();
            UndoManager.main.CommitTask(resize);
        }

        public void ResizeElements(int resizeOption)
        {
            float resizeAmount = 0;
            if (!float.TryParse(scaleInput.text, out resizeAmount))
            {
                InfoLogger.main.LogText("Cannot perform resize - Invalid scale given.");
                return;
            }
            if (resizeOption <= 1 && !ObjectSelector.main.IsAnySelected())
            {
                InfoLogger.main.LogText("Cannot perform resize - No object selected.");
                return;
            }
            else if (resizeOption <= 1)
            {
                TaskResizeElement resizeTask = new TaskResizeElement(ObjectSelector.main.GetCurrentlySelected().GetComponent<ShapeElementGameObject>().element, resizeOption == 1, resizeAmount, scaleUVsToggle.isOn);
                resizeTask.DoTask();
                UndoManager.main.CommitTask(resizeTask);
                return;
            }
            TaskResizeElement resizeAllTask = new TaskResizeElement(null, true, resizeAmount, scaleUVsToggle.isOn);
            resizeAllTask.DoTask();
            UndoManager.main.CommitTask(resizeAllTask);
        }
    }
}