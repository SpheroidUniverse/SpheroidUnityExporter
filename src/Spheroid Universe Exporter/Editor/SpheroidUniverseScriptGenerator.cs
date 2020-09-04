using SpheroidUniverse.SceneGraph;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using static SpheroidUniverse.SceneGraph.Animation;

namespace SpheroidUniverse.Generator
{
    public static class Helpers
    {
        public static string GetFormatedString(this float value) => value.ToString("G", CultureInfo.InvariantCulture);
    }

    public class SpheroidUniverseScriptGenerator
    {
        #region Types

        private class Function
        {
            private readonly StringBuilder _builder = new StringBuilder();

            public string Name { get; }

            public Function(string name, string returnType = null)
            {
                returnType = !string.IsNullOrEmpty(returnType) ? $": {returnType}" : string.Empty;
                _builder.AppendLine($"fun {name}(){returnType} {{");
                Name = name;
            }

            public void AddLines(ScriptTypeBase line) => _builder.Append(line.GetBuilder());

            public void AddFunctionCall(Function function) => AppendLine($"{function.Name}()");

            public void Append(StringBuilder stringBuilder) => _builder.Append(stringBuilder);

            public void Append(string value) => _builder.Append($"\t{value}");

            public void AppendLine() => _builder.AppendLine();

            public void AppendLine(string value) => _builder.AppendLine($"\t{value}");

            public void EndFunction() => _builder.AppendLine("}");

            public void EndFunction(string returnValue)
            {
                AppendLine();
                AppendLine($"return {returnValue}");
                _builder.AppendLine("}");
            }

            public StringBuilder GetBuilder() => _builder;
        }

        private class ScriptTypeBase
        {
            public readonly StringBuilder Builder = new StringBuilder();

            public string Name { get; private set; }

            public string Prefix { get; set; }

            public ScriptTypeBase(string name, string prefix = "\t")
            {
                Name = name;
                Prefix = prefix;
            }

            protected void DeclareVal(string initializePart, bool lineBefore = false, bool lineAfter = false)
            {
                if (lineBefore)
                    Builder.AppendLine();

                Builder.AppendLine($"{Prefix}val {Name} = {initializePart}");

                if (lineAfter)
                    Builder.AppendLine();
            }

            public StringBuilder GetBuilder() => Builder;

            public void Clear() => Builder.Clear();
        }

        private class DynamicType : ScriptTypeBase
        {
            public DynamicType(string valName, string typeName, string prefix = "") : base(valName, prefix) => DeclareVal(typeName);

            public string AddToCollectionLine(string collectionPropertyName, string collectionObject) => $"{Name}.{collectionPropertyName}.add({collectionObject})";

            public string SetPropertyLine(string propertyName, string value) => $"{Name}.{propertyName} = {value}";
        }

        private class NodeScript : ScriptTypeBase
        {
            public NodeScript(string name, string prefix = "\t") : base(name, prefix) => Declare();

            protected virtual void Declare() => DeclareVal(initializePart: "Node()", lineBefore: true);

            public void AddChildren(string nodeName) => Builder.AppendLine($"{Prefix}{Name}.children.add({nodeName})");

            public void SetPosition(Vector3 position) => Builder.AppendLine($"{Prefix}{Name}.position = Vector3(x = {position.X.GetFormatedString()}, y = {position.Y.GetFormatedString()}, z = {position.Z.GetFormatedString()})");

            public void SetScale(Vector3 scale) => Builder.AppendLine($"{Prefix}{Name}.scale = Vector3(x = {scale.X.GetFormatedString()}, y = {scale.Y.GetFormatedString()}, z = {scale.Z.GetFormatedString()})");

            public void SetRotation(Vector3 rotation) => Builder.AppendLine($"{Prefix}{Name}.eulerAngles = Vector3(x = {rotation.X.GetFormatedString()}, y = {rotation.Y.GetFormatedString()}, z = {rotation.Z.GetFormatedString()})");

            public void Append(StringBuilder builder) => Builder.Append(builder);
        }

        private sealed class ModelNodeScript : NodeScript
        {
            public string AnimationName { get; private set; }

            public ModelNodeScript(string name, string animationName, string prefix = "\t") : base(name, prefix)
            {
                AnimationName = animationName;
            }

            protected override void Declare() => DeclareVal(initializePart: "ModelNode()", lineBefore: true);
        }

        private class SceneObject : ScriptTypeBase
        {
            public SceneObject(string name, string prefix = "\t") : base(name, prefix) => Declare();

