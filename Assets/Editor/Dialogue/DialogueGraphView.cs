using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.Experimental.GraphView;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using DialogueSystem;

/// <summary>
/// 对话图形视图 - 管理节点图的显示和交互
/// </summary>
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

        var grid = new GridBackground();
        Insert(0, grid);
        grid.StretchToParentSize();

        this.AddManipulator(new ContextualMenuManipulator(BuildContextualMenu));

        graphViewChanged += OnGraphViewChangedInternal;
    }

    private GraphViewChange OnGraphViewChangedInternal(GraphViewChange change)
    {
        // 处理节点删除 - 同时删除相关的连线
        if (change.elementsToRemove != null)
        {
            var edgesToRemove = new List<Edge>();

            foreach (var element in change.elementsToRemove)
            {
                if (element is DialogueNode node)
                {
                    // 找到所有连接到此节点的边
                    var connectedEdges = edges.ToList().Where(edge =>
                        edge.input?.node == node || edge.output?.node == node).ToList();

                    edgesToRemove.AddRange(connectedEdges);
                }
            }

            // 删除所有相关的边
            foreach (var edge in edgesToRemove)
            {
                if (edge != null && !change.elementsToRemove.Contains(edge))
                {
                    RemoveElement(edge);
                }
            }
        }

        // 处理创建连线
        if (change.edgesToCreate != null)
        {
            var edgesToReplace = new List<Edge>(change.edgesToCreate);
            change.edgesToCreate.Clear();

            foreach (var edge in edgesToReplace)
            {
                var outputNode = edge.output?.node as DialogueNode;
                if (outputNode == null)
                {
                    change.edgesToCreate.Add(edge);
                    continue;
                }

                int branchPriority = outputNode.GetBranchPriorityForPort(edge.output);

                if (branchPriority >= 0 && outputNode.IsConditionalMode())
                {
                    var conditionalEdge = new ConditionalEdge(this, editorWindow);
                    conditionalEdge.input = edge.input;
                    conditionalEdge.output = edge.output;
                    conditionalEdge.branchPriority = branchPriority;

                    var branchData = outputNode.GetConditionalBranchData(branchPriority);
                    if (branchData != null)
                    {
                        conditionalEdge.conditions = new List<ChoiceCondition>(branchData.conditions);
                        conditionalEdge.conditionLogic = branchData.conditionLogic;
                    }

                    // 手动连接端口以更新视觉状态
                    edge.input?.Connect(conditionalEdge);
                    edge.output?.Connect(conditionalEdge);

                    AddElement(conditionalEdge);
                }
                else
                {
                    change.edgesToCreate.Add(edge);
                }
            }
        }

        return change;
    }

    public void SetEditorWindow(DialogueTreeEditor window)
    {
        editorWindow = window;
    }

    public int GetNodeCount()
    {
        return nodes.ToList().Count;
    }

    public void ClearGraph()
    {
        DeleteElements(graphElements.ToList());
        nextNodeIndex = 0;
    }

    public void CenterOnNode0()
    {
        var node0 = nodes.Cast<DialogueNode>().FirstOrDefault(n => n.NodeIndex == 0);
        if (node0 == null)
        {
            Debug.LogWarning("Node 0 not found for centering");
            return;
        }

        var nodePosition = node0.GetPosition();
        if (float.IsNaN(nodePosition.x) || float.IsNaN(nodePosition.y) ||
            float.IsNaN(nodePosition.width) || float.IsNaN(nodePosition.height))
        {
            Debug.Log("Node 0 position contains NaN values, retrying...");
            EditorApplication.delayCall += () => CenterOnNode0();
            return;
        }

        var layoutBounds = node0.layout;
        if (nodePosition.size == Vector2.zero && layoutBounds.size != Vector2.zero)
        {
            nodePosition = layoutBounds;
        }

        var nodeBounds = new Rect(nodePosition.position, nodePosition.size);
        if (nodeBounds.size == Vector2.zero)
        {
            Debug.Log("Node 0 not fully initialized, retrying...");
            EditorApplication.delayCall += () => CenterOnNode0();
            return;
        }

        var graphViewBounds = worldBound;
        if (graphViewBounds.width <= 0 || graphViewBounds.height <= 0)
        {
            Debug.Log("GraphView bounds not ready, retrying...");
            EditorApplication.delayCall += () => CenterOnNode0();
            return;
        }

        var nodeCenter = nodeBounds.center;
        if (float.IsNaN(nodeCenter.x) || float.IsNaN(nodeCenter.y))
        {
            Debug.LogError("Node center calculation resulted in NaN");
            nodeCenter = Vector2.zero;
        }

        var viewportCenter = new Vector2(graphViewBounds.width * 0.5f, graphViewBounds.height * 0.5f);
        var currentZoom = contentViewContainer.transform.scale.x;
        if (float.IsNaN(currentZoom) || currentZoom <= 0)
        {
            currentZoom = 1f;
        }

        var targetPosition = -nodeCenter * currentZoom + viewportCenter;
        if (float.IsNaN(targetPosition.x) || float.IsNaN(targetPosition.y))
        {
            Debug.LogError("Target position calculation resulted in NaN");
            return;
        }

        UpdateViewTransform(targetPosition, new Vector3(currentZoom, currentZoom, 1f));
        Debug.Log($"Successfully centered on Node 0");
    }

    public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
    {
        evt.menu.AppendAction("Create Dialogue Node",
            action => {
                CreateDialogueNode("Character", null, "New Dialogue",
                    GetLocalMousePosition(action.eventInfo.localMousePosition));
                if (editorWindow != null) editorWindow.MarkAsChanged();
            });

        evt.menu.AppendSeparator();
        evt.menu.AppendAction("Save Current", action => editorWindow?.SaveDialogueTree());
        evt.menu.AppendAction("Save As...", action => editorWindow?.SaveAsDialogueTree());
        evt.menu.AppendAction("Load", action => editorWindow?.LoadDialogueTree());

        evt.menu.AppendSeparator();
        evt.menu.AppendAction("Create New", action => {
            if (editorWindow != null)
            {
                if (editorWindow.HasUnsavedChanges)
                {
                    if (!EditorUtility.DisplayDialog("New Document",
                        "You have unsaved changes. Create new document without saving?",
                        "Yes", "Cancel"))
                    {
                        return;
                    }
                }
                editorWindow.NewDialogueTree();
            }
        });

        var selectedNodes = selection.OfType<DialogueNode>().ToList();
        if (selectedNodes.Count > 0)
        {
            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Delete Selected", action => {
                DeleteSelection();
                if (editorWindow != null) editorWindow.MarkAsChanged();
            });
        }
    }

    public override EventPropagation DeleteSelection()
    {
        // 获取要删除的节点
        var nodesToDelete = selection.OfType<DialogueNode>().ToList();

        // 先删除所有相关的连线
        foreach (var node in nodesToDelete)
        {
            var connectedEdges = edges.ToList().Where(edge =>
                edge.input?.node == node || edge.output?.node == node).ToList();

            foreach (var edge in connectedEdges)
            {
                RemoveElement(edge);
            }
        }

        // 然后删除节点
        return base.DeleteSelection();
    }

    public new void DeleteElements(IEnumerable<GraphElement> elements)
    {
        var elementsList = elements.ToList();

        // 找出所有要删除的节点
        var nodesToDelete = elementsList.OfType<DialogueNode>().ToList();

        // 先删除所有相关的连线
        var edgesToDelete = new List<Edge>();
        foreach (var node in nodesToDelete)
        {
            var connectedEdges = edges.ToList().Where(edge =>
                edge.input?.node == node || edge.output?.node == node).ToList();

            edgesToDelete.AddRange(connectedEdges);
        }

        // 移除重复的边
        edgesToDelete = edgesToDelete.Distinct().ToList();

        // 删除边
        foreach (var edge in edgesToDelete)
        {
            if (edge != null)
            {
                RemoveElement(edge);
            }
        }

        // 调用基类方法删除其他元素
        base.DeleteElements(elementsList);
    }

    public DialogueNode CreateDialogueNode(string characterName, Sprite avatarSprite,
                                          string content, Vector2 position = default)
    {
        var dialogueNode = new DialogueNode(characterName, avatarSprite, content,
                                           nextNodeIndex++, editorWindow);
        dialogueNode.SetPosition(new Rect(position, Vector2.zero));
        dialogueNode.OnNodeChanged += () => editorWindow?.MarkAsChanged();
        AddElement(dialogueNode);
        return dialogueNode;
    }

    private Vector2 GetLocalMousePosition(Vector2 mousePosition)
    {
        return contentViewContainer.WorldToLocal(mousePosition);
    }

    public List<RuntimeDialogueData> GetDialogueSequence()
    {
        var exportDict = new Dictionary<string, RuntimeDialogueData>();
        var nodes = this.nodes.Cast<DialogueNode>().ToList();
        var edges = this.edges.ToList();

        foreach (var node in nodes)
        {
            string runtimeAvatarPath = ConvertSpriteToRuntimePath(node.AvatarSprite);

            var exportData = new RuntimeDialogueData
            {
                index = node.NodeIndex,
                name = node.CharacterName,
                avatarAddr = runtimeAvatarPath,
                content = node.DialogueText,
                choices = new List<RuntimeChoice>(),
                nextNodeId = null,
                eventCalls = new List<DialogueEventCall>(node.EventCalls),
                conditionalBranches = new List<RuntimeConditionalBranch>()
            };

            var conditionalBranches = node.GetAllConditionalBranches();
            foreach (var branch in conditionalBranches)
            {
                string targetNodeId = null;
                var connection = edges.FirstOrDefault(e =>
                {
                    if (e.output?.node != node) return false;
                    var conditionalEdge = e as ConditionalEdge;
                    if (conditionalEdge != null)
                    {
                        return conditionalEdge.branchPriority == branch.priority;
                    }
                    else
                    {
                        return node.GetBranchPriorityForPort(e.output) == branch.priority;
                    }
                });

                if (connection?.input?.node is DialogueNode targetNode)
                {
                    targetNodeId = targetNode.GetId();
                }

                var runtimeBranch = new RuntimeConditionalBranch
                {
                    priority = branch.priority,
                    conditions = new List<ChoiceCondition>(branch.conditions),
                    conditionLogic = branch.conditionLogic,
                    targetIndex = -1
                };

                if (!string.IsNullOrEmpty(targetNodeId))
                {
                    var tempDict = exportData.conditionalBranches as List<RuntimeConditionalBranch>;
                    tempDict.Add(runtimeBranch);
                }
                else
                {
                    exportData.conditionalBranches.Add(runtimeBranch);
                }
            }

            exportDict[node.GetId()] = exportData;
        }

        foreach (var node in nodes)
        {
            var outputConnections = edges.Where(edge => edge.output.node == node).ToList();
            var exportData = exportDict[node.GetId()];

            foreach (var connection in outputConnections)
            {
                var targetNode = connection.input.node as DialogueNode;
                if (targetNode == null) continue;

                int choiceIndex = node.GetChoiceIndexForPort(connection.output);

                if (choiceIndex >= 0 && choiceIndex < node.ChoicesData.Count)
                {
                    var choiceData = node.ChoicesData[choiceIndex];
                    var choice = new RuntimeChoice
                    {
                        text = choiceData.text,
                        nextNodeId = targetNode.GetId(),
                        conditions = new List<ChoiceCondition>(choiceData.conditions),
                        conditionLogic = choiceData.conditionLogic
                    };
                    exportData.choices.Add(choice);
                }
                else if (choiceIndex == -1 && !node.IsConditionalMode())
                {
                    exportData.nextNodeId = targetNode.GetId();
                }
            }

            exportData.choices = exportData.choices.OrderBy(c =>
                node.ChoicesData.FindIndex(cd => cd.text == c.text)).ToList();
        }

        var nodeIdToIndex = new Dictionary<string, int>();
        foreach (var kvp in exportDict)
        {
            nodeIdToIndex[kvp.Key] = kvp.Value.index;
        }

        foreach (var data in exportDict.Values)
        {
            foreach (var branch in data.conditionalBranches)
            {
                var node = nodes.FirstOrDefault(n => n.GetId() == exportDict.FirstOrDefault(x => x.Value == data).Key);
                if (node != null)
                {
                    var connection = edges.FirstOrDefault(e =>
                    {
                        if (e.output?.node != node) return false;
                        var conditionalEdge = e as ConditionalEdge;
                        if (conditionalEdge != null)
                        {
                            return conditionalEdge.branchPriority == branch.priority;
                        }
                        else
                        {
                            return node.GetBranchPriorityForPort(e.output) == branch.priority;
                        }
                    });

                    if (connection?.input?.node is DialogueNode targetNode)
                    {
                        string targetNodeId = targetNode.GetId();
                        if (nodeIdToIndex.ContainsKey(targetNodeId))
                        {
                            branch.targetIndex = nodeIdToIndex[targetNodeId];
                        }
                    }
                }
            }
        }

        return exportDict.Values.OrderBy(d => d.index).ToList();
    }

    private string ConvertSpriteToRuntimePath(Sprite sprite)
    {
        if (sprite == null) return "";

        string assetPath = AssetDatabase.GetAssetPath(sprite);
        int resourcesIndex = assetPath.IndexOf("Resources/");

        if (resourcesIndex >= 0)
        {
            string resourcePath = assetPath.Substring(resourcesIndex + 10);
            resourcePath = Path.ChangeExtension(resourcePath, null);
            return resourcePath;
        }
        else
        {
            Debug.LogWarning($"Avatar sprite '{sprite.name}' is not in a Resources folder!");
            return sprite.name;
        }
    }

    public DialogueTreeData SerializeDialogueTree()
    {
        var treeData = new DialogueTreeData();

        try
        {
            var nodes = this.nodes.Cast<DialogueNode>().ToList();
            foreach (var node in nodes)
            {
                if (node == null) continue;

                var nodeData = new DialogueNodeData
                {
                    id = node.GetId(),
                    index = node.NodeIndex,
                    name = node.CharacterName ?? "",
                    avatarAssetPath = node.AvatarSprite != null ? AssetDatabase.GetAssetPath(node.AvatarSprite) : "",
                    content = node.DialogueText ?? "",
                    positionX = node.GetPosition().x,
                    positionY = node.GetPosition().y,
                    choices = new List<ChoiceData>(node.ChoicesData ?? new List<ChoiceData>()),
                    eventCalls = new List<DialogueEventCall>(node.EventCalls ?? new List<DialogueEventCall>()),
                    conditionalBranches = node.GetAllConditionalBranches()
                };
                treeData.nodes.Add(nodeData);
            }

            var edges = this.edges.ToList();
            foreach (var edge in edges)
            {
                if (edge?.output?.node == null || edge?.input?.node == null) continue;

                var outputNode = edge.output.node as DialogueNode;
                var inputNode = edge.input.node as DialogueNode;

                if (outputNode != null && inputNode != null)
                {
                    int choiceIndex = outputNode.GetChoiceIndexForPort(edge.output);
                    int branchPriority = outputNode.GetBranchPriorityForPort(edge.output);

                    string choiceText = "";
                    if (choiceIndex >= 0 && choiceIndex < outputNode.ChoicesData.Count)
                    {
                        choiceText = outputNode.ChoicesData[choiceIndex].text;
                    }

                    var connectionData = new DialogueConnectionData
                    {
                        outputNodeId = outputNode.GetId(),
                        inputNodeId = inputNode.GetId(),
                        choiceIndex = choiceIndex,
                        choiceText = choiceText,
                        branchPriority = branchPriority
                    };
                    treeData.connections.Add(connectionData);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error during serialization: {e.Message}");
            return new DialogueTreeData();
        }

        return treeData;
    }

    public void LoadDialogueTree(DialogueTreeData treeData)
    {
        DeleteElements(graphElements.ToList());

        var nodeDict = new Dictionary<string, DialogueNode>();
        var sortedNodes = treeData.nodes.OrderBy(n => n.index).ToList();

        Debug.Log($"[LoadDialogueTree] Loading {sortedNodes.Count} nodes");

        foreach (var nodeData in sortedNodes)
        {
            Sprite avatarSprite = null;
            if (!string.IsNullOrEmpty(nodeData.avatarAssetPath))
            {
                avatarSprite = AssetDatabase.LoadAssetAtPath<Sprite>(nodeData.avatarAssetPath);
                if (avatarSprite == null)
                {
                    Debug.LogWarning($"Failed to load sprite at path: {nodeData.avatarAssetPath}");
                }
            }

            Vector2 position = new Vector2(nodeData.positionX, nodeData.positionY);
            var node = CreateDialogueNodeWithIndex(nodeData.name, avatarSprite,
                                                  nodeData.content, position, nodeData.index);
            node.SetId(nodeData.id);
            node.SetChoicesData(nodeData.choices);
            node.SetEventCalls(nodeData.eventCalls);

            if (nodeData.conditionalBranches != null && nodeData.conditionalBranches.Count > 0)
            {
                node.LoadConditionalBranches(nodeData.conditionalBranches);
            }

            nodeDict[nodeData.id] = node;
        }

        if (sortedNodes.Count > 0)
        {
            nextNodeIndex = sortedNodes.Max(n => n.index) + 1;
        }
        else
        {
            nextNodeIndex = 0;
        }

        foreach (var connectionData in treeData.connections)
        {
            if (!nodeDict.TryGetValue(connectionData.outputNodeId, out var outputNode)) continue;
            if (!nodeDict.TryGetValue(connectionData.inputNodeId, out var inputNode)) continue;

            Port outputPort = null;

            if (connectionData.choiceIndex >= 0)
            {
                outputPort = outputNode.GetOutputPortByIndex(connectionData.choiceIndex);
            }
            else if (connectionData.branchPriority >= 0 && outputNode.IsConditionalMode())
            {
                outputPort = outputNode.GetConditionalPort(connectionData.branchPriority);
            }
            else
            {
                outputPort = outputNode.GetDefaultOutputPort();
            }

            var inputPort = inputNode.GetInputPort();

            if (outputPort != null && inputPort != null)
            {
                var edge = outputPort.ConnectTo(inputPort);

                if (connectionData.branchPriority >= 0 && outputNode.IsConditionalMode())
                {
                    var conditionalEdge = new ConditionalEdge(this, editorWindow);
                    conditionalEdge.input = edge.input;
                    conditionalEdge.output = edge.output;
                    conditionalEdge.branchPriority = connectionData.branchPriority;

                    var branchData = outputNode.GetConditionalBranchData(connectionData.branchPriority);
                    if (branchData != null)
                    {
                        conditionalEdge.conditions = new List<ChoiceCondition>(branchData.conditions);
                        conditionalEdge.conditionLogic = branchData.conditionLogic;
                    }

                    // 手动连接端口以更新视觉状态
                    edge.input?.Connect(conditionalEdge);
                    edge.output?.Connect(conditionalEdge);

                    AddElement(conditionalEdge);
                }
                else
                {
                    AddElement(edge);
                }
            }
        }

        Debug.Log($"[LoadDialogueTree] Loaded {nodeDict.Count} nodes and {treeData.connections.Count} connections");
    }

    private DialogueNode CreateDialogueNodeWithIndex(string characterName, Sprite avatarSprite,
                                                     string content, Vector2 position, int index)
    {
        var dialogueNode = new DialogueNode(characterName, avatarSprite, content, index, editorWindow);
        dialogueNode.SetPosition(new Rect(position, Vector2.zero));
        dialogueNode.OnNodeChanged += () => editorWindow?.MarkAsChanged();
        AddElement(dialogueNode);
        return dialogueNode;
    }

    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
    {
        return ports.ToList().Where(endPort =>
            endPort.direction != startPort.direction &&
            endPort.node != startPort.node &&
            endPort.portType == startPort.portType).ToList();
    }
}