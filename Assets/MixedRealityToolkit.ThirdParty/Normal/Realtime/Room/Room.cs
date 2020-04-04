using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Normal.Realtime.Native;
using Normal.Realtime.Serialization;

// Represents a room on the server. Manages the connection to the server and the data model.
namespace Normal.Realtime {
    public class Room {
        public delegate void ConnectionStateChanged(Room room, ConnectionState previousConnectionState, ConnectionState connectionState);
        public event ConnectionStateChanged connectionStateChanged;

        public delegate void RPCMessageReceived(Room room, byte[] data, bool reliable);
        public event RPCMessageReceived rpcMessageReceived;

        // Connection state
        public enum ConnectionState {
            Error                 = -1,
            Disconnected          = 0,
            ConnectingToMatcher   = 1,
            PingingRegions        = 2,
            FinishedMatching      = 3,
            ConnectingToRealtime  = 4,
            ConnectedToRealtime   = 5,
            Ready                 = 6, // Datastore is ready
        }
        private ConnectionState _connectionState = ConnectionState.Disconnected;
        public  ConnectionState  connectionState { get { return _connectionState; } }

        public bool connecting {
            get {
                return _connectionState == ConnectionState.ConnectingToMatcher  ||
                       _connectionState == ConnectionState.PingingRegions       ||
                       _connectionState == ConnectionState.FinishedMatching     ||
                       _connectionState == ConnectionState.ConnectingToRealtime ||
                       _connectionState == ConnectionState.ConnectedToRealtime;
            }
        }
        public bool connected       { get { return _connectionState == ConnectionState.Ready; } }
        public bool disconnected    { get { return _connectionState == ConnectionState.Disconnected || _connectionState == ConnectionState.Error; } }

        // Matcher
        private Matcher          _matcher;
        private List<PingResult> _pingResults;
        private int              _ipsLeftToPing = 0;
        private UInt64           _clientIdentifier;
        private byte[]           _connectToken;

        public int clientID { get { return !sessionCapturePlayback ? _client.ClientIndex() : 255; } }

        private double _time;
        public  double  time { get { return _time; } }

        // Datastore
        private Datastore _datastore;
        public  Datastore  datastore { get { return _datastore; } }

        public  double  datastoreFrameDuration = 1.0/20.0;
        private double _deltaTime;
        private uint   _nextUpdateID = 1; // TODO: Convert to zero when 0 isn't used to signal the initial state of the room

        // Native API
        private Client _client;

        // Realtime
        private Realtime _realtime;
        public  Realtime  realtime { get { return _realtime; } }

        private IModel _roomModel;

        // Debug
        private bool _debugLogging = false;
        public  bool  debugLogging { get { return _debugLogging; } set { if (value == _debugLogging) return; Plugin.logLevel = value ? Plugin.LogLevel.LogLevelInfo : Plugin.LogLevel.LogLevelError; _debugLogging = value; } }

        // Session capture / playback
        private SessionCapture _sessionCapture;
        private bool            sessionCaptureRecord   { get { return _sessionCapture != null && _sessionCapture.mode == SessionCapture.Mode.Record; } }
        private bool            sessionCapturePlayback { get { return _sessionCapture != null && _sessionCapture.mode == SessionCapture.Mode.Playback; } }

        public Room() : this(null) {

        }

        public Room(SessionCapture sessionCapture) {
            _sessionCapture = sessionCapture;

            _deltaTime = 0.0;

            _datastore = new Datastore();
            
            if (!sessionCapturePlayback) {
                _client = new Client();
                _client.persistenceMessageReceived += ReceivedPersistenceMessage;
                _client.rpcMessageReceived         += ReceivedRPCMessage;
            }
        }

        ~Room() {
            if (_client != null) {
                _client.persistenceMessageReceived -= ReceivedPersistenceMessage;
                _client.rpcMessageReceived         -= ReceivedRPCMessage;
                _client.Dispose();
            }

            DisposeMatcher();
        }

