using System;
using System.Runtime.InteropServices;
using AOT;

namespace Normal.Realtime.Native {
    // Types
    [StructLayout(LayoutKind.Sequential)]
    public struct NetworkInfo {
        public float roundTripTime;
        public float percentOfPacketsLost;
        public float sentBandwidth;
        public float receivedBandwidth;
        public float ackedBandwidth;
        public ulong numberOfPacketsSent;
        public ulong numberOfPacketsReceived;
        public ulong numberOfPacketsAcked;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PingResult {
        public string address;
        public int time;

        public PingResult(string address, int time) {
            this.address = address;
            this.time = time;
        }
    }

    public static class Plugin {
#if UNITY_IOS && !UNITY_EDITOR
        private const string realtimeDLLName = "__Internal";
#else
        private const string realtimeDLLName = "RealtimeClient";
#endif

        static Plugin() {
            SetUpLogging();
        }

        // Logging
        public static LogLevel logLevel = LogLevel.LogLevelError;
        
        static void SetUpLogging() {
            IntPtr logFunction = Marshal.GetFunctionPointerForDelegate((LogDelegate)Log);
            SetLogFunction(logFunction);
        }

        [DllImport(realtimeDLLName, EntryPoint = "SetLogFunction")]
        public static extern void SetLogFunction(IntPtr logFunction);

        public enum LogLevel {
            LogLevelInfo    = 0,
            LogLevelWarning = 1,
            LogLevelError   = 2,
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int LogDelegate(LogLevel logLevel, string message);

        [MonoPInvokeCallback(typeof(LogDelegate))]
        static int Log(LogLevel level, string message) {
            if ((int)level < (int)logLevel)
                return message.Length;

#if UNITY_EDITOR
            switch (level) {
                default:
                case LogLevel.LogLevelInfo:
                    UnityEngine.Debug.Log("Realtime (native): " + message);
                    break;
                case LogLevel.LogLevelWarning:
                    UnityEngine.Debug.LogWarning("Realtime (native): " + message);
                    break;
                case LogLevel.LogLevelError:
                    UnityEngine.Debug.LogError("Realtime (native): " + message);
                    break;
            }
            return message.Length;
#else
            switch (logLevel) {
                default:
                case LogLevel.LogLevelInfo:
                    System.Console.WriteLine("Log: " + message);
                    break;
                case LogLevel.LogLevelWarning:
                    System.Console.WriteLine("Log: [Warning] " + message);
                    break;
                case LogLevel.LogLevelError:
                    System.Console.WriteLine("Log: [Error]" + message);
                    break;
            }
            return message.Length;
#endif
        }


        // Matcher
        [DllImport(realtimeDLLName, EntryPoint = "MatcherCreate")]
        public static extern IntPtr MatcherCreate();

        [DllImport(realtimeDLLName, EntryPoint = "MatcherDelete")]
        public static extern void MatcherDelete(IntPtr matcher);

        [DllImport(realtimeDLLName, EntryPoint = "MatcherConnect")]
        public static extern void MatcherConnect(IntPtr matcher, string roomName, string appKey, UInt64 clientIdentifier);

        [DllImport(realtimeDLLName, EntryPoint = "MatcherTick")]
        public static extern int MatcherTick(IntPtr matcher);

        [DllImport(realtimeDLLName, EntryPoint = "MatcherDisconnect")]
        public static extern void MatcherDisconnect(IntPtr matcher);

        [DllImport(realtimeDLLName, EntryPoint = "MatcherGetNumberOfIPsToPing")]
        public static extern int MatcherGetNumberOfIPsToPing(IntPtr matcher);

        [DllImport(realtimeDLLName, CharSet = CharSet.Auto, EntryPoint = "MatcherGetAddressToPingAtIndex")]
        [return: MarshalAs(UnmanagedType.LPStr)]
        public static extern string MatcherGetAddressToPingAtIndex(IntPtr matcher, int index);

        [DllImport(realtimeDLLName, EntryPoint = "MatcherContinueConnectionWithPingTimes")]
        public static extern void MatcherContinueConnectionWithPingTimes(IntPtr matcher, PingResult[] pingResults, int pingResultsLength);

