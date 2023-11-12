using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FuzzPhyte.Utility
{
    /// <summary>
    /// Utilizes Debug.Draw to generate:
    ///   A Box
    ///   A Sphere
    ///   A Circle
    /// 
    /// </summary>
    public class FP_UtilityDraw : MonoBehaviour
    {
        /// <summary>
        /// Stolen from the Unity Forums: https://forum.unity.com/threads/debug-drawbox-function-is-direly-needed.1038499/
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="rot"></param>
        /// <param name="scale"></param>
        /// <param name="c"></param>
        /// <param name="time"></param>
        public void DrawBox(Vector3 pos, Quaternion rot, Vector3 scale, Color c, float time)
        {
            Matrix4x4 m = new Matrix4x4();
            m.SetTRS(pos, rot, scale);

            var point1 = m.MultiplyPoint(new Vector3(-0.5f, -0.5f, 0.5f));
            var point2 = m.MultiplyPoint(new Vector3(0.5f, -0.5f, 0.5f));
            var point3 = m.MultiplyPoint(new Vector3(0.5f, -0.5f, -0.5f));
            var point4 = m.MultiplyPoint(new Vector3(-0.5f, -0.5f, -0.5f));

            var point5 = m.MultiplyPoint(new Vector3(-0.5f, 0.5f, 0.5f));
            var point6 = m.MultiplyPoint(new Vector3(0.5f, 0.5f, 0.5f));
            var point7 = m.MultiplyPoint(new Vector3(0.5f, 0.5f, -0.5f));
            var point8 = m.MultiplyPoint(new Vector3(-0.5f, 0.5f, -0.5f));

            Debug.DrawLine(point1, point2, c, time);
            Debug.DrawLine(point2, point3, c, time);
            Debug.DrawLine(point3, point4, c, time);
            Debug.DrawLine(point4, point1, c, time);

            Debug.DrawLine(point5, point6, c, time);
            Debug.DrawLine(point6, point7, c, time);
            Debug.DrawLine(point7, point8, c, time);
            Debug.DrawLine(point8, point5, c, time);

            Debug.DrawLine(point1, point5, c, time);
            Debug.DrawLine(point2, point6, c, time);
            Debug.DrawLine(point3, point7, c, time);
            Debug.DrawLine(point4, point8, c, time);
        }
        /// <summary>
        /// Stole from the Unity GitHub https://github.com/Unity-Technologies/Graphics/pull/2287/files#diff-cc2ed84f51a3297faff7fd239fe421ca1ca75b9643a22f7808d3a274ff3252e9R195
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="radius"></param>
        /// <param name="color"></param>
        /// <param name="time"></param>
        public void DrawSphere(Vector4 pos, float radius, Color color, float time)
        {
            Vector4[] v = FP_UtilityData.s_UnitSphere;
            int len = FP_UtilityData.s_UnitSphere.Length / 3;
            for (int i = 0; i < len; i++)
            {
                var sX = pos + radius * v[0 * len + i];
                var eX = pos + radius * v[0 * len + (i + 1) % len];
                var sY = pos + radius * v[1 * len + i];
                var eY = pos + radius * v[1 * len + (i + 1) % len];
                var sZ = pos + radius * v[2 * len + i];
                var eZ = pos + radius * v[2 * len + (i + 1) % len];
                Debug.DrawLine(sX, eX, color, time);
                Debug.DrawLine(sY, eY, color, time);
                Debug.DrawLine(sZ, eZ, color, time);
            }
        }
        /// <summary>
        /// Also stolen from the Unity Forums thread from above
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="drawSide"></param>
        /// <param name="radius"></param>
        /// <param name="color"></param>
        /// <param name="time"></param>
        public void DrawCircle(Vector4 pos, Vector3 drawSide, float radius, Color color, float time)
        {
            Vector4[] v = FP_UtilityData.s_UnitSphere;
            int len = FP_UtilityData.s_UnitSphere.Length / 3;
            for (int i = 0; i < len; i++)
            {
                var sX = pos + radius * v[0 * len + i];
                var eX = pos + radius * v[0 * len + (i + 1) % len];
                var sY = pos + radius * v[1 * len + i];
                var eY = pos + radius * v[1 * len + (i + 1) % len];
                var sZ = pos + radius * v[2 * len + i];
                var eZ = pos + radius * v[2 * len + (i + 1) % len];
                if (drawSide.x != 0)
                {
                    Debug.DrawLine(sX, eX, color, time);
                }
                if (drawSide.y != 0)
                {
                    Debug.DrawLine(sY, eY, color, time);
                }
                if (drawSide.z != 0)
                {
                    Debug.DrawLine(sZ, eZ, color, time);
                }
            }
        }
    }
}
