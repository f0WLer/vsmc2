using UnityEngine;

namespace VSMC
{
    /// <summary>
    /// The reflection math behind the Mirror tool, kept separate from TaskMirrorElement so the geometry
    /// can be reasoned about (and eventually tested) independently of undo/copy/selection plumbing.
    /// </summary>
    public static class MirrorMath
    {
        // Matches GridManager's grid-center anchor, which stays fixed here regardless of grid size. Y is 0
        // since the bottom plane doesn't move.
        static readonly Vector3 GridCenter = new Vector3(8, 0, 8);

        /// <summary>
        /// A reflection about the grid's bottom-center on the given axis. Determinant -1, unlike any
        /// rotation.
        /// </summary>
        public static Matrix4x4 BuildReflectionMatrix(EnumAxis axis)
        {
            Vector3 scale = Vector3.one;
            switch (axis)
            {
                case EnumAxis.X: scale.x = -1; break;
                case EnumAxis.Y: scale.y = -1; break;
                case EnumAxis.Z: scale.z = -1; break;
            }
            return Matrix4x4.Translate(GridCenter) * Matrix4x4.Scale(scale) * Matrix4x4.Translate(-GridCenter);
        }

        /// <summary>
        /// Mirrors one element's From/To/RotationOrigin/rotation in place (children are the caller's job)
        /// and returns its new model-space matrix, for the caller to pass down as the next level's parent.
        ///
        /// A reflection has determinant -1, but RotationX/Y/Z can only compose to +1 - no Euler triple can
        /// represent a flip. Fix: always flip local X instead (reflecting a box about its own centerline
        /// just swaps From/To, still valid) and let the caller swap East/West face data to match. The axis
        /// is arbitrary - any single flip supplies the determinant a reflection needs.
        /// </summary>
        public static Matrix4x4 MirrorElementInPlace(ShapeElement elem, Matrix4x4 parentWorldOrig, Matrix4x4 parentWorldNew, Matrix4x4 reflect)
        {
            Vector3 size = new Vector3(
                (float)(elem.To[0] - elem.From[0]),
                (float)(elem.To[1] - elem.From[1]),
                (float)(elem.To[2] - elem.From[2]));

            Matrix4x4 localOrig = elem.ApplyTransform(Matrix4x4.identity);

            // Pivot is just a point: reflect its world position and re-express it relative to the new
            // parent chain (the already-mirrored ancestor, for a descendant).
            Vector3 oldOrigin = new Vector3((float)elem.RotationOrigin[0], (float)elem.RotationOrigin[1], (float)elem.RotationOrigin[2]);
            Vector3 oldPivotWorld = parentWorldOrig.MultiplyPoint3x4(oldOrigin);
            Vector3 newPivotWorld = reflect.MultiplyPoint3x4(oldPivotWorld);
            Vector3 newOrigin = parentWorldNew.inverse.MultiplyPoint3x4(newPivotWorld);

            // The local matrix needed to land on the reflected target - determinant -1, unusable as-is.
            Matrix4x4 neededLocal = parentWorldNew.inverse * reflect * parentWorldOrig * localOrig;

            // Folds in the local-X flip: reflecting the [0, size] local box about its own centerline maps
            // local x -> size.x - x.
            Matrix4x4 centerFlip = Matrix4x4.Translate(new Vector3(size.x, 0, 0)) * Matrix4x4.Scale(new Vector3(-1, 1, 1));
            Matrix4x4 newLocalRaw = neededLocal * centerFlip;

            Matrix4x4 rotationOnly = newLocalRaw;
            rotationOnly.SetColumn(3, new Vector4(0, 0, 0, 1));
            Vector3 eulerNew = ExtractEulerXYZ(rotationOnly);

            // The element's local matrix is T(origin) * R * T(-origin) * T(From), whose translation column
            // is origin + R*(From - origin) - solve that for From.
            Vector3 tNewActual = newLocalRaw.GetColumn(3);
            Vector3 newFrom = newOrigin + (Vector3)(rotationOnly.inverse.MultiplyPoint3x4(tNewActual - newOrigin));

            elem.RotationOrigin = new double[] { newOrigin.x, newOrigin.y, newOrigin.z };
            // Matches TaskReparentElement.ExtractEulerXYZ's established sign convention for this codebase.
            elem.RotationX = -eulerNew.x;
            elem.RotationY = -eulerNew.y;
            elem.RotationZ = -eulerNew.z;
            elem.From = new double[] { newFrom.x, newFrom.y, newFrom.z };
            elem.To = new double[] { newFrom.x + size.x, newFrom.y + size.y, newFrom.z + size.z };

            // The local flip above puts the +X face on -X, so East and West trade places.
            if (elem.FacesResolved != null && elem.FacesResolved.Length == 6)
            {
                (elem.FacesResolved[1], elem.FacesResolved[3]) = (elem.FacesResolved[3], elem.FacesResolved[1]);

                // Every face comes out with its U axis reversed - the four that map U to local X directly,
                // and East/West via the swap above, since their CubeUvCoords corner layouts run opposite
                // ways. Rotation has to be negated to keep that correction a pure U flip: the tesselator
                // applies it as a cyclic shift of corners, which the flip reverses, so leaving it alone
                // would need a V flip instead on the 90 and 270 cases.
                foreach (ShapeElementFace f in elem.FacesResolved)
                {
                    if (f?.Uv == null || f.Uv.Length < 4) continue;
                    (f.Uv[0], f.Uv[2]) = (f.Uv[2], f.Uv[0]);
                    f.Rotation = (360 - f.Rotation) % 360;
                }
            }

            Matrix4x4 localNew = elem.ApplyTransform(Matrix4x4.identity);
            return parentWorldNew * localNew;
        }

