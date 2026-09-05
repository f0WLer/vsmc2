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
        int[] childUIDs;
        string[] oldNames;
        string[] newNames;

        public TaskNameChildren(ShapeElement parent)
        {
            ShapeElement[] children = parent.Children ?? System.Array.Empty<ShapeElement>();
            childUIDs = children.Select(c => c.elementUID).ToArray();
            oldNames = children.Select(c => c.Name).ToArray();

            //Names that must not be used as a target - every other element in the shape, excluding the children being renamed.
            HashSet<int> renamedUIDs = new HashSet<int>(childUIDs);
            HashSet<string> reservedNames = new HashSet<string>(
                ShapeElementRegistry.main.GetAllShapeElements()
                    .Where(e => !renamedUIDs.Contains(e.elementUID))
                    .Select(e => e.Name),
                System.StringComparer.CurrentCultureIgnoreCase);

            newNames = new string[children.Length];
            for (int i = 0; i < children.Length; i++)
            {
                //Suffixed _1, _2, ... on collision, matching TaskRenameElement's convention.
                string candidate = parent.Name + (i + 1);
                for (int suffix = 1; reservedNames.Contains(candidate); suffix++)
                {
                    candidate = parent.Name + (i + 1) + "_" + suffix;
                }
                newNames[i] = candidate;
                reservedNames.Add(candidate);
            }
        }

        public override void DoTask()
        {
            ApplyRenames(oldNames, newNames);
        }

        public override void UndoTask()
        {
            ApplyRenames(newNames, oldNames);
        }

        /// <summary>
        /// Renames in two passes using temporary unique names to avoid collisions between siblings.
        /// This also prevents no-op renames, which would remove the element's keyframe entry.
        /// </summary> 
        void ApplyRenames(string[] from, string[] to)
        {
            for (int i = 0; i < childUIDs.Length; i++)
            {
                RenameElement(childUIDs[i], from[i], TempNameFor(childUIDs[i]));
            }
            for (int i = 0; i < childUIDs.Length; i++)
            {
                RenameElement(childUIDs[i], TempNameFor(childUIDs[i]), to[i]);
            }
            ShapeLoader.main.shapeHolder.RefreshAllStepparents();
        }

        static string TempNameFor(int elemUID)
        {
            return "__namechildren_" + elemUID;
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
