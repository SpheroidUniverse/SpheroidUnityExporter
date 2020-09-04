using UnityEngine;

namespace SpheroidUniverse.SceneGraph
{
    public class SpheroidModel : SpheroidNode
    {
        [Header("Model Settings")]
        public string animationName;

        public int viewDistance;

        public string modelPath;
    }
}