        [DllImport(realtimeDLLName, CharSet = CharSet.Auto, EntryPoint = "MatcherGetServerError")]
        [return: MarshalAs(UnmanagedType.LPStr)]
        public static extern string MatcherGetServerError(IntPtr matcher);

        [DllImport(realtimeDLLName, EntryPoint = "MatcherGetConnectTokenLength")]
        public static extern int MatcherGetConnectTokenLength(IntPtr matcher);

        [DllImport(realtimeDLLName, EntryPoint = "MatcherGetConnectToken")]
        public static extern IntPtr MatcherGetConnectToken(IntPtr matcher);

        // Client
        [DllImport(realtimeDLLName, EntryPoint = "ClientSetUpNetworkStack")]
        public static extern void ClientSetUpNetworkStack();

        [DllImport(realtimeDLLName, EntryPoint = "ClientTearDownNetworkStack")]
        public static extern void ClientTearDownNetworkStack();

        [DllImport(realtimeDLLName, EntryPoint = "ClientGenerateSecureClientIdentifier")]
        public static extern UInt64 ClientGenerateSecureClientIdentifier();

        [DllImport(realtimeDLLName, EntryPoint = "ClientCreate")]
        public static extern IntPtr ClientCreate();

        [DllImport(realtimeDLLName, EntryPoint = "ClientDelete")]
        public static extern void ClientDelete(IntPtr client);

        [DllImport(realtimeDLLName, EntryPoint = "ClientGetNetworkStatistics")]
        public extern static void ClientGetNetworkStatistics(IntPtr client, [Out] out NetworkInfo result);

        [DllImport(realtimeDLLName, EntryPoint = "ClientConnect")]
        public static extern void ClientConnect(IntPtr client, UInt64 clientIdentifier, byte[] connectToken, int connectTokenLength);

        [DllImport(realtimeDLLName, EntryPoint = "ClientDisconnect")]
        public static extern void ClientDisconnect(IntPtr client);

        [DllImport(realtimeDLLName, EntryPoint = "ClientTick")]
        public static extern int ClientTick(IntPtr client);

        [DllImport(realtimeDLLName, EntryPoint = "ClientGetConnecting")]
        public static extern bool ClientGetConnecting(IntPtr client);

        [DllImport(realtimeDLLName, EntryPoint = "ClientGetConnected")]
        public static extern bool ClientGetConnected(IntPtr client);

        [DllImport(realtimeDLLName, EntryPoint = "ClientGetDisconnected")]
        public static extern bool ClientGetDisconnected(IntPtr client);

        [DllImport(realtimeDLLName, EntryPoint = "ClientGetClientIndex")]
        public static extern int ClientGetClientIndex(IntPtr client);

        // Info
        [DllImport(realtimeDLLName, EntryPoint = "ClientGetRoomTime")]
        public static extern double ClientGetRoomTime(IntPtr client);

        // Persistence
        [DllImport(realtimeDLLName, EntryPoint = "ClientSendPersistenceMessage")]
        public static extern bool ClientSendPersistenceMessage(IntPtr client, byte[] data, int dataLength, bool reliable);

        // TODO: Possibly move this delegate to the Client/AudioStream? So the native interface calls a static method on the class, which fires this event that instances listen to, instead of keeping track of this in the plugin.
        public delegate void ClientPersistenceMessageReceived(IntPtr nativeClient, int sender, byte[] data, bool reliable);
        public static event  ClientPersistenceMessageReceived clientPersistenceMessageReceived;
        public static void ClientRegisterPersistenceMessageReceivedCallback(IntPtr client) {
            _ClientSetReceivedPersistenceMessageCallback(client, Marshal.GetFunctionPointerForDelegate((PersistenceMessageReceivedFunction)PersistenceMessageReceived));
        }
        public static void ClientUnregisterPersistenceMessageReceivedCallback(IntPtr client) {
            _ClientSetReceivedPersistenceMessageCallback(client, IntPtr.Zero);
        }

