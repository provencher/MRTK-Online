using System;
using UnityEngine;
using Normal.Realtime.Native;
using Normal.Realtime.Serialization;
using Normal.Utility;

namespace Normal.Realtime {
    [ExecutionOrder(-1)] // Make sure our Update() runs before the default to ensure _microphoneDbLevel has been calculated for CalculateVoiceVolume()
    public class RealtimeAvatarVoice : RealtimeComponent {
        public  float voiceVolume { get; private set; }

        private RealtimeAvatarVoiceModel _model;
        public  RealtimeAvatarVoiceModel  model { get { return _model; } set { SetModel(value); } }

        // TODO: Remove this once we have the equivalent of view.ownedLocally and view.ownedLocallyInhierarchy
        private bool isOwnedLocally {
            get {
                RealtimeView view = realtimeView;
                while (view.isChildView) {
                    if (view.isOwnedLocally)
                        return true;

                    view = view.transform.parent.GetComponent<RealtimeView>();
                }
                return view.isOwnedLocally;
            }
        }

        private bool _mute = false;
        public  bool  mute { get { return _mute; } set { SetMute(value); } }
        
        private bool                   _hasMicrophone { get { return (_oculusMicrophoneDevice != null || _nativeMicrophoneDevice != null || _unityMicrophoneDevice != null); } }
        private OculusMicrophoneDevice _oculusMicrophoneDevice;
        private Native.Microphone      _nativeMicrophoneDevice;
        private MicrophoneDevice       _unityMicrophoneDevice;
        private AudioDeviceDataReader  _unityMicrophoneDeviceDataReader;
        private int                    _microphoneSampleRate;
        private int                    _microphoneChannels;
        private int                    _microphoneFrameSize;
        private AudioInputStream       _microphoneStream;

        private AudioPreprocessor                 _audioPreprocessor;
        private AudioPreprocessorPlaybackListener _audioPreprocessorPlaybackListener;
        private float _microphoneDbLevel = -42.0f;

        private AudioOutput _audioOutput;

        void Update() {
            // Send microphone data if needed
            SendMicrophoneData();

            // Calculate voice volume level
            CalculateVoiceVolume();
        }

        void OnDestroy() {
            if (_model != null)
                _model.clientIDStreamIDUpdated -= ClientIDStreamIDUpdated;
            DisconnectAudioStream();
        }

        private void CalculateVoiceVolume() {
            float averageDbSample = -42.0f;

            if (_hasMicrophone) {
                averageDbSample = _microphoneDbLevel;
            } else if (_audioOutput != null) {
                averageDbSample = _audioOutput._dbLevel;
            }

            // These are arbitrary values I picked from my own testing.
            float volumeMinDb = -42.0f;
            float volumeMaxDb = -5.0f;
            float volumeRange = volumeMaxDb - volumeMinDb;

            float normalizedVolume = (averageDbSample - volumeMinDb) / volumeRange;
            if (normalizedVolume < 0.0f)
                normalizedVolume = 0.0f;
            if (normalizedVolume > 1.0f)
                normalizedVolume = 1.0f;

            voiceVolume = normalizedVolume;
        }

        void SetModel(RealtimeAvatarVoiceModel model) {
            if (_model != null)
                _model.clientIDStreamIDUpdated -= ClientIDStreamIDUpdated;
            DisconnectAudioStream();

            _model = model;

            ConnectAudioStream();
            if (_model != null)
                _model.clientIDStreamIDUpdated += ClientIDStreamIDUpdated;
        }

        void ClientIDStreamIDUpdated(RealtimeAvatarVoiceModel model) {
            if (isOwnedLocally)
                return;

            // Connect the new audio stream
            ConnectAudioStream();
        }

