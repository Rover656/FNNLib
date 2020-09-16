using FNNLib;
using FNNLib.Backend;
using FNNLib.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DefaultNamespace {
    public class TransportTest : MonoBehaviour {
        public Text running;

        public bool editorIsClient;

        public GameObject testPrefab;

        private NetworkScene testScene;

        void Start() {
            if ((editorIsClient && Application.isEditor) || (!editorIsClient && !Application.isEditor)) {
                NetworkManager.instance.StartClient("127.0.0.1");

                // TODO: Pass through events to the NetworkManager too? Or leave the individual events in network client/server
                NetworkClient.instance.onConnected.AddListener(() => {
                                                                   var test = new TestPacket {
                                                                                  text = "Hello from the client!"
                                                                              };
                                                                   NetworkClient.instance.Send(test);
                                                               });
            } else {
                NetworkManager.instance.StartServer();
                NetworkServer.instance.onClientConnected.AddListener((client) => {
                                                                         NetworkSceneManager.GetActiveScene()
                                                                            .Instantiate(testPrefab, Vector3.zero,
                                                                                 Quaternion.identity)
                                                                            .GetComponent<NetworkIdentity>()
                                                                            .SpawnWithOwnership(client);
                                                                     });
                testScene = NetworkSceneManager.LoadScene("Test", LoadSceneMode.Additive);
                NetworkSceneManager.SetActiveScene(testScene);
            }

            // Register packet on possible targets. In this packets case, itll register on both client and server.
            // If you want to use a separate handler for client or server, use the NetworkServer or Client instead!
            NetworkManager.instance.RegisterPacketHandler<TestPacket>(HandleTestPacket);
        }

        void HandleTestPacket(ulong clientID, TestPacket packet) {
            if (NetworkManager.instance.isServer) {
                Debug.Log("Received test packet from " + clientID + "! Text is \"" + packet.text + "\"");
            } else {
                Debug.Log("Received test packet from server! Text is \"" + packet.text + "\"");
            }
        }

        void Update() {
            // if ((editorIsClient && Application.isEditor) || (!editorIsClient && !Application.isEditor)) {
            //     running.text = NetworkManager.instance.networkConfig.transport.clientConnected ? "Connected" : "Disconnected";
            // }
            // else {
            //     running.text = NetworkManager.instance.networkConfig.transport.serverRunning ? "Running" : "Stopped";
            // }

            return;

            if (Input.GetMouseButtonDown(0)) {
                var pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                pos.z = 0;
                NetworkSceneManager.GetActiveScene()
                                   .Instantiate(testPrefab, pos,
                                                Quaternion.identity)
                                   .GetComponent<NetworkIdentity>().Spawn();
            }
        }
    }
}