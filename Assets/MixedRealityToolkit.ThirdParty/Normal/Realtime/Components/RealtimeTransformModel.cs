using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Normal.Realtime.Serialization;

public partial class RealtimeTransformModel {
    [RealtimeProperty(1, false)] private Vector3    _position;
    [RealtimeProperty(2, false)] private Quaternion _rotation;
    [RealtimeProperty(3, false)] private Vector3    _scale = Vector3.one;
    [RealtimeProperty(4, false)] private Vector3    _velocity;
    [RealtimeProperty(5, false)] private Vector3    _angularVelocity;
    //[RealtimeProperty(6, false)] private Vector3    _scaleVelocity;

    [Flags]
    private enum Flags : uint {
        Default           = 0,
        ShouldExtrapolate = 1 << 0,
        UseGravity        = 1 << 1,
        IsKinematic       = 1 << 2,
    }
    [RealtimeProperty(7, true)] private uint _physicsState = 0;

    [RealtimeProperty(8, false)] private double _timestamp;

    // Used to signal whether RealtimeTransform should set defaults in SetModel
    public bool freshModel { get; private set; }

    // TODO: Move this to the common model base class once it's written.
    public  delegate void RealtimeTransformModelEvent(RealtimeTransformModel model);
    public  event RealtimeTransformModelEvent willWrite;
    public  event RealtimeTransformModelEvent didRead;
    private bool _didDispatchWillWriteEvent = false;
}

public partial class RealtimeTransformModel : IModel {
    // Properties
    public UnityEngine.Vector3 position {
        get { return _position; }
        set { if (!shouldWritePosition) return; if (value == _position) return; _positionShouldWrite = true; _position = value; }
    }
    public UnityEngine.Quaternion rotation {
        get { return _rotation; }
        set { if (!shouldWriteRotation) return; if (value == _rotation) return; _rotationShouldWrite = true; _rotation = value; }
    }
    public UnityEngine.Vector3 scale {
        get { return _scale; }
        set { if (!shouldWriteScale) return; if (value == _scale) return; _scaleShouldWrite = true; _scale = value; }
    }
    public UnityEngine.Vector3 velocity {
        get { return _velocity; }
        set { if (!shouldWriteVelocityMetadata) return; if (value == _velocity) return; _velocityShouldWrite = true; _velocity = value; }
    }
    public UnityEngine.Vector3 angularVelocity {
        get { return _angularVelocity; }
        set { if (!shouldWriteVelocityMetadata) return; if (value == _angularVelocity) return; _angularVelocityShouldWrite = true; _angularVelocity = value; }
    }
    //public UnityEngine.Vector3 scaleVelocity {
    //    get { return _scaleVelocity; }
    //    set { if (value == _scaleVelocity) return; _scaleVelocityShouldWrite = true; _scaleVelocity = value; }
    //}
    public double timestamp {
        get { return _timestamp; }
        set { if (value == _timestamp) return; _timestampShouldWrite = true; _timestamp = value; }
    }

    private uint physicsState {
        get { return _cache.LookForValueInCache(_physicsState, entry => entry.physicsStateSet, entry => entry.physicsState); }
        set { _cache.UpdateLocalCache(entry => { entry.physicsStateSet = true; entry.physicsState = value; return entry; }); }
    }

    public bool shouldExtrapolate { get { return (physicsState & (uint)Flags.ShouldExtrapolate) != 0; } set { if (value == shouldExtrapolate) return; if (value) physicsState |= (uint)Flags.ShouldExtrapolate; else physicsState &= ~((uint)Flags.ShouldExtrapolate); } }
    public bool useGravity        { get { return (physicsState & (uint)Flags.UseGravity)        != 0; } set { if (value == useGravity)        return; if (value) physicsState |= (uint)Flags.UseGravity;        else physicsState &= ~((uint)Flags.UseGravity);        } }
    public bool isKinematic       { get { return (physicsState & (uint)Flags.IsKinematic)       != 0; } set { if (value == isKinematic)       return; if (value) physicsState |= (uint)Flags.IsKinematic;       else physicsState &= ~((uint)Flags.IsKinematic);       } }


