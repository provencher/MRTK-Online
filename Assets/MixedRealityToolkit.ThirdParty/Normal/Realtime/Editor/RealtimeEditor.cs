using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using Normal.Realtime.Native;

namespace Normal.Realtime {
    [CustomEditor(typeof(Realtime))]
    public class RealtimeEditor : Editor {
        private static bool __showNetworkStatistics = false;

        private Texture  _logo;
        private Realtime _realtime { get { return (Realtime)target; } }

        void OnEnable() {
            _logo = AssetDatabase.LoadAssetAtPath<Texture2D>(Path.Combine(GetResourcesPath(), "NormalEditorUILogo.png"));
            __showNetworkStatistics = EditorPrefs.GetBool("Normal.RealtimeEditor.ShowNetworkStatistics");
        }

        public override void OnInspectorGUI() {
            // Logo
            if (_logo)
                GUI.DrawTexture(GUILayoutUtility.GetRect(EditorGUIUtility.currentViewWidth - 30.0f, _logo.height/2.0f, GUI.skin.box), _logo, ScaleMode.ScaleToFit);

            // Properties
            serializedObject.Update();

            // App Key
            SerializedProperty appKeyProperty = serializedObject.FindProperty("_appKey");
            appKeyProperty.stringValue = EditorGUILayout.TextField("App Key", appKeyProperty.stringValue);

            // Join room on start toggle
            SerializedProperty joinRoomOnStartProperty = serializedObject.FindProperty("_joinRoomOnStart");
            joinRoomOnStartProperty.boolValue = EditorGUILayout.Toggle("Join Room On Start", joinRoomOnStartProperty.boolValue);

            EditorGUI.BeginDisabledGroup(!joinRoomOnStartProperty.boolValue);

            // Room to join on start
            SerializedProperty roomToJoinOnStartProperty = serializedObject.FindProperty("_roomToJoinOnStart");
            roomToJoinOnStartProperty.stringValue = EditorGUILayout.TextField("    Room Name", roomToJoinOnStartProperty.stringValue);

            EditorGUI.EndDisabledGroup();

            // Debug Logging
            SerializedProperty debugLoggingProperty = serializedObject.FindProperty("_debugLogging");
            debugLoggingProperty.boolValue = EditorGUILayout.Toggle("Debug Logging", debugLoggingProperty.boolValue);

            // End Properties
            serializedObject.ApplyModifiedProperties();

            // Network Stats
            GUIStyle foldoutStyle = EditorStyles.foldout;
            FontStyle previousFoldoutFontStyle = foldoutStyle.fontStyle;
            foldoutStyle.fontStyle = FontStyle.Bold;
            __showNetworkStatistics = EditorGUILayout.Foldout(__showNetworkStatistics, "Network Statistics", foldoutStyle);
            foldoutStyle.fontStyle = previousFoldoutFontStyle;

            EditorPrefs.SetBool("Normal.RealtimeEditor.ShowNetworkStatistics", __showNetworkStatistics);

            if (__showNetworkStatistics) {
                bool isPlaying = Application.isPlaying;
                Room room = _realtime.room;
                Room.ConnectionState connectionState = Room.ConnectionState.Disconnected;
                NetworkInfo networkInfo = new NetworkInfo();
                double roomTime = 0.0;
                if (room != null) {
                    connectionState = room.connectionState;
                    networkInfo     = room.GetNetworkStatistics();
                    roomTime        = room.time;
                }

                EditorGUI.BeginDisabledGroup(!isPlaying);
                EditorGUILayout.LabelField("Connection:",        connectionState + "");
                // TODO: roundTripTime is horribly broken. Uncomment once it's fixed properly.
                //EditorGUILayout.LabelField("Round Trip Time:",   String.Format("{0:0.0}",   networkInfo.roundTripTime)        + "ms");
                EditorGUILayout.LabelField("Packet Loss:",       String.Format("{0:0.0}", networkInfo.percentOfPacketsLost) + "%");
                EditorGUILayout.LabelField("Send Bandwidth:",    String.Format("{0:0.0}", networkInfo.sentBandwidth)        + " kbps");
                EditorGUILayout.LabelField("Receive Bandwidth:", String.Format("{0:0.0}", networkInfo.receivedBandwidth)    + " kbps");
                EditorGUILayout.LabelField("Ack Bandwidth:",     String.Format("{0:0.0}", networkInfo.ackedBandwidth)       + " kbps");
                EditorGUILayout.LabelField("Packets Sent:",      networkInfo.numberOfPacketsSent     + "");
                EditorGUILayout.LabelField("Packets Received:",  networkInfo.numberOfPacketsReceived + "");
                EditorGUILayout.LabelField("Packets Acked:",     networkInfo.numberOfPacketsAcked    + "");
                EditorGUILayout.LabelField("Room Time:",         roomTime + "");
                EditorGUI.EndDisabledGroup();

                // Refresh the inspector while in play mode
                if (isPlaying)
                    EditorUtility.SetDirty(target);

                GUILayout.Space(2);
            }

            GUILayout.Space(2);
        }

        private string GetResourcesPath() {
            MonoScript monoScript = MonoScript.FromScriptableObject(this);
            string scriptPath = AssetDatabase.GetAssetPath(monoScript);
            string directoryPath = Path.GetDirectoryName(scriptPath);
            return Path.Combine(directoryPath, "Resources");
        }
    }
}
