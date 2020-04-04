using System;
using Normal.Realtime.Native;

namespace Normal.Realtime.Native {
    public class AudioPreprocessor : IDisposable {
        // Pointer to native class
        private IntPtr _nativeAudioPreprocessor = IntPtr.Zero;

        // Instance
        public AudioPreprocessor(int recordSampleRate, int recordFrameSize, bool automaticGainControl, bool noiseSuppression, bool reverbSuppression, bool echoCancellation, int playbackSampleRate, int playbackChannels, float tail) {
            _nativeAudioPreprocessor = Plugin.AudioPreprocessorCreate(recordSampleRate, recordFrameSize, automaticGainControl, noiseSuppression, reverbSuppression, echoCancellation, playbackSampleRate, playbackChannels, tail);
        }

        // NOTE: This may not be called on the same thread that we created the native room with. It's recommended Dispose() is called manually to prevent any issues.
        ~AudioPreprocessor() {
            // Clean up unmanaged code
            Dispose(false);
        }

        // Ideally called whenever someone is done using an audio preprocessor.
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (_nativeAudioPreprocessor != IntPtr.Zero) {
                Plugin.AudioPreprocessorDelete(_nativeAudioPreprocessor);
                _nativeAudioPreprocessor = IntPtr.Zero;
            }
        }

        // Process audio data
        public bool ProcessRecordSamples(float[] audioData) {
            if (_nativeAudioPreprocessor == IntPtr.Zero)
                throw RealtimeNativeException.NativePointerIsNull("AudioPreprocessor");

            return Plugin.AudioPreprocessorProcessRecordFrame(_nativeAudioPreprocessor, audioData, audioData.Length);
        }

        public bool ProcessPlaybackFrame(float[] audioData) {
            if (_nativeAudioPreprocessor == IntPtr.Zero)
                throw RealtimeNativeException.NativePointerIsNull("AudioPreprocessor");

            return Plugin.AudioPreprocessorProcessPlaybackFrame(_nativeAudioPreprocessor, audioData, audioData.Length);
        }
    }
}
