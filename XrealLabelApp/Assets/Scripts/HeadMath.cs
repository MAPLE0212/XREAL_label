using UnityEngine;

namespace XrealLabel
{
    /// <summary>頭の向き（yaw / pitch / roll）と方向ベクトルの相互変換。角度はすべて度。</summary>
    public static class HeadMath
    {
        /// <summary>-180 &lt; a &lt;= 180 に正規化する。</summary>
        public static float NormalizeAngle(float a)
        {
            a %= 360f;
            if (a > 180f) a -= 360f;
            if (a <= -180f) a += 360f;
            return a;
        }

        /// <summary>前方ベクトルの yaw（右が正）。</summary>
        public static float Yaw(Vector3 forward) => Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

        /// <summary>前方ベクトルの pitch（上が正）。</summary>
        public static float Pitch(Vector3 forward) => Mathf.Asin(Mathf.Clamp(forward.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;

        /// <summary>回転の roll（左に傾けると正）。</summary>
        public static float Roll(Quaternion rotation) => NormalizeAngle(rotation.eulerAngles.z);

        /// <summary>yaw / pitch から単位方向ベクトルを作る。</summary>
        public static Vector3 Direction(float yaw, float pitch) => Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;
    }
}
