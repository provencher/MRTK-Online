using UnityEngine;
using Normal.Realtime;

namespace Normal.Realtime {
    [DisallowMultipleComponent]
    public class RealtimeTransform : RealtimeComponent {
        [SerializeField] private bool _syncPosition  = true;
        [SerializeField] private bool _syncRotation  = true;
        [SerializeField] private bool _syncScale     = true;
        [SerializeField] private bool _extrapolation = false;
                         public  bool  extrapolation { get { return _extrapolation; } set { _extrapolation = value; } }
        
        private RealtimeTransformModel _model;
        public  RealtimeTransformModel  model { get { return _model; } set { SetModel(value); } }
        // TODO: Rather than getting getting the client ID from realtime to compare, I'd rather the model be able to tell us whether it's owned locally or not. The model should have a reference to the room/datastore it belongs to.
        //       Once IModel becomes a class instead of an interface, we'll be able to do this.
        public int  ownerID        { get { return _model.ownerID; } }
        public bool isOwnedLocally { get { return _model.ownerID == realtime.clientID; } }
        public bool isOwnedByWorld { get { return _model.ownerID == -1; } }
    
        private bool   _wrotePoseChangesLastNetworkFrame = false;
        private double _stopExtrapolatingAtRoomTime = -1.0; // TODO: Convert to float once we have a float value for room time since start
        
        private Rigidbody _rigidbody;
    
        // Unity events
        private void Awake() {
            _rigidbody = GetComponent<Rigidbody>();
        }
    
        void OnDestroy() {
            // Clear model (unregisters from model events)
            model = null;
        }
    
        private void FixedUpdate() {
            ApplyRigidbody();
        }
    
        private void Update() {
            ApplyTransform();
        }
    
        // Ownership
        public void RequestOwnership() {
            _model.RequestOwnership(realtime.clientID);
        }
    
        public void ClearOwnership() {
            _model.ClearOwnership();
        }
    
        // Send transform
        private void ModelWillWrite(RealtimeTransformModel model) {
            if (model != _model) {
                Debug.LogError("RealtimeTransform received model willWrite event from another model... This is a bug.");
                return;
            }
    
            // If it's not owned by us, ignore the event.
            if (!isOwnedLocally)
                return;
    
            // We own this RealtimeTransform. Update the model to reflect its current position.
            if (_rigidbody != null) {
                if (_syncPosition) _model.position        = _rigidbody.position;
                if (_syncRotation) _model.rotation        = _rigidbody.rotation;
                if (_syncScale)    _model.scale           =  transform.localScale;
                if (_syncPosition) _model.velocity        = _rigidbody.velocity;
                if (_syncRotation) _model.angularVelocity = _rigidbody.angularVelocity;
                //if (_shouldSyncScale) _model.scaleVelocity   =  // ...
                _model.useGravity  = _rigidbody.useGravity;
                _model.isKinematic = _rigidbody.isKinematic;
            } else {
                if (_syncPosition) _model.position = transform.localPosition;
                if (_syncRotation) _model.rotation = transform.localRotation;
                if (_syncScale)    _model.scale    = transform.localScale;
            }
    
            if (_model.ModelPoseChangesToSend()) {
                // Only send the timestamp if we're sending other pose changes.
                _model.timestamp =  realtime.room.time;
                _wrotePoseChangesLastNetworkFrame = true;
            } else if (_wrotePoseChangesLastNetworkFrame) {
                // If we wrote changes last frame, but this frame the model is clean. Force one more model update to go out so extrapolation calculates a zero velocity.
                _model.timestamp = realtime.room.time;
            }
    
            // If the rigidbody has come to rest, clear ownership
            if (_rigidbody != null && _rigidbody.IsSleeping() && !_rigidbody.isKinematic)
                _model.ClearOwnership();
    
            // Set should extrapolate flag
            _model.shouldExtrapolate = _extrapolation;
        }
    
