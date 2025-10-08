using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace DialogueSystem.Editor
{
    public class DialogueGraphView : GraphView
    {
        private DialogueTreeEditor editorWindow;
        private int nextNodeIndex = 0;

        public DialogueGraphView()
        {
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new ContextualMenuManipulator(BuildContextualMenu));

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            serializeGraphElements = SerializeGraphElementsImplementation;
            canPasteSerializedData = data => !string.IsNullOrEmpty(data);
            unserializeAndPaste = UnserializeAndPasteImplementation;

            focusable = true;
            RegisterCallback<KeyDownEvent>(OnKeyDown);
            RegisterCallback<MouseDownEvent>(evt => Focus());
        }

        public void SetEditorWindow(DialogueTreeEditor window) => editorWindow = window;
        public int GetNodeCount() => nodes.ToList().Count;

        public void ClearGraph()
        {
            DeleteElements(graphElements.ToList());
            nextNodeIndex = 0;
        }

        public void RefreshAllNodesConditions()
        {
            foreach (var node in nodes.Cast<DialogueNode>())
                node.RefreshConditionsUI();
        }

        public void CenterOnNode0()
        {
            var node0 = nodes.Cast<DialogueNode>().FirstOrDefault(n => n.NodeIndex == 0);
            if (node0 == null) return;

            var nodePos = node0.GetPosition();
            if (float.IsNaN(nodePos.x) || nodePos.size == Vector2.zero)
            {
                EditorApplication.delayCall += CenterOnNode0;
                return;
            }

            var viewportCenter = new Vector2(worldBound.width * 0.5f, worldBound.height * 0.5f);
            var zoom = contentViewContainer.transform.scale.x;
            var targetPos = -nodePos.center * zoom + viewportCenter;
            UpdateViewTransform(targetPos, new Vector3(zoom, zoom, 1f));
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.ctrlKey && evt.keyCode == KeyCode.S)
            {
                editorWindow?.SaveDialogueTree();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Delete)
            {
                DeleteSelectedElements();
                editorWindow?.MarkAsChanged();
                evt.StopPropagation();
            }
            else if (evt.ctrlKey && evt.keyCode == KeyCode.D)
            {
                DuplicateSelectedNodes();
                editorWindow?.MarkAsChanged();
                evt.StopPropagation();
            }
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction("Create Dialogue Node", action =>
            {
                CreateDialogueNode("Character", null, "New Dialogue",
                    contentViewContainer.WorldToLocal(action.eventInfo.localMousePosition));
                editorWindow?.MarkAsChanged();
            });

            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Save Current", _ => editorWindow?.SaveDialogueTree());
            evt.menu.AppendAction("Save As...", _ => editorWindow?.SaveAsDialogueTree());
            evt.menu.AppendAction("Load", _ => editorWindow?.LoadDialogueTree());
            evt.menu.AppendSeparator();

            evt.menu.AppendAction("Create New", _ =>
            {
                if (editorWindow?.HasUnsavedChanges == true &&
                    !EditorUtility.DisplayDialog("New Document", "Discard changes?", "Yes", "Cancel"))
                    return;
                editorWindow?.NewDialogueTree();
            });

            var selectedNodes = selection.OfType<DialogueNode>().ToList();
            if (selectedNodes.Count > 0)
            {
                evt.menu.AppendSeparator();
                evt.menu.AppendAction("Duplicate Selected", _ =>
                {
                    DuplicateSelectedNodes();
                    editorWindow?.MarkAsChanged();
                });
                evt.menu.AppendAction("Delete Selected", _ =>
                {
                    DeleteSelectedElements();
                    editorWindow?.MarkAsChanged();
                });
            }
        }

        public DialogueNode CreateDialogueNode(string name, Sprite avatar, string content, Vector2 position)
        {
            var node = new DialogueNode(name, avatar, content, nextNodeIndex++, editorWindow);
            node.SetPosition(new Rect(position, Vector2.zero));
            node.OnNodeChanged += () => editorWindow?.MarkAsChanged();
            AddElement(node);
            return node;
        }

        public void DeleteSelectedElements()
        {
            var elements = selection.OfType<GraphElement>().ToList();
            DeleteElements(elements);
            if (elements.OfType<DialogueNode>().Any())
                ReorganizeNodeIndices();
        }

        private void ReorganizeNodeIndices()
        {
            var allNodes = nodes.Cast<DialogueNode>().OrderBy(n => n.NodeIndex).ToList();
            for (int i = 0; i < allNodes.Count; i++)
                allNodes[i].SetNodeIndex(i);
            nextNodeIndex = allNodes.Count;
        }

        public List<RuntimeDialogueData> GetDialogueSequence()
        {
            var exportDict = new Dictionary<string, RuntimeDialogueData>();
            var nodeList = nodes.Cast<DialogueNode>().ToList();

            foreach (var node in nodeList)
            {
                exportDict[node.GetId()] = new RuntimeDialogueData
                {
                    index = node.NodeIndex,
                    name = node.CharacterName,
                    avatarAddr = ConvertSpriteToRuntimePath(node.AvatarSprite),
                    content = node.DialogueText,
                    choices = new List<RuntimeChoice>(),
                    eventCalls = new List<DialogueEventCall>(node.EventCalls)
                };
            }

            foreach (var edge in edges.ToList())
            {
                if (!(edge.output.node is DialogueNode outNode && edge.input.node is DialogueNode inNode))
                    continue;

                var exportData = exportDict[outNode.GetId()];
                int choiceIndex = outNode.GetChoiceIndexForPort(edge.output);

                if (choiceIndex == -1)
                {
                    exportData.nextNodeId = inNode.GetId();
                }
                else if (choiceIndex >= 0 && choiceIndex < outNode.ChoicesData.Count)
                {
                    var choiceData = outNode.ChoicesData[choiceIndex];
                    exportData.choices.Add(new RuntimeChoice
                    {
                        text = choiceData.text,
                        nextNodeId = inNode.GetId(),
                        conditions = new List<ChoiceCondition>(choiceData.conditions),
                        conditionLogic = choiceData.conditionLogic
                    });
                }
            }

            return exportDict.Values.OrderBy(d => d.index).ToList();
        }

        private string ConvertSpriteToRuntimePath(Sprite sprite)
        {
            if (sprite == null) return "";
            string path = AssetDatabase.GetAssetPath(sprite);
            int resIndex = path.IndexOf("Resources/");
            return resIndex >= 0 ? Path.ChangeExtension(path.Substring(resIndex + 10), null) : sprite.name;
        }

        public DialogueTreeData SerializeDialogueTree()
        {
            var data = new DialogueTreeData();

            foreach (var node in nodes.Cast<DialogueNode>())
            {
                data.nodes.Add(new DialogueNodeData
                {
                    id = node.GetId(),
                    index = node.NodeIndex,
                    name = node.CharacterName ?? "",
                    avatarAssetPath = node.AvatarSprite ? AssetDatabase.GetAssetPath(node.AvatarSprite) : "",
                    content = node.DialogueText ?? "",
                    positionX = node.GetPosition().x,
                    positionY = node.GetPosition().y,
                    choices = new List<ChoiceData>(node.ChoicesData),
                    eventCalls = new List<DialogueEventCall>(node.EventCalls)
                });
            }

            foreach (var edge in edges.ToList())
            {
                if (edge.output.node is DialogueNode outNode && edge.input.node is DialogueNode inNode)
                {
                    int choiceIndex = outNode.GetChoiceIndexForPort(edge.output);
                    data.connections.Add(new DialogueConnectionData
                    {
                        outputNodeId = outNode.GetId(),
                        inputNodeId = inNode.GetId(),
                        choiceIndex = choiceIndex,
                        choiceText = choiceIndex >= 0 && choiceIndex < outNode.ChoicesData.Count
                            ? outNode.ChoicesData[choiceIndex].text : ""
                    });
                }
            }

            return data;
        }

        public void LoadDialogueTree(DialogueTreeData data)
        {
            DeleteElements(graphElements.ToList());

            var nodeDict = new Dictionary<string, DialogueNode>();
            foreach (var nodeData in data.nodes.OrderBy(n => n.index))
            {
                var avatar = !string.IsNullOrEmpty(nodeData.avatarAssetPath)
                    ? AssetDatabase.LoadAssetAtPath<Sprite>(nodeData.avatarAssetPath) : null;

                var node = CreateDialogueNodeWithIndex(nodeData.name, avatar, nodeData.content,
                    new Vector2(nodeData.positionX, nodeData.positionY), nodeData.index);
                node.SetId(nodeData.id);
                node.SetChoicesData(nodeData.choices);
                node.SetEventCalls(nodeData.eventCalls);
                nodeDict[nodeData.id] = node;
            }

            nextNodeIndex = data.nodes.Count > 0 ? data.nodes.Max(n => n.index) + 1 : 0;

            foreach (var conn in data.connections)
            {
                if (nodeDict.TryGetValue(conn.outputNodeId, out var outNode) &&
                    nodeDict.TryGetValue(conn.inputNodeId, out var inNode))
                {
                    var outPort = conn.choiceIndex == -1
                        ? outNode.GetDefaultOutputPort()
                        : outNode.GetOutputPortByIndex(conn.choiceIndex);
                    var inPort = inNode.GetInputPort();

                    if (outPort != null && inPort != null)
                        AddElement(outPort.ConnectTo(inPort));
                }
            }
        }

        private DialogueNode CreateDialogueNodeWithIndex(string name, Sprite avatar, string content, Vector2 position, int index)
        {
            var node = new DialogueNode(name, avatar, content, index, editorWindow);
            node.SetPosition(new Rect(position, Vector2.zero));
            node.OnNodeChanged += () => editorWindow?.MarkAsChanged();
            AddElement(node);
            return node;
        }

        private string SerializeGraphElementsImplementation(IEnumerable<GraphElement> elements)
        {
            var nodes = elements.OfType<DialogueNode>().ToList();
            if (nodes.Count == 0) return string.Empty;

            return string.Join(";", nodes.Select(n =>
            {
                var pos = n.GetPosition();
                var avatarPath = n.AvatarSprite ? AssetDatabase.GetAssetPath(n.AvatarSprite) : "";
                return $"{n.CharacterName}|{avatarPath}|{n.DialogueText}|{pos.x}|{pos.y}|" +
                       $"{JsonUtility.ToJson(new SerializableChoiceDataList { choicesData = n.ChoicesData })}|" +
                       $"{JsonUtility.ToJson(new SerializableEventCallList { eventCalls = n.EventCalls })}";
            }));
        }

        private void UnserializeAndPasteImplementation(string op, string data)
        {
            if (string.IsNullOrEmpty(data)) return;

            var offset = new Vector2(30, 30);
            foreach (var nodeStr in data.Split(';'))
            {
                var parts = nodeStr.Split('|');
                if (parts.Length < 5) continue;

                var avatar = !string.IsNullOrEmpty(parts[1])
                    ? AssetDatabase.LoadAssetAtPath<Sprite>(parts[1]) : null;
                var x = float.Parse(parts[3]) + offset.x;
                var y = float.Parse(parts[4]) + offset.y;

                var node = CreateDialogueNode(parts[0], avatar, parts[2], new Vector2(x, y));

                if (parts.Length > 5)
                {
                    try
                    {
                        var choices = JsonUtility.FromJson<SerializableChoiceDataList>(parts[5]);
                        node.SetChoicesData(choices.choicesData);
                    }
                    catch { }
                }

                if (parts.Length > 6)
                {
                    try
                    {
                        var events = JsonUtility.FromJson<SerializableEventCallList>(parts[6]);
                        node.SetEventCalls(events.eventCalls);
                    }
                    catch { }
                }
            }
        }

        public void DuplicateSelectedNodes()
        {
            var selected = selection.OfType<DialogueNode>().ToList();
            if (selected.Count == 0) return;
            UnserializeAndPasteImplementation("Duplicate", SerializeGraphElementsImplementation(selected));
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter adapter)
        {
            return ports.ToList().Where(p =>
                p.direction != startPort.direction &&
                p.node != startPort.node &&
                p.portType == startPort.portType).ToList();
        }
    }
}