        void ConnectAudioStream() {
            // Delete the old audio stream
            DisconnectAudioStream();

            if (_model == null)
                return;

            if (isOwnedLocally) {
                // Local player, create microphone stream

                // First check if this platform supports our native microphone wrapper (lower latency + native echo cancellation if available)
                _microphoneSampleRate = 48000;
                _microphoneChannels   = 1;

                // Check for Oculus native microphone device API
                bool foundOculusMicrophoneDevice = false;
                if (OculusMicrophoneDevice.IsOculusPlatformAvailable()) {
                    foundOculusMicrophoneDevice = OculusMicrophoneDevice.IsOculusPlatformInitialized();
                    if (!foundOculusMicrophoneDevice && Application.platform == RuntimePlatform.Android)
                        Debug.LogWarning("Normcore: Oculus Platform SDK found, but it's not initialized. Oculus Quest native echo cancellation will be unavailable.");
                }

                if (foundOculusMicrophoneDevice) {
                    // Create Oculus microphone device
                    _oculusMicrophoneDevice = new OculusMicrophoneDevice();
                    _oculusMicrophoneDevice.Start();
                    _microphoneSampleRate = 48000;
                    _microphoneChannels   = 1;
                } else if (Native.Microphone.PlatformSupported()) {
                    _nativeMicrophoneDevice = new Native.Microphone();

                    // If we failed to connect to the local microphone, bail.
                    if (!_nativeMicrophoneDevice.Start()) {
                        Debug.LogError("Failed to connect to default microphone device. Make sure it is plugged in and functioning properly.");
                        _nativeMicrophoneDevice.Dispose();
                        _nativeMicrophoneDevice = null;
                        return;
                    }

                    _microphoneSampleRate = _nativeMicrophoneDevice.SampleRate();
                    _microphoneChannels   = _nativeMicrophoneDevice.Channels();
                } else {
                    // Create a microphone device
                    _unityMicrophoneDevice = MicrophoneDevice.Start("");
                    
                    // If we failed to connect to the local microphone, bail.
                    if (_unityMicrophoneDevice == null) {
                        Debug.LogError("Failed to connect to default microphone device. Make sure it is plugged in and functioning properly.");
                        return;
                    }
                    
                    _unityMicrophoneDeviceDataReader = new AudioDeviceDataReader(_unityMicrophoneDevice);
                    _microphoneSampleRate = _unityMicrophoneDevice.sampleRate;
                    _microphoneChannels   = _unityMicrophoneDevice.numberOfChannels;
                }

                // Compute frame size with the sample rate of the microphone we received
                _microphoneFrameSize = _microphoneSampleRate / 100;

                // Create microphone stream with this sample rate (stream will automatically resample to 48000 before encoding with OPUS)
                _microphoneStream = room.CreateAudioInputStream(true, _microphoneSampleRate, _microphoneChannels);

                // Audio Preprocessor
                bool createAudioPreprocessor = Application.platform != RuntimePlatform.IPhonePlayer; // Create it for all platforms except iOS. iOS provides a nice built-in one.
                
                if (createAudioPreprocessor) {
                    // Turn on echo cancellation for mobile devices;
                    bool echoCancellation = Application.isMobilePlatform && Application.platform != RuntimePlatform.IPhonePlayer;
                    _audioPreprocessor = new AudioPreprocessor(_microphoneSampleRate,_microphoneFrameSize,                  // Input stream
                                                               true,                                                        // Automatic gain control
                                                               true,                                                        // Noise suppression
                                                               true,                                                        // Reverb suppression
                                                               echoCancellation, AudioSettings.outputSampleRate, 2, 0.28f); // Echo cancellation
                    if (echoCancellation) {
                        // Find the audio listener in the scene so we can perform echo cancellation with it
                        AudioListener[] audioListeners = FindObjectsOfType<AudioListener>();
                        if (audioListeners.Length <= 0) {
                            Debug.LogWarning("RealtimeAvatarVoice: Unable to find any AudioListeners in the scene. RealtimeAvatarVoice will not be able to perform echo cancellation.");
                        } else {
                            AudioListener audioListener = audioListeners[0];
                            if (audioListeners.Length > 1)
                                Debug.LogWarning("RealtimeAvatarVoice: Multiple AudioListeners found in the scene. Performing echo cancellation with the first one: " + audioListener.gameObject.name);

                            _audioPreprocessorPlaybackListener = audioListener.gameObject.AddComponent<AudioPreprocessorPlaybackListener>();
                            _audioPreprocessorPlaybackListener.audioPreprocessor = _audioPreprocessor;
                        }
                    }
                }
            } else {
                // Remote player, lookup audio stream and create audio output
                int clientID = _model.clientID;
                int streamID = _model.streamID;
                if (clientID >= 0 && streamID >= 0) {
                    // Find AudioOutputStream
                    AudioOutputStream audioOutputStream = room.GetAudioOutputStream(clientID, streamID);
                    if (audioOutputStream != null) {
                        _audioOutput = gameObject.AddComponent<AudioOutput>();
                        _audioOutput.mute = mute;
                        _audioOutput.StartWithAudioOutputStream(audioOutputStream);
                    } else {
                        Debug.LogError("RealtimeAvatarVoice: Unable to find matching audio stream for avatar (clientID: " + clientID + ", streamID: " + streamID + ").");
                    }
                }
            }
        }

