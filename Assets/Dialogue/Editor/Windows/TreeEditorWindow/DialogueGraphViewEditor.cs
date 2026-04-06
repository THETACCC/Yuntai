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
public class DialogueGraphViewEditor : GraphView
{
    private DialogueTreeEditor editorWindow;
    private int nextNodeIndex = 0;

    public DialogueGraphViewEditor()
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

        // 支持复制粘贴
        serializeGraphElements = OnSerializeGraphElements;
        unserializeAndPaste = OnUnserializeAndPaste;
        canPasteSerializedData = OnCanPasteSerializedData;
    }

    private GraphViewChange OnGraphViewChangedInternal(GraphViewChange change)
    {
        if (change.elementsToRemove != null)
        {
            var edgesToRemove = new List<Edge>();

            foreach (var element in change.elementsToRemove)
            {
                if (element is DialogueNodeEditor node)
                {
                    var connectedEdges = edges.ToList().Where(edge =>
                        edge.input?.node == node || edge.output?.node == node).ToList();

                    edgesToRemove.AddRange(connectedEdges);
                }
            }

            foreach (var edge in edgesToRemove)
            {
                if (edge != null && !change.elementsToRemove.Contains(edge))
                {
                    RemoveElement(edge);
                }
            }
        }

        if (change.edgesToCreate != null)
        {
            var edgesToReplace = new List<Edge>(change.edgesToCreate);
            change.edgesToCreate.Clear();

            foreach (var edge in edgesToReplace)
            {
                var outputNode = edge.output?.node as DialogueNodeEditor;
                if (outputNode == null)
                {
                    change.edgesToCreate.Add(edge);
                    continue;
                }

                int branchPriority = outputNode.GetBranchPriorityForPort(edge.output);

                if (branchPriority >= 0 && outputNode.IsConditionalMode())
                {
                    var conditionalEdge = new ConditionalEdgeEditor(this, editorWindow);
                    conditionalEdge.input = edge.input;
                    conditionalEdge.output = edge.output;
                    conditionalEdge.branchPriority = branchPriority;

                    var branchData = outputNode.GetConditionalBranchData(branchPriority);
                    if (branchData != null)
                    {
                        conditionalEdge.conditions = new List<ChoiceCondition>(branchData.conditions);
                        conditionalEdge.conditionLogic = branchData.conditionLogic;
                    }

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
        var node0 = nodes.Cast<DialogueNodeEditor>().FirstOrDefault(n => n.NodeIndex == 0);
        if (node0 == null)
        {
            Debug.LogWarning("Node 0 not found for centering");
            return;
        }

        var nodePosition = node0.GetPosition();
        if (float.IsNaN(nodePosition.x) || float.IsNaN(nodePosition.y) ||
            float.IsNaN(nodePosition.width) || float.IsNaN(nodePosition.height))
        {
            // 只在窗口可见时才重试
            if (editorWindow != null && editorWindow.hasFocus)
            {
                Debug.Log("Node 0 position contains NaN values, retrying...");
                EditorApplication.delayCall += () => CenterOnNode0();
            }
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
            // 只在窗口可见时才重试
            if (editorWindow != null && editorWindow.hasFocus)
            {
                Debug.Log("Node 0 not fully initialized, retrying...");
                EditorApplication.delayCall += () => CenterOnNode0();
            }
            return;
        }

        var graphViewBounds = worldBound;
        if (graphViewBounds.width <= 0 || graphViewBounds.height <= 0)
        {
            // 只在窗口可见时才重试
            if (editorWindow != null && editorWindow.hasFocus)
            {
                Debug.Log("GraphView bounds not ready, retrying...");
                EditorApplication.delayCall += () => CenterOnNode0();
            }
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

        var selectedNodes = selection.OfType<DialogueNodeEditor>().ToList();
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
        var nodesToDelete = selection.OfType<DialogueNodeEditor>().ToList();

        foreach (var node in nodesToDelete)
        {
            var connectedEdges = edges.ToList().Where(edge =>
                edge.input?.node == node || edge.output?.node == node).ToList();

            foreach (var edge in connectedEdges)
            {
                RemoveElement(edge);
            }
        }

        var result = base.DeleteSelection();

        // 删除后重新排序所有节点的 index
        ReorderNodeIndices();

        return result;
    }

    public new void DeleteElements(IEnumerable<GraphElement> elements)
    {
        var elementsList = elements.ToList();

        var nodesToDelete = elementsList.OfType<DialogueNodeEditor>().ToList();

        var edgesToDelete = new List<Edge>();
        foreach (var node in nodesToDelete)
        {
            var connectedEdges = edges.ToList().Where(edge =>
                edge.input?.node == node || edge.output?.node == node).ToList();

            edgesToDelete.AddRange(connectedEdges);
        }

        edgesToDelete = edgesToDelete.Distinct().ToList();

        foreach (var edge in edgesToDelete)
        {
            if (edge != null)
            {
                RemoveElement(edge);
            }
        }

        base.DeleteElements(elementsList);

        // 删除后重新排序所有节点的 index
        if (nodesToDelete.Count > 0)
        {
            ReorderNodeIndices();
        }
    }

    public DialogueNodeEditor CreateDialogueNode(string characterName, Sprite avatarSprite,
                                          string content, Vector2 position = default)
    {
        var dialogueNode = new DialogueNodeEditor("", null, content,
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
        var nodes = this.nodes.Cast<DialogueNodeEditor>().ToList();
        var edges = this.edges.ToList();

        // 加载角色库
        var characterLibrary = LoadCharacterLibrary();

        foreach (var node in nodes)
        {
            // 根据 characterId 获取角色信息
            string characterName = "";
            string runtimeAvatarPath = "";
            bool isPlayerCharacter = false;
            LocalizedText characterNameLocalized = null;

            if (!string.IsNullOrEmpty(node.CharacterId) && characterLibrary != null)
            {
                var character = System.Array.Find(characterLibrary.characters, c => c.id == node.CharacterId);
                if (character != null)
                {
                    // 根据模式获取Character Name
                    if (character.useNameId && !string.IsNullOrEmpty(character.nameId) && DialogueLocalization.IsLoaded)
                    {
                        // Use ID模式：从DialogueLocalization读取
                        var locData = DialogueLocalization.GetAllLanguages(character.nameId);
                        if (locData != null)
                        {
                            characterNameLocalized = new LocalizedText
                            {
                                zh = locData.ContainsKey(Language.ChineseSimplified) ? locData[Language.ChineseSimplified] : "",
                                en = locData.ContainsKey(Language.English) ? locData[Language.English] : "",
                                ja = locData.ContainsKey(Language.Japanese) ? locData[Language.Japanese] : ""
                            };
                            characterName = characterNameLocalized.en;
                        }
                        else
                        {
                            // ID不存在，使用character字段
                            characterName = character.character ?? "";
                            characterNameLocalized = character.characterName;
                        }
                    }
                    else
                    {
                        // Direct Input模式：直接使用characterName
                        characterName = character.characterName?.en ?? "";
                        characterNameLocalized = character.characterName;
                    }

                    runtimeAvatarPath = ConvertSpritePathToRuntimePath(character.avatarAssetPath);
                    isPlayerCharacter = character.isPlayer;
                }
            }

            // 获取对话内容
            LocalizedText dialogueContent = node.DialogueText ?? new LocalizedText();

            // 只在Use ID模式才从DialogueLocalization读取
            if (node.UseContentId && !string.IsNullOrEmpty(node.ContentId) && DialogueLocalization.IsLoaded)
            {
                var locData = DialogueLocalization.GetAllLanguages(node.ContentId);
                if (locData != null)
                {
                    dialogueContent = new LocalizedText
                    {
                        zh = locData.ContainsKey(Language.ChineseSimplified) ? locData[Language.ChineseSimplified] : "",
                        en = locData.ContainsKey(Language.English) ? locData[Language.English] : "",
                        ja = locData.ContainsKey(Language.Japanese) ? locData[Language.Japanese] : ""
                    };
                }
            }


            var exportData = new RuntimeDialogueData
            {
                index = node.NodeIndex,
                name = characterNameLocalized,
                avatarAddr = runtimeAvatarPath,
                isPlayer = isPlayerCharacter,
                content = dialogueContent,
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
                    var conditionalEdge = e as ConditionalEdgeEditor;
                    if (conditionalEdge != null)
                    {
                        return conditionalEdge.branchPriority == branch.priority;
                    }
                    else
                    {
                        return node.GetBranchPriorityForPort(e.output) == branch.priority;
                    }
                });

                if (connection?.input?.node is DialogueNodeEditor targetNode)
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
                var targetNode = connection.input.node as DialogueNodeEditor;
                if (targetNode == null) continue;

                int choiceIndex = node.GetChoiceIndexForPort(connection.output);

                if (choiceIndex >= 0 && choiceIndex < node.ChoicesData.Count)
                {
                    var choiceData = node.ChoicesData[choiceIndex];

                    // 获取choice文本
                    LocalizedText choiceText = choiceData.text ?? new LocalizedText();

                    // 只在Use ID模式才从DialogueLocalization读取
                    if (choiceData.useTextId && !string.IsNullOrEmpty(choiceData.textId) && DialogueLocalization.IsLoaded)
                    {
                        var locData = DialogueLocalization.GetAllLanguages(choiceData.textId);
                        if (locData != null)
                        {
                            choiceText = new LocalizedText
                            {
                                zh = locData.ContainsKey(Language.ChineseSimplified) ? locData[Language.ChineseSimplified] : "",
                                en = locData.ContainsKey(Language.English) ? locData[Language.English] : "",
                                ja = locData.ContainsKey(Language.Japanese) ? locData[Language.Japanese] : ""
                            };
                        }
                    }


                    var choice = new RuntimeChoice
                    {
                        text = choiceText,
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
                node.ChoicesData.FindIndex(cd => (cd.text?.en ?? "") == (c.text?.en ?? ""))).ToList();
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
                        var conditionalEdge = e as ConditionalEdgeEditor;
                        if (conditionalEdge != null)
                        {
                            return conditionalEdge.branchPriority == branch.priority;
                        }
                        else
                        {
                            return node.GetBranchPriorityForPort(e.output) == branch.priority;
                        }
                    });

                    if (connection?.input?.node is DialogueNodeEditor targetNode)
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

    private CharacterLibraryData LoadCharacterLibrary()
    {
        // 使用固定路径，与 CharacterLibraryManager 和 DialogueNodeEditor 保持一致
        string libraryPath = "Assets/Dialogue/Editor/Data/CharacterLibrary.json";
        
        try
        {
            if (File.Exists(libraryPath))
            {
                string json = File.ReadAllText(libraryPath);
                return JsonUtility.FromJson<CharacterLibraryData>(json);
            }
            else
            {
                Debug.LogWarning($"Character library not found at: {libraryPath}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load character library: {e.Message}");
        }
        return new CharacterLibraryData();
    }

    private string ConvertSpritePathToRuntimePath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath)) return "";

        int resourcesIndex = assetPath.IndexOf("Resources/");

        if (resourcesIndex >= 0)
        {
            string resourcePath = assetPath.Substring(resourcesIndex + 10);
            resourcePath = Path.ChangeExtension(resourcePath, null);
            return resourcePath;
        }
        else
        {
            Debug.LogWarning($"Avatar path '{assetPath}' is not in a Resources folder!");
            return Path.GetFileNameWithoutExtension(assetPath);
        }
    }

    public DialogueTreeData SerializeDialogueTree()
    {
        var treeData = new DialogueTreeData();

        try
        {
            var nodes = this.nodes.Cast<DialogueNodeEditor>().ToList();
            foreach (var node in nodes)
            {
                if (node == null) continue;

                // 获取对话内容
                LocalizedText dialogueContent = node.DialogueText ?? new LocalizedText();
                string contentId = node.ContentId ?? "";
                bool useContentId = node.UseContentId;

                // 只在Use ID模式才从DialogueLocalization读取
                if (useContentId && !string.IsNullOrEmpty(contentId) && DialogueLocalization.IsLoaded)
                {
                    var locData = DialogueLocalization.GetAllLanguages(contentId);
                    if (locData != null)
                    {
                        // 从Google Sheets读取所有语言的文本
                        dialogueContent = new LocalizedText
                        {
                            zh = locData.ContainsKey(Language.ChineseSimplified) ? locData[Language.ChineseSimplified] : "",
                            en = locData.ContainsKey(Language.English) ? locData[Language.English] : "",
                            ja = locData.ContainsKey(Language.Japanese) ? locData[Language.Japanese] : ""
                        };
                    }
                    else
                    {
                        Debug.LogWarning($"[SerializeDialogueTree] Node {node.NodeIndex}: contentId '{contentId}' not found in Google Sheets");
                    }
                }

                // 处理选择文本
                var choicesData = new List<ChoiceData>();
                if (node.ChoicesData != null)
                {
                    foreach (var choice in node.ChoicesData)
                    {
                        var choiceData = new ChoiceData
                        {
                            useTextId = choice.useTextId,  // 保存模式
                            textId = choice.textId ?? "",
                            text = choice.text ?? new LocalizedText(),
                            conditions = choice.conditions,
                            conditionLogic = choice.conditionLogic
                        };

                        // 只在Use ID模式才从DialogueLocalization读取
                        if (choiceData.useTextId && !string.IsNullOrEmpty(choiceData.textId) && DialogueLocalization.IsLoaded)
                        {
                            var locData = DialogueLocalization.GetAllLanguages(choiceData.textId);
                            if (locData != null)
                            {
                                choiceData.text = new LocalizedText
                                {
                                    zh = locData.ContainsKey(Language.ChineseSimplified) ? locData[Language.ChineseSimplified] : "",
                                    en = locData.ContainsKey(Language.English) ? locData[Language.English] : "",
                                    ja = locData.ContainsKey(Language.Japanese) ? locData[Language.Japanese] : ""
                                };
                            }
                            else
                            {
                                Debug.LogWarning($"[SerializeDialogueTree] Node {node.NodeIndex}: choice textId '{choiceData.textId}' not found in Google Sheets");
                            }
                        }

                        choicesData.Add(choiceData);
                    }
                }

                var nodeData = new DialogueNodeData
                {
                    id = node.GetId(),
                    index = node.NodeIndex,
                    characterId = node.CharacterId ?? "",
                    useContentId = useContentId,  // 保存模式标识
                    contentId = contentId,
                    content = dialogueContent,
                    positionX = node.GetPosition().x,
                    positionY = node.GetPosition().y,
                    choices = choicesData,
                    eventCalls = new List<DialogueEventCall>(node.EventCalls ?? new List<DialogueEventCall>()),
                    conditionalBranches = node.GetAllConditionalBranches()
                };
                treeData.nodes.Add(nodeData);
            }

            var edges = this.edges.ToList();
            foreach (var edge in edges)
            {
                if (edge?.output?.node == null || edge?.input?.node == null) continue;

                var outputNode = edge.output.node as DialogueNodeEditor;
                var inputNode = edge.input.node as DialogueNodeEditor;

                if (outputNode != null && inputNode != null)
                {
                    int choiceIndex = outputNode.GetChoiceIndexForPort(edge.output);
                    int branchPriority = outputNode.GetBranchPriorityForPort(edge.output);

                    string choiceText = "";
                    if (choiceIndex >= 0 && choiceIndex < outputNode.ChoicesData.Count)
                    {
                        choiceText = outputNode.ChoicesData[choiceIndex].text?.en ?? "";
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

        var nodeDict = new Dictionary<string, DialogueNodeEditor>();
        var sortedNodes = treeData.nodes.OrderBy(n => n.index).ToList();

        Debug.Log($"[LoadDialogueTree] Loading {sortedNodes.Count} nodes");

        // 直接使用useContentId字段，不做自动判断
        foreach (var nodeData in sortedNodes)
        {
            Vector2 position = new Vector2(nodeData.positionX, nodeData.positionY);
            var node = CreateDialogueNodeWithIndex("", null,
                                                  nodeData.content?.en ?? "", position, nodeData.index);
            node.SetId(nodeData.id);
            node.SetCharacterId(nodeData.characterId);

            // 设置完整的多语言文本
            if (nodeData.content != null)
            {
                node.SetDialogueText(nodeData.content);
            }

            // 设置内容模式和ID
            // 完全依赖useContentId字段，不依赖contentId是否为空
            node.SetContentMode(nodeData.useContentId, nodeData.contentId ?? "");

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
                    var conditionalEdge = new ConditionalEdgeEditor(this, editorWindow);
                    conditionalEdge.input = edge.input;
                    conditionalEdge.output = edge.output;
                    conditionalEdge.branchPriority = connectionData.branchPriority;

                    var branchData = outputNode.GetConditionalBranchData(connectionData.branchPriority);
                    if (branchData != null)
                    {
                        conditionalEdge.conditions = new List<ChoiceCondition>(branchData.conditions);
                        conditionalEdge.conditionLogic = branchData.conditionLogic;
                    }

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

    private DialogueNodeEditor CreateDialogueNodeWithIndex(string characterName, Sprite avatarSprite,
                                                     string content, Vector2 position, int index)
    {
        var dialogueNode = new DialogueNodeEditor("", null, content, index, editorWindow);
        dialogueNode.SetPosition(new Rect(position, Vector2.zero));
        dialogueNode.OnNodeChanged += () => editorWindow?.MarkAsChanged();

        // 确保 editorWindow 引用正确设置
        if (editorWindow != null)
        {
            dialogueNode.SetEditorWindow(editorWindow);
        }

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

    /// <summary>
    /// 重新排序所有节点的 index，使其从 0 开始连续递增
    /// 删除节点后调用，保证 index 连续性
    /// </summary>
    private void ReorderNodeIndices()
    {
        var allNodes = nodes.Cast<DialogueNodeEditor>().ToList();

        if (allNodes.Count == 0)
        {
            nextNodeIndex = 0;
            return;
        }

        // 按当前 index 排序
        allNodes = allNodes.OrderBy(n => n.NodeIndex).ToList();

        // 重新分配 index，从 0 开始
        for (int i = 0; i < allNodes.Count; i++)
        {
            allNodes[i].SetNodeIndex(i);
        }

        // 更新 nextNodeIndex
        nextNodeIndex = allNodes.Count;

        //Debug.Log($"[DialogueGraphViewEditor] Reordered node indices: {allNodes.Count} nodes, nextIndex = {nextNodeIndex}");

        // 标记为已修改
        if (editorWindow != null)
        {
            editorWindow.MarkAsChanged();
        }
    }

    #region Copy/Paste Support

    /// <summary>
    /// 序列化选中的节点用于复制（不包含连线）
    /// </summary>
    private string OnSerializeGraphElements(IEnumerable<GraphElement> elements)
    {
        var nodesToCopy = elements.OfType<DialogueNodeEditor>().ToList();

        if (nodesToCopy.Count == 0)
            return string.Empty;

        var copyData = new CopyPasteData
        {
            nodes = new List<NodeCopyData>()
        };

        foreach (var node in nodesToCopy)
        {
            var nodeData = new NodeCopyData
            {
                characterId = node.CharacterId,
                dialogueText = node.DialogueText?.en ?? "",
                positionX = node.GetPosition().x,
                positionY = node.GetPosition().y,
                choices = new List<ChoiceData>(node.ChoicesData),
                eventCalls = new List<DialogueEventCall>(node.EventCalls),
                conditionalBranches = node.GetAllConditionalBranches()
            };

            copyData.nodes.Add(nodeData);
        }

        string json = JsonUtility.ToJson(copyData, true);
        //Debug.Log($"[Copy] Copied {nodesToCopy.Count} nodes");
        return json;
    }

    /// <summary>
    /// 反序列化并粘贴节点
    /// </summary>
    private void OnUnserializeAndPaste(string operationName, string data)
    {
        try
        {
            var copyData = JsonUtility.FromJson<CopyPasteData>(data);

            if (copyData == null || copyData.nodes == null || copyData.nodes.Count == 0)
            {
                Debug.LogWarning("[Paste] No valid node data to paste");
                return;
            }

            ClearSelection();

            // 计算粘贴偏移（避免覆盖原节点）
            Vector2 offset = new Vector2(50, 50);

            foreach (var nodeData in copyData.nodes)
            {
                // 创建新节点（不带连线）
                Vector2 newPos = new Vector2(nodeData.positionX, nodeData.positionY) + offset;
                var newNode = CreateDialogueNode("", null, nodeData.dialogueText, newPos);

                // 设置角色ID
                newNode.SetCharacterId(nodeData.characterId);

                // 设置选项数据
                if (nodeData.choices != null && nodeData.choices.Count > 0)
                {
                    newNode.SetChoicesData(nodeData.choices);
                }

                // 设置事件调用
                if (nodeData.eventCalls != null && nodeData.eventCalls.Count > 0)
                {
                    newNode.SetEventCalls(nodeData.eventCalls);
                }

                // 设置条件分支
                if (nodeData.conditionalBranches != null && nodeData.conditionalBranches.Count > 0)
                {
                    newNode.LoadConditionalBranches(nodeData.conditionalBranches);
                }

                // 添加到选择集合
                AddToSelection(newNode);
            }

            //Debug.Log($"[Paste] Pasted {copyData.nodes.Count} nodes");

            if (editorWindow != null)
            {
                editorWindow.MarkAsChanged();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Paste] Failed to paste: {e.Message}");
        }
    }

    /// <summary>
    /// 检查是否可以粘贴数据
    /// </summary>
    private bool OnCanPasteSerializedData(string data)
    {
        try
        {
            var copyData = JsonUtility.FromJson<CopyPasteData>(data);
            return copyData != null && copyData.nodes != null && copyData.nodes.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 复制粘贴数据结构
    /// </summary>
    [Serializable]
    private class CopyPasteData
    {
        public List<NodeCopyData> nodes;
    }

    /// <summary>
    /// 节点复制数据（不包含 index 和连接）
    /// </summary>
    [Serializable]
    private class NodeCopyData
    {
        public string characterId;
        public string dialogueText;
        public float positionX;
        public float positionY;
        public List<ChoiceData> choices;
        public List<DialogueEventCall> eventCalls;
        public List<ConditionalBranchData> conditionalBranches;
    }

    #endregion

    /// <summary>
    /// 刷新所有节点的语言显示
    /// </summary>
    public void RefreshAllNodesLanguage()
    {
        var dialogueNodes = nodes.ToList().OfType<DialogueNodeEditor>();
        foreach (var node in dialogueNodes)
        {
            node.RefreshLanguageDisplay();
        }
    }

}
/// <summary>
/// 条件连线 - 不显示标签
/// </summary>
public class ConditionalEdgeEditor : Edge
{
    private DialogueGraphViewEditor graphView;
    private DialogueTreeEditor editorWindow;

    public int branchPriority;
    public List<ChoiceCondition> conditions = new List<ChoiceCondition>();
    public ConditionLogic conditionLogic = ConditionLogic.AND;

    public ConditionalEdgeEditor(DialogueGraphViewEditor graphView, DialogueTreeEditor editorWindow)
    {
        this.graphView = graphView;
        this.editorWindow = editorWindow;
    }

    public void UpdateLabel()
    {
        // 不再显示标签，因为节点内部已有 Conditional Branches 显示
    }
}