        // State
        private void SetConnectionState(ConnectionState connectionState) {
            if (connectionState == _connectionState)
                return;

            ConnectionState previousConnectionState = _connectionState;

            _connectionState = connectionState;

            if (connectionStateChanged != null) {
                try {
                    connectionStateChanged(this, previousConnectionState, connectionState);
                } catch (Exception exception) {
                    Debug.LogException(exception);
                }
            }

            switch (connectionState) {
                case ConnectionState.Error:
                    // Stop recording
                    if (sessionCaptureRecord)
                        _sessionCapture.StopRecording();

                    // Clear datastore
                    _datastore.Reset(null);

                    Debug.LogError("Realtime: Connection failed" + (!_debugLogging ? ". Turn on debug logging for more details." : ""));
                    break;
                case ConnectionState.Disconnected:
                    // Stop recording
                    if (sessionCaptureRecord)
                        _sessionCapture.StopRecording();

                    // Clear datastore
                    _datastore.Reset(null);

                    Debug.Log("Realtime: Disconnected");
                    break;
                case ConnectionState.ConnectingToMatcher:
                    if (_debugLogging)
                        Debug.Log("Realtime: Connecting to matcher.");
                    break;
                case ConnectionState.FinishedMatching:
                    // Reset datastore
                    _datastore.Reset(_roomModel);

                    // Set state to connecting to realtime
                    SetConnectionState(ConnectionState.ConnectingToRealtime);
                    _client.Connect(_clientIdentifier, _connectToken);
                    break;
                case ConnectionState.Ready:
                    Debug.Log("Realtime: Connected");
                    break;
                case ConnectionState.PingingRegions:
                    Debug.Log("Realtime: Pinging regions to determine server with lowest latency for this client.");
                    string[] IPsToPing = _matcher.GetIPsToPing();
                    PingServers(IPsToPing);
                    break;
            }
        }

        // Realtime
        public void _SetRealtime(Realtime realtime) {
            _realtime = realtime;
        }

        // Network Statistics
        public NetworkInfo GetNetworkStatistics() {
            if (_client == null)
                return new NetworkInfo();

            return _client.GetNetworkStatistics();
        }

        // Connect / Disconnect
        public void Connect(string roomName, string appKey, IModel roomModel = null) {
            if (!sessionCapturePlayback) {
                ConnectToServer(roomName, appKey, roomModel);
            } else {
                Debug.Log("Realtime: Connect() called on Room with session capture override. Will begin playing back session capture now.");
                StartSessonCapturePlayback(roomModel);
            }
        }

        private void ConnectToServer(string roomName, string appKey, IModel roomModel = null) {
            if (connectionState != ConnectionState.Disconnected && connectionState != ConnectionState.Error) {
                Debug.LogError("Room asked to connect to server, but room is already connecting or connected (" + connectionState + "). Ignoring.");
                return;
            }
            
            Debug.Log("Realtime: Connecting to room \"" + roomName + "\"");

            SetConnectionState(ConnectionState.ConnectingToMatcher);

            _roomModel = roomModel;

            _clientIdentifier = Client.GenerateSecureClientIdentifier();

            // Begin connecting to the matcher
            JoinRoom(roomName, appKey, _clientIdentifier);
        }

        private void StartSessonCapturePlayback(IModel roomModel = null) {
            // Reset datastore
            _datastore.Reset(roomModel);

            // Fire connecting / connected to Realtime events
            SetConnectionState(ConnectionState.ConnectingToRealtime);
            SetConnectionState(ConnectionState.ConnectedToRealtime);

            // Start session capture playback
            byte[] initialDatastore = _sessionCapture.StartPlayback();

            // Initialize datastore
            _datastore.Deserialize(initialDatastore);

            // Reset _deltaTime
            _deltaTime = 0.0;

            // We're ready!
            SetConnectionState(ConnectionState.Ready);
        }

        public void Disconnect() {
            Disconnect(false);
        }

        private void Disconnect(bool error) {
            // Disconnect client
            if (_client != null)
                _client.Disconnect();

            DisposeMatcher();

            // Stop session capture / playback
            if (_sessionCapture != null) {
                _sessionCapture.StopRecording();
                _sessionCapture.StopPlayback();
            }

            // Set connection state
            if (error)
                SetConnectionState(ConnectionState.Error);
            else
                SetConnectionState(ConnectionState.Disconnected);
        }

