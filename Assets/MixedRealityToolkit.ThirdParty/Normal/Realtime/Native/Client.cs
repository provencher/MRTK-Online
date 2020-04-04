using System;
using System.Collections.Generic;
using Normal.Realtime.Native;

namespace Normal.Realtime.Native {
    public class Client : IDisposable {
        //// Class
        private static volatile int    __numberOfClients = 0;
        private static readonly object __numberOfClientsLock = new Object();
        private static void SetUpNetworkStackIfNeeded() {
            lock (__numberOfClientsLock) {
                if (__numberOfClients == 0)
                    Plugin.ClientSetUpNetworkStack();
                __numberOfClients++;
            }
        }

        private static void TearDownNetworkStackIfNeeded() {
            lock (__numberOfClientsLock) {
                __numberOfClients--;
                if (__numberOfClients == 0)
                    Plugin.ClientTearDownNetworkStack();
            }
        }

        public static UInt64 GenerateSecureClientIdentifier() {
            return Plugin.ClientGenerateSecureClientIdentifier();
        }

        // Events
        public delegate void PersistenceMessageReceived(Client client, int sender, byte[] data, bool reliable);
        public delegate void RPCMessageReceived(Client client, byte[] data, bool reliable);
        public delegate void AudioOutputStreamCreated(Client client, AudioOutputStream audioOutputStream);
        public delegate void AudioOutputStreamClosed(Client client, AudioOutputStream audioOutputStream);

        public event PersistenceMessageReceived persistenceMessageReceived;
        public event RPCMessageReceived         rpcMessageReceived;
        public event AudioOutputStreamCreated   audioOutputStreamCreated;
        public event AudioOutputStreamClosed    audioOutputStreamClosed;

        // Pointer to native class
        private IntPtr _nativeClient = IntPtr.Zero;

        private List<AudioInputStream>  _audioInputStreams;
        private List<AudioOutputStream> _audioOutputStreams;

        //// Instance
        public Client() {
            // Set up network stack if needed
            SetUpNetworkStackIfNeeded();

            // Create a native Client instance and save the pointer here.
            _nativeClient = Plugin.ClientCreate();
            RegisterCallbacks();

            // Lists to hold the audio streams
            _audioInputStreams  = new List<AudioInputStream>();
            _audioOutputStreams = new List<AudioOutputStream>();
        }
        
        // NOTE: This may not be called on the same thread that we created the native client with. It's recommended Dispose() is called manually to prevent any issues.
        ~Client() {
            // Clean up unmanaged code
            Dispose(false);
        }
        
        // Ideally called whenever someone is done using a client.
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        private void Dispose(bool disposing) {
            if (_nativeClient != IntPtr.Zero) {
                // Unregister callbacks
                UnregisterCallbacks();

                // Dispose of all audio streams
                foreach (AudioInputStream audioInputStream in _audioInputStreams)
                    audioInputStream.Dispose();
                _audioInputStreams.Clear();
                foreach (AudioOutputStream audioOutputStream in _audioOutputStreams)
                    audioOutputStream.Dispose();
                _audioOutputStreams.Clear();

                // Delete client
                Plugin.ClientDelete(_nativeClient);

                // Prevents any further calls to the native plugin
                _nativeClient = IntPtr.Zero;

                // Tear down network stack if needed
                TearDownNetworkStackIfNeeded();
            }
        }

        // Callbacks
        private void RegisterCallbacks() {
            // Persistence
            Plugin.ClientRegisterPersistenceMessageReceivedCallback(_nativeClient);
            Plugin.clientPersistenceMessageReceived += ReceivedPersistenceMessageCallback;

            // RPC
            Plugin.ClientRegisterRPCMessageReceivedCallback(_nativeClient);
            Plugin.clientRPCMessageReceived += ReceivedRPCMessageCallback;

            // Audio
            // Tell the plugin to start piping audioOutputStreamCreated callbacks through the clientAudioOutputStreamCreated event.
            // NOTE: By registering for callbacks, the native interface is going to allocate an AudioOutputStream strong reference
            // for us. We need to ensure we call ClientDeleteAudioOutputStream() on all pointers we receive when we're done with them.
            // Luckily, the AudioOutputStream C# wrapper takes care of this in its dispose method.
            Plugin.ClientRegisterAudioOutputStreamCreatedCallback(_nativeClient);
            Plugin.ClientRegisterAudioOutputStreamClosedCallback(_nativeClient);

            // Register for the event (Note: this is static, so it fires for all clients)
            Plugin.clientAudioOutputStreamCreated += AudioOutputStreamCreatedCallback;
            Plugin.clientAudioOutputStreamClosed  += AudioOutputStreamClosedCallback;
        }

        private void UnregisterCallbacks() {
            // Persistence
            Plugin.ClientUnregisterPersistenceMessageReceivedCallback(_nativeClient);
            Plugin.clientPersistenceMessageReceived -= ReceivedPersistenceMessageCallback;

            // RPC
            // TODO: If multiple clients are created, and one is destroyed. All persistence messages will stop because we're unregister the callback
            Plugin.ClientUnregisterRPCMessageReceivedCallback(_nativeClient);
            Plugin.clientRPCMessageReceived -= ReceivedRPCMessageCallback;

            // Audio
            Plugin.ClientUnregisterAudioOutputStreamCreatedCallback(_nativeClient);
            Plugin.ClientUnregisterAudioOutputStreamClosedCallback(_nativeClient);
            Plugin.clientAudioOutputStreamCreated -= AudioOutputStreamCreatedCallback;
            Plugin.clientAudioOutputStreamClosed  -= AudioOutputStreamClosedCallback;
        }