        void DisconnectAudioStream() {
            if (_microphoneStream != null) {
                // Destroy AudioPreprocessorPlaybackListener
                if (_audioPreprocessorPlaybackListener != null) {
                    Destroy(_audioPreprocessorPlaybackListener);
                    _audioPreprocessorPlaybackListener = null;
                }

                // Dispose of audio preprocessor
                if (_audioPreprocessor != null) {
                    _audioPreprocessor.Dispose();
                    _audioPreprocessor = null;
                }

                // Close microphone stream
                _microphoneStream.Close();

                // Dispose microphone device
                if (_oculusMicrophoneDevice != null) {
                    _oculusMicrophoneDevice.Stop();
                    _oculusMicrophoneDevice.Dispose();
                    _oculusMicrophoneDevice = null;
                }
                if (_nativeMicrophoneDevice != null) {
                    _nativeMicrophoneDevice.Stop();
                    _nativeMicrophoneDevice.Dispose();
                    _nativeMicrophoneDevice = null;
                }
                if (_unityMicrophoneDevice != null) {
                    _unityMicrophoneDevice.Dispose();
                    _unityMicrophoneDevice = null;
                }

                // Clean up
                _unityMicrophoneDeviceDataReader = null;
                _microphoneStream = null;
            }

            // Remove audio output
            if (_audioOutput != null) {
                _audioOutput.Stop();
                Destroy(_audioOutput);
                _audioOutput = null;
            }
        }

        void SendMicrophoneData() {
            if (_microphoneStream == null)
                return;

            // Store the client ID / stream ID so remote clients can find the corresponding AudioOutputStream
            _model.clientID = _microphoneStream.ClientID();
            _model.streamID = _microphoneStream.StreamID();

            // Check if AudioInputStream is valid.
            if (_model.clientID < 0 || _model.streamID < 0)
                return;

            // Send audio data in _microphoneFrameSize chunks until we're out of microphone data to send
            float[] audioData = new float[_microphoneFrameSize];
            bool    didGetAudioData = false;
            while (GetMicrophoneAudioData(audioData)) {
                // If we have an _audioPreprocessor, preprocess the microphone data to remove noise and echo
                if (_audioPreprocessor != null)
                    _audioPreprocessor.ProcessRecordSamples(audioData);

                // TODO: This is a lame hack. Ideally I'd like to stop sending audio data all together.
                //       Note that even when muted audio still needs to run through the audio processor to make sure echo cancellation works properly when mute is turned back off.
                if (_mute)
                    Array.Clear(audioData, 0, audioData.Length);

                // Send out microphone data
                _microphoneStream.SendRawAudioData(audioData);

                didGetAudioData = true;
            }

            // If we got audio data, update the current microphone level.
            // Note: I moved this here so that we do our volume level calculations on microphone audio that has run through the AudioPreprocessor.
            if (didGetAudioData) {
                int firstFrame = audioData.Length - 256;
                if (firstFrame < 0)
                    firstFrame = 0;
                int firstSample = firstFrame * _microphoneChannels;
                _microphoneDbLevel = StaticFunctions.CalculateAverageDbForAudioBuffer(audioData, firstSample);
            }
        }

        private bool GetMicrophoneAudioData(float[] audioData) {
            if (_oculusMicrophoneDevice != null)
                return _oculusMicrophoneDevice.GetAudioData(audioData);
            if (_nativeMicrophoneDevice != null)
                return _nativeMicrophoneDevice.GetAudioData(audioData);
            else if (_unityMicrophoneDeviceDataReader != null)
                return _unityMicrophoneDeviceDataReader.GetData(audioData);
            
            return false;
        }

        void SetMute(bool mute) {
            if (mute == _mute)
                return;

            if (_audioOutput != null)
                _audioOutput.mute = mute;

            _mute = mute;
        }
    }

    public class RealtimeAvatarVoiceModel : IModel {
        public delegate void ClientIDStreamIDUpdated(RealtimeAvatarVoiceModel model);
        public event ClientIDStreamIDUpdated clientIDStreamIDUpdated;

