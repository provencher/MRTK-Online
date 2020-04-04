using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Normal.Realtime {
    [RequireComponent(typeof(Realtime))]
    public class RealtimeSessionCapture : MonoBehaviour {
        private enum Mode {
            Off,
            Record,
            Playback
        }
        [SerializeField] private Mode _mode = Mode.Off;

        // Playback
        [Header("Playback")]
        [SerializeField] private string[] _playbackCaptureFiles;
        
        private Realtime _realtime;
        private Room     _room;

        private void Awake() {
            _realtime = GetComponent<Realtime>();

            string outputDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Normal\\SessionCapture");

            if (_mode == Mode.Record) {
                string outputFileName = "Session_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".realtime";
                string outputFilePath = Path.Combine(outputDirectory, outputFileName);
                Directory.CreateDirectory(outputDirectory);

                // Create session
                Debug.Log("Record file path: " + outputFilePath);
                SessionCapture sessionCapture = new SessionCapture(outputFilePath);

                // Set on Realtime
                _realtime.room = new Room(sessionCapture);
            } else if (_mode == Mode.Playback) {
                // If no playback files are specified, attempt to find the most recent one in the output directory.
                if (_playbackCaptureFiles == null || _playbackCaptureFiles.Length <= 0) {
                    FileInfo file = new DirectoryInfo(outputDirectory).GetFiles("*.realtime").OrderByDescending(f => f.CreationTime).FirstOrDefault();
                    if (file != default(FileInfo))
                        _playbackCaptureFiles = new string[] { file.FullName };
                }

                if (_playbackCaptureFiles != null) {
                    // Create session
                    SessionCapture sessionCapture = new SessionCapture(_playbackCaptureFiles);

                    // Set on Realtime
                    _realtime.room = new Room(sessionCapture);
                } else {
                    Debug.LogError("RealtimeSessionCapture: Unable to find any session capture files to play back.");
                }
            }
        }
    }
}
