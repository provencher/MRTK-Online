using System;
using Normal.Realtime.Native;

namespace Normal.Realtime.Native {
    public class Microphone : IDisposable {
        // Class
        public static bool PlatformSupported() {
            return Plugin.MicrophonePlatformSupported();
        }

        // Pointer to native class
        private IntPtr _nativeMicrophone = IntPtr.Zero;

        // Instance
        public Microphone() {
            _nativeMicrophone = Plugin.MicrophoneCreate();
        }

        // NOTE: This may not be called on the same thread that we created the native room with. It's recommended Dispose() is called manually to prevent any issues.
        ~Microphone() {
            // Clean up unmanaged code
            Dispose(false);
        }

        // Ideally called whenever someone is done using an audio preprocessor.
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (_nativeMicrophone != IntPtr.Zero) {
                Plugin.MicrophoneDelete(_nativeMicrophone);
                _nativeMicrophone = IntPtr.Zero;
            }
        }

        public bool Start() {
            if (_nativeMicrophone == IntPtr.Zero)
                throw RealtimeNativeException.NativePointerIsNull("Microphone");

            return Plugin.MicrophoneStart(_nativeMicrophone);
		}

		public void Stop() {
            if (_nativeMicrophone == IntPtr.Zero)
                throw RealtimeNativeException.NativePointerIsNull("Microphone");

            Plugin.MicrophoneStop(_nativeMicrophone);
		}

        public int SampleRate() {
            if (_nativeMicrophone == IntPtr.Zero)
                throw RealtimeNativeException.NativePointerIsNull("Microphone");
            
            return Plugin.MicrophoneGetSampleRate(_nativeMicrophone);
        }

        public int Channels() {
            if (_nativeMicrophone == IntPtr.Zero)
                throw RealtimeNativeException.NativePointerIsNull("Microphone");
            
            return Plugin.MicrophoneGetChannels(_nativeMicrophone);
        }
		
        public bool GetAudioData(float[] audioData) {
            if (_nativeMicrophone == IntPtr.Zero)
                throw RealtimeNativeException.NativePointerIsNull("Microphone");

            return Plugin.MicrophoneGetAudioData(_nativeMicrophone, audioData, audioData.Length);
		}
    }
}
