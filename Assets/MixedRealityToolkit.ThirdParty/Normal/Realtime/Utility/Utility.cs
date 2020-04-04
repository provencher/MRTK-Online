using UnityEngine;
using System;

namespace Normal.Utility {
    public class StaticFunctions {
        public static void SetLayerRecursively(GameObject gameObject, int layer) {
            if (gameObject == null)
                return;

            gameObject.layer = layer;

            foreach (Transform child in gameObject.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        public static float CalculateAverageDbForAudioBuffer(float[] audioBuffer, int offset = 0) {
            float averageDbSample = 0.0f;
            for (int i = offset; i < audioBuffer.Length; i++) {
                float dbSample = audioBuffer[i];
                averageDbSample += dbSample * dbSample;
            }

            averageDbSample = Mathf.Sqrt(averageDbSample / audioBuffer.Length);
            averageDbSample = LinearToDb(averageDbSample);
            //averageDbSample = Mathf.Exp(-2.0f * averageDbSample) * averageDbSample;
            return averageDbSample;
        }

        public static float LinearToDb(float linear) {
            float db = -100.0f;

            if (linear != 0.0f)
                db = 20.0f * Mathf.Log10(linear);

            return db;
        }

        // Swing Twist Decomposition
        public static void SwingTwistDecomposition(Quaternion rotation, Vector3 direction, out Quaternion swing, out Quaternion twist) {
            Vector3 rotationAxis = new Vector3(rotation.x, rotation.y, rotation.z);
            Vector3 projection = Vector3.Project(rotationAxis, direction); // return projection v1 on to v2  (parallel component)
            Vector4 twist4 = new Vector4(projection.x, projection.y, projection.z, rotation.w);
            twist4 = NormalizeQuaternion(twist4);
            twist = new Quaternion(twist4.x, twist4.y, twist4.z, twist4.w);
            swing = rotation * Quaternion.Inverse(twist);
        }

        static Vector4 NormalizeQuaternion(Vector4 quaternion) {
            float m = quaternion.magnitude;
            if (m < float.Epsilon) {
                quaternion = StabilizeLengthOfQuaternion(quaternion);
                m = quaternion.magnitude;
            }
            quaternion /= m;
            return quaternion;
        }
    
        static Vector4 StabilizeLengthOfQuaternion(Vector4 q) {
            float cs = (float)(Mathf.Abs(q.x) + Mathf.Abs(q.y) + Mathf.Abs(q.z) + Mathf.Abs(q.w));
            if (cs > 0.0f)
                return new Vector4(q.x/=cs, q.y/=cs, q.z/=cs, q.w/=cs);
            else
                return new Vector4(0.0f, 0.0f, 0.0f, 1.0f);
        }

        public static double LerpClamped(double a, double b, double t) {
            if (t > 1.0)
                t = 1.0;
            else if (t < 0.0)
                t = 0.0;
            return a*(1.0-t) + b*t;
        }
    }

    public static class Extensions {
        // Will only add a component if one doesn't exist already. If a new component is created, it will be returned.
        public static T AddComponentIfNeeded<T>(this GameObject gameObject) where T : Component {
            T component = gameObject.GetComponent<T>();
            if (component != null)
                return null;

            return gameObject.AddComponent<T>();
        }
    }

    public class ExecutionOrder : Attribute {
        public int order;

        public ExecutionOrder(int order) {
            this.order = order;
        }
    }
}
