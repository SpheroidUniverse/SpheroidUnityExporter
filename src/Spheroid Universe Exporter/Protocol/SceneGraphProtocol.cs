using System.Collections.Generic;

namespace SpheroidUniverse.SceneGraph
{
    public enum NodeType
    {
        Undefined = 0,
        Node = 1,
        ModelNode = 2
    }

    public class Vector3
    {
        public float X { get; set; }

        public float Y { get; set; }

        public float Z { get; set; }

        public Vector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    public sealed class KeyFrame
    {
        public float Time { get; set; }

        public float? X { get; set; }

        public float? Y { get; set; }

        public float? Z { get; set; }

        public float? W { get; set; }
    }

    public sealed class Scene
    {
        public IList<Node> Nodes { get; set; }

        public IList<Audio> Audios { get; set; }
    }

    public sealed class Audio
    {
        public string Path { get; set; }
    }

    public sealed class Node
    {
        public string Title { get; set; }

        public string ModelPath { get; set; }

        public string SoundPath { get; set; }

        public NodeType Type { get; set; } = NodeType.Node;

        public Vector3 Position { get; set; } = new Vector3(0, 0, 0);

        public Vector3 Rotation { get; set; } = new Vector3(0, 0, 0);

        public Vector3 Scale { get; set; } = new Vector3(1, 1, 1);

        public bool LoopAnimation { get; set; }

        public string AnimationName { get; set; }

        public int ViewDistance { get; set; }

        public IList<Animation> Animations { get; set; }

        public IList<Node> Nodes { get; set; } = new List<Node>();
    }

    public sealed class Animation
    {
        public enum PropertyType
        {
            Undefined = 0,
            Position = 1,
            Rotation = 2,
            Scale = 3,
            Opacity = 4
        }

        public class Property
        {
            public PropertyType Type { get; set; }

            public IList<KeyFrame> KeyFrames { get; set; }
        }

        public string Name { get; set; }

        public IList<Property> Properties { get; set; }
    }
}