        // Receive transform
        void ModelDidRead(RealtimeTransformModel model) {
            if (model != _model) {
                Debug.LogError("RealtimeTransform received model didRead event from another model... This is a bug.");
                return;
            }
    
            // If it's owned by us, ignore the event.
            if (isOwnedLocally)
                return;
    
            // Used to keep the value consistent after ownership change (also means the editor reflects the current state for remote transforms).
            _extrapolation = _model.shouldExtrapolate;
    
            // We've received a new frame, stop simulating locally now that we have more information on the collision.
            _stopExtrapolatingAtRoomTime = -1.0f;
        }
    
        // Apply
        private void ApplyTransform() {
            if (_rigidbody != null)
                return;
    
            if (isOwnedLocally)
                return;
    
            // If the model is owned by someone, we extrapolate & lerp.
            if (_model.ownerID != -1) {
                // Snapshot
                Vector3        position = _syncPosition ? _model.position : transform.localPosition;
                Quaternion     rotation = _syncRotation ? _model.rotation : transform.localRotation;
                Vector3           scale = _syncScale    ? _model.scale    : transform.localScale;
                bool  shouldExtrapolate = _model.shouldExtrapolate;
                double        timestamp = _model.timestamp;
    
                float deltaTime = (float)(realtime.room.time - timestamp);
    
                // If the last snapshot is too old, skip extrapolation.
                if (deltaTime > 1.0f)
                    shouldExtrapolate = false;
    
                // Extrapolate
                if (shouldExtrapolate) {
                    // Calculate velocity & angularVelocity
                    Vector3 velocity        = Vector3.zero;
                    Vector3 angularVelocity = Vector3.zero;
    
                    Vector3    previousPosition  = _model.previousPosition;
                    Quaternion previousRotation  = _model.previousRotation;
                    double     previousTimestamp = _model.previousTimestamp;
    
                    // Only extrapolate if we have a previous frame, otherwise we'll have no data to extrapolate with.
                    if (previousTimestamp > 0.0) {
                        Vector3    positionDelta           = position - previousPosition;
                        Quaternion rotationDeltaQuaternion = Quaternion.Inverse(previousRotation) * rotation;
                        Vector3    rotationDeltaDegrees    = ReduceAngles(rotationDeltaQuaternion.eulerAngles);
                        Vector3    rotationDelta           = new Vector3(rotationDeltaDegrees.x * Mathf.Deg2Rad, rotationDeltaDegrees.y * Mathf.Deg2Rad, rotationDeltaDegrees.z * Mathf.Deg2Rad);
                        float      timeDelta = (float)(timestamp - previousTimestamp);
    
                        if (timeDelta > 0.0f) {
                            velocity        = positionDelta / timeDelta;
                            angularVelocity = rotationDelta / timeDelta;
                        }
    
                        // Cap extrapolation time
                        float extrapolateTimeMax = Time.fixedDeltaTime * 10.0f; // Limit to 5 frames.
                        deltaTime = Mathf.Clamp(deltaTime, 0.0f, extrapolateTimeMax);

                        // Simulate
                        PhysXEmulation.SimulatePhysX(ref position, ref rotation, ref velocity, ref angularVelocity, false, Vector3.zero, 0.0f, 0.0f, float.PositiveInfinity, float.PositiveInfinity, RigidbodyConstraints.None, deltaTime, Time.fixedDeltaTime);
                    }
                }
    
                // Apply
                float lerpFactor = 25.0f;
                if (_syncPosition) transform.localPosition =     Vector3.Lerp(transform.localPosition, position, lerpFactor * Time.deltaTime);
                if (_syncRotation) transform.localRotation = Quaternion.Slerp(transform.localRotation, rotation, lerpFactor * Time.deltaTime);
                if (_syncScale)    transform.localScale    =     Vector3.Lerp(transform.localScale,    scale,    lerpFactor * Time.deltaTime);
            } else {
                // If the model isn't owned by anyone, set the transform position instantly.
                if (_syncPosition) transform.localPosition = _model.position;
                if (_syncRotation) transform.localRotation = _model.rotation;
                if (_syncScale)    transform.localScale    = _model.scale;
            }
        }
    