            protected virtual void Declare() => DeclareVal(initializePart: "SceneObjectData()", lineBefore: true);

            public void SetNode(string nodeName) => Builder.AppendLine($"{Prefix}{Name}.node = {nodeName}");

            public void SetViewDistance(int viewDistance) => Builder.AppendLine($"{Prefix}{Name}.viewDistance = {viewDistance}");

            public void SetModel(string modelName) => Builder.AppendLine($"{Prefix}{Name}.model = {modelName}");

            public void SetAnimation(string animationName) => Builder.AppendLine($"{Prefix}{Name}.animationName = \"{animationName}\"");

            public void SetParent(string parentName) => Builder.AppendLine($"{Prefix}{Name}.parent = {parentName}");

            public void SetSceneAudio(string sceneAudioName) => Builder.AppendLine($"{Prefix}{Name}.sceneAudio = {sceneAudioName}");
        }

        private sealed class Model : ScriptTypeBase
        {
            private readonly List<ModelNodeScript> _modelNodes = new List<ModelNodeScript>();
            private readonly List<Animator> _animators = new List<Animator>();
            private Model _innerModel;

            public string SourcePath { get; private set; }

            public Model(string sourcePath, string name, string prefix = "\t") : base(name, prefix)
            {
                SourcePath = sourcePath;
            }

            public void AddModelNodeToLoad(ModelNodeScript modelNode) => _modelNodes.Add(modelNode);

            public void AddAnimatorsToPlay(List<Animator> animators) => _animators.AddRange(animators);

            public void SetInnerModelToLoad(Model model)
            {
                if (_innerModel == null)
                    _innerModel = model;
                else
                    _innerModel.SetInnerModelToLoad(model);
            }

            public string ModelConstructor => $"{Prefix}Model(source = Source(\"{SourcePath}\"))";

            public StringBuilder Build()
            {
                if (_modelNodes.Count == 0)
                    throw new InvalidOperationException(nameof(_modelNodes));

                Builder.AppendLine();
                Builder.AppendLine($"{ModelConstructor}.load {{ success, error->");
                Builder.AppendLine($"{Prefix}\tif (success) {{");

                foreach (var modelNode in _modelNodes)
                {
                    Builder.AppendLine($"{Prefix}\t\t{modelNode.Name}.model = this");

                    if (!string.IsNullOrEmpty(modelNode.AnimationName))
                        Builder.AppendLine($"{Prefix}\t\t{modelNode.Name}.playAnimation(\"{modelNode.AnimationName}\", loop = true)");
                }

                Builder.AppendLine($"{Prefix}\t\tprintln(\"Model '{SourcePath}' has been loaded\")");
                Builder.AppendLine($"{Prefix}\t}} else");
                Builder.AppendLine($"{Prefix}\t\tprintln(\"Failed to load model '{SourcePath}'. Cause: $error\")");

                if (_animators.Count > 0)
                {
                    Builder.AppendLine();

                    foreach (var animator in _animators)
                        Builder.AppendLine($"{Prefix}{animator.GetPlayLine()}");
                }

                if (_innerModel != null)
                {
                    _innerModel.Prefix += Prefix;
                    Builder.Append(_innerModel.Build());
                }

                return Builder.AppendLine($"{Prefix}}}");
            }
        }

        private sealed class SceneAudioScript : ScriptTypeBase
        {
            private readonly List<NodeScript> _nodes = new List<NodeScript>();
            private SceneAudioScript _innerSceneAudio;

            public string SourcePath { get; private set; }

            public SceneAudioScript(string sourcePath, string name, string prefix = "\t") : base(name, prefix)
            {
                SourcePath = sourcePath;
            }

            public void AddNodeToLoad(NodeScript modelNode) => _nodes.Add(modelNode);

            public void SetInnerModelToLoad(SceneAudioScript sceneAudio)
            {
                if (_innerSceneAudio == null)
                    _innerSceneAudio = sceneAudio;
                else
                    _innerSceneAudio.SetInnerModelToLoad(sceneAudio);
            }

            public string SceneAudioConstructor => $"{Prefix}SceneAudio(source = Source(\"{SourcePath}\"))";

