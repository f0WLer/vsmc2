using UnityEngine;

namespace VSMC
{
    /// <summary>
    /// Derives a ShapeElement face's model-space plane (point + normal), for tools that need exact
    /// geometric alignment rather than just visual/mesh data. Works in the same raw (non-/16) unit space
    /// as ShapeElement.ApplyTransform, not ShapeTesselator's Unity-scaled storedMatrix, so it composes
    /// cleanly with From/To/RotationOrigin edits and TaskAddToElementPosition's movement vectors.
    /// </summary>
    public static class FacePlaneUtil
    {
        /// <summary>
        /// The model-space matrix for this element's parent chain only - i.e. everything ApplyTransform
        /// composes up to but NOT including this element's own transform. Needed by ModelEditor's Align
        /// Faces code: translating an element via From+RotationOrigin (see TaskAddToElementPosition with
        /// alsoMoveRotationOrigin: true) cancels the element's own rotation out of the local-to-world
        /// relationship, so only the parent chain's rotation matters when converting a world displacement
        /// into a local movement vector.
        /// </summary>
        public static Matrix4x4 GetParentModelMatrix(ShapeElement elem)
        {
            Matrix4x4 matrix = Matrix4x4.identity;
            foreach (ShapeElement pathElem in elem.GetParentPath())
            {
                matrix = pathElem.ApplyTransform(matrix);
            }
            return matrix;
        }

        /// <summary>
        /// The full model-space matrix for this element, including its own transform (not just its parents').
        /// </summary>
        public static Matrix4x4 GetElementModelMatrix(ShapeElement elem)
        {
            return elem.ApplyTransform(GetParentModelMatrix(elem));
        }

        /// <summary>
        /// A point on the given face, in the element's own local (pre-rotation) frame, i.e. the raw
        /// offset from From that ApplyTransform's From-translate step expects, not an absolute From/To
        /// value. Any point on the face's plane works equally well here, the face center is just convenient.
        /// </summary>
        public static Vector3 GetLocalFaceOffset(ShapeElement elem, int faceIndex)
        {
            Vector3 extent = new Vector3(
                (float)(elem.To[0] - elem.From[0]),
                (float)(elem.To[1] - elem.From[1]),
                (float)(elem.To[2] - elem.From[2]));
            Vector3 half = extent / 2f;

            switch (faceIndex)
            {
                case (int)FaceEnum.North: return new Vector3(half.x, half.y, 0);
                case (int)FaceEnum.East: return new Vector3(extent.x, half.y, half.z);
                case (int)FaceEnum.South: return new Vector3(half.x, half.y, extent.z);
                case (int)FaceEnum.West: return new Vector3(0, half.y, half.z);
                case (int)FaceEnum.Up: return new Vector3(half.x, extent.y, half.z);
                case (int)FaceEnum.Down: return new Vector3(half.x, 0, half.z);
            }
            return half;
        }

        /// <summary>
        /// The given face's plane in model space: a point on the plane and its unit outward normal.
        /// </summary>
        public static void GetFacePlane(ShapeElement elem, int faceIndex, out Vector3 point, out Vector3 normal)
        {
            Matrix4x4 modelMatrix = GetElementModelMatrix(elem);
            point = modelMatrix.MultiplyPoint3x4(GetLocalFaceOffset(elem, faceIndex));
            normal = modelMatrix.MultiplyVector(BlockFacing.ALLFACES[faceIndex].vector).normalized;
        }
    }
}
