using System;

namespace Normal.Realtime.Native {
    public class RealtimeNativeException : Exception {
        public RealtimeNativeException(string message) : base(message) { }

        public static RealtimeNativeException NativePointerIsNull(string className) {
            return new RealtimeNativeException("Attempting to use native object (" + className + ") after it has already been deleted.");
        }
    }
}
