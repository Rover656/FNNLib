using FNNLib;
using UnityEngine;

namespace DefaultNamespace {
    [RequireComponent(typeof(Rigidbody2D))]
    public class TestPlayerController : NetworkBehaviour {
        public float speed = 2;

        private Rigidbody2D _rb;

        public void Start() {
            _rb = GetComponent<Rigidbody2D>();
        }

        private void Update() {
            if (!isLocalPlayer) return;
            
            var moveVector = Vector2.zero;

            if (Input.GetKey("w"))
                moveVector.y += 1;
            if (Input.GetKey("s"))
                moveVector.y -= 1;
            if (Input.GetKey("a"))
                moveVector.x -= 1;
            if (Input.GetKey("d"))
                moveVector.x += 1;

            _rb.velocity = moveVector * speed;
        }
    }
}