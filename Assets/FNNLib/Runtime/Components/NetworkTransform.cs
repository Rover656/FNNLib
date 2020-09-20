using FNNLib.RPC;
using UnityEngine;

namespace FNNLib.Components {
    public class NetworkTransform : NetworkBehaviour {
        [Header("Configuration")]
        [SerializeField] private float sendsPerSecond = 20;
        [SerializeField] private float interpolationMultiplier = 1;

        private float _lastSendTime;
        private Vector3 targetPosition;
        private Quaternion targetRotation;
        private float interpolationSpeed = 1;
        
        void Awake() {
            targetPosition = transform.position;
            targetRotation = transform.rotation;
        }

        private void Update() {
            if (isOwner) {
                // TODO: Server time with support for network compensation
                if (Time.unscaledTime - _lastSendTime >= (1f / sendsPerSecond)) {
                    // TODO: Check position or rotation has changed
                    _lastSendTime = Time.unscaledTime;

                    if (isServer)
                        InvokeClientRPCOnAllExcept(ApplyTransform, ownerClientID, transform.position,
                                                    transform.rotation);
                    else InvokeServerRPC(SubmitTransform, transform.position, transform.rotation);
                }
                return;
            }

            // Perform interpolation
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, Time.deltaTime * interpolationSpeed);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, Time.deltaTime * interpolationSpeed);
        }

        [ClientRPC]
        private void ApplyTransform(Vector3 position, Quaternion rotation) {            
            float positonSpeed = Vector3.Distance(transform.position, position);
            float rotationSpeed = Quaternion.Angle(transform.rotation, rotation);
            interpolationSpeed = Mathf.Max(positonSpeed, rotationSpeed) * (interpolationMultiplier * 10);
            targetPosition = position;
            targetRotation = rotation;
        }

        [ServerRPC]
        private void SubmitTransform(Vector3 position, Quaternion rotation) {
            // Apply the transform if we are a dedicated server.
            if (!isClient)
                ApplyTransform(position, rotation);
            
            InvokeClientRPCOnAllExcept(ApplyTransform, ownerClientID, position, rotation);
        }
    }
}