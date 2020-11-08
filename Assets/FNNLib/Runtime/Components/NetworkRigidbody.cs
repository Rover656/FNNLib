using FNNLib.RPC;
using UnityEngine;

namespace FNNLib.Components {
    [RequireComponent(typeof(Rigidbody))]
    public class NetworkRigidbody : NetworkBehaviour {
        [Header("Configuration")]
        [SerializeField] private float sendsPerSecond = 20;
        [SerializeField] private float interpolationMultiplier = 1;

        private Rigidbody _rigidBody;

        private float _lastSendTime;
        private float _interpolationSpeed = 1;

        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private Vector3 _targetVelocity;
        private Vector3 _targetAngularVelocity;
        
        void Awake() {
            _rigidBody = GetComponent<Rigidbody>();

            _targetPosition = transform.position;
            _targetRotation = transform.rotation;
            _targetVelocity = _rigidBody.velocity;
            _targetAngularVelocity = _rigidBody.angularVelocity;
        }

        private void Update() {
            if (isOwner) {
                // TODO: Server time with support for network compensation
                if (Time.unscaledTime - _lastSendTime >= (1f / sendsPerSecond)) {
                    // TODO: Check position or rotation has changed
                    _lastSendTime = Time.unscaledTime;

                    if (isServer)
                        InvokeClientRPCOnAllExcept(ApplyRigidBody, ownerClientID, transform.position, transform.rotation, _rigidBody.velocity, _rigidBody.angularVelocity);
                    else InvokeServerRPC(SubmitRigidbody, transform.position, transform.rotation, _rigidBody.velocity, _rigidBody.angularVelocity);
                }
                return;
            }

            // Perform interpolation
            transform.position = Vector3.MoveTowards(transform.position, _targetPosition, Time.deltaTime * _interpolationSpeed);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, _targetRotation, Time.deltaTime * _interpolationSpeed);
            _rigidBody.velocity = _targetVelocity;
            _rigidBody.angularVelocity = _targetAngularVelocity;
        }

        [ClientRPC]
        private void ApplyRigidBody(Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity) {            
            float positonSpeed = Vector3.Distance(transform.position, position);
            float rotationSpeed = Quaternion.Angle(transform.rotation, rotation);
            _interpolationSpeed = Mathf.Max(positonSpeed, rotationSpeed) * (interpolationMultiplier * 10);

            _targetPosition = position;
            _targetRotation = rotation;
            _targetVelocity = velocity;
            _targetAngularVelocity = angularVelocity;
        }

        [ServerRPC]
        private void SubmitRigidbody(Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity) {
            // Apply the transform if we aren't the owner
            if (!isOwner)
                ApplyRigidBody(position, rotation, velocity, angularVelocity);
            
            InvokeClientRPCOnAllExcept(ApplyRigidBody, ownerClientID, position, rotation, velocity, angularVelocity);
        }
    }
}