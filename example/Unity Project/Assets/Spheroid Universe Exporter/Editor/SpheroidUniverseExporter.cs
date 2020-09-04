using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using Debug = UnityEngine.Debug;
using System.IO;
using System.Text;
using UnityEditor.Animations;
using System.Linq;
using SpheroidUniverse.SceneGraph;
using Animation = SpheroidUniverse.SceneGraph.Animation;
using PropertyType = SpheroidUniverse.SceneGraph.Animation.PropertyType;
using Scene = SpheroidUniverse.SceneGraph.Scene;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SpheroidUniverse.Generator;

namespace SpheroidUniverse.Exporter
{
    public sealed class SpheroidUniverseExporter : MonoBehaviour
    {
        private enum Axis
        {
            Undefined = 0,
            X = 1,
            Y = 2,
            Z = 3
        }

        private const string Tag = "Generated";
        private const string DirectoryPath = "Out";
        private static readonly string JsonFilePath = $"{DirectoryPath}/scenegraph.json";
        private static readonly string ScriptFilePath = $"{DirectoryPath}/Scene.spheroid";

        [MenuItem("Spheroid Universe/Export to JSON")]
        static void ExportToJson()
        {
            var modelsCounter = 0;
            var audiosCounter = 0;
            var nodesCounter = 0;

            AddTagIfNotExist();

            var scene = new Scene() { Nodes = new List<Node>() };
            var modelObjects = FindObjectsOfType(typeof(SpheroidModel)) as IList<SpheroidModel>;
            Node correctionNode = null;

            foreach (var modelObject in modelObjects)
            {
                modelsCounter++;

                var node = TranslateToObject(
                    currentObject: modelObject.gameObject,
                    previousNode: null,
                    nodesCounter: ref nodesCounter);

                if (node == null)
                    continue;

                if (correctionNode == null)
                    correctionNode = new Node() { Rotation = new SceneGraph.Vector3(0, 180, 0) };

                correctionNode.Nodes.Add(node);
            }

            if (correctionNode != null)
                scene.Nodes.Add(correctionNode);

            var audioObjects = FindObjectsOfType(typeof(SpheroidAudio)) as IList<SpheroidAudio>;

            foreach (var audio in audioObjects)
            {
                if (scene.Audios == null)
                    scene.Audios = new List<Audio>();

                scene.Audios.Add(new Audio() { Path = audio.soundPath });
                audiosCounter++;
            }

            var nodeObjects = FindObjectsOfType(typeof(SpheroidNode)) as IList<SpheroidNode>;

            foreach (var nodeObject in nodeObjects)
                nodeObject.Node = null;

            var generatedObjects = GameObject.FindGameObjectsWithTag(Tag);

            foreach (var generatedObject in generatedObjects)
                try
                {
                    generatedObject.tag = "Untagged";
                    DestroyImmediate(generatedObject.GetComponent<SpheroidNode>());
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                }

            var json = JsonConvert.SerializeObject(scene, Formatting.Indented, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Converters = new List<JsonConverter> {
                    new Newtonsoft.Json.Converters.StringEnumConverter()
                }
            });

            if (!Directory.Exists(DirectoryPath))
                Directory.CreateDirectory(DirectoryPath);

            if (File.Exists(JsonFilePath))
                File.Delete(JsonFilePath);

            File.WriteAllText(
                path: JsonFilePath,
                contents: json,
                encoding: Encoding.UTF8);

            Debug.Log($"Scene parsing has been completed!\n" +
                $"Models: {modelsCounter}\n" +
                $"Audios: {audiosCounter}\n" +
                $"Nodes: {nodesCounter} ");
        }

        [MenuItem("Spheroid Universe/Export to Spheroid Script")]
        static void ExportToSpheroidScript()
        {
            ExportToJson();

            var json = File.ReadAllText(JsonFilePath);
            var scene = JsonConvert.DeserializeObject<Scene>(json);
            var script = new SpheroidUniverseScriptGenerator().GenerateScript(scene);

            if (File.Exists(ScriptFilePath))
                File.Delete(ScriptFilePath);

            File.WriteAllText(
                path: ScriptFilePath,
                contents: script,
                encoding: Encoding.UTF8);

            Debug.Log($"Spheroid script has been generated!");
        }

        private static void AddTagIfNotExist()
        {
            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");

            if (asset != null)
            {
                var so = new SerializedObject(asset);
                var tags = so.FindProperty("tags");

                for (int i = 0; i < tags.arraySize; ++i)
                    if (tags.GetArrayElementAtIndex(i).stringValue == Tag)
                        return;

                tags.InsertArrayElementAtIndex(0);
                tags.GetArrayElementAtIndex(0).stringValue = Tag;
                so.ApplyModifiedProperties();
                so.Update();
            }
        }

