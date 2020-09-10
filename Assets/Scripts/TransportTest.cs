using System;
using System.Collections.Generic;
using FNNLib.Core;
using FNNLib.Messaging;
using FNNLib.Serialization;
using FNNLib.Transports;
using TMPro;
// using FNNLib.Transports;
using UnityEngine;
using UnityEngine.UI;

namespace DefaultNamespace {
    public class TransportTest : MonoBehaviour {
        public Text running;

        public bool editorIsClient;
        
        void Start() {
            if ((editorIsClient && Application.isEditor) || (!editorIsClient && !Application.isEditor)) {
                NetworkManager.Instance.StartClient("127.0.0.1");
                
                // TODO: Pass through events to the server, client and even manager.
                Transport.currentTransport.onClientConnected.AddListener(() => {
                                                                             var test = new TestPacket();
                                                                             test.text = "Hello from the client!";
                                                                             NetworkManager.Instance.Send(0, test);
                                                                         });
            } else {
                NetworkManager.Instance.StartServer();
            }
            
            // Register packet on possible targets. In this packets case, itll register on both client and server.
            // If you want to use a separate handler for client or server, use the NetworkServer or Client instead!
            NetworkManager.Instance.RegisterPacketHandler<TestPacket>(HandleTestPacket);
        }

        void HandleTestPacket(int clientID, TestPacket packet) {
            if (NetworkManager.Instance.isServer) {
                Debug.Log("Received test packet from " + clientID + "! Text is \"" + packet.text + "\"");
            }
            else {
                Debug.Log("Received test packet from server! Text is \"" + packet.text + "\"");
            }
        }

        void Update() {
            if ((editorIsClient && Application.isEditor) || (!editorIsClient && !Application.isEditor)) {
                running.text = NetworkManager.Instance.transport.clientConnected ? "Connected" : "Disconnected";
            } else {
                running.text = NetworkManager.Instance.transport.serverRunning ? "Running" : "Stopped";
            }
        }
    }
}