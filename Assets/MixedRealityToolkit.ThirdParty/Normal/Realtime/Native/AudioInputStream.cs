using System;
using Normal.Realtime.Native;

namespace Normal.Realtime.Native {
    public class AudioInputStream : IDisposable {
        // Pointer to native class
        private IntPtr _nativeAudioInputStream = IntPtr.Zero;

        // Instance
        public AudioInputStream(IntPtr nativeAudioInputStream) {
            _nativeAudioInputStream = nativeAudioInputStream;
        }

        // NOTE: This may not be called on the same thread that we created the native room with. It's recommended Dispose() is called manually to prevent any issues.
        ~AudioInputStream() {
            // Clean up unmanaged code
            Dispose(false);
        }

        // Ideally called whenever someone is done using an audio source.
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (_nativeAudioInputStream != IntPtr.Zero) {
                Plugin.ClientDeleteAudioInputStream(_nativeAudioInputStream);
                _nativeAudioInputStream = IntPtr.Zero;
            }
        }

        // Metadata
        public int ClientID() {
            if (_nativeAudioInputStream == IntPtr.Zero)
                throw RealtimeNativeException.NativePointerIsNull("AudioInputStream");

            // TODO: It might be worth caching this if the calls are expensive
            return Plugin.AudioInputStreamGetClientID(_nativeAudioInputStream);
        }

        public int StreamID() {
            if (_nativeAudioInputStream == IntPtr.Zero)
                throw RealtimeNativeException.NativePointerIsNull("AudioInputStream");

            // TODO: It might be worth caching this if the calls are expensive
            return Plugin.AudioInputStreamGetStreamID(_nativeAudioInputStream);
        }

        // Close
        public void Close() {
            if (_nativeAudioInputStream == IntPtr.Zero)
                return;

            Plugin.AudioInputStreamClose(_nativeAudioInputStream);
        }

        // Send audio data
        public bool SendRawAudioData(float[] audioData) {
            if (_nativeAudioInputStream == IntPtr.Zero)
                throw RealtimeNativeException.NativePointerIsNull("AudioInputStream");

            return Plugin.AudioInputStreamSendRawAudioData(_nativeAudioInputStream, audioData, audioData.Length);
        }
    }
}
