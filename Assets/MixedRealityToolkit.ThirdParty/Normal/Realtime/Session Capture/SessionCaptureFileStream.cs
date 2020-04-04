using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using Normal.Realtime.Serialization;

namespace Normal.Realtime {
    public class SessionCaptureFileStream {
        public enum Mode {
            Write,
            Read
        }

        private string _filePath;
        public  string  filePath { get { return _filePath; } }
        private Mode _mode;
        public  Mode  mode { get { return _mode; } }

        private FileStream _fileStream;
        private GZipStream _gzipStream;

        private bool   _writing;
        public  bool    writing { get { return _writing; } }
        private bool   _reading;
        public  bool    reading { get { return _reading; } }
        private double _startTimestamp;
        public  double  startTimestamp { get { return _startTimestamp; } }

        // Reading
        private int    _clientID;
        public  int     clientID { get { return _clientID; } }
        private uint   _nextUpdateDeltaTimestamp;

        public SessionCaptureFileStream(string filePath, Mode mode) {
            _filePath = filePath;
            _mode     = mode;

            // Create streams
            _fileStream = new FileStream(_filePath,   mode == Mode.Write ?        FileMode.Create   :        FileMode.Open);
            _gzipStream = new GZipStream(_fileStream, mode == Mode.Write ? CompressionMode.Compress : CompressionMode.Decompress);
        }

        // NOTE: This may not be called on the same thread that we created the session capture file stream with. It's recommended Dispose() is called manually to prevent any issues.
        ~SessionCaptureFileStream() {
            // Clean up unmanaged code
            Dispose(false);
        }

        // Ideally called whenever someone is done using a client.
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing) {
            // Flush streams
            Flush();

            // Dispose streams
            if (_gzipStream != null) {
                _gzipStream.Dispose();
                _gzipStream = null;
            }
            if (_fileStream != null) {
                _fileStream.Dispose();
                _fileStream = null;
            }
        }

        //// Writing
        public void WriteHeader(int clientIndex, double startTimestamp, byte[] data) {
            if (_mode != Mode.Write) {
                Debug.LogError("SessionCaptureFileStream: Cannot call WriteHeader on read stream.");
                return;
            }

            if (_writing) {
                Debug.LogError("SessionCaptureFileStream: WriteHeader() has been called twice. Ignoring. This is a bug!");
                return;
            }

            // Keep track of start timestamps so all future timestamps can be written as deltas
            _startTimestamp = startTimestamp;

            // Write local client ID
            WriteVarint32ToStream(_gzipStream, WriteStream.ConvertNegativeOneIntToUInt(clientIndex));

            // Write start timestamp
            WriteDoubleToStream(_gzipStream, _startTimestamp);

            // Initial datastore data length
            WriteVarint32ToStream(_gzipStream, (uint)data.Length);

            // Initial datastore data
            _gzipStream.Write(data, 0, data.Length);

            _writing = true;
        }

        public void Flush() {
            if (_gzipStream != null)
                _gzipStream.Flush();
            if (_fileStream != null)
                _fileStream.Flush();
        }

        public void WriteDeltaUpdate(double timestamp, int sender, byte[] data, int dataLength, bool reliable, uint updateID, bool incoming) {
            if (!_writing) {
                Debug.LogError("SessionCaptureFileStream: Attempting to write delta update before file header has been written. Ignoring update. This is a bug!");
                return;
            }

            // Calculate delta timestamp
            double deltaTimestamp = timestamp - _startTimestamp;
            if (deltaTimestamp < 0) {
                Debug.LogError("SessionCaptureFileStream: Attempting to write update with timestamp that's before the header start timestamp. Ignoring update. This is a bug!");
                return;
            }

            // Timestamp (serialize with 10ms precision)
            WriteVarint32ToStream(_gzipStream, (uint)System.Math.Round(deltaTimestamp * 100.0));

            // Sender + reliable + send/receive
            WriteVarint32ToStream(_gzipStream, CombineSenderReliableAndIncoming(sender, reliable, incoming));

            // Update ID
            if (reliable)
                WriteVarint32ToStream(_gzipStream, updateID);
            
            // We only care about outgoing unreliable messages and incoming reliable messages.
            bool shouldWriteData = (!incoming && !reliable) || (incoming && reliable);
            if (shouldWriteData) {
                // Data length
                WriteVarint32ToStream(_gzipStream, (uint)dataLength);

                // Data
                _gzipStream.Write(data, 0, dataLength);
            } else {
                // No data
                WriteVarint32ToStream(_gzipStream, 0);
            }
        }

