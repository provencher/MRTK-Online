using System;
using Normal.Realtime.Native;

namespace Normal.Realtime.Native {
    public class AudioOutputStream : IDisposable {
        // Pointer to native class
        private IntPtr _nativeAudioOutputStream = IntPtr.Zero;
        private IntPtr _nativeAudioOutputStreamIdentifier = IntPtr.Zero;
        public  bool AudioOutputStreamMatchesIdentifier(IntPtr nativeAudioOutputStreamIdentifier) {
            return _nativeAudioOutputStreamIdentifier != IntPtr.Zero && nativeAudioOutputStreamIdentifier == _nativeAudioOutputStreamIdentifier;
        }

        // Instance
        public AudioOutputStream(IntPtr nativeAudioOutputStream, IntPtr nativeAudioOutputStreamIdentifier) {
            _nativeAudioOutputStream           = nativeAudioOutputStream;
            _nativeAudioOutputStreamIdentifier = nativeAudioOutputStreamIdentifier;
        }

        // NOTE: This may not be called on the same thread that we created the native room with. It's recommended Dispose() is called manually to prevent any issues.
        ~AudioOutputStream() {
            // Clean up unmanaged code
            Dispose(false);
        }

        // Ideally called whenever someone is done using an audio source.
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (_nativeAudioOutputStream != IntPtr.Zero) {
                Plugin.ClientDeleteAudioOutputStream(_nativeAudioOutputStream);
                _nativeAudioOutputStream = IntPtr.Zero;
                _nativeAudioOutputStreamIdentifier = IntPtr.Zero;
            }
        }

        public bool nativePointerIsNull { get { return _nativeAudioOutputStream == IntPtr.Zero; } }

        // Metadata
        public int ClientID() {
            if (_nativeAudioOutputStream == IntPtr.Zero)
                throw RealtimeNativeException.NativePointerIsNull("AudioOutputStream");

            // TODO: It might be worth caching this if the calls are expensive
            return Plugin.AudioOutputStreamGetClientID(_nativeAudioOutputStream);
        }

        public int StreamID() {
            if (_nativeAudioOutputStream == IntPtr.Zero)
                throw RealtimeNativeException.NativePointerIsNull("AudioOutputStream");

            // TODO: It might be worth caching this if the calls are expensive
            return Plugin.AudioOutputStreamGetStreamID(_nativeAudioOutputStream);
        }

        public int SampleRate() {
            if (_nativeAudioOutputStream == IntPtr.Zero)
                throw RealtimeNativeException.NativePointerIsNull("AudioOutputStream");

            return Plugin.AudioOutputStreamGetSampleRate(_nativeAudioOutputStream);
        }

        // Set desired sample rate. Native plugin will resample to it automatically.
        // Set to 0.0f to reset it back to the sample rate that's sent from the server.
        public void SetSampleRate(int sampleRate) {
            if (_nativeAudioOutputStream == IntPtr.Zero)
                throw RealtimeNativeException.NativePointerIsNull("AudioOutputStream");

            Plugin.AudioOutputStreamSetSampleRate(_nativeAudioOutputStream, sampleRate);
        }

        public int Channels() {
            if (_nativeAudioOutputStream == IntPtr.Zero)
                throw RealtimeNativeException.NativePointerIsNull("AudioOutputStream");

            return Plugin.AudioOutputStreamGetChannels(_nativeAudioOutputStream);
        }

        public bool IsOpen() {
            if (_nativeAudioOutputStream == IntPtr.Zero)
                return false;

            return Plugin.AudioOutputStreamGetIsOpen(_nativeAudioOutputStream) != 0;
        }

        public bool GetAudioData(float[] audioData) {
            if (_nativeAudioOutputStream == IntPtr.Zero)
                throw RealtimeNativeException.NativePointerIsNull("AudioOutputStream");

            // TODO: Fix this
            return Plugin.AudioOutputStreamGetAudioData(_nativeAudioOutputStream, audioData, audioData.Length) > 0;
        }
    }
}
