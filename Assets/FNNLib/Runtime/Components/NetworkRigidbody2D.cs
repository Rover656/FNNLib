using FNNLib.RPC;
using UnityEngine;

namespace FNNLib.Components {
    [NetworkAware]
    [RequireComponent(typeof(Rigidbody2D))]
    public class NetworkRigidbody2D : NetworkBehaviour {
        [Header("Configuration")]
        [SerializeField] private float sendsPerSecond = 20;
        [SerializeField] private float interpolationMultiplier = 1;

        private Rigidbody2D _rigidBody;

        private float _lastSendTime;
        private float _interpolationSpeed = 1;

        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private Vector2 _targetVelocity;
        private float _targetAngularVelocity;
        
        void Awake() {
            _rigidBody = GetComponent<Rigidbody2D>();

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
                        InvokeClientRPCForAllExcept(ApplyRigidBody, ownerClientID, transform.position, transform.rotation, _rigidBody.velocity, _rigidBody.angularVelocity);
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
        private void ApplyRigidBody(Vector3 position, Quaternion rotation, Vector2 velocity, float angularVelocity) {            
            float positonSpeed = Vector3.Distance(transform.position, position);
            float rotationSpeed = Quaternion.Angle(transform.rotation, rotation);
            _interpolationSpeed = Mathf.Max(positonSpeed, rotationSpeed) * (interpolationMultiplier * 10);

            _targetPosition = position;
            _targetRotation = rotation;
            _targetVelocity = velocity;
            _targetAngularVelocity = angularVelocity;
        }

        [ServerRPC]
        private void SubmitRigidbody(Vector3 position, Quaternion rotation, Vector2 velocity, float angularVelocity) {
            // Apply the transform if we are a dedicated server.
            if (!isClient)
                ApplyRigidBody(position, rotation, velocity, angularVelocity);
            
            InvokeClientRPCForAllExcept(ApplyRigidBody, ownerClientID, position, rotation, velocity, angularVelocity);
        }
    }
}