using UnityEngine;
using UnityEditor;

namespace Normal.Realtime {
    [CustomEditor(typeof(RealtimeTransform))]
    public class RealtimeTransformEditor : Editor {
        private RealtimeTransform realtimeTransform { get { return (RealtimeTransform)target; } }
        private bool isOnline { get { return realtimeTransform.model != null; } }
    
        public override void OnInspectorGUI() {
            GUILayout.Space(8);
    
            // Properties
            GUI.enabled = !Application.isPlaying;
            serializedObject.Update();
            SerializedProperty syncPositionProperty  = serializedObject.FindProperty("_syncPosition");
            SerializedProperty syncRotationProperty  = serializedObject.FindProperty("_syncRotation");
            SerializedProperty syncScaleProperty     = serializedObject.FindProperty("_syncScale");
            SerializedProperty extrapolationProperty = serializedObject.FindProperty("_extrapolation");
            syncPositionProperty.boolValue  = EditorGUILayout.Toggle("Sync Position", syncPositionProperty.boolValue);
            syncRotationProperty.boolValue  = EditorGUILayout.Toggle("Sync Rotation", syncRotationProperty.boolValue);
            syncScaleProperty.boolValue     = EditorGUILayout.Toggle("Sync Scale",    syncScaleProperty.boolValue);
            GUI.enabled = !Application.isPlaying || realtimeTransform.isOwnedLocally;
            extrapolationProperty.boolValue = EditorGUILayout.Toggle("Extrapolation", extrapolationProperty.boolValue);
            GUI.enabled = !Application.isPlaying;
            serializedObject.ApplyModifiedProperties();
    
    
            GUILayout.Space(4);
    
            GUI.enabled = isOnline;
    
            // Ownership
            GUILayout.Label("Owner: " + GetOwner());
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Request Ownership"))
                RequestOwnership();
            if (GUILayout.Button("Clear Ownership"))
                ClearOwnership();
            GUILayout.EndHorizontal();
    
            GUILayout.Space(4);
    
            // Reset
            GUI.enabled = true;
        }
    
        string GetOwner() {
            if (!isOnline)
                return "Offline";
    
            // Owned by the world
            if (realtimeTransform.isOwnedByWorld)
                return "None";
    
            // Owned by the local client
            if (realtimeTransform.isOwnedLocally)
                return "Local client (" + realtimeTransform.ownerID + ")";
    
            // Owned by a remote client
            return "Remote client (" + realtimeTransform.ownerID + ")";
        }
    
        void RequestOwnership() {
            realtimeTransform.RequestOwnership();
        }
    
        void ClearOwnership() {
            realtimeTransform.ClearOwnership();
        }
    }
}
