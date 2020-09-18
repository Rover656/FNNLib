using FNNLib.RPC;
using UnityEngine;

namespace FNNLib.Components {
    
    [RequiresComponent(typeof(Rigidbody))]
    public class NetworkRigidbody : NetworkBehaviour {
        [Header("Configuration")]
        [SerializeField] private float sendsPerSecond = 20;
        [SerializeField] private float interpolationMultiplier = 1;

        private Rigidbody rigidBody;

        private float _lastSendTime;
        private float interpolationSpeed = 1;

        private Vector3 targetPosition;
        private Quaternion targetRotation;
        private Vector3 targetVelocity;
        private Vector3 targetAngularVelocity;
        
        void Awake() {
            rigidBody = getComponent<Rigidbody>();

            targetPosition = transform.position;
            targetRotation = transform.rotation;
            targetVelocity = rigidBody.velocity;
            targetAngularVelocity = rigidBody.angularVelocity;
        }

        private void Update() {
            if (isOwner) {
                // TODO: Server time with support for network compensation
                if (Time.unscaledTime - _lastSendTime >= (1f / sendsPerSecond)) {
                    // TODO: Check position or rotation has changed
                    _lastSendTime = Time.unscaledTime;

                    if (isServer)
                        InvokeClientRPCForAllExcept(ApplyRigidBody, ownerClientID, transform.position, transform.rotation);
                    else InvokeServerRPC(SubmitRigidbody, transform.position, transform.rotation, rigidBody.velocity, rigidBody.angularVelocity);
                }
                return;
            }

            // Perform interpolation
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, Time.deltaTime * interpolationSpeed);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, Time.deltaTime * interpolationSpeed);
            rigidBody.velocity = targetVelocity;
            rigidBody.angularVelocity = targetAngularVelocity;
        }

        [ClientRPC]
        private void ApplyRigidBody(Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity) {            
            float positonSpeed = Vector3.Distance(transform.position, position);
            float rotationSpeed = Quaternion.Angle(transform.rotation, rotation);
            interpolationSpeed = Mathf.Max(positonSpeed, rotationSpeed) * (interpolationMultiplier * 10);

            targetPosition = position;
            targetRotation = rotation;
            targetVelocity = velocity;
            targetAngularVelocity = angularVelocity;
        }

        [ServerRPC]
        private void SubmitRigidbody(Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity) {
            // Apply the transform if we are a dedicated server.
            if (!isClient)
                ApplyTransform(position, rotation, velocity, angularVelocity);
            
            InvokeClientRPCForAllExcept(ApplyRigidBody, ownerClientID, position, rotation, velocity, angularVelocity);
        }
    }
}