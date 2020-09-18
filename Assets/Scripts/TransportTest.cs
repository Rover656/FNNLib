using FNNLib;
using FNNLib.SceneManagement;
using UnityEngine;
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

                NetworkManager.instance.clientOnConnect.AddListener(() => {
                                                                   var test = new TestPacket {
                                                                                  text = "Hello from the client!"
                                                                              };
                                                                   NetworkManager.instance.ClientSend(test);
                                                               });
            } else {
                NetworkManager.instance.StartServer();
                // NetworkServer.instance.onClientConnected.AddListener((client) => {
                //                                                          NetworkSceneManager.GetActiveScene()
                //                                                             .Instantiate(testPrefab, Vector3.zero,
                //                                                                  Quaternion.identity)
                //                                                             .GetComponent<NetworkIdentity>()
                //                                                             .SpawnWithOwnership(client);
                //                                                      });
                // testScene = NetworkSceneManager.LoadScene("Test", LoadSceneMode.Additive);
                // NetworkSceneManager.SetActiveScene(testScene);
            }

            // Register packet on possible targets.
            NetworkManager.instance.RegisterServerPacketHandler<TestPacket>(HandleTestPacket);
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

            // if (Input.GetMouseButtonDown(0)) {
            //     var pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            //     pos.z = 0;
            //     NetworkSceneManager.GetActiveScene()
            //                        .Instantiate(testPrefab, pos,
            //                                     Quaternion.identity)
            //                        .GetComponent<NetworkIdentity>().Spawn();
            // }
        }
    }
}