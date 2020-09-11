﻿using FNNLib.Core;
using FNNLib.Transports;
using UnityEngine;
using UnityEngine.UI;

namespace DefaultNamespace {
    public class TransportTest : MonoBehaviour {
        public Text running;

        public bool editorIsClient;

        void Start() {
            if ((editorIsClient && Application.isEditor) || (!editorIsClient && !Application.isEditor)) {
                NetworkManager.Instance.StartClient("127.0.0.1");

                // TODO: Pass through events to the NetworkManager too? Or leave the individual events in network client/server
                NetworkClient.Instance.onConnected.AddListener(() => {
                                                                   var test = new TestPacket {
                                                                                  text = "Hello from the client!"
                                                                              };
                                                                   NetworkClient.Instance.Send(test);
                                                               });
            }
            else {
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
            }
            else {
                running.text = NetworkManager.Instance.transport.serverRunning ? "Running" : "Stopped";
            }
        }
    }
}