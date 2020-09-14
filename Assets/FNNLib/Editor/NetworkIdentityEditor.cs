using UnityEditor;

namespace FNNLib.Editor {
    [CustomEditor(typeof(NetworkIdentity))]
    public class NetworkIdentityEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            base.OnInspectorGUI();
        }
    }
}