        // Tick
        public void Tick(double deltaTime) {
            if (!sessionCapturePlayback)
                RoomTick(deltaTime);
            else
                PlaybackTick(deltaTime);
        }

        private void RoomTick(double deltaTime) {
            // Connect to a room on the Matcher if needed.
            MatcherTick();

            // Info
            // We grab time here because we want to have the same timestamp on deserialize/serialize operations called by ClienTick()/PersistenceTick().
            _time = _client.RoomTime();

            // Process incoming network packets and send out anything that's been sitting around.
            // TODO: Given that this is going to send/receive packets all at once. Maybe we should call Tick twice? Call it once here to receive packets + dispatch messages, and again at the end to send them right away.
            // TODO: We can't do the above TODO until _client.Tick() does its own delta time calculation
            ClientTick();

            // Persistence
            PersistenceTick(deltaTime);
        }

        private void MatcherTick() {
            if (_matcher == null)
                return;

            int matcherState = _matcher.Tick();

            switch (matcherState) {
                case (int)Matcher.State.ReadyToPingRegions:
                    SetConnectionState(ConnectionState.PingingRegions);
                    break;

                case (int)Matcher.State.Disconnected:
                    if (_debugLogging)
                        Debug.LogError("Realtime: The matcher has disconnected.");
                    SetConnectionState(ConnectionState.Disconnected);
                    DisposeMatcher();
                    break;
                case (int)Matcher.State.Done:
                    _connectToken = _matcher.GetConnectToken();
                    DisposeMatcher();
                    SetConnectionState(ConnectionState.FinishedMatching);
                    break;
                case (int)Matcher.State.Error:
                    Debug.LogError("Realtime: There was an issue connecting to a room on the matcher. (" + _matcher.GetServerError() + ")");
                    SetConnectionState(ConnectionState.Error);
                    DisposeMatcher();
                    break;
                default:
                    break;
            }
        }

        // Matcher
        /// <summary>
        ///  Join a realtime room. If one with the given roomID is not currently running, it will ping
        ///  servers around the world to find the closest region to start up a new room.
        /// <param name="roomID">The UUID of a room to join.</param>
        /// <param name="appKey">Normal Developer App Key.</param>
        /// <param name="clientIdentifier">Secure random UInt64 used to identify this client</param>
        /// </summary>
        private void JoinRoom(string roomID, string appKey, UInt64 clientIdentifier) {
            if (_matcher != null) {
                DisposeMatcher();
            }

            SetConnectionState(ConnectionState.ConnectingToMatcher);
            _matcher = new Matcher();
            _matcher.Connect(roomID, appKey, clientIdentifier);
        }

        /// <summary>
        /// Ping an array of IPs and return the IP with the lowest ping time.
        /// <param name="servers">An array of IP strings.</param>
        /// </summary>
        private void PingServers(string[] servers) {
            SetConnectionState(ConnectionState.PingingRegions);
            _pingResults = new List<PingResult>();
            _ipsLeftToPing = servers.Length;

            foreach (string ip in servers) {
                _realtime.StartCoroutine(StartPing(ip));
            }
        }

        /// <summary>
        ///   Starts a new ping operation for a given IP.
        /// </summary>
        IEnumerator StartPing(string address) {
            Ping ping = new Ping(address);
            float startTime = Time.realtimeSinceStartup;

            // Wait for up to 8 seconds for the ping to finish.
            while (!ping.isDone && (Time.realtimeSinceStartup - startTime) < 8.0f) {
                yield return null;
            }

            PingFinished(ping);
        }