            public StringBuilder Build()
            {
                if (_nodes.Count == 0)
                    throw new InvalidOperationException(nameof(_nodes));

                Builder.AppendLine();
                Builder.AppendLine($"{SceneAudioConstructor}.load {{ success, error->");
                Builder.AppendLine($"{Prefix}\tif (success) {{");

                foreach (var node in _nodes)
                    Builder.AppendLine($"{Prefix}\t\t{node.Name}.playAudio(this, loop = true)");

                Builder.AppendLine($"{Prefix}\t\tprintln(\"SceneAudio '{SourcePath}' has been loaded\")");
                Builder.AppendLine($"{Prefix}\t}} else");
                Builder.AppendLine($"{Prefix}\t\tprintln(\"Failed to load SceneAudio '{SourcePath}'. Cause: $error\")");

                if (_innerSceneAudio != null)
                {
                    _innerSceneAudio.Prefix += Prefix;
                    Builder.Append(_innerSceneAudio.Build());
                }

                return Builder.AppendLine($"{Prefix}}}");
            }
        }

        private sealed class AudioScript : ScriptTypeBase
        {
            public string SourcePath { get; private set; }

            public AudioScript(string sourcePath, string name, string prefix = "\t") : base(name, prefix)
            {
                SourcePath = sourcePath;
            }

            public StringBuilder Build()
            {
                Builder.AppendLine();
                Builder.AppendLine($"{Prefix}Audio(source = Source(\"{SourcePath}\")).load {{ success, error->");
                Builder.AppendLine($"{Prefix}\tif (success) {{");
                Builder.AppendLine($"{Prefix}\t\t{Name} = this");
                Builder.AppendLine($"{Prefix}\t\tthis.play(loop = true)");
                Builder.AppendLine($"{Prefix}\t\tprintln(\"Audio '{SourcePath}' has been loaded\")");
                Builder.AppendLine($"{Prefix}\t}} else");
                Builder.AppendLine($"{Prefix}\t\tprintln(\"Failed to load Audio '{SourcePath}'. Cause: $error\")");
                return Builder.AppendLine($"{Prefix}}}");
            }
        }

        private sealed class Animator : ScriptTypeBase
        {
            private readonly string _name;
            private readonly string _nodeName;
            private readonly bool _loop;
            private readonly AnimationCollection _collection;

            public Animator(string name, string nodeName, bool loop, AnimationCollection animationCollection, string prefix = "\t") : base(name, prefix)
            {
                _name = name;
                _nodeName = nodeName;
                _loop = loop;
                _collection = animationCollection;
            }

            public string GetPlayLine() => $"{Prefix}{_name}.play(loop = {_loop.ToString().ToLower()})";

            public void Build()
            {
                DeclareVal($"{_nodeName}.getAnimationController(");
                _collection.Prefix += Prefix;
                _collection.Build();
                Builder.Append(_collection.GetBuilder());
                Builder.AppendLine($"\n{Prefix})");
            }
        }

        private enum CollectionType
        {
            Undefined = 0,
            Sequence = 1,
            Group = 2
        }

        private sealed class AnimationCollection : AnimationBase
        {
            private readonly List<AnimationBase> _animations = new List<AnimationBase>();
            private readonly CollectionType _collectionType;

            public AnimationCollection(string name, CollectionType collectionType, string prefix = "\t") : base(name, prefix) => _collectionType = collectionType;

            public void AddAnimation(AnimationBase animation) => _animations.Add(animation);

            public override void Build()
            {
                Builder.AppendLine($"{Prefix}Animation{_collectionType}(listOf(");

                for (int i = 0; i < _animations.Count; i++)
                {
                    var animation = _animations[i];
                    animation.Prefix += Prefix;
                    animation.Build();
                    Builder.Append(animation.GetBuilder());

                    var isLast = i == _animations.Count - 1;

                    if (!isLast)
                        Builder.Append(", ");

                    Builder.AppendLine();
                }

                Builder.Append($"{Prefix}))");
            }
        }

        private class Animation : AnimationBase
        {
            private readonly float _duration;
            private readonly KeyFrame _keyFrame;
            private readonly PropertyType _animatedProperty;

            public Animation(string name, PropertyType animatedProperty, float duration, KeyFrame keyFrame, string prefix = "\t") : base(name, prefix)
            {
                _duration = duration;
                _keyFrame = keyFrame;
                _animatedProperty = animatedProperty;
            }

