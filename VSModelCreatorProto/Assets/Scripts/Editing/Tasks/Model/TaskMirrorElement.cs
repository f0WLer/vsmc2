using UnityEngine;

namespace VSMC
{
    /// <summary>
    /// Duplicates an element and its children, then reflects the duplicate across the grid center on the
    /// given axis - a true geometric mirror, not the 180-degree "rotor" workaround.
    /// </summary>
    public class TaskMirrorElement : IEditTask
    {
        public int copyUID = -1;
        public ShapeElement createdElement;

        public TaskMirrorElement(ShapeElement toMirror, EnumAxis axis)
        {
            // Same eager-build-then-park pattern as TaskCopyElement: everything happens once here so
            // DoTask/UndoTask only need to toggle visibility/registration, keeping this a single undo step
            // for the whole subtree.
            createdElement = toMirror.CopyThisElement();
            copyUID = toMirror.elementUID;

            // GetParentPath is empty for a root and walks stepparents as well as parents, so it needs no
            // guard, and guarding on ParentElement would skip a stepparented root's real chain, leaving
            // the reflection to be computed in the wrong frame.
            Matrix4x4 parentWorld = Matrix4x4.identity;
            foreach (ShapeElement pathElem in toMirror.GetParentPath())
            {
                parentWorld = pathElem.ApplyTransform(parentWorld);
            }

            // Mirror the copy's FacesResolved directly (face slot swaps, UV flips) rather than re-resolving
            // from the private Faces dict, which would discard those changes.
            MirrorMath.MirrorSubtreeInPlace(createdElement, parentWorld, axis);

            foreach (ShapeElement elem in createdElement.GetThisAndAllChildrenRecursively())
            {
                ShapeTesselator.RecreateMeshesForShapeElement(elem);
            }
        }

        public override void DoTask()
        {
            ShapeElement orig = ShapeElementRegistry.main.GetShapeElementByUID(copyUID);
            if (orig.ParentElement != null)
            {
                createdElement.SetParent(orig.ParentElement);
            }
            else
            {
                ShapeHolder.CurrentLoadedShape.AddRootShapeElement(createdElement);
            }

            ShapeElementRegistry.main.ReregisterShapeElement(createdElement, true);
            ShapeLoader.main.shapeHolder.RestoreElementFromDeletionLimbo(createdElement, true);
            ShapeTesselator.ResolveMatricesForShapeElementAndChildren(createdElement);

            // RegenerateMeshFromMeshData() only rebuilds the single element it's called on, so every
            // mirrored element - not just the root - needs its own rebuild here to pick up the face/UV
            // changes from the constructor.
            foreach (ShapeElement elem in createdElement.GetThisAndAllChildrenRecursively())
            {
                elem.gameObject.RegenerateMeshFromMeshData();
            }
            createdElement.gameObject.ReapplyTransformsFromMeshData(true);

            ElementHierarchyManager.ElementHierarchy.StartCreatingElementPrefabs(ShapeHolder.CurrentLoadedShape);

            ObjectSelector.main.SelectObject(createdElement.gameObject.gameObject, false, false);
        }

        public override void UndoTask()
        {
            ShapeLoader.main.shapeHolder.SendElementToDeletionLimbo(createdElement, true);
            ShapeElementRegistry.main.UnregisterShapeElement(createdElement, true);
            if (createdElement.ParentElement != null)
            {
                createdElement.RemoveParent();
            }
            else
            {
                ShapeHolder.CurrentLoadedShape.RemoveRootShapeElement(createdElement);
            }
            ElementHierarchyManager.ElementHierarchy.StartCreatingElementPrefabs(ShapeHolder.CurrentLoadedShape);
        }

        public override bool MergeTasksIfPossible(IEditTask nextTask)
        {
            return false;
        }

        public override VSEditMode GetRequiredEditMode()
        {
            return VSEditMode.Model;
        }

        public override string GetTaskName()
        {
            return "Mirror Element";
        }
    }
}
