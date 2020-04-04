using UnityEngine;

namespace Normal.Realtime {
    public class AudioDeviceDataReader {
        // TODO: Support switching?
        //       I think we just need to reset the variables set in the contructor
        public  MicrophoneDevice microphone { get; private set; }
    
        private int _previousLocalWriteHeadPosition;
        private int _writeHeadLoopCount;
        private int _readHeadPosition;
    
        public AudioDeviceDataReader(MicrophoneDevice microphone) {
            this.microphone = microphone;
    
            // Initialize read and write head positions to the current position of the microphone write head.
            _previousLocalWriteHeadPosition = microphone.deviceWriteHeadPosition;
            _writeHeadLoopCount             = 0;
            _readHeadPosition               = microphone.deviceWriteHeadPosition;
        }
    
        public bool GetData(float[] buffer) {
            // Get the current write head position
            int localWriteHeadPosition = microphone.deviceWriteHeadPosition;
            if (localWriteHeadPosition < _previousLocalWriteHeadPosition)
                _writeHeadLoopCount++;
            _previousLocalWriteHeadPosition = localWriteHeadPosition;
    
            // Calculate the global write head position
            int writeHeadPosition = _writeHeadLoopCount * microphone.deviceBufferSampleCount + localWriteHeadPosition;
    
            // Number of samples to read into the supplied buffer.
            int numberOfSamplesToRead = buffer.Length / microphone.numberOfChannels;
    
            // Figure out where the read head will be after filling this buffer.
            int nextReadHeadPosition = _readHeadPosition + numberOfSamplesToRead;
    
            if (numberOfSamplesToRead > microphone.deviceBufferSampleCount) {
                Debug.LogError("Reading microphone audio data. Supplied buffer is larger than the microphone buffer. (" + buffer.Length + ", " + microphone.deviceBufferSampleCount * microphone.numberOfChannels + ")");
                return false;
            }
    
            // If the next read head will be past the write head position, bail.
            if (nextReadHeadPosition >= writeHeadPosition)
                return false;
    
            // TODO: Will this barf if buffer.Length is > _microphone.samples or will GetData automatically loop back to the beginning for us?
            bool success = microphone.GetBufferData(buffer, _readHeadPosition % microphone.deviceBufferSampleCount);
            _readHeadPosition = nextReadHeadPosition;
    
            return success;
        }
    
        public bool GetMostRecentData(float[] buffer) {
            // Number of samples to read into the supplied buffer.
            int numberOfSamplesToRead = buffer.Length / microphone.numberOfChannels;
            if (numberOfSamplesToRead > microphone.deviceBufferSampleCount) {
                Debug.LogError("Reading microphone audio data. Supplied buffer is larger than the microphone buffer. (" + buffer.Length + ", " + microphone.deviceBufferSampleCount * microphone.numberOfChannels + ")");
                return false;
            }
    
            int localWriteHeadPosition = microphone.deviceWriteHeadPosition;
            int localReadHeadPosition = localWriteHeadPosition - numberOfSamplesToRead;
            while (localReadHeadPosition >= microphone.deviceBufferSampleCount)
                localReadHeadPosition -= microphone.deviceBufferSampleCount;
            while (localReadHeadPosition < 0)
                localReadHeadPosition += microphone.deviceBufferSampleCount;
    
            return microphone.GetBufferData(buffer, localReadHeadPosition);
        }
    }
}