            public override void Build()
            {
                Builder.Append($"{Prefix}{_animatedProperty}Animation(" +
                       $"to = Vector3(x = {(_keyFrame.X ?? 0).GetFormatedString()}, y = {(_keyFrame.Y ?? 0).GetFormatedString()}, z = {(_keyFrame.Z ?? 0).GetFormatedString()}), " +
                       $"duration = TimeInterval(seconds = {_duration.GetFormatedString()}), " +
                       $"interpolation = \"linear\")");
            }
        }

        private abstract class AnimationBase : ScriptTypeBase
        {
            public AnimationBase(string name, string prefix = "\t") : base(name, prefix) { }

            public abstract void Build();
        }

        #endregion

        #region Generator

        private static readonly StringBuilder _fileBuilder = new StringBuilder();
        private readonly List<SceneObject> _objects = new List<SceneObject>();
        private readonly Dictionary<string, Model> _models = new Dictionary<string, Model>();
        private readonly Dictionary<string, SceneAudioScript> _sceneAudios = new Dictionary<string, SceneAudioScript>();
        private readonly Dictionary<Function, List<Animator>> _animators = new Dictionary<Function, List<Animator>>();
        private Counter _counter = new Counter() { AnimationProperties = new Dictionary<PropertyType, int>() };

        private struct Counter
        {
            public int Node { get; set; }
            public int ModelNode { get; set; }
            public int Model { get; set; }
            public int SceneAudio { get; set; }
            public int SceneObject { get; set; }
            public Dictionary<PropertyType, int> AnimationProperties { get; set; }
            public int SequenceAnimation { get; set; }
            public int GroupAnimation { get; set; }
            public int Animator { get; set; }
        }

        private Function GenerateCreateSceneFunction(Scene scene)
        {
            var function = new Function("createScene");
            var sceneData = new DynamicType("sceneData", "SceneData()", prefix: "\t");
            function.AddLines(sceneData);

            var audios = scene.Audios;

            if (audios != null)
                for (var i = 0; i < audios.Count; i++)
                {
                    var audioName = $"audio{i}";
                    _fileBuilder.AppendLine($"var {audioName}");
                    var audio = new AudioScript(audios[i].Path, audioName);
                    function.Append(audio.Build());
                }

            var nodes = scene.Nodes;
            var scriptNodes = new List<NodeScript>();

            foreach (var node in nodes)
            {
                var scriptNode = GetScriptNode(function, node, sceneData.Name);
                Debug.Assert(scriptNode != null);
                scriptNodes.Add(scriptNode);
            }

            foreach (var model in _models.Values)
            {
                model.Prefix = string.Empty;
                function.AppendLine($"val {model.Name} = {model.ModelConstructor}");
            }

            foreach (var sceneAudio in _sceneAudios.Values)
            {
                sceneAudio.Prefix = string.Empty;
                function.AppendLine($"val {sceneAudio.Name} = {sceneAudio.SceneAudioConstructor}");
            }

            foreach (var scriptNode in scriptNodes)
            {
                function.AddLines(scriptNode);
                function.AppendLine(sceneData.AddToCollectionLine("sceneNode.children", scriptNode.Name));
            }

            if (_animators.TryGetValue(function, out var animators))
                foreach (var animator in animators)
                {
                    animator.Build();
                    function.AppendLine();
                    function.AddLines(animator);
                    function.AppendLine(sceneData.AddToCollectionLine("animations", animator.Name));
                }

            foreach (var sceneObject in _objects)
            {
                function.AddLines(sceneObject);
                function.AppendLine(sceneData.AddToCollectionLine("objects", sceneObject.Name));
            }

            function.EndFunction(sceneData.Name);
            return function;
        }

        public string GenerateScript(Scene scene)
        {
            var createSceneFunction = GenerateCreateSceneFunction(scene);
            _fileBuilder.Append(createSceneFunction.GetBuilder());

            var result = _fileBuilder.ToString();
            ClearAll();
            return result;
        }

        private void ClearAll()
        {
            _fileBuilder.Clear();
            _objects.Clear();
            _models.Clear();
            _sceneAudios.Clear();
            _animators.Clear();
            _counter.AnimationProperties.Clear();
            _counter.Animator = 0;
            _counter.GroupAnimation = 0;
            _counter.Model = 0;
            _counter.ModelNode = 0;
            _counter.SceneAudio = 0;
            _counter.SceneObject = 0;
            _counter.SequenceAnimation = 0;
        }