    // Previous snapshot
    public Vector3    previousPosition  { get; private set; }
    public Quaternion previousRotation  { get; private set; }
    public double     previousTimestamp { get; private set; }


    // Will this model write
    public bool ModelPoseChangesToSend() {
        return _positionShouldWrite || _rotationShouldWrite || _scaleShouldWrite;
    }


    public bool shouldWritePosition = false;
    public bool shouldWriteRotation = false;
    public bool shouldWriteScale    = false;
    public bool shouldWriteVelocityMetadata = false;

    // Ownership
    public int ownerID { get { return _metaModel.ownerID; } }
    
    public void RequestOwnership(int clientIndex) {
        _metaModel.ownerID = clientIndex;
    }
    
    public void ClearOwnership() {
        _metaModel.ownerID = -1;
    }
    
    // Meta model
    private MetaModel _metaModel;
    
    private bool _positionShouldWrite;
    private bool _rotationShouldWrite;
    private bool _scaleShouldWrite;
    private bool _velocityShouldWrite;
    private bool _angularVelocityShouldWrite;
    //private bool _scaleVelocityShouldWrite;
    private bool _timestampShouldWrite;

    // Change Cache
    private struct LocalCacheEntry {
        public bool physicsStateSet;
        public uint physicsState;
    }
    private LocalChangeCache<LocalCacheEntry> _cache;

    public RealtimeTransformModel() {
        freshModel = true;
        
        _metaModel = new MetaModel();

        _cache = new LocalChangeCache<LocalCacheEntry>();
    }
    
    // Serialization
    enum PropertyID {
        Position = 1,
        Rotation = 2,
        Scale = 3,
        Velocity = 4,
        AngularVelocity = 5,
        ScaleVelocity = 6,
        PhysicsState = 7,
        Timestamp = 8,
    }

    public int WriteLength(StreamContext context) {
        // Dispatch will write event.
        if (!_didDispatchWillWriteEvent) {
            // Mark dispatched first in case willWrite triggers a WriteLength() call.
            _didDispatchWillWriteEvent = true;

            if (willWrite != null) {
                try {
                    willWrite(this);
                } catch (Exception exception) {
                    Debug.LogException(exception);
                }
            }
        }

        int length = 0;
        
        if (context.fullModel) {
            // Flatten cache
            _physicsState = physicsState;
            _cache.Clear();

            // Meta model
            length += WriteStream.WriteModelLength(0, _metaModel, context);

            // Write all properties
            if (shouldWritePosition)
                length += WriteStream.WriteBytesLength((uint)PropertyID.Position,        WriteStream.Vector3ToBytesLength());
            if (shouldWriteRotation)
                length += WriteStream.WriteBytesLength((uint)PropertyID.Rotation,        WriteStream.QuaternionToBytesLength());
            if (shouldWriteScale)
                length += WriteStream.WriteBytesLength((uint)PropertyID.Scale,           WriteStream.Vector3ToBytesLength());

            if (shouldWriteVelocityMetadata) {
                length += WriteStream.WriteBytesLength((uint)PropertyID.Velocity,        WriteStream.Vector3ToBytesLength());
                length += WriteStream.WriteBytesLength((uint)PropertyID.AngularVelocity, WriteStream.Vector3ToBytesLength());
                //length += WriteStream.WriteBytesLength((uint)PropertyID.ScaleVelocity,   WriteStream.Vector3ToBytesLength());
            }
            length += WriteStream.WriteVarint32Length((uint)PropertyID.PhysicsState, _physicsState);

            length += WriteStream.WriteBytesLength((uint)PropertyID.Timestamp, sizeof(double));
        } else {
            // Meta model
            length += WriteStream.WriteModelLength(0, _metaModel, context);
            
            // Unreliable properties
            if (context.unreliableChannel) {
                if (_positionShouldWrite) {
                    length += WriteStream.WriteBytesLength((uint)PropertyID.Position, WriteStream.Vector3ToBytesLength());
                }
                if (_rotationShouldWrite) {
                    length += WriteStream.WriteBytesLength((uint)PropertyID.Rotation, WriteStream.QuaternionToBytesLength());
                }
                if (_scaleShouldWrite) {
                    length += WriteStream.WriteBytesLength((uint)PropertyID.Scale, WriteStream.Vector3ToBytesLength());
                }
                if (_velocityShouldWrite) {
                    length += WriteStream.WriteBytesLength((uint)PropertyID.Velocity, WriteStream.Vector3ToBytesLength());
                }
                if (_angularVelocityShouldWrite) {
                    length += WriteStream.WriteBytesLength((uint)PropertyID.AngularVelocity, WriteStream.Vector3ToBytesLength());
                }
                ///if (_scaleVelocityShouldWrite) {
                //    length += WriteStream.WriteBytesLength((uint)PropertyID.ScaleVelocity, WriteStream.Vector3ToBytesLength());
                //}
                if (_timestampShouldWrite) {
                    length += WriteStream.WriteBytesLength((uint)PropertyID.Timestamp, sizeof(double));
                }
            } else if (context.reliableChannel) {
                LocalCacheEntry entry = _cache.localCache;
                if (entry.physicsStateSet)
                    length += WriteStream.WriteVarint32Length((uint)PropertyID.PhysicsState, entry.physicsState);
            }
        }

        // If the length is zero, a Write event will never get called.
        // TODO: Unfortunately this means that if the WriteLength is zero, the event will be dispatched multiple times in a single WriteStream.Serialize() pass.
        // If the model changes between those events, serialization will fail...
        if (length == 0)
            _didDispatchWillWriteEvent = false;
        
        return length;
    }
    