        private void ApplyRigidbody() {
            if (_rigidbody == null)
                return;
    
            if (isOwnedLocally)
                return;
    
            // If we're using rigidbody extrapolation and we've collided with something, let the local rigidbody simulation run until we receive another packet or 500ms has passed
            if (_rigidbody != null && _extrapolation && _stopExtrapolatingAtRoomTime >= 0.0 && (realtime.room.time - _stopExtrapolatingAtRoomTime) < 0.5f)
                return;
    
            if (_rigidbody.useGravity != _model.useGravity)
                _rigidbody.useGravity  = _model.useGravity;
    
            if (_rigidbody.isKinematic != _model.isKinematic)
                _rigidbody.isKinematic  = _model.isKinematic;
    
            // If the model is owned by someone, we extrapolate & lerp.
            if (_model.ownerID != -1) {
                // Snapshot
                Vector3        position = _syncPosition ? _model.position : _rigidbody.position;
                Quaternion     rotation = _syncRotation ? _model.rotation : _rigidbody.rotation;
                Vector3           scale = _syncScale    ? _model.scale    :  transform.localScale;
                Vector3        velocity = _model.velocity;
                Vector3 angularVelocity = _model.angularVelocity;
                bool  shouldExtrapolate = _model.shouldExtrapolate;
                bool         useGravity = _model.useGravity;
                double        timestamp = _model.timestamp;
    
                float deltaTime = (float)(realtime.room.time - timestamp);
    
                // If the last snapshot is too old, skip extrapolation.
                if (deltaTime > 1.0f)
                    shouldExtrapolate = false;
    
                // Extrapolate
                if (shouldExtrapolate) {
                    // Cap extrapolation time
                    float extrapolateTimeMax = _model.isKinematic ? Time.fixedDeltaTime * 5.0f : 0.5f; // Limit to 5 frames if kinematic. 500ms if non-kinematic.
                    deltaTime = Mathf.Clamp(deltaTime, 0.0f, extrapolateTimeMax);

                    // Simulate
                    // TODO: I suspect that this function is off even though I've checked it. I know the single frame one is good, but I'm worried this one is off which is causing switching to the local simulation on bounce to not work correctly.
                    PhysXEmulation.SimulatePhysX(ref position, ref rotation, ref velocity, ref angularVelocity, useGravity, Physics.gravity, _rigidbody.drag, _rigidbody.angularDrag, _rigidbody.maxDepenetrationVelocity, _rigidbody.maxAngularVelocity, _rigidbody.constraints, deltaTime, Time.fixedDeltaTime);
                }
    
                // Apply
                float lerpFactor = 25.0f;
                // Set scale before position/rotation otherwise scale will get reset.
                if (_syncScale)     transform.localScale =     Vector3.Lerp( transform.localScale, scale,    lerpFactor * Time.fixedDeltaTime);
                if (_syncPosition) _rigidbody.MovePosition(    Vector3.Lerp(_rigidbody.position,   position, lerpFactor * Time.fixedDeltaTime));
                if (_syncRotation) _rigidbody.MoveRotation(Quaternion.Slerp(_rigidbody.rotation,   rotation, lerpFactor * Time.fixedDeltaTime));
            } else {
                // If the model isn't owned by anyone, set the transform position instantly.
                if (_syncScale)     transform.localScale      = _model.scale;
                if (_syncPosition) _rigidbody.position        = _model.position;
                if (_syncRotation) _rigidbody.rotation        = _model.rotation;
                if (_syncPosition) _rigidbody.velocity        = Vector3.zero;
                if (_syncRotation) _rigidbody.angularVelocity = Vector3.zero;
            }
        }
    
