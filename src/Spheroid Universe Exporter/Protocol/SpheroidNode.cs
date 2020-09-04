using UnityEngine;

namespace SpheroidUniverse.SceneGraph
{
    public class SpheroidNode : MonoBehaviour
    {
        [Header("Node Settings")]
        public bool export = true;

        public Node Node { get; set; }

        [Header("Sound Settings")]

        public string soundPath;
    }
}