using System.Linq;
using Normal.Realtime.Serialization;

namespace Normal.Realtime {
    public partial class Datastore {
        private IModel _roomModel;
        public  IModel  roomModel { get { return _roomModel; } }

        private RealtimeSet<RealtimeViewModel> _sceneViewModels;
        public  RealtimeSet<RealtimeViewModel>  sceneViewModels { get { return _sceneViewModels; } }

        private RealtimeSet<RealtimeViewModel> _prefabViewModels;
        public  RealtimeSet<RealtimeViewModel>  prefabViewModels { get { return _prefabViewModels; } }
        public delegate void PrefabViewModelAdded(  Datastore datastore, RealtimeViewModel model, bool remote);
        public delegate void PrefabViewModelRemoved(Datastore datastore, RealtimeViewModel model, bool remote);
        public event PrefabViewModelAdded   prefabRealtimeViewModelAdded;
        public event PrefabViewModelRemoved prefabRealtimeViewModelRemoved;

        public void Reset(IModel roomModel = null) {
            // Tear Down
            if (_prefabViewModels != null) {
                _prefabViewModels.modelAdded   -= PrefabViewModelAddedToSet;
                _prefabViewModels.modelRemoved -= PrefabViewModelRemovedFromSet;
            }

            // Set Up
            _roomModel = roomModel;
            _sceneViewModels  = new RealtimeSet<RealtimeViewModel>();
            _prefabViewModels = new RealtimeSet<RealtimeViewModel>();
            _prefabViewModels.modelAdded   += PrefabViewModelAddedToSet;
            _prefabViewModels.modelRemoved += PrefabViewModelRemovedFromSet;

            SetUpSerialization();
        }

        public RealtimeViewModel GetSceneRealtimeViewModelForUUID(byte[] sceneViewUUID) {
            foreach (RealtimeViewModel viewModel in _sceneViewModels) {
                if (viewModel.sceneViewUUID.SequenceEqual(sceneViewUUID))
                    return viewModel;
            }
            return null;
        }

        public bool AddSceneRealtimeViewModel(RealtimeViewModel viewModel) {
            if (viewModel.sceneViewUUID.Length == 0) {
                UnityEngine.Debug.LogError("Scene RealtimeView is missing a UUID. This is a bug!");
                return false;
            }
            if (!_sceneViewModels.Add(viewModel)) {
                UnityEngine.Debug.LogError("RealtimeViewModel already exists in Datastore! This is a bug!");
                return false;
            }
            return true;
        }

        public void AddPrefabRealtimeViewModel(RealtimeViewModel prefabViewModel) {
            if (!_prefabViewModels.Add(prefabViewModel)) {
                UnityEngine.Debug.LogError("RealtimeViewModel already exists in Datastore! This is a bug!");
                return;
            }
        }

        public bool RemovePrefabRealtimeViewModel(RealtimeViewModel model) {
            return _prefabViewModels.Remove(model);
        }

        private void PrefabViewModelAddedToSet(RealtimeSet<RealtimeViewModel> set, RealtimeViewModel model, bool remote) {
            if (prefabRealtimeViewModelAdded != null)
                prefabRealtimeViewModelAdded(this, model, remote);
        }

        private void PrefabViewModelRemovedFromSet(RealtimeSet<RealtimeViewModel> set, RealtimeViewModel model, bool remote) {
            if (prefabRealtimeViewModelRemoved != null)
                prefabRealtimeViewModelRemoved(this, model, remote);
        }
    }

    // TODO: Rather than having Datastore implement the model protocol directly, it would probably make sense to create a DatastoreModel class that's only for storing data. Then Datastore acts as a controller that manages it.
    public partial class Datastore : IModel {
        private enum Properties : uint {
            SceneRealtimeViewModels  = 4,
            RoomModel                = 5,
            PrefabRealtimeViewModels = 6,
        }

        public WriteBuffer writeBuffer { get { return _writeBuffer; } }

        // Serialization
        private WriteBuffer _writeBuffer;
        private WriteStream _writeStream;

        // Deserialization
        private ReadBuffer  _readBuffer;
        private ReadStream  _readStream;

        private void SetUpSerialization() {
            _writeBuffer = new WriteBuffer();
            _writeStream = new WriteStream(_writeBuffer);

            _readBuffer = new ReadBuffer(null);
            _readStream = new ReadStream(_readBuffer);
        }

        public void Deserialize(byte[] buffer) {
            if (buffer.Length <= 0)
                return;

            // Swap buffer & reset cursor
            _readBuffer.SetBuffer(buffer);

            // Read
            _readStream.DeserializeRootModel(this);
        }

        public void SerializeDeltaUpdates(bool reliable, uint updateID = 0) {
            // Reset the write buffer
            _writeBuffer.Reset();

            // Serialize
            _writeStream.SerializeRootModelDeltaUpdates(this, reliable, updateID);
        }

        public uint DeserializeDeltaUpdates(byte[] buffer, bool reliable, bool updateIsFromUs) {
            if (buffer.Length <= 0)
                return 0;

            // Swap buffer & reset cursor
            _readBuffer.SetBuffer(buffer);

            uint updateID = 0;

            // Read the update ID if this is a reliable update
            if (reliable)
                updateID = _readBuffer.ReadVarint32();

            // Read
            // TODO: Remove the updateIsFromUS check once models are smart enough to check the sender
            _readStream.DeserializeRootModelDeltaUpdates(this, reliable, updateIsFromUs ? updateID : 0);

            return updateID;
        }
        

        // Serialization
        public int WriteLength(StreamContext context) {
            int length = 0;

            // TODO: Ideally WriteStream can handle null properly (and just ignore writing the property entirely)
            if (_roomModel != null)
                length += WriteStream.WriteModelLength((uint)Properties.RoomModel, _roomModel, context);
            length += WriteStream.WriteCollectionLength((uint)Properties.SceneRealtimeViewModels,  _sceneViewModels, context);
            length += WriteStream.WriteCollectionLength((uint)Properties.PrefabRealtimeViewModels, _prefabViewModels, context);

            return length;
        }

        public void Write(WriteStream stream, StreamContext context) {
            // TODO: Ideally WriteStream can handle null properly (and just ignore writing the property entirely)
            if (_roomModel != null)
                stream.WriteModel((uint)Properties.RoomModel, _roomModel, context);
            stream.WriteCollection((uint)Properties.SceneRealtimeViewModels,  _sceneViewModels, context);
            stream.WriteCollection((uint)Properties.PrefabRealtimeViewModels, _prefabViewModels, context);
        }

        public void Read(ReadStream stream, StreamContext context) {
            // Loop through each property and deserialize
            uint propertyID;
            while (stream.ReadNextPropertyID(out propertyID)) {
                switch (propertyID) {
                    case (uint)Properties.SceneRealtimeViewModels:
                        stream.ReadCollection(_sceneViewModels, context);
                        break;
                    case (uint)Properties.RoomModel:
                        stream.ReadModel(_roomModel, context);
                        break;
                    case (uint)Properties.PrefabRealtimeViewModels:
                        stream.ReadCollection(_prefabViewModels, context);
                        break;
                    default:
                        stream.SkipProperty();
                        break;
                }
            }
        }
    }
}