        // TODO: I use byte here instead of bool for reliable because marshalling has trouble with c++ bool vs c# bool vs c BOOL. Fix this at some point.
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate void PersistenceMessageReceivedFunction(IntPtr client, int sender, IntPtr data, int dataLength, byte reliable);
        [MonoPInvokeCallback(typeof(PersistenceMessageReceivedFunction))]
        static   void PersistenceMessageReceived(IntPtr client, int sender, IntPtr data, int dataLength, byte reliable) {
            // Note: This compiles down to a memcpy in mono. I wish I could just use the data directly, but the
            //       only way to avoid the memcpy is to use the pointer directly. And I don't want the deserializer
            //       to be worrying about overflows in C#.
            byte[] managedData = new byte[dataLength];
            Marshal.Copy(data, managedData, 0, dataLength);

            if (clientPersistenceMessageReceived != null)
                clientPersistenceMessageReceived(client, sender, managedData, reliable != 0);
        }
        [DllImport(realtimeDLLName, EntryPoint = "ClientSetReceivedPersistenceMessageCallback")]
        public static extern IntPtr _ClientSetReceivedPersistenceMessageCallback(IntPtr client, IntPtr persistenceMessageReceivedCallback);

        // RPC
        [DllImport(realtimeDLLName, EntryPoint = "ClientSendRPCMessage")]
        public static extern bool ClientSendRPCMessage(IntPtr client, byte[] data, int dataLength, bool reliable);

        // TODO: Possibly move this delegate to the Client/AudioStream? So the native interface calls a static method on the class, which fires this event that instances listen to, instead of keeping track of this in the plugin.
        public delegate void ClientRPCMessageReceived(IntPtr nativeClient, int sender, byte[] data, bool reliable);
        public static event  ClientRPCMessageReceived clientRPCMessageReceived;
        public static void ClientRegisterRPCMessageReceivedCallback(IntPtr client) {
            _ClientSetReceivedRPCMessageCallback(client, Marshal.GetFunctionPointerForDelegate((RPCMessageReceivedFunction)RPCMessageReceived));
        }
        public static void ClientUnregisterRPCMessageReceivedCallback(IntPtr client) {
            _ClientSetReceivedRPCMessageCallback(client, IntPtr.Zero);
        }

        // TODO: I use byte here instead of bool for reliable because marshalling has trouble with c++ bool vs c# bool vs c BOOL. Fix this at some point.
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate void RPCMessageReceivedFunction(IntPtr client, int sender, IntPtr data, int dataLength, byte reliable);
        [MonoPInvokeCallback(typeof(RPCMessageReceivedFunction))]
        static   void RPCMessageReceived(IntPtr client, int sender, IntPtr data, int dataLength, byte reliable) {
            // Note: This compiles down to a memcpy in mono. I wish I could just use the data directly, but the
            //       only way to avoid the memcpy is to use the pointer directly. And I don't want the deserializer
            //       to be worrying about overflows in C#.
            byte[] managedData = new byte[dataLength];
            Marshal.Copy(data, managedData, 0, dataLength);
            
            if (clientRPCMessageReceived != null)
                clientRPCMessageReceived(client, sender, managedData, reliable != 0);
        }
        [DllImport(realtimeDLLName, EntryPoint = "ClientSetReceivedRPCMessageCallback")]
        public static extern IntPtr _ClientSetReceivedRPCMessageCallback(IntPtr client, IntPtr rpcMessageReceivedCallback);

        // Audio
        [DllImport(realtimeDLLName, EntryPoint = "ClientCreateAudioInputStream")]
        public static extern IntPtr ClientCreateAudioInputStream(IntPtr client, bool voice, int sampleRate, int channels);

        [DllImport(realtimeDLLName, EntryPoint = "ClientDeleteAudioInputStream")]
        public static extern void ClientDeleteAudioInputStream(IntPtr audioInputStream);