        // Network Statistics
        public NetworkInfo GetNetworkStatistics() {
            if (_nativeClient == IntPtr.Zero)
                throw RealtimeNativeException.NativePointerIsNull("Client");

            NetworkInfo networkInfo;
            Plugin.ClientGetNetworkStatistics(_nativeClient, out networkInfo);
            return networkInfo;
        }

        // Connect / Disconnect
        public void Connect(UInt64 clientIdentifier, byte[] connectToken) {
            if (_nativeClient == IntPtr.Zero)
                throw RealtimeNativeException.NativePointerIsNull("Client");

            // TODO: Connect/Disconnect need to fail gracefully if called multiple times.
            Plugin.ClientConnect(_nativeClient, clientIdentifier, connectToken, connectToken.Length);
        }
        
        public void Disconnect() {
            if (_nativeClient == IntPtr.Zero)
                throw RealtimeNativeException.NativePointerIsNull("Client");

            Plugin.ClientDisconnect(_nativeClient);
        }

        // Tick
        public int Tick() {
            if (_nativeClient == IntPtr.Zero)
                throw RealtimeNativeException.NativePointerIsNull("Client");

            // TODO: ClientTick should do its own deltaTime calculation. It will be more precise and will prevent the client and server from getting out of sync.
            return Plugin.ClientTick(_nativeClient);
        }

        // Metadata
        public int ClientIndex() {
            if (_nativeClient == IntPtr.Zero)
                throw RealtimeNativeException.NativePointerIsNull("Client");

            return Plugin.ClientGetClientIndex(_nativeClient);
        }

        public double RoomTime() {
            if (_nativeClient == IntPtr.Zero)
                throw RealtimeNativeException.NativePointerIsNull("Client");

            return Plugin.ClientGetRoomTime(_nativeClient);
        }

        // Persistence
        public bool SendPersistenceMessage(byte[] data, int dataLength, bool reliable) {
            if (_nativeClient == IntPtr.Zero)
                throw RealtimeNativeException.NativePointerIsNull("Client");

            return Plugin.ClientSendPersistenceMessage(_nativeClient, data, dataLength, reliable);
        }

        void ReceivedPersistenceMessageCallback(IntPtr nativeClient, int sender, byte[] data, bool reliable) {
            if (nativeClient != _nativeClient)
                return;

            if (persistenceMessageReceived != null)
                persistenceMessageReceived(this, sender, data, reliable);
        }

        // RPC
        public bool SendRPCMessage(byte[] data, int dataLength, bool reliable) {
            if (_nativeClient == IntPtr.Zero)
                throw RealtimeNativeException.NativePointerIsNull("Client");

            return Plugin.ClientSendRPCMessage(_nativeClient, data, dataLength, reliable);
        }

        void ReceivedRPCMessageCallback(IntPtr nativeClient, int sender, byte[] data, bool reliable) {
            if (nativeClient != _nativeClient)
                return;

            if (rpcMessageReceived != null)
                rpcMessageReceived(this, data, reliable);
        }

        // Audio
        public AudioInputStream CreateAudioInputStream(bool voice, int sampleRate, int channels) {
            if (_nativeClient == IntPtr.Zero)
                throw RealtimeNativeException.NativePointerIsNull("Client");

            if (sampleRate <= 0)
                throw new ArgumentException("Attempting to create audio stream with invalid sample rate (" + sampleRate + ").");
            if (channels <= 0)
                throw new ArgumentException("Attempting to create audio stream with an invalid number of channels (" + channels + ").");

            IntPtr nativeAudioInputStream = Plugin.ClientCreateAudioInputStream(_nativeClient, voice, sampleRate, channels);

            AudioInputStream audioInputStream = new AudioInputStream(nativeAudioInputStream);
            _audioInputStreams.Add(audioInputStream);

            return audioInputStream;
        }

        void AudioOutputStreamCreatedCallback(IntPtr nativeClient, IntPtr nativeAudioOutputStream, IntPtr nativeAudioOutputStreamIdentifier) {
            // This method is fired for every client, ignore events meant for other clients
            if (nativeClient != _nativeClient)
                return;

            AudioOutputStream audioOutputStream = new AudioOutputStream(nativeAudioOutputStream, nativeAudioOutputStreamIdentifier);
            _audioOutputStreams.Add(audioOutputStream);

            // Fire event
            if (audioOutputStreamCreated != null)
                audioOutputStreamCreated(this, audioOutputStream);
        }

        void AudioOutputStreamClosedCallback(IntPtr nativeClient, IntPtr nativeAudioOutputStreamIdentifier) {
            // This method is fired for every client, ignore events meant for other clients
            if (nativeClient != _nativeClient)
                return;

            AudioOutputStream audioOutputStreamToClose = null;
            foreach (AudioOutputStream audioOutputStream in _audioOutputStreams) {
                // Find the matching audio output stream.
                if (audioOutputStream.AudioOutputStreamMatchesIdentifier(nativeAudioOutputStreamIdentifier)) {
                    audioOutputStreamToClose = audioOutputStream;
                    break;
                }
            }

            if (audioOutputStreamToClose != null) {
                // Fire event
                if (audioOutputStreamClosed != null)
                    audioOutputStreamClosed(this, audioOutputStreamToClose);

                // Dispose of AudioOutputStream
                _audioOutputStreams.Remove(audioOutputStreamToClose);
                audioOutputStreamToClose.Dispose();
            }
        }

        public AudioOutputStream GetAudioOutputStream(int clientID, int streamID) {
            // Find the matching audio output stream.
            foreach (AudioOutputStream audioOutputStream in _audioOutputStreams) {
                if (audioOutputStream.ClientID() == clientID && audioOutputStream.StreamID() == streamID)
                    return audioOutputStream;
            }

            // Nothing found
            return null;
        }
    }
}
