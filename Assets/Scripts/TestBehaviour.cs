using System.Collections;
using System.Collections.Generic;
using FNNLib;
using FNNLib.RPC;
using FNNLib.Serialization;
using UnityEngine;

namespace DefaultNamespace {
    public class TestBehaviour : NetworkBehaviour {
        public override void NetworkStart() {
            NetworkManager.instance.serverOnClientConnect.AddListener((clientID) => {
                                                                          InvokeClientRPCFor(Test, clientID, "Hello world!");
                                                                 });
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
    }
}