        /// <summary>
        /// Each ping will call this when it's done or has failed. When we have all the pings
        /// we call the completion handler with the ping with the lowest time.
        /// <param name="p">The ping that just completed.</param>
        /// </summary>
        private void PingFinished(Ping p) {
            PingResult pr = new PingResult(p.ip, p.time);
            _pingResults.Add(pr);

            // Log an error if a ping failed, but we need to respond to the matcher with the time regardless, which will be -1.
            if (!p.isDone && _debugLogging) {
                Debug.LogError(string.Format("Ping failed for address: {0} with ping time: {1}", p.ip, p.time));
            }

            _ipsLeftToPing--;

            if (_ipsLeftToPing <= 0) {
                foreach (PingResult sPing in _pingResults) {
                    if (_debugLogging)
                        Debug.Log(string.Format("Realtime: IP: {0} Ping Time: {1}", sPing.address, sPing.time));
                }

                if (_debugLogging && (_connectionState != ConnectionState.PingingRegions || _matcher == null)) {
                    Debug.LogWarning("Realtime: Yo, Jeff here. If you see this log, lmk, you can safely ignore it, but I want to know what's happening. The client (" + _clientIdentifier + ") sent back ping results in the state (" + connectionState + ") which was not PingingRegions.");
                } else {
                    _matcher.ContinueConnectionWithPingTimes(_pingResults.ToArray());
                }
            }
        }

        private void ClientTick() {
            if (connectionState != ConnectionState.ConnectingToRealtime &&
                connectionState != ConnectionState.ConnectedToRealtime  &&
                connectionState != ConnectionState.Ready)
                return;

            int clientConnectionState = _client.Tick();

            // Update connection state using native client
            switch (clientConnectionState) {
                case -1: // Error
                    SetConnectionState(ConnectionState.Error);
                    break;
                case 0: // Disconnected
                    SetConnectionState(ConnectionState.Disconnected);
                    break;
                case 1: // Connecting
                    SetConnectionState(ConnectionState.ConnectingToRealtime);
                    break;
                case 2: // Connected
                    if (connectionState == ConnectionState.ConnectingToRealtime)
                        SetConnectionState(ConnectionState.ConnectedToRealtime);
                    break;
                default:
                    Debug.LogError("NativeClient returned unknown client state: " + clientConnectionState + ". This is a bug.");
                    break;
            }
        }

        // Persistence
        private void PersistenceTick(double deltaTime) {
            // Wait until we're in sync with the server
            if (_connectionState != ConnectionState.Ready)
                return;

            _deltaTime += deltaTime;
            if (_deltaTime >= datastoreFrameDuration) {
                _deltaTime -= datastoreFrameDuration;

                try {
                    WriteBuffer writeBuffer = _datastore.writeBuffer;

                    // Unreliable
                    _datastore.SerializeDeltaUpdates(false);
                    if (writeBuffer.bytesWritten > 0) {
                        _client.SendPersistenceMessage(writeBuffer.GetBuffer(), writeBuffer.bytesWritten, false);

                        // Record delta update if local session capture is enabled and set to record
                        if (sessionCaptureRecord)
                            _sessionCapture.WriteDeltaUpdate(_time, clientID, writeBuffer.GetBuffer(), writeBuffer.bytesWritten, false, 0, false);
                    }

                    // Reliable
                    _datastore.SerializeDeltaUpdates(true, _nextUpdateID);
                    if (writeBuffer.bytesWritten > 0) {
                        _client.SendPersistenceMessage(writeBuffer.GetBuffer(), writeBuffer.bytesWritten, true);

                        // Record delta update if local session capture is enabled and set to record
                        if (sessionCaptureRecord)
                            _sessionCapture.WriteDeltaUpdate(_time, clientID, writeBuffer.GetBuffer(), writeBuffer.bytesWritten, true, _nextUpdateID, false);

                        // Only increment if data was written
                        _nextUpdateID++;
                    }
                } catch (Exception exception) {
                    Debug.LogException(exception);
                    Debug.LogError("Failed to serialize datastore message. Disconnecting.");
                    Disconnect(true);
                }
            }
        }