        //// Reading
        public byte[] ReadHeader() {
            if (_mode != Mode.Read) {
                Debug.LogError("SessionCaptureFileStream: Cannot call ReadHeader() on write stream.");
                return null;
            }

            if (_reading) {
                Debug.LogError("SessionCaptureFileStream: ReadHeader() has been called on a session that's already reading. Ignoring. This is a bug!");
                return null;
            }

            // Read local client ID
            uint clientIDUInt;
            if (!ReadVarint32FromStream(_gzipStream, out clientIDUInt)) {
                PrematurelyReachedEndOfStream();
                return null;
            }
            _clientID = ReadStream.ConvertUIntToNegativeOneInt(clientIDUInt);

            // Read start timestamp
            _startTimestamp = ReadDoubleFromStream(_gzipStream);

            // Read initial datastore
            uint dataLengthUInt;
            if (!ReadVarint32FromStream(_gzipStream, out dataLengthUInt)) {
                PrematurelyReachedEndOfStream();
                return null;
            }
            int dataLength = (int)dataLengthUInt;
            byte[] data = new byte[dataLength];
            int bytesRead = _gzipStream.Read(data, 0, dataLength);
            if (bytesRead != dataLength) {
                PrematurelyReachedEndOfStream();
                return null;
            }

            // Start reading
            _reading = true;

            // Read next update timestamp
            ReadNextUpdateDeltaTimestamp();

            if (!_reading)
                Debug.Log("SessionCaptureFileStream: No delta updates found after initial datastore snapshot. Reading stopped.");

            return data;
        }

        public bool PeekNextUpdateDeltaTimestamp(out double deltaTimestamp) {
            if (!_reading) {
                deltaTimestamp = 0.0;
                return false;
            }

            // Check if it's time to deserialize the next timestamp
            deltaTimestamp = _nextUpdateDeltaTimestamp / 100.0;

            return true;
        }

        public bool ReadDeltaUpdate(double playbackTime, ref double timestamp, ref int sender, ref byte[] data, ref bool reliable, ref uint updateID, ref bool incoming) {
            if (!_reading)
                return false;

            // Timestamp
            timestamp = _startTimestamp + (_nextUpdateDeltaTimestamp / 100.0);

            // Check if this update is too new
            if (timestamp > playbackTime)
                return false;

            // Sender + reliable + send/receive
            uint senderReliableAndIncoming;
            if (!ReadVarint32FromStream(_gzipStream, out senderReliableAndIncoming)) {
                PrematurelyReachedEndOfStream();
                return false;
            }
            SplitSenderReliableAndIncoming(senderReliableAndIncoming, out sender, out reliable, out incoming);

            // Update ID
            if (reliable) {
                if (!ReadVarint32FromStream(_gzipStream, out updateID)) {
                    PrematurelyReachedEndOfStream();
                    return false;
                }
            } else {
                updateID = 0;
            }

            // Data length
            uint dataLengthUInt;
            if (!ReadVarint32FromStream(_gzipStream, out dataLengthUInt)) {
                PrematurelyReachedEndOfStream();
                return false;
            }
            int dataLength = (int)dataLengthUInt;

            // Data
            data = new byte[dataLength];
            int bytesRead = _gzipStream.Read(data, 0, dataLength);
            if (bytesRead != dataLength) {
                PrematurelyReachedEndOfStream();
                return false;
            }

            // Prime for the next update
            ReadNextUpdateDeltaTimestamp();

            return true;
        }

