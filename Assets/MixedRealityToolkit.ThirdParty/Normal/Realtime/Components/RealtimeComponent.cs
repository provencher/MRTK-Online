using UnityEngine;

namespace Normal.Realtime {
    [RequireComponent(typeof(RealtimeView))]
    public class RealtimeComponent : MonoBehaviour {
        public  RealtimeView realtimeView { get; private set; }
        public  Realtime     realtime     { get { return realtimeView.realtime; } }
        public  Room         room         { get { return realtime.room; } }
	}
}