        // TODO: Possibly move this delegate to the Client/AudioStream? So the native interface calls a static method on the class, which fires this event that instances listen to, instead of keeping track of this in the plugin.
        public delegate void ClientAudioOutputStreamCreated(IntPtr nativeClient, IntPtr nativeAudioOutputStream, IntPtr nativeAudioOutputStreamIdentifier);
        public static event ClientAudioOutputStreamCreated clientAudioOutputStreamCreated;
        public static void ClientRegisterAudioOutputStreamCreatedCallback(IntPtr client) {
            _ClientSetAudioOutputStreamCreatedCallback(client, Marshal.GetFunctionPointerForDelegate((AudioOutputStreamCreatedFunction)AudioOutputStreamCreated));
        }
        public static void ClientUnregisterAudioOutputStreamCreatedCallback(IntPtr client) {
            _ClientSetAudioOutputStreamCreatedCallback(client, IntPtr.Zero);
        }
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate void AudioOutputStreamCreatedFunction(IntPtr client, IntPtr audioOutputStream, IntPtr audioOutputStreamIdentifier);
        [MonoPInvokeCallback(typeof(AudioOutputStreamCreatedFunction))]
        static   void AudioOutputStreamCreated(IntPtr client, IntPtr audioOutputStream, IntPtr audioOutputStreamIdentifier) {
            if (clientAudioOutputStreamCreated != null)
                clientAudioOutputStreamCreated(client, audioOutputStream, audioOutputStreamIdentifier);
            else
                UnityEngine.Debug.LogError("Received AudioOutputStreamCreated callback, but no clients are listening. This will leak memory.");
        }
        [DllImport(realtimeDLLName, EntryPoint = "ClientSetAudioOutputStreamCreatedCallback")]
        public static extern IntPtr _ClientSetAudioOutputStreamCreatedCallback(IntPtr client, IntPtr audioStreamCreatedCallback);

        [DllImport(realtimeDLLName, EntryPoint = "ClientDeleteAudioOutputStream")]
        public static extern void ClientDeleteAudioOutputStream(IntPtr audioOutputStream);

        // TODO: Possibly move this delegate to the Client/AudioStream? So the native interface calls a static method on the class, which fires this event that instances listen to, instead of keeping track of this in the plugin.
        public delegate void ClientAudioOutputStreamClosed(IntPtr nativeClient, IntPtr nativeAudioOutputStreamIdentifier);
        public static event ClientAudioOutputStreamClosed clientAudioOutputStreamClosed;
        public static void ClientRegisterAudioOutputStreamClosedCallback(IntPtr client) {
            _ClientSetAudioOutputStreamClosedCallback(client, Marshal.GetFunctionPointerForDelegate((AudioOutputStreamClosedFunction)AudioOutputStreamClosed));
        }
        public static void ClientUnregisterAudioOutputStreamClosedCallback(IntPtr client) {
            _ClientSetAudioOutputStreamClosedCallback(client, IntPtr.Zero);
        }
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate void AudioOutputStreamClosedFunction(IntPtr client, IntPtr audioOutputStreamIdentifier);
        [MonoPInvokeCallback(typeof(AudioOutputStreamClosedFunction))]
        static void AudioOutputStreamClosed(IntPtr client, IntPtr audioOutputStreamIdentifier) {
            if (clientAudioOutputStreamClosed != null)
                clientAudioOutputStreamClosed(client, audioOutputStreamIdentifier);
        }
        [DllImport(realtimeDLLName, EntryPoint = "ClientSetAudioOutputStreamClosedCallback")]
        public static extern IntPtr _ClientSetAudioOutputStreamClosedCallback(IntPtr client, IntPtr audioStreamClosedCallback);

        // Audio Input Stream
        [DllImport(realtimeDLLName, EntryPoint = "AudioInputStreamGetClientID")]
        public static extern int AudioInputStreamGetClientID(IntPtr audioInputStream);

        [DllImport(realtimeDLLName, EntryPoint = "AudioInputStreamGetStreamID")]
        public static extern int AudioInputStreamGetStreamID(IntPtr audioInputStream);

        [DllImport(realtimeDLLName, EntryPoint = "AudioInputStreamClose")]
        public static extern void AudioInputStreamClose(IntPtr audioInputStream);

