using System;
using System.Collections.Generic;
using Normal.Realtime.Native;
using System.Runtime.InteropServices;

namespace Normal.Realtime.Native {
    public class Matcher : IDisposable {
        public enum State {
            Error                 = -1,
            Disconnected          = 0,
            Connected             = 1,
            ConnectingToMatcher   = 2,
            ReadyToPingRegions    = 3,
            SendingPingResults    = 4,
            FinishingConnection   = 5,
            Done                  = 6
        };

        // Pointer to native class
        private IntPtr _nativeMatcher = IntPtr.Zero;

        //// Instance
        public Matcher() {
            // Create a native Matcher instance and save the pointer here.
            _nativeMatcher = Plugin.MatcherCreate();
        }

        // NOTE: This may not be called on the same thread that we created the native room with. It's recommended Dispose() is called manually to prevent any issues.
        ~Matcher() {
            // Clean up unmanaged code
            Dispose(false);
        }

        // Ideally called whenever someone is done using a client.
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing) {
            if (_nativeMatcher != IntPtr.Zero) {
                // Delete matcher
                Plugin.MatcherDelete(_nativeMatcher);

                // Prevents any further calls to the native plugin
                _nativeMatcher = IntPtr.Zero;
            }
        }

        // Connect / Disconnect
        public void Connect(string roomName, string appKey, UInt64 clientIdentifier) {
            if (_nativeMatcher == IntPtr.Zero)
                throw RealtimeNativeException.NativePointerIsNull("Matcher");

            Plugin.MatcherConnect(_nativeMatcher, roomName, appKey, clientIdentifier);
        }

        // Disconnect
        public void Disconnect() {
            if (_nativeMatcher == IntPtr.Zero)
                throw RealtimeNativeException.NativePointerIsNull("Matcher");

            Plugin.MatcherDisconnect(_nativeMatcher);
        }

        // Tick
        public int Tick() {
            if (_nativeMatcher == IntPtr.Zero)
                throw RealtimeNativeException.NativePointerIsNull("Matcher");

            return Plugin.MatcherTick(_nativeMatcher);
        }

        public string[] GetIPsToPing() {
            if (_nativeMatcher == IntPtr.Zero)
                throw RealtimeNativeException.NativePointerIsNull("Matcher");

            int numberOfAddressesToPing = Plugin.MatcherGetNumberOfIPsToPing(_nativeMatcher);
            if (numberOfAddressesToPing <= 0)
                return new string[] {};

            List<string> addressesToPing = new List<string>(numberOfAddressesToPing);
            for (int i = 0; i < numberOfAddressesToPing; i++) {
                addressesToPing.Add(Plugin.MatcherGetAddressToPingAtIndex(_nativeMatcher, i));
            }

            return addressesToPing.ToArray();
        }

        public void ContinueConnectionWithPingTimes(PingResult[] pingResults) {
            if (_nativeMatcher == IntPtr.Zero)
                throw RealtimeNativeException.NativePointerIsNull("Matcher");

            Plugin.MatcherContinueConnectionWithPingTimes(_nativeMatcher, pingResults, pingResults.Length);
        }

        public string GetServerError() {
            if (_nativeMatcher == IntPtr.Zero)
                throw RealtimeNativeException.NativePointerIsNull("Matcher");

            return Plugin.MatcherGetServerError(_nativeMatcher);
        }

        public byte[] GetConnectToken() {
            if (_nativeMatcher == IntPtr.Zero)
                throw RealtimeNativeException.NativePointerIsNull("Matcher");

            int    connectTokenLength  = Plugin.MatcherGetConnectTokenLength(_nativeMatcher);
            IntPtr connectTokenPointer = Plugin.MatcherGetConnectToken(_nativeMatcher);
            byte[] connectToken = new byte[connectTokenLength];
            Marshal.Copy(connectTokenPointer, connectToken, 0, connectTokenLength);
            return connectToken;
        }
    }
}
