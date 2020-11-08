using System.Collections;
using FNNLib;
using FNNLib.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DefaultNamespace {
    public class TransportTest : MonoBehaviour {
        public Text running;

        public bool reverseRoles;

        public GameObject testPrefab;

        private NetworkScene testScene;

        void Start() {
            if ((Application.isEditor && !reverseRoles) || (!Application.isEditor && reverseRoles)) {
                NetworkManager.instance.serverOnClientConnect.AddListener(SpawnPlayerObj);
                NetworkManager.instance.StartHost();
            } else {
                NetworkManager.instance.StartClient("127.0.0.1");
            }
        }

        private void SpawnPlayerObj(ulong client) {
            if (testScene == null)
                testScene = NetworkSceneManager.LoadScene("Test", LoadSceneMode.Additive, LoadSceneMode.Additive);
            
            var obj = NetworkSceneManager.GetScene(1).Instantiate(testPrefab, Vector3.zero, Quaternion.identity);
            obj.GetComponent<NetworkIdentity>().SpawnAsPlayerObject(client);

            StartCoroutine(Test(obj));
        }

        private IEnumerator Test(GameObject obj) {
            yield return new WaitForSeconds(1f);
            // Move to test scene (for testing purposes).
            NetworkSceneManager.MoveNetworkObjectToScene(obj.GetComponent<NetworkIdentity>(), testScene);
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