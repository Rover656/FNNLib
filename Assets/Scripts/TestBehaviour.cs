using System.Collections;
using System.Collections.Generic;
using FNNLib;
using FNNLib.Backend;
using FNNLib.RPC;
using FNNLib.Serialization;
using UnityEngine;

namespace DefaultNamespace {
    public class TestBehaviour : NetworkBehaviour {
        public override void NetworkStart() {
            // NetworkServer.instance.onClientConnected.AddListener((clientID) => {
            //                                                          StartCoroutine(Thing(clientID));
            //                                                      });
            if (isClient)
                InvokeServerRPC(ServerTest, "hello");
        }

        [ClientRPC]
        public void Test(string hi) {
            Debug.Log("RPC called! Server said: " + hi);
        }

        [ServerRPC(requireOwnership = false)]
        public void ServerTest(string hello) {
            Debug.Log("Server RPC called! Client said: " + hello);
        }

        private IEnumerator Thing(ulong clientID) {
            yield return new WaitForSeconds(2f);
            // InvokeClientRPC(Test, new List<ulong> {clientID}, "Hello world!");
            // InvokeClientRPCForAll(Test, "Hello world!");
            // InvokeClientRPCFor(Test, clientID, "Hello world!");
        }
    }
}