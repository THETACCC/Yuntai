using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using System;
using System.Collections.Generic;
using System.Linq;
using DialogueSystem;

/// <summary>
/// 对话节点 - 优化布局版
/// </summary>
public partial class DialogueNode : Node
{
    private DialogueTreeEditor editorWindow;
    private TextField characterNameField;
    private ObjectField avatarField;
    private TextField dialogueTextField;
    private VisualElement eventsContainer;
    private Button addEventButton;
    private VisualElement choicesContainer;
    private Button addChoiceButton;
    private VisualElement conditionalBranchesContainer;
    private Port inputPort;
    private Port defaultOutputPort;
    private List<Port> choiceOutputPorts = new List<Port>();
    private List<Port> conditionalPorts = new List<Port>();
    private Dictionary<int, ConditionalBranchData> conditionalBranchesData = new Dictionary<int, ConditionalBranchData>();

    private string nodeId;
    private int nodeIndex;
    private bool isConditionalMode = false;
    private int nextBranchPriority = 1;

    public string CharacterName { get; private set; }
    public Sprite AvatarSprite { get; private set; }
    public string DialogueText { get; private set; }
    public List<ChoiceData> ChoicesData { get; private set; } = new List<ChoiceData>();
    public List<DialogueEventCall> EventCalls { get; private set; } = new List<DialogueEventCall>();
    public int NodeIndex => nodeIndex;

    public event System.Action OnNodeChanged;

    public DialogueNode(string characterName = "Character", Sprite avatarSprite = null,
                       string dialogueText = "New Dialogue", int index = 0,
                       DialogueTreeEditor editor = null)
    {
        this.CharacterName = characterName;
        this.AvatarSprite = avatarSprite;
        this.DialogueText = dialogueText;
        this.nodeIndex = index;
        this.nodeId = System.Guid.NewGuid().ToString();
        this.editorWindow = editor;

        UpdateTitle();
        CreateInputPort();
        CreateOutputPortWithAddButton();
        CreateCharacterNameField();
        CreateAvatarField();
        CreateDialogueTextField();
        CreateEventsSection();
        CreateConditionalBranchesSection();
        CreateChoicesSection();

        RefreshExpandedState();
        RefreshPorts();
    }

    #region Basic Setup
    private void UpdateTitle()
    {
        title = $"Node [{nodeIndex}]";
    }

    public void SetNodeIndex(int index)
    {
        nodeIndex = index;
        UpdateTitle();
    }

    private void NotifyChange()
    {
        OnNodeChanged?.Invoke();
    }

    private void CreateInputPort()
    {
        inputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
        inputPort.portName = "Input";
        inputContainer.Add(inputPort);
    }

    private void CreateOutputPortWithAddButton()
    {
        var outputRow = new VisualElement();
        outputRow.style.flexDirection = FlexDirection.Row;
        outputRow.style.alignItems = Align.Center;
        outputRow.style.justifyContent = Justify.SpaceBetween;
        outputRow.style.width = Length.Percent(100);

        defaultOutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
        defaultOutputPort.portName = "Next";
        defaultOutputPort.userData = -1;
        defaultOutputPort.style.flexGrow = 1;

        var addBranchButton = new Button(OnAddBranch)
        {
            text = "+"
        };
        addBranchButton.style.width = 20;
        addBranchButton.style.height = 20;
        addBranchButton.style.fontSize = 14;
        addBranchButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        addBranchButton.style.flexShrink = 0;

        outputRow.Add(defaultOutputPort);
        outputRow.Add(addBranchButton);
        outputContainer.Add(outputRow);
    }
    #endregion

    #region Character and Dialogue Fields
    private void CreateCharacterNameField()
    {
        characterNameField = new TextField("Character Name:")
        {
            value = CharacterName
        };
        characterNameField.style.minWidth = 300;
        characterNameField.style.maxWidth = 300;
        characterNameField.RegisterValueChangedCallback(evt =>
        {
            CharacterName = evt.newValue;
            NotifyChange();
        });
        mainContainer.Add(characterNameField);
    }

    private void CreateAvatarField()
    {
        var avatarContainer = new VisualElement();
        avatarField = new ObjectField("Avatar Sprite:")
        {
            objectType = typeof(Sprite),
            value = AvatarSprite,
            allowSceneObjects = false
        };
        avatarField.style.minWidth = 300;
        avatarField.style.maxWidth = 300;
        avatarField.style.overflow = Overflow.Hidden;

        avatarField.RegisterCallback<GeometryChangedEvent>(evt =>
        {
            var label = avatarField.Q<Label>();
            if (label != null)
            {
                label.style.overflow = Overflow.Hidden;
                label.style.textOverflow = TextOverflow.Ellipsis;
            }
        });

        var warningLabel = new Label();
        warningLabel.style.color = new StyleColor(Color.red);
        warningLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        warningLabel.style.paddingLeft = 5;
        warningLabel.style.paddingTop = 2;
        warningLabel.style.display = DisplayStyle.None;

        avatarField.RegisterValueChangedCallback(evt =>
        {
            AvatarSprite = evt.newValue as Sprite;
            if (AvatarSprite != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(AvatarSprite);
                if (!assetPath.Contains("/Resources/"))
                {
                    warningLabel.text = $"⚠ WARNING: '{AvatarSprite.name}' is NOT in a Resources folder!";
                    warningLabel.style.display = DisplayStyle.Flex;
                }
                else
                {
                    warningLabel.style.display = DisplayStyle.None;
                }
            }
            else
            {
                warningLabel.style.display = DisplayStyle.None;
            }
            NotifyChange();
        });

        avatarContainer.Add(avatarField);
        avatarContainer.Add(warningLabel);
        mainContainer.Add(avatarContainer);

        if (AvatarSprite != null)
        {
            string assetPath = AssetDatabase.GetAssetPath(AvatarSprite);
            if (!assetPath.Contains("/Resources/"))
            {
                warningLabel.text = $"⚠ WARNING: '{AvatarSprite.name}' is NOT in a Resources folder!";
                warningLabel.style.display = DisplayStyle.Flex;
            }
        }
    }

    private void CreateDialogueTextField()
    {
        dialogueTextField = new TextField("Dialogue:")
        {
            value = DialogueText,
            multiline = true
        };
        dialogueTextField.style.minWidth = 300;
        dialogueTextField.style.maxWidth = 300;
        dialogueTextField.style.minHeight = 60;
        dialogueTextField.RegisterValueChangedCallback(evt =>
        {
            DialogueText = evt.newValue;
            NotifyChange();
        });
        mainContainer.Add(dialogueTextField);
    }
    #endregion

