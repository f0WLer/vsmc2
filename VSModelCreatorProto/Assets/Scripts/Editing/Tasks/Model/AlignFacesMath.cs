using UnityEngine;

namespace VSMC
{
    /// <summary>
    /// The pure plane-alignment math behind the Align Faces tool, kept independent of ShapeElement/UI so
    /// it can be reasoned about (and tested) on its own.
    /// </summary>
    public static class AlignFacesMath
    {
        /// <summary>
        /// Planes are treated as parallel once their normals' dot product magnitude is within this of 1 -
        /// same-facing and opposite-facing normals both count, only the plane orientation matters.
        /// A translation-only alignment can't fix any actual angle between two planes, so this only needs
        /// to absorb float32 noise accumulated through a handful of chained Matrix4x4 multiplications
        /// (ApplyTransform up the parent chain), not tolerate a visibly tilted pair of faces. 1e-5 gives
        /// about a 0.0045 radian (~0.26 degree) tolerance - tight enough to reject a real tilt, loose
        /// enough that ordinary float32 rounding noise doesn't cause spurious rejections.
        /// </summary>
        public const float ParallelDotTolerance = 1f - 1e-5f;

        /// <summary>
        /// Below this signed distance, the planes are treated as already coincident - a valid no-op.
        /// </summary>
        public const float CoplanarDistanceTolerance = 0.0001f;

        /// <summary>
        /// Computes the world/model-space translation that moves the source plane onto the target plane,
        /// along their common normal. Returns false if the planes aren't parallel (translation is then
        /// Vector3.zero). Works for same-facing and opposite-facing normals alike, and for planes that
        /// don't overlap in their tangent directions - only the perpendicular offset changes.
        /// </summary>
        public static bool TryComputeTranslation(Vector3 sourcePoint, Vector3 sourceNormal, Vector3 targetPoint, Vector3 targetNormal, out Vector3 translation)
        {
            Vector3 sn = sourceNormal.normalized;
            Vector3 tn = targetNormal.normalized;

            if (Mathf.Abs(Vector3.Dot(sn, tn)) < ParallelDotTolerance)
            {
                translation = Vector3.zero;
                return false;
            }

            float distance = Vector3.Dot(targetPoint - sourcePoint, tn);
            translation = tn * distance;
            return true;
        }

        public static bool IsNegligible(Vector3 translation)
        {
            return translation.magnitude < CoplanarDistanceTolerance;
        }
    }
}