        /// <summary>
        /// Mirrors an element and its full subtree in place, using the invariant that every model-space
        /// point of the mirrored subtree must be the reflection of the corresponding point in the source.
        /// </summary>
        public static void MirrorSubtreeInPlace(ShapeElement root, Matrix4x4 parentWorld, EnumAxis axis)
        {
            Matrix4x4 reflect = BuildReflectionMatrix(axis);
            MirrorSubtreeInPlaceRecursive(root, parentWorld, parentWorld, reflect);
        }

        static void MirrorSubtreeInPlaceRecursive(ShapeElement elem, Matrix4x4 parentWorldOrig, Matrix4x4 parentWorldNew, Matrix4x4 reflect)
        {
            Matrix4x4 worldOrig = parentWorldOrig * elem.ApplyTransform(Matrix4x4.identity);
            Matrix4x4 worldNew = MirrorElementInPlace(elem, parentWorldOrig, parentWorldNew, reflect);

            if (elem.Children != null)
            {
                foreach (ShapeElement child in elem.Children)
                {
                    MirrorSubtreeInPlaceRecursive(child, worldOrig, worldNew, reflect);
                }
            }
        }

        /// <summary>
        /// Decomposes a rotation matrix into the RotationX/Y/Z Euler triple this codebase's element
        /// rotation fields expect. Duplicated from TaskReparentElement.ExtractEulerXYZ (kept private there)
        /// rather than changing that method's visibility for this feature.
        /// </summary>
        public static Vector3 ExtractEulerXYZ(Matrix4x4 m)
        {
            Vector3 rot = new Vector3();
            Matrix4x4 n = m.transpose;
            Quaternion q = Quaternion.LookRotation(n.GetColumn(2), n.GetColumn(1));

            float sinr_cosp = 2.0f * (q.w * q.x + q.y * q.z);
            float cosr_cosp = 1.0f - 2.0f * (q.x * q.x + q.y * q.y);
            rot.x = Mathf.Atan2(sinr_cosp, cosr_cosp);

            float sinp = 2.0f * (q.w * q.y - q.z * q.x);
            if (Mathf.Abs(sinp) >= 1)
            {
                rot.y = Mathf.PI / 2 * Mathf.Sign(sinp);
            }
            else
            {
                rot.y = Mathf.Asin(sinp);
            }

            float siny_cosp = 2.0f * (q.w * q.z + q.x * q.y);
            float cosy_cosp = 1.0f - 2.0f * (q.y * q.y + q.z * q.z);
            rot.z = Mathf.Atan2(siny_cosp, cosy_cosp);

            return rot * Mathf.Rad2Deg;
        }
    }
}