    #region Events Section
    private void CreateEventsSection()
    {
        var eventsLabel = new Label("Events (UnityEvent):");
        eventsLabel.style.marginTop = 10;
        eventsLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        mainContainer.Add(eventsLabel);

        eventsContainer = new VisualElement();
        eventsContainer.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f, 0.3f));
        eventsContainer.style.borderTopWidth = 1;
        eventsContainer.style.borderBottomWidth = 1;
        eventsContainer.style.borderLeftWidth = 1;
        eventsContainer.style.borderRightWidth = 1;
        eventsContainer.style.borderTopColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
        eventsContainer.style.borderBottomColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
        eventsContainer.style.borderLeftColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
        eventsContainer.style.borderRightColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
        eventsContainer.style.paddingTop = 5;
        eventsContainer.style.paddingBottom = 5;
        eventsContainer.style.paddingLeft = 5;
        eventsContainer.style.paddingRight = 5;
        eventsContainer.style.marginTop = 2;
        mainContainer.Add(eventsContainer);

        addEventButton = new Button(() =>
        {
            AddEventCall();
            NotifyChange();
        })
        {
            text = "+ Add Event"
        };
        addEventButton.style.marginTop = 2;
        mainContainer.Add(addEventButton);

        UpdateEventsDisplay();
    }

    private void AddEventCall()
    {
        EventCalls.Add(new DialogueEventCall());
        UpdateEventsDisplay();
    }

    private void RemoveEventCall(int index)
    {
        if (index >= 0 && index < EventCalls.Count)
        {
            EventCalls.RemoveAt(index);
            UpdateEventsDisplay();
        }
    }

    private void UpdateEventsDisplay()
    {
        eventsContainer.Clear();

        if (EventCalls.Count == 0)
        {
            var noEventsLabel = new Label("List is Empty");
            noEventsLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
            noEventsLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            noEventsLabel.style.paddingLeft = 10;
            noEventsLabel.style.paddingTop = 5;
            noEventsLabel.style.paddingBottom = 5;
            eventsContainer.Add(noEventsLabel);
            return;
        }

        for (int i = 0; i < EventCalls.Count; i++)
        {
            int currentIndex = i;
            var eventCall = EventCalls[i];

            var eventContainer = new VisualElement();
            eventContainer.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f, 0.5f));
            eventContainer.style.marginTop = 3;
            eventContainer.style.paddingTop = 5;
            eventContainer.style.paddingBottom = 5;
            eventContainer.style.paddingLeft = 5;
            eventContainer.style.paddingRight = 5;

            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;

            var titleLabel = new Label($"Event {i}");
            titleLabel.style.flexGrow = 1;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

            var removeButton = new Button(() =>
            {
                RemoveEventCall(currentIndex);
                NotifyChange();
            })
            {
                text = "×"
            };
            removeButton.style.width = 20;
            removeButton.style.height = 18;
            removeButton.style.fontSize = 12;

            titleRow.Add(titleLabel);
            titleRow.Add(removeButton);
            eventContainer.Add(titleRow);

            GameObject currentGameObject = null;
            if (!string.IsNullOrEmpty(eventCall.targetObjectName))
            {
                currentGameObject = GameObject.Find(eventCall.targetObjectName);
            }

            var gameObjectField = new ObjectField("Target GameObject:")
            {
                objectType = typeof(GameObject),
                value = currentGameObject,
                allowSceneObjects = true
            };
            gameObjectField.style.marginTop = 3;
            gameObjectField.style.maxWidth = 240;
            gameObjectField.style.overflow = Overflow.Hidden;

            gameObjectField.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                var label = gameObjectField.Q<Label>();
                if (label != null)
                {
                    label.style.overflow = Overflow.Hidden;
                    label.style.textOverflow = TextOverflow.Ellipsis;
                }
            });

            gameObjectField.RegisterValueChangedCallback(evt =>
            {
                if (currentIndex < EventCalls.Count)
                {
                    var selectedGO = evt.newValue as GameObject;
                    EventCalls[currentIndex].targetObjectName = selectedGO != null ? selectedGO.name : "";
                    UpdateEventsDisplay();
                    NotifyChange();
                }
            });
            eventContainer.Add(gameObjectField);

            if (currentGameObject != null)
            {
                var components = currentGameObject.GetComponents<Component>();
                var componentNames = new List<string> { "None" };
                var componentTypes = new List<System.Type> { null };

                foreach (var comp in components)
                {
                    if (comp != null)
                    {
                        componentNames.Add(comp.GetType().Name);
                        componentTypes.Add(comp.GetType());
                    }
                }

                int selectedEventComponentIndex = 0;
                if (!string.IsNullOrEmpty(eventCall.componentTypeName))
                {
                    selectedEventComponentIndex = componentNames.IndexOf(eventCall.componentTypeName);
                    if (selectedEventComponentIndex < 0) selectedEventComponentIndex = 0;
                }

                var eventComponentDropdown = new PopupField<string>("Component:", componentNames, selectedEventComponentIndex);
                eventComponentDropdown.style.marginTop = 3;
                eventComponentDropdown.RegisterValueChangedCallback(evt =>
                {
                    if (currentIndex < EventCalls.Count)
                    {
                        EventCalls[currentIndex].componentTypeName = evt.newValue != "None" ? evt.newValue : "";
                        UpdateEventsDisplay();
                        NotifyChange();
                    }
                });
                eventContainer.Add(eventComponentDropdown);

                if (selectedEventComponentIndex > 0 && componentTypes[selectedEventComponentIndex] != null)
                {
                    var selectedComponent = currentGameObject.GetComponent(componentTypes[selectedEventComponentIndex]);
                    if (selectedComponent != null)
                    {
                        var methods = componentTypes[selectedEventComponentIndex]
                            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
                            .Where(m => !m.IsSpecialName && m.GetParameters().Length <= 1)
                            .ToList();

                        var methodNames = new List<string> { "None" };
                        var methodInfos = new List<System.Reflection.MethodInfo> { null };

                        foreach (var method in methods)
                        {
                            var parameters = method.GetParameters();
                            if (parameters.Length == 0)
                            {
                                methodNames.Add(method.Name + " ()");
                                methodInfos.Add(method);
                            }
                            else if (parameters.Length == 1)
                            {
                                var paramType = parameters[0].ParameterType;
                                if (paramType == typeof(int) || paramType == typeof(float) ||
                                    paramType == typeof(string) || paramType == typeof(bool))
                                {
                                    methodNames.Add($"{method.Name} ({paramType.Name})");
                                    methodInfos.Add(method);
                                }
                            }
                        }

                        int selectedMethodIndex = 0;
                        if (!string.IsNullOrEmpty(eventCall.methodName))
                        {
                            for (int j = 0; j < methodInfos.Count; j++)
                            {
                                if (methodInfos[j] != null && methodInfos[j].Name == eventCall.methodName)
                                {
                                    selectedMethodIndex = j;
                                    break;
                                }
                            }
                        }

                        var methodDropdown = new PopupField<string>("Function:", methodNames, selectedMethodIndex);
                        methodDropdown.style.marginTop = 3;
                        methodDropdown.RegisterValueChangedCallback(evt =>
                        {
                            if (currentIndex < EventCalls.Count)
                            {
                                int index = methodNames.IndexOf(evt.newValue);
                                if (index > 0 && methodInfos[index] != null)
                                {
                                    var method = methodInfos[index];
                                    EventCalls[currentIndex].methodName = method.Name;

                                    var parameters = method.GetParameters();
                                    if (parameters.Length == 0)
                                    {
                                        EventCalls[currentIndex].parameterType = ParameterType.None;
                                    }
                                    else if (parameters.Length == 1)
                                    {
                                        var paramType = parameters[0].ParameterType;
                                        if (paramType == typeof(int))
                                            EventCalls[currentIndex].parameterType = ParameterType.Int;
                                        else if (paramType == typeof(float))
                                            EventCalls[currentIndex].parameterType = ParameterType.Float;
                                        else if (paramType == typeof(string))
                                            EventCalls[currentIndex].parameterType = ParameterType.String;
                                        else if (paramType == typeof(bool))
                                            EventCalls[currentIndex].parameterType = ParameterType.Bool;
                                    }
                                }
                                else
                                {
                                    EventCalls[currentIndex].methodName = "";
                                    EventCalls[currentIndex].parameterType = ParameterType.None;
                                }
                                UpdateEventsDisplay();
                                NotifyChange();
                            }
                        });
                        eventContainer.Add(methodDropdown);

                        if (selectedMethodIndex > 0 && methodInfos[selectedMethodIndex] != null)
                        {
                            var parameters = methodInfos[selectedMethodIndex].GetParameters();
                            if (parameters.Length == 1)
                            {
                                var paramContainer = new VisualElement();
                                paramContainer.style.marginTop = 3;
                                paramContainer.style.paddingLeft = 10;

                                var paramType = parameters[0].ParameterType;

                                if (paramType == typeof(string))
                                {
                                    var stringField = new TextField("Parameter:")
                                    {
                                        value = eventCall.stringParameter
                                    };
                                    stringField.RegisterValueChangedCallback(evt =>
                                    {
                                        if (currentIndex < EventCalls.Count)
                                        {
                                            EventCalls[currentIndex].stringParameter = evt.newValue;
                                            NotifyChange();
                                        }
                                    });
                                    paramContainer.Add(stringField);
                                }
                                else if (paramType == typeof(int))
                                {
                                    var intField = new IntegerField("Parameter:")
                                    {
                                        value = eventCall.intParameter
                                    };
                                    intField.RegisterValueChangedCallback(evt =>
                                    {
                                        if (currentIndex < EventCalls.Count)
                                        {
                                            EventCalls[currentIndex].intParameter = evt.newValue;
                                            NotifyChange();
                                        }
                                    });
                                    paramContainer.Add(intField);
                                }
                                else if (paramType == typeof(float))
                                {
                                    var floatField = new FloatField("Parameter:")
                                    {
                                        value = eventCall.floatParameter
                                    };
                                    floatField.RegisterValueChangedCallback(evt =>
                                    {
                                        if (currentIndex < EventCalls.Count)
                                        {
                                            EventCalls[currentIndex].floatParameter = evt.newValue;
                                            NotifyChange();
                                        }
                                    });
                                    paramContainer.Add(floatField);
                                }
                                else if (paramType == typeof(bool))
                                {
                                    var boolField = new Toggle("Parameter:")
                                    {
                                        value = eventCall.boolParameter
                                    };
                                    boolField.RegisterValueChangedCallback(evt =>
                                    {
                                        if (currentIndex < EventCalls.Count)
                                        {
                                            EventCalls[currentIndex].boolParameter = evt.newValue;
                                            NotifyChange();
                                        }
                                    });
                                    paramContainer.Add(boolField);
                                }

                                eventContainer.Add(paramContainer);
                            }
                        }
                    }
                }
            }
            else
            {
                var hintLabel = new Label("Select a GameObject first");
                hintLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
                hintLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                hintLabel.style.marginTop = 3;
                hintLabel.style.paddingLeft = 10;
                eventContainer.Add(hintLabel);
            }

            eventsContainer.Add(eventContainer);
        }
    }
    #endregion

    #region Conditional Branches Section
    private void CreateConditionalBranchesSection()
    {
        conditionalBranchesContainer = new VisualElement();
        conditionalBranchesContainer.style.marginTop = 10;
        mainContainer.Add(conditionalBranchesContainer);
    }

    private void UpdateConditionalBranchesDisplay()
    {
        conditionalBranchesContainer.Clear();

        if (!isConditionalMode || conditionalBranchesData.Count <= 1)
        {
            return;
        }

        var branchesLabel = new Label("Conditional Branches:");
        branchesLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        branchesLabel.style.marginBottom = 5;
        conditionalBranchesContainer.Add(branchesLabel);

        var sortedBranches = conditionalBranchesData.OrderByDescending(b => b.Key).ToList();

        foreach (var kvp in sortedBranches)
        {
            int priority = kvp.Key;
            if (priority == 0) continue;

            var branchData = kvp.Value;
            CreateBranchConditionEditor(priority, branchData);
        }
    }

    private void CreateBranchConditionEditor(int priority, ConditionalBranchData branchData)
    {
        var branchContainer = new VisualElement();
        branchContainer.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f, 0.5f));
        branchContainer.style.marginTop = 5;
        branchContainer.style.paddingTop = 8;
        branchContainer.style.paddingBottom = 8;
        branchContainer.style.paddingLeft = 8;
        branchContainer.style.paddingRight = 8;
        branchContainer.style.borderTopLeftRadius = 4;
        branchContainer.style.borderTopRightRadius = 4;
        branchContainer.style.borderBottomLeftRadius = 4;
        branchContainer.style.borderBottomRightRadius = 4;
        branchContainer.style.borderLeftWidth = 3;
        branchContainer.style.borderLeftColor = GetPriorityColor(priority);

        var headerRow = new VisualElement();
        headerRow.style.flexDirection = FlexDirection.Row;
        headerRow.style.alignItems = Align.Center;
        headerRow.style.marginBottom = 8;

        var branchLabel = new Label($"Priority {priority}");
        branchLabel.style.flexGrow = 1;
        branchLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        branchLabel.style.fontSize = 11;
        branchLabel.style.color = GetPriorityColor(priority);

        var addCondButton = new Button(() =>
        {
            branchData.conditions.Add(new ChoiceCondition());
            UpdateConditionalBranchesDisplay();
            NotifyChange();
        })
        {
            text = "+ Condition"
        };
        addCondButton.style.marginLeft = 5;

        headerRow.Add(branchLabel);
        headerRow.Add(addCondButton);
        branchContainer.Add(headerRow);

        for (int i = 0; i < branchData.conditions.Count; i++)
        {
            int condIndex = i;
            var condition = branchData.conditions[i];

            var condUI = CreateConditionUI(condition, () =>
            {
                branchData.conditions.RemoveAt(condIndex);
                UpdateConditionalBranchesDisplay();
                NotifyChange();
            }, (newCondition) =>
            {
                branchData.conditions[condIndex] = newCondition;
                NotifyChange();
            });

            branchContainer.Add(condUI);
        }

        if (branchData.conditions.Count > 1)
        {
            var logicRow = new VisualElement();
            logicRow.style.flexDirection = FlexDirection.Row;
            logicRow.style.marginTop = 8;
            logicRow.style.justifyContent = Justify.Center;

            var andBtn = new Button(() =>
            {
                branchData.conditionLogic = ConditionLogic.AND;
                UpdateConditionalBranchesDisplay();
                NotifyChange();
            })
            {
                text = "AND"
            };
            andBtn.style.width = 60;
            andBtn.style.backgroundColor = branchData.conditionLogic == ConditionLogic.AND
                ? new Color(0.3f, 0.5f, 0.3f) : new Color(0.25f, 0.25f, 0.25f);

            var orBtn = new Button(() =>
            {
                branchData.conditionLogic = ConditionLogic.OR;
                UpdateConditionalBranchesDisplay();
                NotifyChange();
            })
            {
                text = "OR"
            };
            orBtn.style.width = 60;
            orBtn.style.marginLeft = 5;
            orBtn.style.backgroundColor = branchData.conditionLogic == ConditionLogic.OR
                ? new Color(0.3f, 0.5f, 0.3f) : new Color(0.25f, 0.25f, 0.25f);

            logicRow.Add(andBtn);
            logicRow.Add(orBtn);
            branchContainer.Add(logicRow);
        }

        conditionalBranchesContainer.Add(branchContainer);
    }

    private VisualElement CreateConditionUI(ChoiceCondition condition, Action onRemove, Action<ChoiceCondition> onUpdate)
    {
        var condContainer = new VisualElement();
        condContainer.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
        condContainer.style.marginTop = 5;
        condContainer.style.paddingTop = 5;
        condContainer.style.paddingBottom = 5;
        condContainer.style.paddingLeft = 5;
        condContainer.style.paddingRight = 5;
        condContainer.style.borderTopLeftRadius = 3;
        condContainer.style.borderTopRightRadius = 3;
        condContainer.style.borderBottomLeftRadius = 3;
        condContainer.style.borderBottomRightRadius = 3;

        var headerRow = new VisualElement();
        headerRow.style.flexDirection = FlexDirection.Row;
        headerRow.style.alignItems = Align.Center;
        headerRow.style.marginBottom = 5;

        var condLabel = new Label("Condition");
        condLabel.style.flexGrow = 1;
        condLabel.style.fontSize = 9;
        condLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

        var removeBtn = new Button(() => onRemove())
        {
            text = "×"
        };
        removeBtn.style.width = 18;
        removeBtn.style.height = 18;
        removeBtn.style.fontSize = 11;

        headerRow.Add(condLabel);
        headerRow.Add(removeBtn);
        condContainer.Add(headerRow);

        GameObject currentGO = null;
        if (!string.IsNullOrEmpty(condition.targetObjectName))
        {
            currentGO = GameObject.Find(condition.targetObjectName);
        }

        var goField = new ObjectField("GameObject:")
        {
            objectType = typeof(GameObject),
            value = currentGO,
            allowSceneObjects = true
        };
        goField.style.fontSize = 9;
        goField.style.maxWidth = 240;
        goField.style.overflow = Overflow.Hidden;

        goField.RegisterCallback<GeometryChangedEvent>(evt =>
        {
            var label = goField.Q<Label>();
            if (label != null)
            {
                label.style.overflow = Overflow.Hidden;
                label.style.textOverflow = TextOverflow.Ellipsis;
            }
        });

        goField.RegisterValueChangedCallback(evt =>
        {
            var selectedGO = evt.newValue as GameObject;
            condition.targetObjectName = selectedGO != null ? selectedGO.name : "";
            condition.componentTypeName = "";
            condition.variableName = "";
            onUpdate(condition);
            // 重新显示这个 condition 的内容
            var parent = condContainer.parent;
            if (parent != null)
            {
                UpdateConditionalBranchesDisplay();
            }
        });
        condContainer.Add(goField);

        if (currentGO != null)
        {
            var components = currentGO.GetComponents<Component>();
            var componentNames = new List<string> { "None" };
            var componentTypes = new List<System.Type> { null };

            foreach (var comp in components)
            {
                if (comp != null)
                {
                    componentNames.Add(comp.GetType().Name);
                    componentTypes.Add(comp.GetType());
                }
            }

            int selectedComponentIndex = 0;
            if (!string.IsNullOrEmpty(condition.componentTypeName))
            {
                selectedComponentIndex = componentNames.IndexOf(condition.componentTypeName);
                if (selectedComponentIndex < 0) selectedComponentIndex = 0;
            }

            var componentDropdown = new PopupField<string>("Component:", componentNames, selectedComponentIndex);
            componentDropdown.style.marginTop = 3;
            componentDropdown.style.maxWidth = 240;
            componentDropdown.style.fontSize = 9;
            componentDropdown.RegisterValueChangedCallback(evt =>
            {
                condition.componentTypeName = evt.newValue != "None" ? evt.newValue : "";
                condition.variableName = "";
                onUpdate(condition);
                UpdateConditionalBranchesDisplay();
            });
            condContainer.Add(componentDropdown);

            if (selectedComponentIndex > 0 && componentTypes[selectedComponentIndex] != null)
            {
                var componentType = componentTypes[selectedComponentIndex];

                var fields = componentType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                    .Where(f => f.FieldType == typeof(int) || f.FieldType == typeof(float) || f.FieldType == typeof(bool))
                    .ToList();

                var properties = componentType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                    .Where(p => (p.PropertyType == typeof(int) || p.PropertyType == typeof(float) || p.PropertyType == typeof(bool)) && p.CanRead)
                    .ToList();

                var variableNames = new List<string> { "None" };
                variableNames.AddRange(fields.Select(f => f.Name));
                variableNames.AddRange(properties.Select(p => p.Name));

                if (variableNames.Count > 1)
                {
                    int selectedVarIndex = string.IsNullOrEmpty(condition.variableName) ? 0 : variableNames.IndexOf(condition.variableName);
                    if (selectedVarIndex < 0) selectedVarIndex = 0;
                    string defaultVarValue = variableNames[selectedVarIndex];

                    var varRow = new VisualElement();
                    varRow.style.flexDirection = FlexDirection.Row;
                    varRow.style.alignItems = Align.Center;
                    varRow.style.marginTop = 5;
                    varRow.style.maxWidth = 240;

                    var varLabel = new Label("Var:");
                    varLabel.style.width = 30;
                    varLabel.style.fontSize = 9;
                    varLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
                    varLabel.style.marginRight = 2;
                    varLabel.style.flexShrink = 0;

                    var varDropdown = new PopupField<string>(variableNames, defaultVarValue);
                    varDropdown.style.width = 70;
                    varDropdown.style.marginRight = 2;
                    varDropdown.style.fontSize = 9;
                    varDropdown.style.flexShrink = 0;
                    varDropdown.RegisterValueChangedCallback(evt =>
                    {
                        condition.variableName = evt.newValue == "None" ? "" : evt.newValue;
                        onUpdate(condition);
                        UpdateConditionalBranchesDisplay();
                    });

                    varRow.Add(varLabel);
                    varRow.Add(varDropdown);

                    if (selectedVarIndex > 0)
                    {
                        System.Type selectedVarType = null;
                        string selectedVarName = defaultVarValue;

                        var field = fields.FirstOrDefault(f => f.Name == selectedVarName);
                        if (field != null)
                        {
                            selectedVarType = field.FieldType;
                        }
                        else
                        {
                            var property = properties.FirstOrDefault(p => p.Name == selectedVarName);
                            if (property != null)
                            {
                                selectedVarType = property.PropertyType;
                            }
                        }

                        List<ComparisonType> comparisonTypes;
                        if (selectedVarType == typeof(bool))
                        {
                            comparisonTypes = new List<ComparisonType>
                            {
                                ComparisonType.Equal,
                                ComparisonType.NotEqual
                            };
                        }
                        else
                        {
                            comparisonTypes = new List<ComparisonType>
                            {
                                ComparisonType.Equal,
                                ComparisonType.NotEqual,
                                ComparisonType.Greater,
                                ComparisonType.Less,
                                ComparisonType.GreaterOrEqual,
                                ComparisonType.LessOrEqual
                            };
                        }

                        var comparisonNames = comparisonTypes.Select(c => GetComparisonDisplayName(c)).ToList();

                        int selectedCompIndex = comparisonTypes.IndexOf(condition.comparison);
                        if (selectedCompIndex < 0) selectedCompIndex = 0;
                        string defaultCompValue = comparisonNames[selectedCompIndex];

                        var compDropdown = new PopupField<string>(comparisonNames, defaultCompValue);
                        compDropdown.style.width = 50;
                        compDropdown.style.marginRight = 2;
                        compDropdown.style.fontSize = 9;
                        compDropdown.style.flexShrink = 0;
                        compDropdown.RegisterValueChangedCallback(evt =>
                        {
                            int index = comparisonNames.IndexOf(evt.newValue);
                            condition.comparison = comparisonTypes[index];
                            onUpdate(condition);
                        });

                        varRow.Add(compDropdown);

                        if (selectedVarType == typeof(bool))
                        {
                            var boolValues = new List<string> { "True", "False" };
                            string defaultBoolValue = "True";

                            if (!string.IsNullOrEmpty(condition.compareValue))
                            {
                                if (condition.compareValue.Equals("False", System.StringComparison.OrdinalIgnoreCase))
                                {
                                    defaultBoolValue = "False";
                                }
                            }
                            else
                            {
                                condition.compareValue = "True";
                            }

                            var boolDropdown = new PopupField<string>(boolValues, defaultBoolValue);
                            boolDropdown.style.flexGrow = 1;
                            boolDropdown.style.flexShrink = 1;
                            boolDropdown.style.minWidth = 40;
                            boolDropdown.style.fontSize = 9;
                            boolDropdown.RegisterValueChangedCallback(evt =>
                            {
                                condition.compareValue = evt.newValue;
                                onUpdate(condition);
                            });

                            varRow.Add(boolDropdown);
                        }
                        else
                        {
                            var valueField = new TextField();
                            valueField.value = condition.compareValue;
                            valueField.style.flexGrow = 1;
                            valueField.style.flexShrink = 1;
                            valueField.style.minWidth = 40;
                            valueField.style.fontSize = 9;
                            valueField.RegisterValueChangedCallback(evt =>
                            {
                                condition.compareValue = evt.newValue;
                                onUpdate(condition);
                            });

                            varRow.Add(valueField);
                        }
                    }

                    condContainer.Add(varRow);
                }
                else
                {
                    var noVarsLabel = new Label("No public int/float/bool variables found");
                    noVarsLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
                    noVarsLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                    noVarsLabel.style.marginTop = 5;
                    noVarsLabel.style.paddingLeft = 10;
                    noVarsLabel.style.fontSize = 9;
                    condContainer.Add(noVarsLabel);
                }
            }
        }
        else
        {
            var hintLabel = new Label("Select a GameObject first");
            hintLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
            hintLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            hintLabel.style.marginTop = 5;
            hintLabel.style.paddingLeft = 10;
            hintLabel.style.fontSize = 9;
            condContainer.Add(hintLabel);
        }

        return condContainer;
    }

    private Color GetPriorityColor(int priority)
    {
        var colors = new Color[]
        {
            new Color(0.4f, 0.7f, 1f),
            new Color(0.5f, 1f, 0.5f),
            new Color(1f, 0.8f, 0.4f),
            new Color(1f, 0.5f, 0.8f),
            new Color(0.8f, 0.5f, 1f),
        };
        return colors[(priority - 1) % colors.Length];
    }
    #endregion

    #region Choices Section
    private void CreateChoicesSection()
    {
        var choicesLabel = new Label("Player Choices:");
        choicesLabel.style.marginTop = 10;
        choicesLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        mainContainer.Add(choicesLabel);

        choicesContainer = new VisualElement();
        mainContainer.Add(choicesContainer);

        addChoiceButton = new Button(() =>
        {
            AddChoice(new ChoiceData { text = "New Choice" });
            NotifyChange();
        })
        {
            text = "Add Choice"
        };
        addChoiceButton.style.marginTop = 5;
        mainContainer.Add(addChoiceButton);
    }

    private void AddChoice(ChoiceData choiceData)
    {
        int index = ChoicesData.Count;
        ChoicesData.Add(choiceData);
        RebuildChoiceUI(index);
        RefreshExpandedState();
        RefreshPorts();
    }

    private void RemoveChoice(int index)
    {
        if (index >= 0 && index < ChoicesData.Count)
        {
            var graphView = GetFirstAncestorOfType<DialogueGraphView>();

            // 保存所有 choice 的连接信息（除了要删除的）
            var connectionData = new Dictionary<int, Port>();
            for (int i = 0; i < choiceOutputPorts.Count; i++)
            {
                if (i == index) continue; // 跳过要删除的

                var port = choiceOutputPorts[i];
                if (port != null && port.connected)
                {
                    var edge = port.connections.FirstOrDefault();
                    if (edge != null && edge.input != null)
                    {
                        // 保存连接的目标端口
                        connectionData[i] = edge.input;
                    }
                }
            }

            // 删除要移除的 choice 的连线
            if (index < choiceOutputPorts.Count)
            {
                var port = choiceOutputPorts[index];
                if (graphView != null && port != null)
                {
                    var edges = port.connections.ToList();
                    foreach (var edge in edges)
                    {
                        graphView.RemoveElement(edge);
                    }
                }
            }

            // 删除数据
            ChoicesData.RemoveAt(index);

            // 清空并重建 UI
            choicesContainer.Clear();
            choiceOutputPorts.Clear();

            for (int i = 0; i < ChoicesData.Count; i++)
            {
                RebuildChoiceUI(i);
            }

            // 恢复其他 choice 的连接
            foreach (var kvp in connectionData)
            {
                int oldIndex = kvp.Key;
                Port targetPort = kvp.Value;

                // 计算新的索引（如果在删除位置之后，索引需要减1）
                int newIndex = oldIndex > index ? oldIndex - 1 : oldIndex;

                if (newIndex >= 0 && newIndex < choiceOutputPorts.Count && graphView != null)
                {
                    var newOutputPort = choiceOutputPorts[newIndex];
                    if (newOutputPort != null && targetPort != null)
                    {
                        var newEdge = newOutputPort.ConnectTo(targetPort);
                        graphView.AddElement(newEdge);
                    }
                }
            }

            RefreshExpandedState();
            RefreshPorts();
        }
    }

    private void RebuildChoiceUI(int index)
    {
        var choiceContainer = new VisualElement();
        choiceContainer.style.marginTop = 10;
        choiceContainer.style.marginBottom = 5;
        choiceContainer.style.borderLeftWidth = 3;
        choiceContainer.style.borderLeftColor = GetChoiceColor(index);
        choiceContainer.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f, 0.5f));
        choiceContainer.style.paddingTop = 8;
        choiceContainer.style.paddingBottom = 8;
        choiceContainer.style.paddingLeft = 8;
        choiceContainer.style.paddingRight = 8;
        choiceContainer.style.borderTopLeftRadius = 4;
        choiceContainer.style.borderTopRightRadius = 4;
        choiceContainer.style.borderBottomLeftRadius = 4;
        choiceContainer.style.borderBottomRightRadius = 4;

        var outputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
        outputPort.portName = ChoicesData[index].text;
        outputPort.userData = -2 - index;
        outputPort.portColor = GetChoiceColorRaw(index);
        choiceOutputPorts.Add(outputPort);

        var portRow = new VisualElement();
        portRow.style.flexDirection = FlexDirection.Row;
        portRow.style.alignItems = Align.Center;
        portRow.style.justifyContent = Justify.SpaceBetween; // 标签在左，端口在右
        portRow.style.marginBottom = 8;

        var portLabel = new Label($"Choice {index + 1}:");
        portLabel.style.fontSize = 11;
        portLabel.style.color = GetChoiceColor(index);
        portLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

        portRow.Add(portLabel);
        portRow.Add(outputPort);
        choiceContainer.Add(portRow);

        var inputRow = new VisualElement();
        inputRow.style.flexDirection = FlexDirection.Row;
        inputRow.style.alignItems = Align.Center;
        inputRow.style.marginBottom = 5;

        var textLabel = new Label("Text:");
        textLabel.style.minWidth = 40;
        textLabel.style.maxWidth = 40;
        textLabel.style.fontSize = 10;
        textLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
        textLabel.style.marginRight = 5;

        var choiceField = new TextField();
        choiceField.value = ChoicesData[index].text;
        choiceField.style.flexGrow = 1;
        choiceField.style.flexShrink = 1;
        choiceField.style.minWidth = 80;

        int currentIndex = index;
        choiceField.RegisterValueChangedCallback(evt =>
        {
            if (currentIndex < ChoicesData.Count)
            {
                ChoicesData[currentIndex].text = evt.newValue;
                if (currentIndex < choiceOutputPorts.Count)
                {
                    choiceOutputPorts[currentIndex].portName = evt.newValue;
                }
                NotifyChange();
            }
        });

        var removeButton = new Button(() =>
        {
            RemoveChoice(currentIndex);
            NotifyChange();
        })
        {
            text = "×"
        };
        removeButton.style.minWidth = 22;
        removeButton.style.maxWidth = 22;
        removeButton.style.minHeight = 22;
        removeButton.style.maxHeight = 22;
        removeButton.style.marginLeft = 5;
        removeButton.style.fontSize = 14;
        removeButton.style.backgroundColor = new StyleColor(new Color(0.6f, 0.2f, 0.2f));
        removeButton.style.flexShrink = 0;

        inputRow.Add(textLabel);
        inputRow.Add(choiceField);
        inputRow.Add(removeButton);
        choiceContainer.Add(inputRow);

        var conditionsHeaderRow = new VisualElement();
        conditionsHeaderRow.style.flexDirection = FlexDirection.Row;
        conditionsHeaderRow.style.alignItems = Align.Center;
        conditionsHeaderRow.style.marginTop = 5;

        var conditionsLabel = new Label("Conditions:");
        conditionsLabel.style.fontSize = 9;
        conditionsLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
        conditionsLabel.style.flexGrow = 1;

        var addCondButton = new Button(() =>
        {
            if (currentIndex < ChoicesData.Count)
            {
                ChoicesData[currentIndex].conditions.Add(new ChoiceCondition());
                UpdateChoiceConditionsDisplay(choiceContainer, currentIndex);
                NotifyChange();
            }
        })
        {
            text = "+"
        };
        addCondButton.style.minWidth = 18;
        addCondButton.style.maxWidth = 18;
        addCondButton.style.minHeight = 18;
        addCondButton.style.maxHeight = 18;
        addCondButton.style.fontSize = 12;
        addCondButton.style.flexShrink = 0;

        conditionsHeaderRow.Add(conditionsLabel);
        conditionsHeaderRow.Add(addCondButton);
        choiceContainer.Add(conditionsHeaderRow);

        var conditionsContent = new VisualElement();
        conditionsContent.name = "conditionsContent";
        choiceContainer.Add(conditionsContent);

        UpdateChoiceConditionsDisplay(choiceContainer, currentIndex);

        choicesContainer.Add(choiceContainer);
    }

    private void UpdateChoiceConditionsDisplay(VisualElement choiceContainer, int choiceIndex)
    {
        if (choiceIndex >= ChoicesData.Count) return;

        var conditionsContent = choiceContainer.Q("conditionsContent");
        if (conditionsContent == null) return;

        conditionsContent.Clear();

        var choiceData = ChoicesData[choiceIndex];

        if (choiceData.conditions.Count == 0)
        {
            var emptyLabel = new Label("(no conditions)");
            emptyLabel.style.fontSize = 8;
            emptyLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            emptyLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            emptyLabel.style.marginTop = 2;
            emptyLabel.style.marginLeft = 10;
            conditionsContent.Add(emptyLabel);
            return;
        }

        conditionsContent.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f, 0.8f));
        conditionsContent.style.paddingTop = 8;
        conditionsContent.style.paddingBottom = 8;
        conditionsContent.style.paddingLeft = 8;
        conditionsContent.style.paddingRight = 8;
        conditionsContent.style.marginTop = 3;
        conditionsContent.style.borderTopLeftRadius = 3;
        conditionsContent.style.borderTopRightRadius = 3;
        conditionsContent.style.borderBottomLeftRadius = 3;
        conditionsContent.style.borderBottomRightRadius = 3;

        for (int i = 0; i < choiceData.conditions.Count; i++)
        {
            int condIndex = i;
            var condition = choiceData.conditions[i];

            var condContainer = new VisualElement();
            condContainer.style.marginTop = i > 0 ? 5 : 0;
            condContainer.style.paddingTop = 8;
            condContainer.style.paddingBottom = 8;
            condContainer.style.paddingLeft = 8;
            condContainer.style.paddingRight = 8;
            condContainer.style.backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.12f));
            condContainer.style.borderTopLeftRadius = 3;
            condContainer.style.borderTopRightRadius = 3;
            condContainer.style.borderBottomLeftRadius = 3;
            condContainer.style.borderBottomRightRadius = 3;

            var condHeader = new VisualElement();
            condHeader.style.flexDirection = FlexDirection.Row;
            condHeader.style.alignItems = Align.Center;
            condHeader.style.marginBottom = 8;

            var condLabel = new Label($"Condition {i + 1}");
            condLabel.style.flexGrow = 1;
            condLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            condLabel.style.fontSize = 10;
            condLabel.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f));

            var removeCondButton = new Button(() =>
            {
                if (choiceIndex < ChoicesData.Count)
                {
                    ChoicesData[choiceIndex].conditions.RemoveAt(condIndex);
                    UpdateChoiceConditionsDisplay(choiceContainer, choiceIndex);
                    NotifyChange();
                }
            })
            {
                text = "×"
            };
            removeCondButton.style.width = 18;
            removeCondButton.style.height = 18;
            removeCondButton.style.fontSize = 12;

            condHeader.Add(condLabel);
            condHeader.Add(removeCondButton);
            condContainer.Add(condHeader);

            GameObject currentGameObject = null;
            if (!string.IsNullOrEmpty(condition.targetObjectName))
            {
                currentGameObject = GameObject.Find(condition.targetObjectName);
            }

            var gameObjectField = new ObjectField("GameObject:")
            {
                objectType = typeof(GameObject),
                value = currentGameObject,
                allowSceneObjects = true
            };
            gameObjectField.style.marginTop = 3;
            gameObjectField.style.maxWidth = 240;
            gameObjectField.style.overflow = Overflow.Hidden;

            // 找到内部的 Label 并设置文本省略
            gameObjectField.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                var label = gameObjectField.Q<Label>();
                if (label != null)
                {
                    label.style.overflow = Overflow.Hidden;
                    label.style.textOverflow = TextOverflow.Ellipsis;
                }
            });

            gameObjectField.RegisterValueChangedCallback(evt =>
            {
                if (choiceIndex < ChoicesData.Count && condIndex < ChoicesData[choiceIndex].conditions.Count)
                {
                    var selectedGO = evt.newValue as GameObject;
                    ChoicesData[choiceIndex].conditions[condIndex].targetObjectName = selectedGO != null ? selectedGO.name : "";
                    ChoicesData[choiceIndex].conditions[condIndex].componentTypeName = "";
                    ChoicesData[choiceIndex].conditions[condIndex].variableName = "";
                    UpdateChoiceConditionsDisplay(choiceContainer, choiceIndex);
                    NotifyChange();
                }
            });
            condContainer.Add(gameObjectField);

            if (currentGameObject != null)
            {
                var components = currentGameObject.GetComponents<Component>();
                var componentNames = new List<string> { "None" };
                var componentTypes = new List<System.Type> { null };

                foreach (var comp in components)
                {
                    if (comp != null)
                    {
                        componentNames.Add(comp.GetType().Name);
                        componentTypes.Add(comp.GetType());
                    }
                }

                int selectedChoiceComponentIndex = 0;
                if (!string.IsNullOrEmpty(condition.componentTypeName))
                {
                    selectedChoiceComponentIndex = componentNames.IndexOf(condition.componentTypeName);
                    if (selectedChoiceComponentIndex < 0) selectedChoiceComponentIndex = 0;
                }

                var choiceComponentDropdown = new PopupField<string>("Component:", componentNames, selectedChoiceComponentIndex);
                choiceComponentDropdown.style.marginTop = 3;
                choiceComponentDropdown.style.maxWidth = 240;
                choiceComponentDropdown.RegisterValueChangedCallback(evt =>
                {
                    if (choiceIndex < ChoicesData.Count && condIndex < ChoicesData[choiceIndex].conditions.Count)
                    {
                        ChoicesData[choiceIndex].conditions[condIndex].componentTypeName = evt.newValue != "None" ? evt.newValue : "";
                        ChoicesData[choiceIndex].conditions[condIndex].variableName = "";
                        UpdateChoiceConditionsDisplay(choiceContainer, choiceIndex);
                        NotifyChange();
                    }
                });
                condContainer.Add(choiceComponentDropdown);

                if (selectedChoiceComponentIndex > 0 && componentTypes[selectedChoiceComponentIndex] != null)
                {
                    var componentType = componentTypes[selectedChoiceComponentIndex];

                    var fields = componentType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                        .Where(f => f.FieldType == typeof(int) || f.FieldType == typeof(float) || f.FieldType == typeof(bool))
                        .ToList();

                    var properties = componentType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                        .Where(p => (p.PropertyType == typeof(int) || p.PropertyType == typeof(float) || p.PropertyType == typeof(bool)) && p.CanRead)
                        .ToList();

                    var variableNames = new List<string> { "None" };
                    variableNames.AddRange(fields.Select(f => f.Name));
                    variableNames.AddRange(properties.Select(p => p.Name));

                    if (variableNames.Count > 1)
                    {
                        int selectedChoiceVarIndex = string.IsNullOrEmpty(condition.variableName) ? 0 : variableNames.IndexOf(condition.variableName);
                        if (selectedChoiceVarIndex < 0) selectedChoiceVarIndex = 0;
                        string defaultVarValue = variableNames[selectedChoiceVarIndex];

                        // Variable row - 带标签的紧凑布局
                        var varRow = new VisualElement();
                        varRow.style.flexDirection = FlexDirection.Row;
                        varRow.style.alignItems = Align.Center;
                        varRow.style.marginTop = 5;
                        varRow.style.maxWidth = 240;

                        var varLabel = new Label("Var:");
                        varLabel.style.width = 30;
                        varLabel.style.fontSize = 9;
                        varLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
                        varLabel.style.marginRight = 2;
                        varLabel.style.flexShrink = 0;

                        var varDropdown = new PopupField<string>(variableNames, defaultVarValue);
                        varDropdown.style.width = 70;
                        varDropdown.style.marginRight = 2;
                        varDropdown.style.fontSize = 9;
                        varDropdown.style.flexShrink = 0;
                        varDropdown.RegisterValueChangedCallback(evt =>
                        {
                            if (choiceIndex < ChoicesData.Count && condIndex < ChoicesData[choiceIndex].conditions.Count)
                            {
                                ChoicesData[choiceIndex].conditions[condIndex].variableName = evt.newValue == "None" ? "" : evt.newValue;
                                UpdateChoiceConditionsDisplay(choiceContainer, choiceIndex);
                                NotifyChange();
                            }
                        });

                        varRow.Add(varLabel);
                        varRow.Add(varDropdown);

                        if (selectedChoiceVarIndex > 0)
                        {
                            System.Type selectedVarType = null;
                            string selectedVarName = defaultVarValue;

                            var field = fields.FirstOrDefault(f => f.Name == selectedVarName);
                            if (field != null)
                            {
                                selectedVarType = field.FieldType;
                            }
                            else
                            {
                                var property = properties.FirstOrDefault(p => p.Name == selectedVarName);
                                if (property != null)
                                {
                                    selectedVarType = property.PropertyType;
                                }
                            }

                            List<ComparisonType> comparisonTypes;
                            if (selectedVarType == typeof(bool))
                            {
                                comparisonTypes = new List<ComparisonType>
                                {
                                    ComparisonType.Equal,
                                    ComparisonType.NotEqual
                                };
                            }
                            else
                            {
                                comparisonTypes = new List<ComparisonType>
                                {
                                    ComparisonType.Equal,
                                    ComparisonType.NotEqual,
                                    ComparisonType.Greater,
                                    ComparisonType.Less,
                                    ComparisonType.GreaterOrEqual,
                                    ComparisonType.LessOrEqual
                                };
                            }

                            var comparisonNames = comparisonTypes.Select(c => GetComparisonDisplayName(c)).ToList();

                            int selectedChoiceCompIndex = comparisonTypes.IndexOf(condition.comparison);
                            if (selectedChoiceCompIndex < 0) selectedChoiceCompIndex = 0;
                            string defaultCompValue = comparisonNames[selectedChoiceCompIndex];

                            var compDropdown = new PopupField<string>(comparisonNames, defaultCompValue);
                            compDropdown.style.width = 50;
                            compDropdown.style.marginRight = 2;
                            compDropdown.style.fontSize = 9;
                            compDropdown.style.flexShrink = 0;
                            compDropdown.RegisterValueChangedCallback(evt =>
                            {
                                if (choiceIndex < ChoicesData.Count && condIndex < ChoicesData[choiceIndex].conditions.Count)
                                {
                                    int index = comparisonNames.IndexOf(evt.newValue);
                                    ChoicesData[choiceIndex].conditions[condIndex].comparison = comparisonTypes[index];
                                    NotifyChange();
                                }
                            });

                            varRow.Add(compDropdown);

                            if (selectedVarType == typeof(bool))
                            {
                                var boolValues = new List<string> { "True", "False" };
                                string defaultBoolValue = "True";

                                if (!string.IsNullOrEmpty(condition.compareValue))
                                {
                                    if (condition.compareValue.Equals("False", System.StringComparison.OrdinalIgnoreCase))
                                    {
                                        defaultBoolValue = "False";
                                    }
                                }
                                else
                                {
                                    ChoicesData[choiceIndex].conditions[condIndex].compareValue = "True";
                                }

                                var boolDropdown = new PopupField<string>(boolValues, defaultBoolValue);
                                boolDropdown.style.flexGrow = 1;
                                boolDropdown.style.flexShrink = 1;
                                boolDropdown.style.minWidth = 40;
                                boolDropdown.style.fontSize = 9;
                                boolDropdown.RegisterValueChangedCallback(evt =>
                                {
                                    if (choiceIndex < ChoicesData.Count && condIndex < ChoicesData[choiceIndex].conditions.Count)
                                    {
                                        ChoicesData[choiceIndex].conditions[condIndex].compareValue = evt.newValue;
                                        NotifyChange();
                                    }
                                });

                                varRow.Add(boolDropdown);
                            }
                            else
                            {
                                var valueField = new TextField();
                                valueField.value = condition.compareValue;
                                valueField.style.flexGrow = 1;
                                valueField.style.flexShrink = 1;
                                valueField.style.minWidth = 40;
                                valueField.style.fontSize = 9;
                                valueField.RegisterValueChangedCallback(evt =>
                                {
                                    if (choiceIndex < ChoicesData.Count && condIndex < ChoicesData[choiceIndex].conditions.Count)
                                    {
                                        ChoicesData[choiceIndex].conditions[condIndex].compareValue = evt.newValue;
                                        NotifyChange();
                                    }
                                });

                                varRow.Add(valueField);
                            }
                        }

                        condContainer.Add(varRow);
                    }
                    else
                    {
                        var noVarsLabel = new Label("No public int/float/bool variables found");
                        noVarsLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
                        noVarsLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                        noVarsLabel.style.marginTop = 5;
                        noVarsLabel.style.paddingLeft = 10;
                        noVarsLabel.style.fontSize = 9;
                        condContainer.Add(noVarsLabel);
                    }
                }
            }
            else
            {
                var hintLabel = new Label("Select a GameObject first");
                hintLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
                hintLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                hintLabel.style.marginTop = 5;
                hintLabel.style.paddingLeft = 10;
                hintLabel.style.fontSize = 9;
                condContainer.Add(hintLabel);
            }

            conditionsContent.Add(condContainer);
        }

        if (choiceData.conditions.Count > 1)
        {
            var logicRow = new VisualElement();
            logicRow.style.flexDirection = FlexDirection.Row;
            logicRow.style.marginTop = 8;
            logicRow.style.alignItems = Align.Center;
            logicRow.style.justifyContent = Justify.Center;

            var andToggle = new Button(() =>
            {
                if (choiceIndex < ChoicesData.Count)
                {
                    ChoicesData[choiceIndex].conditionLogic = ConditionLogic.AND;
                    UpdateChoiceConditionsDisplay(choiceContainer, choiceIndex);
                    NotifyChange();
                }
            })
            {
                text = "AND"
            };
            andToggle.style.width = 60;
            andToggle.style.height = 22;
            andToggle.style.fontSize = 10;
            andToggle.style.unityFontStyleAndWeight = choiceData.conditionLogic == ConditionLogic.AND ?
                FontStyle.Bold : FontStyle.Normal;
            andToggle.style.backgroundColor = choiceData.conditionLogic == ConditionLogic.AND ?
                new StyleColor(new Color(0.3f, 0.5f, 0.3f)) :
                new StyleColor(new Color(0.25f, 0.25f, 0.25f));

            var orToggle = new Button(() =>
            {
                if (choiceIndex < ChoicesData.Count)
                {
                    ChoicesData[choiceIndex].conditionLogic = ConditionLogic.OR;
                    UpdateChoiceConditionsDisplay(choiceContainer, choiceIndex);
                    NotifyChange();
                }
            })
            {
                text = "OR"
            };
            orToggle.style.width = 60;
            orToggle.style.height = 22;
            orToggle.style.fontSize = 10;
            orToggle.style.marginLeft = 5;
            orToggle.style.unityFontStyleAndWeight = choiceData.conditionLogic == ConditionLogic.OR ?
                FontStyle.Bold : FontStyle.Normal;
            orToggle.style.backgroundColor = choiceData.conditionLogic == ConditionLogic.OR ?
                new StyleColor(new Color(0.3f, 0.5f, 0.3f)) :
                new StyleColor(new Color(0.25f, 0.25f, 0.25f));

            logicRow.Add(andToggle);
            logicRow.Add(orToggle);
            conditionsContent.Add(logicRow);
        }
    }

    private StyleColor GetChoiceColor(int index)
    {
        return new StyleColor(GetChoiceColorRaw(index));
    }

    private Color GetChoiceColorRaw(int index)
    {
        var colors = new Color[]
        {
            new Color(0.4f, 0.7f, 1f),
            new Color(0.5f, 1f, 0.5f),
            new Color(1f, 0.8f, 0.4f),
            new Color(1f, 0.5f, 0.8f),
            new Color(0.8f, 0.5f, 1f),
            new Color(0.5f, 1f, 1f),
        };

        return colors[index % colors.Length];
    }
    #endregion

    #region Conditional Branch Methods
    private void OnAddBranch()
    {
        if (!isConditionalMode)
        {
            ConvertToConditionalMode();
        }

        AddConditionalBranch(nextBranchPriority++);
        NotifyChange();
    }

    private void ConvertToConditionalMode()
    {
        isConditionalMode = true;

        // 保存原有的连接
        var graphView = GetFirstAncestorOfType<DialogueGraphView>();
        Edge existingEdge = null;
        Port existingTargetPort = null;

        if (defaultOutputPort != null && defaultOutputPort.connected)
        {
            existingEdge = defaultOutputPort.connections.FirstOrDefault();
            if (existingEdge != null)
            {
                existingTargetPort = existingEdge.input;
                // 先删除旧的连接
                if (graphView != null)
                {
                    graphView.RemoveElement(existingEdge);
                }
            }
        }

        // 移除旧的默认端口
        var oldPortParent = defaultOutputPort.parent;
        if (oldPortParent != null)
        {
            outputContainer.Remove(oldPortParent);
        }

        // 创建新的输出行，带有正确的布局
        var outputRow = new VisualElement();
        outputRow.style.flexDirection = FlexDirection.Row;
        outputRow.style.alignItems = Align.Center;
        outputRow.style.justifyContent = Justify.SpaceBetween;
        outputRow.style.width = Length.Percent(100);

        // 创建新的默认端口
        defaultOutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
        defaultOutputPort.portName = "Default";
        defaultOutputPort.userData = 0;
        defaultOutputPort.style.flexGrow = 1;

        // 创建加号按钮，并让它靠右
        var addBranchButton = new Button(OnAddBranch)
        {
            text = "+"
        };
        addBranchButton.style.width = 20;
        addBranchButton.style.height = 20;
        addBranchButton.style.fontSize = 14;
        addBranchButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        addBranchButton.style.flexShrink = 0;

        outputRow.Add(defaultOutputPort);
        outputRow.Add(addBranchButton);
        outputContainer.Insert(0, outputRow);

        conditionalBranchesData[0] = new ConditionalBranchData
        {
            priority = 0,
            conditions = new List<ChoiceCondition>(),
            conditionLogic = ConditionLogic.AND
        };

        // 恢复原有连接到新的default端口
        if (existingTargetPort != null && graphView != null)
        {
            var newEdge = defaultOutputPort.ConnectTo(existingTargetPort);
            graphView.AddElement(newEdge);
        }

        UpdateConditionalBranchesDisplay();
        RefreshExpandedState();
        RefreshPorts();
    }

    private void AddConditionalBranch(int priority)
    {
        // 创建带有正确布局的容器
        var container = new VisualElement();
        container.style.flexDirection = FlexDirection.Row;
        container.style.alignItems = Align.Center;
        container.style.justifyContent = Justify.SpaceBetween;
        container.style.width = Length.Percent(100);

        var port = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
        port.portName = $"Priority {priority}";
        port.userData = priority;
        port.style.flexGrow = 1;

        // 修复：删除按钮动态读取端口的当前 priority，而不是使用闭包捕获的值
        var removeBtn = new Button(() =>
        {
            // 从端口的 userData 读取当前的 priority
            if (port != null && port.userData is int currentPriority)
            {
                RemoveBranch(currentPriority);
            }
        })
        {
            text = "×"
        };
        removeBtn.style.width = 20;
        removeBtn.style.height = 20;
        removeBtn.style.fontSize = 14;
        removeBtn.style.backgroundColor = new StyleColor(new Color(0.6f, 0.2f, 0.2f));
        removeBtn.style.flexShrink = 0;

        container.Add(port);
        container.Add(removeBtn);

        outputContainer.Insert(outputContainer.childCount - 1, container);
        conditionalPorts.Add(port);

        conditionalBranchesData[priority] = new ConditionalBranchData
        {
            priority = priority,
            conditions = new List<ChoiceCondition>(),
            conditionLogic = ConditionLogic.AND
        };

        UpdateConditionalBranchesDisplay();
        RefreshExpandedState();
        RefreshPorts();
    }

    private void RemoveBranch(int priorityToRemove)
    {
        var graphView = GetFirstAncestorOfType<DialogueGraphView>();
        var portToRemove = conditionalPorts.FirstOrDefault(p => p != null && (int)p.userData == priorityToRemove);

        if (portToRemove != null)
        {
            // 第一步：彻底清除所有连接
            if (portToRemove.connected)
            {
                // 复制连接列表，避免在迭代时修改
                var edges = portToRemove.connections.ToList();

                foreach (var edge in edges)
                {
                    if (edge == null) continue;

                    // 从两端断开连接
                    if (edge.output != null && edge.output.connected)
                    {
                        edge.output.Disconnect(edge);
                    }
                    if (edge.input != null && edge.input.connected)
                    {
                        edge.input.Disconnect(edge);
                    }

                    // 从图中移除edge
                    if (graphView != null)
                    {
                        graphView.RemoveElement(edge);
                    }
                }

                // 再次确保端口断开所有连接
                portToRemove.DisconnectAll();
            }

            // 第二步：从UI中移除端口容器
            var container = portToRemove.parent;
            if (container != null && container.parent != null)
            {
                container.parent.Remove(container);
            }

            // 第三步：从列表中移除端口引用
            conditionalPorts.Remove(portToRemove);

            // 清空端口引用
            portToRemove = null;
        }

        // 第四步：从数据字典中移除
        if (conditionalBranchesData.ContainsKey(priorityToRemove))
        {
            conditionalBranchesData.Remove(priorityToRemove);
        }

        // 第五步：重新整理剩余的分支
        if (conditionalPorts.Count == 0)
        {
            // 如果没有条件分支了，转回默认模式
            ConvertToDefaultMode();
            return;
        }

        // 重新排列剩余的优先级
        var remainingPorts = conditionalPorts.Where(p => p != null).OrderByDescending(p => (int)p.userData).ToList();
        conditionalPorts.Clear();

        var newBranchesData = new Dictionary<int, ConditionalBranchData>();

        // 保留 default 分支
        if (conditionalBranchesData.ContainsKey(0))
        {
            newBranchesData[0] = conditionalBranchesData[0];
        }

        int newPriority = 1;
        foreach (var port in remainingPorts)
        {
            int oldPriority = (int)port.userData;

            if (oldPriority > priorityToRemove)
            {
                // 需要降低优先级
                int adjustedPriority = oldPriority - 1;
                port.userData = adjustedPriority;
                port.portName = $"Priority {adjustedPriority}";

                if (conditionalBranchesData.ContainsKey(oldPriority))
                {
                    newBranchesData[adjustedPriority] = conditionalBranchesData[oldPriority];
                    newBranchesData[adjustedPriority].priority = adjustedPriority;
                }
            }
            else
            {
                // 不需要调整
                if (conditionalBranchesData.ContainsKey(oldPriority))
                {
                    newBranchesData[oldPriority] = conditionalBranchesData[oldPriority];
                }
            }

            conditionalPorts.Add(port);
            newPriority = Math.Max(newPriority, (int)port.userData + 1);
        }

        conditionalBranchesData = newBranchesData;
        nextBranchPriority = newPriority;

        // 第六步：刷新UI
        UpdateConditionalBranchesDisplay();
        RefreshExpandedState();
        RefreshPorts();
        NotifyChange();
    }

    private void ConvertToDefaultMode()
    {
        isConditionalMode = false;

        // 保存default端口的连接
        var graphView = GetFirstAncestorOfType<DialogueGraphView>();
        Edge existingEdge = null;
        Port existingTargetPort = null;

        if (defaultOutputPort != null && defaultOutputPort.connected)
        {
            existingEdge = defaultOutputPort.connections.FirstOrDefault();
            if (existingEdge != null)
            {
                existingTargetPort = existingEdge.input;
                // 先删除旧的连接
                if (graphView != null)
                {
                    graphView.RemoveElement(existingEdge);
                }
            }
        }

        conditionalBranchesData.Clear();
        nextBranchPriority = 1;

        var oldPortParent = defaultOutputPort.parent;
        if (oldPortParent != null)
        {
            outputContainer.Remove(oldPortParent);
        }

        CreateOutputPortWithAddButton();

        // 恢复连接
        if (existingTargetPort != null && graphView != null)
        {
            var newEdge = defaultOutputPort.ConnectTo(existingTargetPort);
            graphView.AddElement(newEdge);
        }

        UpdateConditionalBranchesDisplay();
        RefreshExpandedState();
        RefreshPorts();
    }

    public bool IsConditionalMode()
    {
        return isConditionalMode;
    }

    public int GetBranchPriorityForPort(Port port)
    {
        if (port == defaultOutputPort)
        {
            return isConditionalMode ? 0 : -1;
        }

        if (port.userData is int priority)
        {
            return priority;
        }

        return -1;
    }

    public Port GetConditionalPort(int priority)
    {
        if (priority == 0) return defaultOutputPort;
        return conditionalPorts.FirstOrDefault(p => (int)p.userData == priority);
    }

    public ConditionalBranchData GetConditionalBranchData(int priority)
    {
        return conditionalBranchesData.ContainsKey(priority) ? conditionalBranchesData[priority] : null;
    }

    public void UpdateConditionalBranchData(int priority, List<ChoiceCondition> conditions, ConditionLogic logic)
    {
        if (conditionalBranchesData.ContainsKey(priority))
        {
            conditionalBranchesData[priority].conditions = new List<ChoiceCondition>(conditions);
            conditionalBranchesData[priority].conditionLogic = logic;
        }
    }

    public List<ConditionalBranchData> GetAllConditionalBranches()
    {
        return conditionalBranchesData.Values.OrderByDescending(b => b.priority).ToList();
    }

    public void LoadConditionalBranches(List<ConditionalBranchData> branches)
    {
        if (branches == null || branches.Count == 0) return;

        var nonDefaultBranches = branches.Where(b => b.priority > 0).OrderByDescending(b => b.priority).ToList();

        if (nonDefaultBranches.Count > 0)
        {
            if (!isConditionalMode)
            {
                ConvertToConditionalMode();
            }

            foreach (var branch in nonDefaultBranches)
            {
                AddConditionalBranch(branch.priority);
                conditionalBranchesData[branch.priority] = new ConditionalBranchData
                {
                    priority = branch.priority,
                    conditions = new List<ChoiceCondition>(branch.conditions),
                    conditionLogic = branch.conditionLogic
                };
            }

            nextBranchPriority = nonDefaultBranches.Max(b => b.priority) + 1;
        }

        UpdateConditionalBranchesDisplay();
    }
    #endregion

    #region Public Methods
    public void SetChoicesData(List<ChoiceData> choicesData)
    {
        ChoicesData.Clear();
        choicesContainer.Clear();
        choiceOutputPorts.Clear();

        for (int i = 0; i < choicesData.Count; i++)
        {
            ChoicesData.Add(choicesData[i]);
            RebuildChoiceUI(i);
        }

        RefreshExpandedState();
        RefreshPorts();
    }

    public void SetEventCalls(List<DialogueEventCall> eventCalls)
    {
        EventCalls = eventCalls ?? new List<DialogueEventCall>();
        UpdateEventsDisplay();
    }

    public int GetChoiceIndexForPort(Port port)
    {
        if (port.userData is int value && value <= -2)
        {
            return -2 - value;
        }
        return -1;
    }

    public Port GetOutputPortByIndex(int index)
    {
        return index >= 0 && index < choiceOutputPorts.Count ? choiceOutputPorts[index] : null;
    }

    public Port GetDefaultOutputPort() => defaultOutputPort;
    public Port GetInputPort() => inputPort;
    public string GetId() => nodeId;
    public void SetId(string id) => nodeId = id;
    #endregion

    #region Helper Methods
    private string GetComparisonDisplayName(ComparisonType comparisonType)
    {
        switch (comparisonType)
        {
            case ComparisonType.Equal:
                return "==";
            case ComparisonType.NotEqual:
                return "!=";
            case ComparisonType.Greater:
                return ">";
            case ComparisonType.Less:
                return "<";
            case ComparisonType.GreaterOrEqual:
                return ">=";
            case ComparisonType.LessOrEqual:
                return "<=";
            default:
                return "==";
        }
    }
    #endregion
}