        [DllImport(realtimeDLLName, EntryPoint = "AudioInputStreamSendRawAudioData")]
        public static extern bool AudioInputStreamSendRawAudioData(IntPtr audioInputStream, float[] audioData, int audioDataLength);

        // Audio Output Stream
        [DllImport(realtimeDLLName, EntryPoint = "AudioOutputStreamGetClientID")]
        public static extern int AudioOutputStreamGetClientID(IntPtr audioOutputStream);

        [DllImport(realtimeDLLName, EntryPoint = "AudioOutputStreamGetStreamID")]
        public static extern int AudioOutputStreamGetStreamID(IntPtr audioOutputStream);

        [DllImport(realtimeDLLName, EntryPoint = "AudioOutputStreamGetSampleRate")]
        public static extern int AudioOutputStreamGetSampleRate(IntPtr audioOutputStream);

        [DllImport(realtimeDLLName, EntryPoint = "AudioOutputStreamSetSampleRate")]
        public static extern void AudioOutputStreamSetSampleRate(IntPtr audioOutputStream, int sampleRate);

        [DllImport(realtimeDLLName, EntryPoint = "AudioOutputStreamGetChannels")]
        public static extern int AudioOutputStreamGetChannels(IntPtr audioOutputStream);

        [DllImport(realtimeDLLName, EntryPoint = "AudioOutputStreamGetIsOpen")]
        public static extern int AudioOutputStreamGetIsOpen(IntPtr audioOutputStream);

        [DllImport(realtimeDLLName, EntryPoint = "AudioOutputStreamGetAudioData")]
        public static extern int AudioOutputStreamGetAudioData(IntPtr audioOutputStream, float[] audioData, int audioDataLength);

        // Audio Preprocessor
        [DllImport(realtimeDLLName, EntryPoint = "AudioPreprocessorCreate")]
        public static extern IntPtr AudioPreprocessorCreate(int recordSampleRate, int recordFrameSize, bool automaticGainControl, bool noiseSuppression, bool reverbSuppression, bool echoCancellation, int playbackSampleRate, int playbackChannels, float tail);

        [DllImport(realtimeDLLName, EntryPoint = "AudioPreprocessorDelete")]
        public static extern void AudioPreprocessorDelete(IntPtr audioPreprocessor);

        [DllImport(realtimeDLLName, EntryPoint = "AudioPreprocessorProcessRecordFrame")]
        public static extern bool AudioPreprocessorProcessRecordFrame(IntPtr audioPreprocessor, float[] audioData, int audioDataLength);

        [DllImport(realtimeDLLName, EntryPoint = "AudioPreprocessorProcessPlaybackFrame")]
        public static extern bool AudioPreprocessorProcessPlaybackFrame(IntPtr audioPreprocessor, float[] audioData, int audioDataLength);

        // Microphone
        [DllImport(realtimeDLLName, EntryPoint = "MicrophonePlatformSupported")]
        public static extern bool MicrophonePlatformSupported();

        [DllImport(realtimeDLLName, EntryPoint = "MicrophoneCreate")]
        public static extern IntPtr MicrophoneCreate();

        [DllImport(realtimeDLLName, EntryPoint = "MicrophoneDelete")]
        public static extern void MicrophoneDelete(IntPtr microphone);

        [DllImport(realtimeDLLName, EntryPoint = "MicrophoneStart")]
        public static extern bool MicrophoneStart(IntPtr microphone);

        [DllImport(realtimeDLLName, EntryPoint = "MicrophoneStop")]
        public static extern void MicrophoneStop(IntPtr microphone);

        [DllImport(realtimeDLLName, EntryPoint = "MicrophoneGetSampleRate")]
        public static extern int MicrophoneGetSampleRate(IntPtr microphone);

        [DllImport(realtimeDLLName, EntryPoint = "MicrophoneGetChannels")]
        public static extern int MicrophoneGetChannels(IntPtr microphone);

        [DllImport(realtimeDLLName, EntryPoint = "MicrophoneGetAudioData")]
        public static extern bool MicrophoneGetAudioData(IntPtr microphone, float[] audioData, int audioDataLength);
    }
}