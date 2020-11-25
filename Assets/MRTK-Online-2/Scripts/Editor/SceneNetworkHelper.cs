using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.UI;
using Normal.Realtime;
using prvncher.MRTK_Online.NetworkHelpers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace prvncher.MRTK_Online.Editor.NetworkHelpers
{
    public static class SceneNetworkHelper
    {
        [MenuItem("MRTK-Online/Add network components to ObjectManipulators")]
        static void AddNetworkComponentsToObjectManipulators()
        {
            var objectManipulators = GameObject.FindObjectsOfType<ObjectManipulator>();
            foreach (var objectManipulator in objectManipulators)
            {
                objectManipulator.EnsureComponent<RealtimeTransform>();
                objectManipulator.EnsureComponent<NetworkedManipulator>();
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
    }
}