using UnityEngine;

namespace VRBadminton.Input
{
    public static class BadmintonInputMath
    {
        public static Matrix4x4 MatrixFromRowMajor(float[] values)
        {
            Matrix4x4 matrix = Matrix4x4.identity;
            if (values == null || values.Length < 9)
            {
                return matrix;
            }

            matrix.m00 = values[0];
            matrix.m01 = values[1];
            matrix.m02 = values[2];
            matrix.m10 = values[3];
            matrix.m11 = values[4];
            matrix.m12 = values[5];
            matrix.m20 = values[6];
            matrix.m21 = values[7];
            matrix.m22 = values[8];
            return matrix;
        }

        public static Quaternion QuaternionFromRowMajor(float[] values)
        {
            if (values == null || values.Length < 9)
            {
                return Quaternion.identity;
            }

            return QuaternionFromMatrix(MatrixFromRowMajor(values));
        }

        public static Quaternion QuaternionFromMatrix(Matrix4x4 matrix)
        {
            float trace = matrix.m00 + matrix.m11 + matrix.m22;
            float x;
            float y;
            float z;
            float w;
            if (trace > 0f)
            {
                float s = Mathf.Sqrt(trace + 1f) * 2f;
                w = 0.25f * s;
                x = (matrix.m21 - matrix.m12) / s;
                y = (matrix.m02 - matrix.m20) / s;
                z = (matrix.m10 - matrix.m01) / s;
            }
            else if (matrix.m00 > matrix.m11 && matrix.m00 > matrix.m22)
            {
                float s = Mathf.Sqrt(1f + matrix.m00 - matrix.m11 - matrix.m22) * 2f;
                w = (matrix.m21 - matrix.m12) / s;
                x = 0.25f * s;
                y = (matrix.m01 + matrix.m10) / s;
                z = (matrix.m02 + matrix.m20) / s;
            }
            else if (matrix.m11 > matrix.m22)
            {
                float s = Mathf.Sqrt(1f + matrix.m11 - matrix.m00 - matrix.m22) * 2f;
                w = (matrix.m02 - matrix.m20) / s;
                x = (matrix.m01 + matrix.m10) / s;
                y = 0.25f * s;
                z = (matrix.m12 + matrix.m21) / s;
            }
            else
            {
                float s = Mathf.Sqrt(1f + matrix.m22 - matrix.m00 - matrix.m11) * 2f;
                w = (matrix.m10 - matrix.m01) / s;
                x = (matrix.m02 + matrix.m20) / s;
                y = (matrix.m12 + matrix.m21) / s;
                z = 0.25f * s;
            }

            Quaternion quaternion = new Quaternion(x, y, z, w);
            return Normalize(quaternion);
        }

        public static Quaternion QuaternionFromArray(float[] values)
        {
            if (values == null || values.Length < 4)
            {
                return Quaternion.identity;
            }

            return Normalize(new Quaternion(values[0], values[1], values[2], values[3]));
        }

        public static Vector3 Vector3FromArray(float[] values)
        {
            if (values == null || values.Length < 3)
            {
                return Vector3.zero;
            }

            return new Vector3(values[0], values[1], values[2]);
        }

        public static Vector3 TransformDirection(Matrix4x4 rowMajorMatrix, Vector3 value)
        {
            return new Vector3(
                rowMajorMatrix.m00 * value.x + rowMajorMatrix.m01 * value.y + rowMajorMatrix.m02 * value.z,
                rowMajorMatrix.m10 * value.x + rowMajorMatrix.m11 * value.y + rowMajorMatrix.m12 * value.z,
                rowMajorMatrix.m20 * value.x + rowMajorMatrix.m21 * value.y + rowMajorMatrix.m22 * value.z);
        }

        public static Matrix4x4 MirrorForwardBack(Matrix4x4 matrix)
        {
            Matrix4x4 mirrored = Matrix4x4.identity;
            mirrored.m00 = matrix.m00;
            mirrored.m01 = matrix.m01;
            mirrored.m02 = -matrix.m02;
            mirrored.m10 = matrix.m10;
            mirrored.m11 = matrix.m11;
            mirrored.m12 = -matrix.m12;
            mirrored.m20 = -matrix.m20;
            mirrored.m21 = -matrix.m21;
            mirrored.m22 = matrix.m22;
            return mirrored;
        }

        public static Quaternion MirrorForwardBack(Quaternion rotation)
        {
            return QuaternionFromMatrix(MirrorForwardBack(Matrix4x4.Rotate(rotation)));
        }

        public static float FaceAngleFromRacket(BadmintonRacketFrame frame)
        {
            Vector3 faceNormal = TransformDirection(frame.RotationMatrix, Vector3.forward).normalized;
            if (faceNormal.sqrMagnitude < 0.0001f)
            {
                return 37.5f;
            }

            float pitch = Mathf.Atan2(faceNormal.y, Mathf.Max(0.001f, Mathf.Abs(faceNormal.z))) * Mathf.Rad2Deg;
            return Mathf.Clamp(pitch + 45f, -45f, 120f);
        }

        // Sensor swing direction is reported in phone space. For front-court lifts, the mirrored
        // court-side motion can invert the vertical sign even though the gameplay intent is the same.
        public static bool IsSideMirroredLiftGesture(
            Vector3 swingDirection,
            float racketLateralPosition,
            float faceAngle)
        {
            // Ignore weak vertical evidence and near-center samples so generic swings are not
            // reclassified.
            if (swingDirection.sqrMagnitude < 0.0001f ||
                Mathf.Abs(swingDirection.y) < 0.18f ||
                Mathf.Abs(racketLateralPosition) < 0.18f)
            {
                return false;
            }

            if (Mathf.Sign(swingDirection.y) != Mathf.Sign(racketLateralPosition))
            {
                return false;
            }

            // Keep the correction in a lift-capable face range; steep closed faces use the
            // normal resolver.
            return faceAngle >= -45f && faceAngle <= 85f;
        }

        public static float RelAngle(float value, float offset)
        {
            float x = value - offset;
            while (x > 180f)
            {
                x -= 360f;
            }

            while (x < -180f)
            {
                x += 360f;
            }

            return x;
        }

        public static Quaternion Normalize(Quaternion quaternion)
        {
            float length = Mathf.Sqrt(
                quaternion.x * quaternion.x +
                quaternion.y * quaternion.y +
                quaternion.z * quaternion.z +
                quaternion.w * quaternion.w);
            if (length <= 0.000001f)
            {
                return Quaternion.identity;
            }

            return new Quaternion(
                quaternion.x / length,
                quaternion.y / length,
                quaternion.z / length,
                quaternion.w / length);
        }
    }
}