        private static Node TranslateToObject(GameObject currentObject, SpheroidNode previousNode, ref int nodesCounter)
        {
            if (currentObject == null)
                throw new ArgumentNullException(nameof(currentObject));

            var type = NodeType.Undefined;

            if (!currentObject.TryGetComponent<SpheroidNode>(out var currentNode))
            {
                currentNode = currentObject.gameObject.AddComponent<SpheroidNode>();
                currentNode.tag = Tag;
                type = NodeType.Node;
                nodesCounter++;
            }

            else if (previousNode != null && currentNode.Node != null)
            {
                currentNode.Node.Nodes.Add(previousNode.Node);
                return null;
            }

            if (!currentNode.export)
                return null;

            var transform = currentNode.transform;
            var node = currentNode.Node;
            type = type != NodeType.Undefined ? type : currentNode is SpheroidModel ? NodeType.ModelNode : throw new InvalidOperationException();

            if (node != null)
            {
                if (previousNode != null)
                    node.Nodes.Add(previousNode.Node);

                return node;
            }

            node = new Node()
            {
                Type = type,
                Title = transform.gameObject.name,
                Position = transform.localPosition.ToVector3(),
                Scale = transform.localScale.ToVector3(),
                Rotation = transform.localRotation.eulerAngles.ToVector3(),
                Nodes = previousNode != null ? new List<Node>() { previousNode.Node } : null,
                SoundPath = currentNode.soundPath
            };

            currentNode.Node = node;

            if (type == NodeType.ModelNode)
            {
                var modelObject = (SpheroidModel)currentNode;

                if (string.IsNullOrEmpty(modelObject.modelPath))
                    throw new ArgumentOutOfRangeException($"Model ({currentNode.name}) MUST have model path.");

                if (!string.IsNullOrEmpty(modelObject.animationName))
                    node.AnimationName = modelObject.animationName;

                node.ModelPath = modelObject.modelPath;
                node.ViewDistance = modelObject.viewDistance;
            }

            var gameObject = transform.gameObject;

            if (TryGetAnimation(gameObject, out var animations, out var isLoopAnimation))
            {
                node.Animations = animations;
                node.LoopAnimation = isLoopAnimation;
            }

            var parent = transform.parent;

            if (parent == null)
                return node;

            return TranslateToObject(parent.gameObject, currentNode, ref nodesCounter);
        }

        private static bool TryGetAnimation(GameObject gameObject, out List<Animation> animations, out bool isLoopAnimation)
        {
            try
            {
                var clips = AnimationUtility.GetAnimationClips(gameObject);
                isLoopAnimation = false;

                if (clips.Length == 0)
                {
                    animations = null;
                    return false;
                }

                animations = new List<Animation>();
                var animator = gameObject.GetComponent<Animator>();
                var animatorController = animator.runtimeAnimatorController as AnimatorController;
                var childAnimatorStates = animatorController.layers.SelectMany(layer => layer.stateMachine.states);

                foreach (var childState in childAnimatorStates)
                {
                    var state = childState.state;
                    var clip = state.motion as AnimationClip;

                    if (clip == null)
                        continue;

                    var bindings = AnimationUtility.GetCurveBindings(clip);
                    var animation = new Animation() { Name = clip.name };
                    var animationProperties = new Dictionary<PropertyType, Animation.Property>();

                    if (!isLoopAnimation && clip.isLooping)
                        isLoopAnimation = clip.isLooping;

                    foreach (var binding in bindings)
                    {
                        var curve = AnimationUtility.GetEditorCurve(clip, binding);
                        ParsePropertyName(binding.propertyName, out var animationPropertyType, out var axis);

                        if (!animationProperties.TryGetValue(animationPropertyType, out var property))
                            animationProperties[animationPropertyType] = property = new Animation.Property() { Type = animationPropertyType, KeyFrames = new List<KeyFrame>() };

                        var timeKeyFrame = property.KeyFrames.ToDictionary(x => x.Time, x => x);

                        foreach (var key in curve.keys)
                        {
                            var time = key.time;
                            var value = key.value;

                            if (!timeKeyFrame.TryGetValue(time, out var keyFrame))
                            {
                                timeKeyFrame[time] = keyFrame = new KeyFrame() { Time = time };
                                property.KeyFrames.Add(keyFrame);
                            }

                            switch (axis)
                            {
                                case Axis.X:
                                    keyFrame.X = value;
                                    break;

                                case Axis.Y:
                                    keyFrame.Y = value;
                                    break;

                                case Axis.Z:
                                    keyFrame.Z = value;
                                    break;

                                default:
                                    throw new InvalidOperationException(nameof(axis));
                            }
                        }
                    }

                    animation.Properties = animationProperties.Values.ToList();
                    animations.Add(animation);
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError(ex.Message);
                animations = null;
                isLoopAnimation = false;
                return false;
            }
        }

        private static void ParsePropertyName(string propertyName, out PropertyType animationType, out Axis axis)
        {
            switch (propertyName)
            {
                case "m_LocalPosition.x":
                    animationType = PropertyType.Position;
                    axis = Axis.X;
                    break;

                case "m_LocalPosition.y":
                    animationType = PropertyType.Position;
                    axis = Axis.Y;
                    break;

                case "m_LocalPosition.z":
                    animationType = PropertyType.Position;
                    axis = Axis.Z;
                    break;

                case "m_LocalScale.x":
                    animationType = PropertyType.Scale;
                    axis = Axis.X;
                    break;

                case "m_LocalScale.y":
                    animationType = PropertyType.Scale;
                    axis = Axis.Y;
                    break;

                case "m_LocalScale.z":
                    animationType = PropertyType.Scale;
                    axis = Axis.Z;
                    break;

                case "localEulerAnglesRaw.x":
                    animationType = PropertyType.Rotation;
                    axis = Axis.X;
                    break;

                case "localEulerAnglesRaw.y":
                    animationType = PropertyType.Rotation;
                    axis = Axis.Y;
                    break;

                case "localEulerAnglesRaw.z":
                    animationType = PropertyType.Rotation;
                    axis = Axis.Z;
                    break;

                default:
                    throw new InvalidOperationException(nameof(propertyName));
            }
        }
    }

    public static class Helpers
    {
        public static SceneGraph.Vector3 ToVector3(this UnityEngine.Vector3 vector3) =>
           new SceneGraph.Vector3(vector3.x, vector3.y, vector3.z);
    }
}