        private NodeScript GetScriptNode(Function function, Node node, string parent)
        {
            NodeScript nodeScript;

            switch (node.Type)
            {
                case NodeType.Node:
                    {
                        nodeScript = new NodeScript($"node{++_counter.Node}");
                        nodeScript.SetPosition(node.Position);
                        nodeScript.SetRotation(node.Rotation);
                        nodeScript.SetScale(node.Scale);

                        if (node.Nodes != null)
                            foreach (var child in node.Nodes)
                            {
                                var parsedNode = GetScriptNode(function, child, nodeScript.Name);
                                nodeScript.Append(parsedNode.GetBuilder());
                                nodeScript.AddChildren(parsedNode.Name);
                            }

                        break;
                    }
                case NodeType.ModelNode:
                    {
                        var animationName = node.AnimationName;
                        var modelNodeScript = new ModelNodeScript($"modelNode{++_counter.ModelNode}", animationName);
                        modelNodeScript.SetPosition(node.Position);
                        modelNodeScript.SetRotation(node.Rotation);
                        modelNodeScript.SetScale(node.Scale);

                        if (node.Nodes != null)
                            foreach (var child in node.Nodes)
                            {
                                var parsedNode = GetScriptNode(function, child, modelNodeScript.Name);
                                parsedNode.Append(parsedNode.GetBuilder());
                                modelNodeScript.AddChildren(parsedNode.Name);
                            }

                        var path = node.ModelPath;

                        if (!_models.TryGetValue(path, out var model))
                            _models[path] = model = new Model(path, $"model{++_counter.Model}");

                        var sceneObject = new SceneObject($"object{++_counter.SceneObject}");
                        sceneObject.SetNode(modelNodeScript.Name);
                        sceneObject.SetViewDistance(node.ViewDistance);
                        sceneObject.SetModel(model.Name);
                        sceneObject.SetParent(parent);

                        var soundPath = node.SoundPath;

                        if (!string.IsNullOrEmpty(soundPath))
                        {
                            if (!_sceneAudios.TryGetValue(soundPath, out var sceneAudio))
                                _sceneAudios[soundPath] = sceneAudio = new SceneAudioScript(soundPath, $"sceneAudio{++_counter.SceneAudio}");

                            sceneAudio.AddNodeToLoad(modelNodeScript);
                            sceneObject.SetSceneAudio(sceneAudio.Name);
                        }

                        if (!string.IsNullOrEmpty(animationName))
                            sceneObject.SetAnimation(animationName);

                        _objects.Add(sceneObject);
                        model.AddModelNodeToLoad(modelNodeScript);
                        nodeScript = modelNodeScript;
                        break;
                    }
                default:
                    throw new InvalidOperationException(nameof(node.Type));
            }

            if (TryGetAnimator(node, nodeScript.Name, out var animator))
                if (!_animators.TryGetValue(function, out var animators))
                    _animators.Add(function, new List<Animator>() { animator });
                else
                    animators.Add(animator);

            return nodeScript;
        }

        private bool TryGetAnimator(Node node, string nodeName, out Animator animator)
        {
            if (node.Animations == null)
            {
                animator = null;
                return false;
            }

            var lastTime = 0f;
            var animatorSequence = new AnimationCollection($"sequence{++_counter.SequenceAnimation}", CollectionType.Sequence);

            foreach (var animation in node.Animations)
            {
                var properties = animation.Properties.ToDictionary(x => x.Type);
                var animationGroup = new AnimationCollection($"groupAnimation{++_counter.GroupAnimation}", CollectionType.Group);

                foreach (var property in properties)
                {
                    var animationProperty = property.Key;
                    var keyFrames = property.Value.KeyFrames.OrderBy(x => x.Time).ToList();
                    var sequence = new AnimationCollection($"sequence{++_counter.SequenceAnimation}", CollectionType.Sequence);

                    if (!_counter.AnimationProperties.TryGetValue(animationProperty, out var value))
                        _counter.AnimationProperties[animationProperty] = value;

                    foreach (var keyFrame in keyFrames)
                    {
                        var duration = keyFrame.Time - lastTime;

                        sequence.AddAnimation(new Animation(
                            $"{animationProperty.ToString().ToLower()}Animation{++value}",
                            animationProperty,
                            duration,
                            keyFrame));

                        lastTime = keyFrame.Time;
                    }

                    animationGroup.AddAnimation(sequence);
                    lastTime = 0;
                }

                animatorSequence.AddAnimation(animationGroup);
            }

            animator = new Animator($"animator{++_counter.Animator}", nodeName, node.LoopAnimation, animatorSequence);
            return true;
        }

        #endregion
    }
}
