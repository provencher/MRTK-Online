using System;
using UnityEngine;

namespace Normal.Realtime {
    public class MicrophoneDevice : IDisposable {
        // TODO: Find a better way to do this which supports multiple devices! 
        public static MicrophoneDevice sharedMicrophone { get; private set; }
        private AudioClip _microphone;
        private string    _deviceName;

        private int       _sampleRate;
        private int       _numberOfChannels;
        private int       _sampleCount;

        public int sampleRate { get { return _sampleRate; } }
        public int numberOfChannels { get { return _numberOfChannels; } }
        public int deviceWriteHeadPosition { get { return Microphone.GetPosition(_deviceName); } }
        public int deviceBufferSampleCount { get { return _sampleCount; } }

        public static MicrophoneDevice Start(string deviceName) {
            if (Microphone.devices.Length <= 0)
                return null;

            int idealFrequency = 48000; // Ideal for OPUS
            int frequency = idealFrequency;
            int minimumFrequency;
            int maximumFrequency;
            Microphone.GetDeviceCaps(deviceName, out minimumFrequency, out maximumFrequency);

            if (idealFrequency < minimumFrequency)
                frequency = minimumFrequency;
            else if (idealFrequency > maximumFrequency && maximumFrequency > 0)
                frequency = maximumFrequency;

            AudioClip microphone = Microphone.Start(deviceName, true, 1, frequency);
            if (microphone == null)
                return null;

            return new MicrophoneDevice(deviceName, microphone);
        }

        MicrophoneDevice(string deviceName, AudioClip microphone) {
            // TODO: Add explicit Microphone.Start/Microphone.Stop?
            _deviceName       =  deviceName;
            _microphone       =  microphone;
            _sampleRate       = _microphone.frequency;
            _numberOfChannels = _microphone.channels;
            _sampleCount      = _microphone.samples;

            sharedMicrophone = this;
        }

        ~MicrophoneDevice() {
            if (sharedMicrophone == this)
                sharedMicrophone = null;

            Dispose(false);
        }

        // Ideally called whenever someone is done using a microphone device.
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing) {
            if (_microphone != null) {
                Microphone.End(_deviceName);
                _microphone = null;
            }
        }

        public bool GetBufferData(float[] buffer, int offsetSamples) {
            if (_microphone != null)
                return _microphone.GetData(buffer, offsetSamples);

            return false;
        }
    }
}