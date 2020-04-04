using System.Collections.Generic;
using Normal.Realtime.Serialization;

namespace Normal.Realtime {
    public class RealtimeViewComponentsModel : IModel {
        private class Component {
            public int    componentID;
            public IModel model;
            public Component(int componentID, IModel model) {
                this.componentID = componentID;
                this.model = model;
            }
        }
        private List<Component>         _components;
        private Dictionary<int, IModel> _componentMap;
    
        public RealtimeViewComponentsModel(Dictionary<int, IModel> componentMap) {
            // Component map
            _componentMap = componentMap;
    
            // Components array for faster writes
            _components = new List<Component>();
            foreach (KeyValuePair<int, IModel> pair in componentMap)
                _components.Add(new Component(pair.Key, pair.Value));
        }
    
        public IModel this[int componentID] {
            get {
                IModel model;
                if (_componentMap.TryGetValue((int)componentID, out model))
                    return model;
                else
                    return null;
            }
        }
    
        // Serialization
        public int WriteLength(StreamContext context) {
            int length = 0;
    
            foreach (Component component in _components)
                length += WriteStream.WriteModelLength((uint)component.componentID, component.model, context);
    
            return length;
        }
    
        public void Write(WriteStream stream, StreamContext context) {
            foreach (Component component in _components) {
                 stream.WriteModel((uint)component.componentID, component.model, context);
            }
        }
    
        public void Read(ReadStream stream, StreamContext context) {
            uint componentID;
            while (stream.ReadNextPropertyID(out componentID)) {
                IModel model;
                if (_componentMap.TryGetValue((int)componentID, out model))
                    stream.ReadModel(model, context);
                else
                    stream.SkipProperty();
            }
        }
    }
}