    public void Write(WriteStream stream, StreamContext context) {
        if (context.fullModel) {
            // Meta model
            stream.WriteModel(0, _metaModel, context);

            // Write all properties
            if (shouldWritePosition)
                stream.WriteBytes((uint)PropertyID.Position, WriteStream.Vector3ToBytes(_position));
            if (shouldWriteRotation)
                stream.WriteBytes((uint)PropertyID.Rotation, WriteStream.QuaternionToBytes(_rotation));
            if (shouldWriteScale)
                stream.WriteBytes((uint)PropertyID.Scale, WriteStream.Vector3ToBytes(_scale));

            if (shouldWriteVelocityMetadata) {
                stream.WriteBytes((uint)PropertyID.Velocity, WriteStream.Vector3ToBytes(_velocity));
                stream.WriteBytes((uint)PropertyID.AngularVelocity, WriteStream.Vector3ToBytes(_angularVelocity));
                //stream.WriteBytes((uint)PropertyID.ScaleVelocity, WriteStream.Vector3ToBytes(_scaleVelocity));
            }
            stream.WriteVarint32((uint)PropertyID.PhysicsState, _physicsState);

            stream.WriteBytes((uint)PropertyID.Timestamp, BitConverter.GetBytes(_timestamp));
        } else {
            // Meta model
            stream.WriteModel(0, _metaModel, context);
            
            // Unreliable properties
            if (context.unreliableChannel) {
                if (_positionShouldWrite) {
                    stream.WriteBytes((uint)PropertyID.Position, WriteStream.Vector3ToBytes(_position));
                    _positionShouldWrite = false;
                }
                if (_rotationShouldWrite) {
                    stream.WriteBytes((uint)PropertyID.Rotation, WriteStream.QuaternionToBytes(_rotation));
                    _rotationShouldWrite = false;
                }
                if (_scaleShouldWrite) {
                    stream.WriteBytes((uint)PropertyID.Scale, WriteStream.Vector3ToBytes(_scale));
                    _scaleShouldWrite = false;
                }
                if (_velocityShouldWrite) {
                    stream.WriteBytes((uint)PropertyID.Velocity, WriteStream.Vector3ToBytes(_velocity));
                    _velocityShouldWrite = false;
                }
                if (_angularVelocityShouldWrite) {
                    stream.WriteBytes((uint)PropertyID.AngularVelocity, WriteStream.Vector3ToBytes(_angularVelocity));
                    _angularVelocityShouldWrite = false;
                }
                //if (_scaleVelocityShouldWrite) {
                //    stream.WriteBytes((uint)PropertyID.ScaleVelocity, WriteStream.Vector3ToBytes(_scaleVelocity));
                //    _scaleVelocityShouldWrite = false;
                //}
                if (_timestampShouldWrite) {
                    stream.WriteBytes((uint)PropertyID.Timestamp, BitConverter.GetBytes(_timestamp));
                    _timestampShouldWrite = false;
                }
            } else if (context.reliableChannel) {
                // If we're going to send an update. Push the cache to inflight.
                LocalCacheEntry entry = _cache.localCache;
                if (entry.physicsStateSet)
                    _cache.PushLocalCacheToInflight(context.updateID);

                if (entry.physicsStateSet)
                    stream.WriteVarint32((uint)PropertyID.PhysicsState, entry.physicsState);
            }
        }

        // Reset will write event flag.
        _didDispatchWillWriteEvent = false;
    }
    
