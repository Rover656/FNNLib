using System.Collections;
using System.Collections.Generic;
using FNNLib;
using FNNLib.RPC;
using FNNLib.Serialization;
using FNNLib.Transports;
using UnityEngine;

namespace DefaultNamespace {
    public class TestBehaviour : NetworkBehaviour {
        public override void NetworkStart() {
            NetworkManager.instance.serverOnClientConnect.AddListener((clientID) => {
                                                                          InvokeClientRPCOn(Test, clientID, "Hello world!");
                                                                          StartCoroutine(AwaitResponse(InvokeClientRPCOn(TestResponse, clientID)));
                                                                 });
            if (isClient) {
                // StartCoroutine(AwaitResponse(InvokeServerRPC(TestResponse)));
            }
        }

        private IEnumerator AwaitResponse(RPCResponse<bool> response) {
            while (!response.isDone) yield return null;
            Debug.Log("Received response from server, it was: " + response.value);
        }

        [ClientRPC]
        // [ServerRPC(requireOwnership = false)] // TODO: Allow a function to be both client and server callable? Is that bad?
        public bool TestResponse() {
            Debug.Log("TESTRESPONSE CALLED");
            return true;
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