        void ReceivedPersistenceMessage(Client client, int sender, byte[] data, bool reliable) {
            if (connectionState != ConnectionState.ConnectingToRealtime &&
                connectionState != ConnectionState.ConnectedToRealtime  &&
                connectionState != ConnectionState.Ready) {
                Debug.LogError("Room received persistence message while not in a connection state where it would be expecting one (" + connectionState + "). This is a bug! Ignoring.");
                return;
            }

            bool updateIsFromUs = sender == clientID;

            // TODO: We need to add try catches around all native -> managed callbacks (ideally in Plugin.cs or Client.cs if we have to). If C# throws an exception, it skips the native code that runs too which can lead to leaks.
            bool isInitialRoomMessage = connectionState != ConnectionState.Ready;
            try {
                // If we're not in a ready state, try to initialize the datastore with this message.
                if (connectionState != ConnectionState.Ready) {
                    // Make sure we didn't accidentally receive an unreliable update that arrived out of order from the original room setup message...
                    if (reliable) {
                        // Retrieve the room time now that the client has calculated it
                        _time = _client.RoomTime();

                        // Initialize datastore
                        _datastore.Deserialize(data);

                        // Reset _deltaTime
                        _deltaTime = 0.0;

                        // We're ready!
                        SetConnectionState(ConnectionState.Ready);

                        // Start session capture if local session capture is enabled and set to record
                        if (sessionCaptureRecord)
                            _sessionCapture.StartRecording(clientID, time, data);
                    }
                } else {
                    // Apply delta update to the datastore
                    uint updateID = _datastore.DeserializeDeltaUpdates(data, reliable, updateIsFromUs);

                    // Record delta update if local session capture is enabled and set to record
                    if (sessionCaptureRecord)
                        _sessionCapture.WriteDeltaUpdate(_time, sender, data, data.Length, reliable, updateID, true);
                }
            } catch (Exception e) {
                bool readUpdateID = reliable;
                if (isInitialRoomMessage)
                    readUpdateID = false;

                Debug.LogException(e);
                BufferAnalyzer bufferAnalyzer = new BufferAnalyzer();
                Debug.LogError("Failed to deserialize incoming datastore message. Disconnecting. (" + isInitialRoomMessage + ") (" + reliable + "): " + bufferAnalyzer.AnalyzeBuffer(new ReadBuffer(data), readUpdateID));
                Disconnect(true);
            }
        }

        private void PlaybackTick(double deltaTime) {
            if (!_sessionCapture.playing)
                return;

            // Tick
            _sessionCapture.PlaybackTick(deltaTime);

            // Read updates until we've read up to the current playback time
            SessionCapture.DeltaUpdate deltaUpdate = _sessionCapture.ReadDeltaUpdate();
            while (deltaUpdate != null) {
                // Update room time
                _time = deltaUpdate.timestamp;

                // Apply delta update
                if (deltaUpdate.data.Length > 0)
                    _datastore.DeserializeDeltaUpdates(deltaUpdate.data, deltaUpdate.reliable, false);

                // Read next update
                deltaUpdate = _sessionCapture.ReadDeltaUpdate();
            }

            if (!_sessionCapture.playing)
                SetConnectionState(ConnectionState.Disconnected);
        }

        private void DisposeMatcher() {
            if (_matcher != null) {
                if (_debugLogging)
                    Debug.Log("Disposing Matcher.");

                _matcher.Dispose();
                _matcher = null;
            }
        }

        // RPC
        public bool SendRPCMessage(byte[] data, bool reliable) {
            return _client.SendRPCMessage(data, data.Length, reliable);
        }

        public bool SendRPCMessage(byte[] data, int dataLength, bool reliable) {
            return _client.SendRPCMessage(data, dataLength, reliable);
        }

        private void ReceivedRPCMessage(Client client, byte[] data, bool reliable) {
            try {
                if (rpcMessageReceived != null)
                    rpcMessageReceived(this, data, reliable);
            } catch (Exception exception) {
                Debug.LogException(exception);
            }
        }

        // Audio
        public AudioInputStream CreateAudioInputStream(bool voice, int sampleRate, int channels) {
            if (sessionCapturePlayback)
                return null; // TODO: Implement audio stream support

            return _client.CreateAudioInputStream(voice, sampleRate, channels);
        }

        public AudioOutputStream GetAudioOutputStream(int clientID, int streamID) {
            if (sessionCapturePlayback)
                return null; // TODO: Implement audio stream support

            return _client.GetAudioOutputStream(clientID, streamID);
        }
    }
}