        public void SkipToTime(double playbackTime) {
            if (!_reading)
                return;

            double timestamp = 0.0;
            int    sender    = 0;
            byte[] data      = null;
            bool   reliable  = false;
            uint   updateID  = 0;
            bool   incoming  = false;
            while (ReadDeltaUpdate(playbackTime, ref timestamp, ref sender, ref data, ref reliable, ref updateID, ref incoming));
        }

        private void ReadNextUpdateDeltaTimestamp() {
            if (!ReadVarint32FromStream(_gzipStream, out _nextUpdateDeltaTimestamp)) {
                Debug.Log("SessionCaptureFileStream: Reached end of session. Reading stopped.");
                _reading = false;
            }
        }

        private void PrematurelyReachedEndOfStream() {
            Debug.LogError("SessionCaptureFileStream: Prematurely reached end of stream. This usually means the session file was terminated improperly or is corrupt. Reading stopped.");
            _reading = false;
        }

        //// Utility
        private static void WriteVarint32ToStream(Stream stream, uint value) {
            // Write 7 bits of the value (with the varint flag bit set) until we've got 7 bits or less left.
            while (value > 0x7F) {
                stream.WriteByte((byte)((value & 0x7F) | 0x80));
                value >>= 7;
            }

            // Write the final 7 bits without the flag set.
            stream.WriteByte((byte)value);
        }

        private static bool ReadVarint32FromStream(Stream stream, out uint value) {
            int varintByte = 0;
            int varint     = 0;
            for (int i = 0; i < 5; i++) {
                // Read byte
                varintByte = stream.ReadByte();
                if (varintByte < 0) {
                    value = 0;
                    return false;
                }

                // Apply the bits to our value (and strip the varint flag bit)
                varint |= (varintByte & 0x7F) << i*7;

                // Return if we've hit the final byte in the varint.
                if (varintByte < 0x80) {
                    value = (uint)varint;
                    return true;
                }
            }

            // If we hit this point, we haven't read the final varint byte. This is either a 64bit varint or an invalid varint.
            // Try to skip to the end of a 64bit varint data to keep the position of the stream correct. Return a varint with the lower 32 bits.
            for (int i = 0; i < 5; i++) {
                if (stream.ReadByte() < 0x80) {
                    value = (uint)varint;
                    return true;
                }
            }

            // If we've hit this point, all 10 bytes had the varint flag set. This is an invalid varint.
            Debug.LogError("Session Capture: ReadVarint32 uncovered invalid varint value. This is a bug!");
            value = 0;
            return false;
        }

        private static void WriteDoubleToStream(Stream stream, double value) {
            // Convert to bytes
            byte[] bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(bytes);

            if (bytes.Length != 8) {
                Debug.LogError("Session Capture: BitConverter double -> bytes returned the wrong number of bytes! This is a bug! (" + bytes.Length + ", 8)");
                bytes = new byte[8]; // Write zero
            }

            // Write
            stream.Write(bytes, 0, 8);
        }

        private static double ReadDoubleFromStream(Stream stream) {
            // Read bytes
            byte[] bytes = new byte[8];
            stream.Read(bytes, 0, 8);

            // Flip the bytes if we're on big endian
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(bytes);

            // Decode
            return BitConverter.ToDouble(bytes, 0);
        }

        private static uint CombineSenderReliableAndIncoming(int sender, bool reliable, bool incoming) {
            uint senderUInt   = WriteStream.ConvertNegativeOneIntToUInt(sender);
            uint reliableUInt = reliable ? 1u : 0u;
            uint incomingUInt = incoming ? 1u : 0u;
            return (senderUInt << 2) | (reliableUInt << 1) | (incomingUInt << 0);
        }

        private static void SplitSenderReliableAndIncoming(uint value, out int sender, out bool reliable, out bool incoming) {
            uint senderUInt   = (value >> 2);
            uint reliableUInt = (value >> 1) & 0x1;
            uint incomingUInt = (value >> 0) & 0x1;

            sender   = ReadStream.ConvertUIntToNegativeOneInt(senderUInt);
            reliable = reliableUInt != 0 ? true : false;
            incoming = incomingUInt != 0 ? true : false;
        }
    }
}