        // Collisions
        private void OnCollisionEnter(Collision collision) {
            // If we're using extrapolation, start simulating locally in order to make the bounce smoother.
            if (_extrapolation)
                _stopExtrapolatingAtRoomTime = realtime.room.time;
    
            // If we are currently the authoritative owner of this rigidbody and we've collided with a rigidbody that isn't owned by anyone, let's take over ownership and start simulating it.
            if (isOwnedLocally) {
                Rigidbody rigidbody = collision.rigidbody;
                if (rigidbody != null) {
                    RealtimeTransform otherRealtimeTransform = rigidbody.GetComponent<RealtimeTransform>();
                    if (otherRealtimeTransform != null) {
                        // If the other realtime rigidbody isn't owned by anyone and it's not kinematic, take it over.
                        if (otherRealtimeTransform.isOwnedByWorld && !otherRealtimeTransform.model.isKinematic)
                            otherRealtimeTransform.RequestOwnership();
                    }
                }
            }
        }
    
        void SetModel(RealtimeTransformModel model) {
            if (_model != null) {
                // Clear events
                _model.willWrite -= ModelWillWrite;
                _model.didRead   -= ModelDidRead;
            }
    
            _model = model;
    
            if (_model != null) {    
                // Register for events
                _model.willWrite += ModelWillWrite;
                _model.didRead   += ModelDidRead;
    
                _model.shouldWritePosition = _syncPosition;
                _model.shouldWriteRotation = _syncRotation;
                _model.shouldWriteScale    = _syncScale;
                _model.shouldWriteVelocityMetadata = _rigidbody != null;
    
                // Sync with the model
                if (_model.freshModel) {
                    // If this is a fresh model, fill it with the current transform state
                    if (_rigidbody != null) {
                        if (_syncPosition) _model.position        = _rigidbody.position;
                        if (_syncRotation) _model.rotation        = _rigidbody.rotation;
                        if (_syncScale)    _model.scale           =  transform.localScale;
                        if (_syncPosition) _model.velocity        = _rigidbody.velocity;
                        if (_syncRotation) _model.angularVelocity = _rigidbody.angularVelocity;
                        //if (_shouldSyncScale)    _model.scaleVelocity   =  // ...
                        if (_rigidbody != null)  _model.useGravity  = _rigidbody.useGravity;
                        if (_rigidbody != null)  _model.isKinematic = _rigidbody.isKinematic;
                    } else {
                        if (_syncPosition) _model.position = transform.localPosition;
                        if (_syncRotation) _model.rotation = transform.localRotation;
                        if (_syncScale)    _model.scale    = transform.localScale;
                    }
                } else {
                    // If this is not a fresh model, set the transform using the model
                    if (_rigidbody != null) {
                        if (_syncScale)     transform.localScale      = _model.scale;
                        if (_syncPosition) _rigidbody.position        = _model.position;
                        if (_syncRotation) _rigidbody.rotation        = _model.rotation;
                        if (_syncPosition) _rigidbody.velocity        = _model.velocity;
                        if (_syncRotation) _rigidbody.angularVelocity = _model.angularVelocity;
                        if (_rigidbody != null)  _rigidbody.useGravity      = _model.useGravity;
                        if (_rigidbody != null)  _rigidbody.isKinematic     = _model.isKinematic;
                    } else {
                        if (_syncPosition) transform.localPosition = _model.position;
                        if (_syncRotation) transform.localRotation = _model.rotation;
                        if (_syncScale)    transform.localScale    = _model.scale;
                    }
                }
            }
        }
    
        // Utility
        private static Vector3 ReduceAngles(Vector3 angles) {
            // Cap all angles between -180 and 180
    
            // X
            if (angles.x >  180.0f) angles.x -= 360.0f;
            if (angles.x < -180.0f) angles.x += 360.0f;
    
            // Y
            if (angles.y >  180.0f) angles.y -= 360.0f;
            if (angles.y < -180.0f) angles.y += 360.0f;
    
            // Z
            if (angles.z >  180.0f) angles.z -= 360.0f;
            if (angles.z < -180.0f) angles.z += 360.0f;
    
            return angles;
        }
    }
}
