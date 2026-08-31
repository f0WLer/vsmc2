using System.Collections.Generic;
using System.Linq;

namespace VSMC
{
    /// <summary>
    /// An undoable task that renames all direct children of an element to "{ParentName}{index}",
    /// numbered in their current sibling order starting at 1.
    /// </summary>
    public class TaskNameChildren : IEditTask
    {
        int parentUID;
        int[] childUIDs;
        string[] oldNames;
        string[] newNames;

        public TaskNameChildren(ShapeElement parent)
        {
            parentUID = parent.elementUID;

            ShapeElement[] children = parent.Children ?? new ShapeElement[0];
            childUIDs = children.Select(c => c.elementUID).ToArray();
            oldNames = children.Select(c => c.Name).ToArray();

            //Names that must not be used as a target - every other element in the shape, excluding the children being renamed.
            HashSet<string> reservedNames = new HashSet<string>(
                ShapeElementRegistry.main.GetAllShapeElements()
                    .Where(e => !childUIDs.Contains(e.elementUID))
                    .Select(e => e.Name),
                System.StringComparer.CurrentCultureIgnoreCase);

            newNames = new string[children.Length];
            for (int i = 0; i < children.Length; i++)
            {
                string candidate = parent.Name + (i + 1);
                while (reservedNames.Contains(candidate))
                {
                    candidate += "_1";
                }
                newNames[i] = candidate;
                reservedNames.Add(candidate);
            }
        }

        public override void DoTask()
        {
            for (int i = 0; i < childUIDs.Length; i++)
            {
                RenameElement(childUIDs[i], oldNames[i], newNames[i]);
            }
            ShapeLoader.main.shapeHolder.RefreshAllStepparents();
        }

        public override void UndoTask()
        {
            for (int i = 0; i < childUIDs.Length; i++)
            {
                RenameElement(childUIDs[i], newNames[i], oldNames[i]);
            }
            ShapeLoader.main.shapeHolder.RefreshAllStepparents();
        }

        void RenameElement(int elemUID, string fromName, string toName)
        {
            ShapeElement elem = ShapeElementRegistry.main.GetShapeElementByUID(elemUID);
            elem.Name = toName;
            elem.gameObject.name = toName;

            ElementHierarchyManager.ElementHierarchy.GetElementHierarchyItem(elem).elementName.text = toName;

            //Animations rely on object names, so we need to rename each entry that has this name.
            if (ShapeHolder.CurrentLoadedShape.Animations != null)
            {
                foreach (Animation anim in ShapeHolder.CurrentLoadedShape.Animations)
                {
                    foreach (AnimationKeyFrame keyFrame in anim.KeyFrames)
                    {
                        if (keyFrame.Elements.ContainsKey(fromName))
                        {
                            keyFrame.Elements[toName] = keyFrame.Elements[fromName];
                            keyFrame.Elements.Remove(fromName);
                        }
                    }
                }
            }

            //Backdrops also rely on object names.
            foreach (ShapeElement e in ShapeElementRegistry.main.GetAllShapeElements())
            {
                if (e.StepParentName != null && e.StepParentName.Equals(fromName, System.StringComparison.CurrentCultureIgnoreCase))
                {
                    e.StepParentName = toName;
                }
            }
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
            return "Name Children";
        }
    }
}
