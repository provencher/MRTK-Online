using System;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Normal.Realtime {
    public class OculusMicrophoneDevice : IDisposable {
        private static Type       __oculusPlatformCore;
        private static MethodInfo __IsInitializedMethod;

        static OculusMicrophoneDevice() {
            __oculusPlatformCore = Type.GetType("Oculus.Platform.Core,Assembly-CSharp");
            if (__oculusPlatformCore != null)
                __IsInitializedMethod = __oculusPlatformCore.GetMethod("IsInitialized", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        }

        public static bool IsOculusPlatformAvailable() {
            return __oculusPlatformCore != null;
        }

        public static bool IsOculusPlatformInitialized() {
            if (__IsInitializedMethod == null) {
                Debug.LogError("Normal: Oculus Platform SDK detected, but unable to determine if it is initialized. Oculus native microphone support will be unavailable.");
                return false;
            }

            object returnValue = __IsInitializedMethod.Invoke(null, null);
            if (returnValue == null)
                return false;

            return (bool)returnValue;
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
#    if UNITY_64 || UNITY_EDITOR_64
        public const string OVR_DLL_NAME = "LibOVRPlatform64_1";
#else
        public const string OVR_DLL_NAME = "LibOVRPlatform32_1";
#endif
#elif UNITY_EDITOR || UNITY_EDITOR_64
        public const string OVR_DLL_NAME = "ovrplatform";
#else
        public const string OVR_DLL_NAME = "ovrplatformloader";
#endif

        [DllImport(OVR_DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ovr_Microphone_Create();

        [DllImport(OVR_DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ovr_Microphone_Destroy(IntPtr obj);

        [DllImport(OVR_DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ovr_Microphone_Start(IntPtr obj);

        [DllImport(OVR_DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ovr_Microphone_Stop(IntPtr obj);

        [DllImport(OVR_DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr ovr_Microphone_GetPCMFloat(IntPtr obj, float[] outputBuffer, UIntPtr outputBufferNumElements);

        private IntPtr    _microphone;

        public OculusMicrophoneDevice() {
            if (!IsOculusPlatformInitialized()) {
                Debug.LogError("Normcore: Oculus platform is not initialized. OculusMicrophoneDevice will not work properly.");
                return;
            }

            // Create a native microphone instance
            try {
                _microphone = ovr_Microphone_Create();
            } catch (Exception exception) {
                Debug.LogError("Normcore: Failed to initialize Oculus platform microphone.");
                Debug.LogException(exception);
            }

            if (_microphone == IntPtr.Zero) {
                Debug.LogError("Normcore: Oculus platform API returned null microphone reference. This is a bug!");
                return;
            }
        }

        ~OculusMicrophoneDevice() {
            Dispose(false);
        }

        // Ideally called whenever someone is done using a microphone device.
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (_microphone != IntPtr.Zero) {
                ovr_Microphone_Destroy(_microphone);
                _microphone = IntPtr.Zero;
            }
        }

        public void Start() {
            if (_microphone == IntPtr.Zero) {
                Debug.LogError("Normcore: Start called on Oculus microphone device that is not initialized. Did it fail to initialize or was Dispose called?");
                return;
            }

            try {
                ovr_Microphone_Start(_microphone);
            } catch (Exception exception) {
                Debug.LogError("Normcore: Failed to start Oculus platform microphone.");
                Debug.LogException(exception);
            }
        }

        public void Stop() {
            if (_microphone == IntPtr.Zero)
                return;

            try {
                ovr_Microphone_Stop(_microphone);
            } catch (Exception exception) {
                Debug.LogError("Normcore: Failed to stop Oculus platform microphone.");
                Debug.LogException(exception);
            }
        }

        public bool GetAudioData(float[] audioData) {
            if (_microphone == IntPtr.Zero) {
                Debug.LogError("Normcore: GetBufferData called on Oculus microphone device that is not initialized. Did it fail to initialize or was Dispose called?");
                return false;
            }

            UIntPtr samplesRead = ovr_Microphone_GetPCMFloat(_microphone, audioData, (UIntPtr)audioData.Length);
            if (samplesRead == (UIntPtr)0)
                return false;

            if (samplesRead.ToUInt32() < audioData.Length) {
                Debug.LogError("Normcore: Oculus microphone device read less audio samples than we requested. (" + audioData.Length + ", " + samplesRead + ")");
                return false;
            }

            return true;
        }
    }
}
