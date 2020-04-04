using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Normal.Realtime {
    public class SessionCapture {
        public enum Mode {
            Record,
            Playback
        }

        private Mode _mode;
        public  Mode  mode { get { return _mode; } }

        private bool _recording;
        public  bool  recording { get { return _recording; } }
        private bool _playing;
        public  bool  playing   { get { return _playing;   } }

        // Recording
        private string                   _recordFilePath;
        private SessionCaptureFileStream _recordFileStream;

        // Playback
        private double _playbackTime;
        private Dictionary<int, Dictionary<uint, DeltaUpdate>> _clientToIncomingReliableUpdatesMap;

        private string             _primaryPlaybackFilePath;
        private PlaybackStream     _primaryPlaybackStream;
        private Queue<DeltaUpdate> _primaryPlaybackDeltaUpdatesToBeProcessed;
        private string[]                        _secondaryPlaybackFilePaths;
        private Dictionary<int, PlaybackStream> _secondaryPlaybackStreams;

        private Queue<DeltaUpdate> _playbackDeltaUpdates;

        public SessionCapture(string recordFilePath) {
            _mode = Mode.Record;
            _recordFilePath = recordFilePath;
        }

        public SessionCapture(string[] playbackFilePaths) {
            _mode = Mode.Playback;
            if (playbackFilePaths.Length <= 0)
                throw new ArgumentNullException();

            _primaryPlaybackFilePath = playbackFilePaths[0];
            if (playbackFilePaths.Length > 1)
                _secondaryPlaybackFilePaths = playbackFilePaths.Skip(1).ToArray();
        }

        ~SessionCapture() {
            StopRecording();
            StopPlayback();
        }

        // Recording
        public void StartRecording(int clientIndex, double startTimestamp, byte[] data) {
            if (_mode != Mode.Record) {
                Debug.LogError("SessionCapture: Cannot call StartRecording on playback session.");
                return;
            }

            if (_recording) {
                Debug.LogError("SessionCapture: StartRecording() has been called on a session that's already recording. Ignoring. This is a bug!");
                return;
            }

            // Create write stream
            _recordFileStream = new SessionCaptureFileStream(_recordFilePath, SessionCaptureFileStream.Mode.Write);

            // Write header
            _recordFileStream.WriteHeader(clientIndex, startTimestamp, data);

            _recording = true;
        }

        public void StopRecording() {
            // Dispose write stream
            if (_recordFileStream != null) {
                _recordFileStream.Dispose();
                _recordFileStream = null;
            }

            _recording = false;
        }

        public void WriteDeltaUpdate(double timestamp, int sender, byte[] data, int dataLength, bool reliable, uint updateID, bool incoming) {
            if (_mode != Mode.Record) {
                Debug.LogError("SessionCapture: Cannot call WriteDeltaUpdate on playback session. Ignoring. This is a bug!");
                return;
            }

            if (!_recording) {
                Debug.LogError("SessionCapture: Asked to write delta update, but recording hasn't started. Ignoring update. This is a bug!");
                return;
            }

            // Record delta update
            _recordFileStream.WriteDeltaUpdate(timestamp, sender, data, dataLength, reliable, updateID, incoming);
        }

        // Playback
        public byte[] StartPlayback() {
            if (_mode != Mode.Playback) {
                Debug.LogError("SessionCapture: Cannot call StartPlayback on record session.");
                return null;
            }

            if (_playing) {
                Debug.LogError("SessionCapture: StartPlayback() has been called on a session that's already playing. Ignoring. This is a bug!");
                return null;
            }

            // Create a queue to hold updates that are ready for playback
            _playbackDeltaUpdates = new Queue<DeltaUpdate>();

            // Create a dictionary of deserialized incoming reliable updates. These will be used to calculate roundtrip time.
            _clientToIncomingReliableUpdatesMap = new Dictionary<int, Dictionary<uint, DeltaUpdate>>();

            // Create a queue to hold updates that we've deserialized from the primary stream that haven't been processed yet.
            _primaryPlaybackDeltaUpdatesToBeProcessed = new Queue<DeltaUpdate>();

            // Open up file stream for the primary capture file
            _primaryPlaybackStream = new PlaybackStream(_primaryPlaybackFilePath);

            // Read the file header + initial datastore
            byte[] datastore = _primaryPlaybackStream.ReadHeader();

            // Start playback at the primary session capture start time.
            _playbackTime = _primaryPlaybackStream.startTimestamp;

            // Open up file streams for secondary capture files
            _secondaryPlaybackStreams = new Dictionary<int, PlaybackStream>();
            if (_secondaryPlaybackFilePaths != null) {
                foreach (string secondaryPlaybackFilePath in _secondaryPlaybackFilePaths) {
                    PlaybackStream playbackStream = new PlaybackStream(secondaryPlaybackFilePath);
                    playbackStream.ReadHeader();

                    // Fast forward to the primary stream start time
                    playbackStream.SkipToTime(_playbackTime);

                    _secondaryPlaybackStreams.Add(playbackStream.clientID, playbackStream);
                }
            }

            // Mark the session as playing
            _playing = true;

            return datastore;
        }

        public void StopPlayback() {
            // Dispose playback streams
            if (_primaryPlaybackStream != null) {
                _primaryPlaybackStream.Dispose();
                _primaryPlaybackStream = null;
            }
            if (_secondaryPlaybackStreams != null) {
                foreach (KeyValuePair<int, PlaybackStream> secondaryPlaybackStream in _secondaryPlaybackStreams)
                    secondaryPlaybackStream.Value.Dispose();
                _secondaryPlaybackStreams.Clear();
                _secondaryPlaybackStreams = null;
            }

            _playing = false;
        }

        public void PlaybackTick(double deltaTime) {
            if (!_playing)
                return;

            // Increment playback time
            _playbackTime += deltaTime;

            // Keep track of whether this frame produced any updates for playback
            bool didAddDeltaUpdateToPlaybackQueue = false;

            // Read delta updates from primary playback stream
            DeltaUpdate deltaUpdate = new DeltaUpdate();
            while (_primaryPlaybackStream.ReadDeltaUpdate(_playbackTime, deltaUpdate)) {
                // Add all outgoing updates to the queue to be processed
                if (deltaUpdate.outgoing)
                    _primaryPlaybackDeltaUpdatesToBeProcessed.Enqueue(deltaUpdate);

                // If it's an incoming reliable update, keep track so we can use the timestamp to calculate timestamp offsets.
                if (deltaUpdate.incoming && deltaUpdate.reliable) {
                    // If this is an incoming reliable update, add it directly to the queue of updates to play back. It doesn't need any timestamp adjustments or anything.
                    _playbackDeltaUpdates.Enqueue(deltaUpdate);
                    didAddDeltaUpdateToPlaybackQueue = true;

                    // Retrieve dictionary for this sender. Create a new one if needed.
                    Dictionary<uint, DeltaUpdate> incomingReliableUpdates;
                    if (!_clientToIncomingReliableUpdatesMap.TryGetValue(deltaUpdate.sender, out incomingReliableUpdates)) {
                        incomingReliableUpdates = new Dictionary<uint, DeltaUpdate>();
                        _clientToIncomingReliableUpdatesMap.Add(deltaUpdate.sender, incomingReliableUpdates);
                    }

                    // Add the update
                    incomingReliableUpdates.Add(deltaUpdate.updateID, deltaUpdate);
                }

                // Create a new update for the next loop
                deltaUpdate = new DeltaUpdate();
            }

            // Adjust the timestamps for outgoing unreliable messages by measuring the roundtrip time on reliable messages.
            // We're using incoming reliable updates for playback (because the server assigns uniqueIDs and things), but unreliable updates are recorded when they're sent.
            // That means that the reliable updates will have timestamps that are later than when they were actually sent. To fix this, we adjust the timestamps of the
            // unreliable updates to account for the roundtrip delay that we're seeing.

            // Grab the incoming reliable updates for the primary stream's client ID
            Dictionary<uint, DeltaUpdate> primaryPlaybackClientIncomingReliableUpdates;
            _clientToIncomingReliableUpdatesMap.TryGetValue(_primaryPlaybackStream.clientID, out primaryPlaybackClientIncomingReliableUpdates);

            // Loop through updates and process them until we run out or we hit one that requires us to wait for more time to pass / more frames to be decoded
            while (_primaryPlaybackDeltaUpdatesToBeProcessed.Count > 0) {
                // Peek at update
                deltaUpdate = _primaryPlaybackDeltaUpdatesToBeProcessed.Peek();

                // Outgoing unreliable update.
                if (deltaUpdate.outgoing && deltaUpdate.unreliable) {
                    bool success = AdjustOutgoingUnreliableDeltaUpdateTimestamp(_playbackTime, _primaryPlaybackStream, deltaUpdate);

                    // If we were unable to successfully adjust the update, break out of the loop. We'll try again next frame after more time has passed.
                    if (!success)
                        break;

                    // Add to outgoing updates
                    _playbackDeltaUpdates.Enqueue(deltaUpdate);
                    didAddDeltaUpdateToPlaybackQueue = true;
                }

                // Outgoing reliable update. Find the matching incoming reliable update and use it to adjust the unreliableTimestampOffset
                else if (deltaUpdate.outgoing && deltaUpdate.reliable) {
                    bool success = AdjustPlaybackStreamSendTimestampOffsetWithOutgoingReliableDeltaUpdate(_primaryPlaybackStream, primaryPlaybackClientIncomingReliableUpdates, deltaUpdate);

                    // If we were unable to successfully adjust the send timestamp offset, break out of the loop. We'll try again next frame after more time has passed.
                    if (!success)
                        break;
                }

                // If we hit this point, the update has been processed, dequeue it and move onto the next one.
                _primaryPlaybackDeltaUpdatesToBeProcessed.Dequeue();
            }

            // Loop through each secondary stream. Perform the timestamp adjustments and add them to the playback queue.
            foreach (KeyValuePair<int, PlaybackStream> secondaryPlaybackStream in _secondaryPlaybackStreams) {
                int            clientID       = secondaryPlaybackStream.Key;
                PlaybackStream playbackStream = secondaryPlaybackStream.Value;

                // Grab the incoming reliable updates for this stream's clientID
                Dictionary<uint, DeltaUpdate> incomingReliableUpdates;
                _clientToIncomingReliableUpdatesMap.TryGetValue(clientID, out incomingReliableUpdates);

                // Read updates up until playbackTime processing them as we go. Stop if we run out of updates or we hit an update that we can't process yet.
                deltaUpdate = new DeltaUpdate();
                while (playbackStream.ReadDeltaUpdate(_playbackTime, deltaUpdate)) {
                    // Outgoing unreliable update.
                    if (deltaUpdate.outgoing && deltaUpdate.unreliable) {
                        bool success = AdjustOutgoingUnreliableDeltaUpdateTimestamp(_playbackTime, playbackStream, deltaUpdate);

                        // If we were unable to successfully adjust the update, break out of the loop. We'll try again next frame after more time has passed.
                        if (!success)
                            break;

                        // Add to outgoing updates
                        _playbackDeltaUpdates.Enqueue(deltaUpdate);
                        didAddDeltaUpdateToPlaybackQueue = true;
                    }

                    // Outgoing reliable update. Find the matching incoming reliable update and use it to adjust the unreliableTimestampOffset
                    else if (deltaUpdate.outgoing && deltaUpdate.reliable) {
                        bool success = AdjustPlaybackStreamSendTimestampOffsetWithOutgoingReliableDeltaUpdate(playbackStream, incomingReliableUpdates, deltaUpdate);

                        // If we were unable to successfully adjust the send timestamp offset, break out of the loop. We'll try again next frame after more time has passed.
                        if (!success)
                            break;
                    }
                }
            }

            // If we added any updates to the playback queue, sort it to make sure that they're in order after timestamp adjustments were made.
            if (didAddDeltaUpdateToPlaybackQueue)
                _playbackDeltaUpdates = new Queue<DeltaUpdate>(_playbackDeltaUpdates.OrderBy(playbackDeltaUpdate => playbackDeltaUpdate.timestamp));

            // If we've finished reading updates, we're done playing.
            _playing = _primaryPlaybackStream.reading;
        }

        private static bool AdjustOutgoingUnreliableDeltaUpdateTimestamp(double playbackTime, PlaybackStream playbackStream, DeltaUpdate deltaUpdate) {
            // Adjust the timestamp
            double timestamp = deltaUpdate.timestamp + playbackStream.sendTimestampOffset;
            
            // If the update is now newer than the current playback time, then bail.
            if (timestamp > playbackTime)
                return false;
            
            // Adjust update timestamp
            deltaUpdate.timestamp += playbackStream.sendTimestampOffset;

            return true;
        }

        private static bool AdjustPlaybackStreamSendTimestampOffsetWithOutgoingReliableDeltaUpdate(PlaybackStream playbackStream, Dictionary<uint, DeltaUpdate> incomingReliableUpdates, DeltaUpdate outgoingReliableDeltaUpdate) {
            // Check for a matching reliable delta update. If we haven't received it yet, then bail, it's not time to decode this update yet.
            DeltaUpdate incomingReliableDeltaUpdate;
            if (incomingReliableUpdates == null || !incomingReliableUpdates.TryGetValue(outgoingReliableDeltaUpdate.updateID, out incomingReliableDeltaUpdate))
                return false;

            // Found a matching reliable update. Use the round trip time to adjust outgoing unreliable updates.
            playbackStream.sendTimestampOffset = incomingReliableDeltaUpdate.timestamp - outgoingReliableDeltaUpdate.timestamp;

            // Remove from incoming reliable updates
            incomingReliableUpdates.Remove(outgoingReliableDeltaUpdate.updateID);

            return true;
        }

        public DeltaUpdate ReadDeltaUpdate() {
            if (!_playing)
                return null;

            if (_playbackDeltaUpdates.Count == 0)
                return null;

            DeltaUpdate deltaUpdate = _playbackDeltaUpdates.Peek();
            // If this update is newer than the current playback time, bail
            if (deltaUpdate.timestamp > _playbackTime)
                return null;

            // Dequeue
            _playbackDeltaUpdates.Dequeue();

            return deltaUpdate;
        }

        public class DeltaUpdate {
            public double timestamp;
            public int    sender;
            public byte[] data;
            public bool   reliable;
            public bool   unreliable { get { return !reliable; } }
            public uint   updateID;
            public bool   incoming;
            public bool   outgoing { get { return !incoming; } }
        }

        private class PlaybackStream {
            public bool   reading { get { return _fileStream.reading; } }
            public double startTimestamp { get { return _fileStream.startTimestamp; } }
            public int    clientID       { get { return _fileStream.clientID;       } }
            public double sendTimestampOffset { get { return _sendTimestampOffset; } set { _sendTimestampOffset = value; } }

            private SessionCaptureFileStream _fileStream;
            private double                   _sendTimestampOffset;

            public PlaybackStream(string filePath) {
                _fileStream          = new SessionCaptureFileStream(filePath, SessionCaptureFileStream.Mode.Read);
                _sendTimestampOffset = 0.0;
            }

            // NOTE: This may not be called on the same thread that we created the playback stream with. It's recommended Dispose() is called manually to prevent any issues.
            ~PlaybackStream() {
                // Clean up unmanaged code
                Dispose(false);
            }

            // Ideally called whenever someone is done using a client.
            public void Dispose() {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            private void Dispose(bool disposing) {
                if (_fileStream != null) {
                    _fileStream.Dispose();
                    _fileStream = null;
                }
            }

            public byte[] ReadHeader() {
                return _fileStream.ReadHeader();
            }

            public bool ReadDeltaUpdate(double playbackTime, DeltaUpdate deltaUpdate) {
                return _fileStream.ReadDeltaUpdate(playbackTime, ref deltaUpdate.timestamp, ref deltaUpdate.sender, ref deltaUpdate.data, ref deltaUpdate.reliable, ref deltaUpdate.updateID, ref deltaUpdate.incoming);
            }

            public void SkipToTime(double playbackTime) {
                _fileStream.SkipToTime(playbackTime);
            }
        }
    }
}