        // TODO: In the future, get the clientID by traversing up to the parent model until we find a metamodel with an owner ID.
        public int clientID {
            get { return _cache.LookForValueInCache(_clientID, entry => entry.clientIDSet, entry => entry.clientID); }
            set { if (value == clientID) return; _cache.UpdateLocalCache(entry => { entry.clientIDSet = true; entry.clientID = value; return entry; }); }
        }
        public int streamID {
            get { return _cache.LookForValueInCache(_streamID, entry => entry.streamIDSet, entry => entry.streamID); }
            set { if (value == streamID) return; _cache.UpdateLocalCache(entry => { entry.streamIDSet = true; entry.streamID = value; return entry; }); }
        }

        private int  _clientID = -1;
        private int  _streamID = -1;

        // Change Cache
        private struct LocalCacheEntry {
            public bool clientIDSet;
            public int  clientID;
            public bool streamIDSet;
            public int  streamID;
        }
        private LocalChangeCache<LocalCacheEntry> _cache;

        public RealtimeAvatarVoiceModel() {
            _cache = new LocalChangeCache<LocalCacheEntry>();
        }

        // Serialization
        public int WriteLength(StreamContext context) {
            int length = 0;

            if (context.fullModel) {
                // Flatten cache
                _clientID = clientID;
                _streamID = streamID;
                _cache.Clear();

                // ClientID/StreamID
                length += WriteStream.WriteVarint32Length(1, WriteStream.ConvertNegativeOneIntToUInt(_clientID));
                length += WriteStream.WriteVarint32Length(2, WriteStream.ConvertNegativeOneIntToUInt(_streamID));
            } else {
                // ClientID/StreamID
                if (context.reliableChannel) {
                    LocalCacheEntry entry = _cache.localCache;
                    if (entry.clientIDSet)
                        length += WriteStream.WriteVarint32Length(1, WriteStream.ConvertNegativeOneIntToUInt(entry.clientID));
                    if (entry.streamIDSet)
                        length += WriteStream.WriteVarint32Length(2, WriteStream.ConvertNegativeOneIntToUInt(entry.streamID));
                }
            }

            return length;
        }

        public void Write(WriteStream stream, StreamContext context) {
            if (context.fullModel) {
                // ClientID/StreamID
                stream.WriteVarint32(1, WriteStream.ConvertNegativeOneIntToUInt(_clientID));
                stream.WriteVarint32(2, WriteStream.ConvertNegativeOneIntToUInt(_streamID));
            } else {
                // ClientID/StreamID
                if (context.reliableChannel) {
                    // If we're going to send an update. Push the cache to inflight.
                    LocalCacheEntry entry = _cache.localCache;
                    if (entry.clientIDSet || entry.streamIDSet)
                        _cache.PushLocalCacheToInflight(context.updateID);

                    if (entry.clientIDSet)
                        stream.WriteVarint32(1, WriteStream.ConvertNegativeOneIntToUInt(entry.clientID));
                    if (entry.streamIDSet)
                        stream.WriteVarint32(2, WriteStream.ConvertNegativeOneIntToUInt(entry.streamID));
                }
            }
        }

        public void Read(ReadStream stream, StreamContext context) {
            bool clientIDStreamIDUpdateExistsInCache = _cache.ValueExistsInCache(entry => entry.clientIDSet || entry.streamIDSet);

            // Remove from in-flight
            if (context.deltaUpdatesOnly && context.reliableChannel)
                _cache.RemoveUpdateFromInflight(context.updateID);

            bool receivedClientIDStreamIDUpdate = false;

            // Deserialize
            uint propertyID;
            while (stream.ReadNextPropertyID(out propertyID)) {
                switch (propertyID) {
                    case 1:
                        // Deserialize value
                        int clientID = ReadStream.ConvertUIntToNegativeOneInt(stream.ReadVarint32());
                        
                        // Fire off a notification if it changed
                        if (clientID != _clientID)
                            receivedClientIDStreamIDUpdate = true;

                        // Store
                        _clientID = clientID;
                        break;
                    case 2:
                        // Deserialize value
                        int streamID = ReadStream.ConvertUIntToNegativeOneInt(stream.ReadVarint32());

                        // Fire off a notification if it changed
                        if (streamID != _streamID)
                            receivedClientIDStreamIDUpdate = true;

                        // Store
                        _streamID = streamID;
                        break;
                    default:
                        stream.SkipProperty();
                        break;
                }
            }

            // If we received a new value, and there's currently nothing in the cache that trumps this change, fire an update.
            if (receivedClientIDStreamIDUpdate && !clientIDStreamIDUpdateExistsInCache) {
                if (clientIDStreamIDUpdated != null)
                    clientIDStreamIDUpdated(this);
            }
        }
    }
}