    public void Read(ReadStream stream, StreamContext context) {
        // Used to signal whether RealtimeTransform should set defaults in SetModel
        freshModel = false;

        // Remove from in-flight
        if (context.deltaUpdatesOnly && context.reliableChannel)
            _cache.RemoveUpdateFromInflight(context.updateID);

        // Store previous snapshot (used for transform extrapolation)
        Vector3    preReadPosition = position;
        Quaternion preReadRotation = rotation;

        // Loop through each property and deserialize
        uint propertyID;
        while (stream.ReadNextPropertyID(out propertyID)) {
            switch (propertyID) {
                case 0:
                    // Meta model
                    stream.ReadModel(_metaModel, context);
                    break;
                case (uint)PropertyID.Position: {
                    _position = ReadStream.Vector3FromBytes(stream.ReadBytes());
                    _positionShouldWrite = false;
                    break;
                }
                case (uint)PropertyID.Rotation: {
                    _rotation = ReadStream.QuaternionFromBytes(stream.ReadBytes());
                    _rotationShouldWrite = false;
                    break;
                }
                case (uint)PropertyID.Scale: {
                    _scale = ReadStream.Vector3FromBytes(stream.ReadBytes());
                    _scaleShouldWrite = false;
                    break;
                }
                case (uint)PropertyID.Velocity: {
                    _velocity = ReadStream.Vector3FromBytes(stream.ReadBytes());
                    _velocityShouldWrite = false;
                    break;
                }
                case (uint)PropertyID.AngularVelocity: {
                    _angularVelocity = ReadStream.Vector3FromBytes(stream.ReadBytes());
                    _angularVelocityShouldWrite = false;
                    break;
                }
                //case (uint)PropertyID.ScaleVelocity: {
                //    _scaleVelocity = ReadStream.Vector3FromBytes(stream.ReadBytes());
                //    _scaleVelocityShouldWrite = false;
                //    break;
                //}
                case (uint)PropertyID.PhysicsState: {
                    _physicsState = stream.ReadVarint32();
                    break;
                }
                case (uint)PropertyID.Timestamp: {
                    // We got a timestamp, cache the previous snapshot's values.
                    previousPosition  = preReadPosition;
                    previousRotation  = preReadRotation;
                    previousTimestamp = timestamp;
                    
                    byte[] bytes = stream.ReadBytes();
                    _timestamp = BitConverter.ToDouble(bytes, 0);
                    break;
                }
                default:
                    stream.SkipProperty();
                    break;
            }
        }

        if (didRead != null) {
            try {
                didRead(this);
            } catch (Exception exception) {
                Debug.LogException(exception);
            }
        }
    }
}
