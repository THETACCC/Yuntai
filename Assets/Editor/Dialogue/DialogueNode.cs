using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DialogueSystem.Editor
{
    public partial class DialogueNode : Node
    {
        private DialogueTreeEditor editorWindow;
        private VisualElement eventsContainer;
        private VisualElement choicesContainer;
        private Port inputPort;
        private Port defaultOutputPort;
        private List<Port> choiceOutputPorts = new List<Port>();
        private string nodeId;
        private int nodeIndex;

        public string CharacterName { get; private set; }
        public Sprite AvatarSprite { get; private set; }
        public string DialogueText { get; private set; }
        public List<ChoiceData> ChoicesData { get; private set; } = new List<ChoiceData>();
        public List<DialogueEventCall> EventCalls { get; private set; } = new List<DialogueEventCall>();
        public int NodeIndex => nodeIndex;

        public event Action OnNodeChanged;

        public DialogueNode(string characterName, Sprite avatarSprite, string dialogueText, int index, DialogueTreeEditor editor)
        {
            CharacterName = characterName;
            AvatarSprite = avatarSprite;
            DialogueText = dialogueText;
            nodeIndex = index;
            nodeId = Guid.NewGuid().ToString();
            editorWindow = editor;

            title = $"Node [{nodeIndex}]";

            CreateInputPort();
            CreateDefaultOutputPort();
            CreateCharacterNameField();
            CreateAvatarField();
            CreateDialogueTextField();
            CreateEventsSection();
            CreateChoicesSection();

            RefreshExpandedState();
            RefreshPorts();
        }

        public void SetNodeIndex(int index)
        {
            nodeIndex = index;
            title = $"Node [{nodeIndex}]";
        }

        private void NotifyChange() => OnNodeChanged?.Invoke();

        private void CreateInputPort()
        {
            inputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            inputPort.portName = "Input";
            inputContainer.Add(inputPort);
        }

        private void CreateDefaultOutputPort()
        {
            defaultOutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
            defaultOutputPort.portName = "Next";
            outputContainer.Add(defaultOutputPort);
        }

        private void CreateCharacterNameField()
        {
            var field = new TextField("Character Name:") { value = CharacterName, style = { minWidth = 300 } };
            field.RegisterValueChangedCallback(evt => { CharacterName = evt.newValue; NotifyChange(); });
            mainContainer.Add(field);
        }

        private void CreateAvatarField()
        {
            var container = new VisualElement();
            var field = new ObjectField("Avatar Sprite:")
            {
                objectType = typeof(Sprite),
                value = AvatarSprite,
                allowSceneObjects = false,
                style = { minWidth = 300 }
            };

            var warning = new Label
            {
                style = {
                    color = Color.red,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    paddingLeft = 5,
                    paddingTop = 2,
                    display = DisplayStyle.None
                }
            };

            field.RegisterValueChangedCallback(evt =>
            {
                AvatarSprite = evt.newValue as Sprite;
                if (AvatarSprite != null)
                {
                    string path = AssetDatabase.GetAssetPath(AvatarSprite);
                    if (!path.Contains("/Resources/"))
                    {
                        warning.text = $"⚠ '{AvatarSprite.name}' NOT in Resources folder!";
                        warning.style.display = DisplayStyle.Flex;
                    }
                    else warning.style.display = DisplayStyle.None;
                }
                else warning.style.display = DisplayStyle.None;
                NotifyChange();
            });

            container.Add(field);
            container.Add(warning);
            mainContainer.Add(container);

            if (AvatarSprite != null && !AssetDatabase.GetAssetPath(AvatarSprite).Contains("/Resources/"))
            {
                warning.text = $"⚠ '{AvatarSprite.name}' NOT in Resources folder!";
                warning.style.display = DisplayStyle.Flex;
            }
        }

        private void CreateDialogueTextField()
        {
            var field = new TextField("Dialogue:")
            {
                value = DialogueText,
                multiline = true,
                style = { minWidth = 300, minHeight = 60 }
            };
            field.RegisterValueChangedCallback(evt => { DialogueText = evt.newValue; NotifyChange(); });
            mainContainer.Add(field);
        }

        private void CreateEventsSection()
        {
            mainContainer.Add(new Label("Events (UnityEvent):")
            {
                style = { marginTop = 10, unityFontStyleAndWeight = FontStyle.Bold }
            });

            eventsContainer = new VisualElement
            {
                style = {
                    backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.3f),
                    borderTopWidth = 1, borderBottomWidth = 1, borderLeftWidth = 1, borderRightWidth = 1,
                    borderTopColor = new Color(0.3f, 0.3f, 0.3f), borderBottomColor = new Color(0.3f, 0.3f, 0.3f),
                    borderLeftColor = new Color(0.3f, 0.3f, 0.3f), borderRightColor = new Color(0.3f, 0.3f, 0.3f),
                    paddingTop = 5, paddingBottom = 5, paddingLeft = 5, paddingRight = 5, marginTop = 2
                }
            };
            mainContainer.Add(eventsContainer);

            mainContainer.Add(new Button(() => { EventCalls.Add(new DialogueEventCall()); UpdateEventsDisplay(); NotifyChange(); })
            {
                text = "+ Add Event",
                style = { marginTop = 2 }
            });

            UpdateEventsDisplay();
        }

        private void UpdateEventsDisplay()
        {
            eventsContainer.Clear();

            if (EventCalls.Count == 0)
            {
                eventsContainer.Add(new Label("List is Empty")
                {
                    style = {
                        color = new Color(0.7f, 0.7f, 0.7f),
                        unityFontStyleAndWeight = FontStyle.Italic,
                        paddingLeft = 10, paddingTop = 5, paddingBottom = 5
                    }
                });
                return;
            }

            for (int i = 0; i < EventCalls.Count; i++)
            {
                int idx = i;
                var eventCall = EventCalls[i];
                var container = CreateEventUI(idx, eventCall);
                eventsContainer.Add(container);
            }
        }

        private VisualElement CreateEventUI(int index, DialogueEventCall eventCall)
        {
            var container = new VisualElement
            {
                style = {
                    backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.5f),
                    marginTop = 3, paddingTop = 5, paddingBottom = 5, paddingLeft = 5, paddingRight = 5
                }
            };

            // Title row
            var titleRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            titleRow.Add(new Label($"Event {index}") { style = { flexGrow = 1, unityFontStyleAndWeight = FontStyle.Bold } });
            titleRow.Add(new Button(() => { EventCalls.RemoveAt(index); UpdateEventsDisplay(); NotifyChange(); })
            {
                text = "×",
                style = { width = 20, height = 18, fontSize = 12 }
            });
            container.Add(titleRow);

            // GameObject field
            var go = !string.IsNullOrEmpty(eventCall.targetObjectName) ? GameObject.Find(eventCall.targetObjectName) : null;
            var goField = new ObjectField("Target GameObject:")
            {
                objectType = typeof(GameObject),
                value = go,
                allowSceneObjects = true,
                style = { marginTop = 3 }
            };
            goField.RegisterValueChangedCallback(evt =>
            {
                EventCalls[index].targetObjectName = (evt.newValue as GameObject)?.name ?? "";
                UpdateEventsDisplay();
                NotifyChange();
            });
            container.Add(goField);

            if (go != null)
                container.Add(CreateComponentSelection(index, eventCall, go));
            else
                container.Add(new Label("Select a GameObject first")
                {
                    style = {
                        color = new Color(0.7f, 0.7f, 0.7f),
                        unityFontStyleAndWeight = FontStyle.Italic,
                        marginTop = 3, paddingLeft = 10
                    }
                });

            return container;
        }

        private VisualElement CreateComponentSelection(int index, DialogueEventCall eventCall, GameObject go)
        {
            var result = new VisualElement();
            var components = go.GetComponents<Component>().Where(c => c != null).ToList();
            var compNames = new List<string> { "None" };
            var compTypes = new List<Type> { null };

            compNames.AddRange(components.Select(c => c.GetType().Name));
            compTypes.AddRange(components.Select(c => c.GetType()));

            int selectedIdx = string.IsNullOrEmpty(eventCall.componentTypeName)
                ? 0 : Math.Max(0, compNames.IndexOf(eventCall.componentTypeName));

            var compDropdown = new PopupField<string>("Component:", compNames, selectedIdx) { style = { marginTop = 3 } };
            compDropdown.RegisterValueChangedCallback(evt =>
            {
                EventCalls[index].componentTypeName = evt.newValue != "None" ? evt.newValue : "";
                UpdateEventsDisplay();
                NotifyChange();
            });
            result.Add(compDropdown);

            if (selectedIdx > 0 && compTypes[selectedIdx] != null)
            {
                var methodSelection = CreateMethodSelection(index, eventCall, go, compTypes[selectedIdx]);
                result.Add(methodSelection);
            }

            return result;
        }

        private VisualElement CreateMethodSelection(int index, DialogueEventCall eventCall, GameObject go, Type compType)
        {
            var result = new VisualElement();
            var methods = compType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName && m.GetParameters().Length <= 1).ToList();

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
                    var pType = parameters[0].ParameterType;
                    if (pType == typeof(int) || pType == typeof(float) || pType == typeof(string) || pType == typeof(bool))
                    {
                        methodNames.Add($"{method.Name} ({pType.Name})");
                        methodInfos.Add(method);
                    }
                }
            }

            int selectedIdx = 0;
            if (!string.IsNullOrEmpty(eventCall.methodName))
            {
                for (int i = 0; i < methodInfos.Count; i++)
                {
                    if (methodInfos[i]?.Name == eventCall.methodName)
                    {
                        selectedIdx = i;
                        break;
                    }
                }
            }

            var methodDropdown = new PopupField<string>("Function:", methodNames, selectedIdx) { style = { marginTop = 3 } };
            methodDropdown.RegisterValueChangedCallback(evt =>
            {
                int idx = methodNames.IndexOf(evt.newValue);
                if (idx > 0 && methodInfos[idx] != null)
                {
                    var method = methodInfos[idx];
                    EventCalls[index].methodName = method.Name;
                    var pars = method.GetParameters();
                    EventCalls[index].parameterType = pars.Length == 0 ? ParameterType.None :
                        pars[0].ParameterType == typeof(int) ? ParameterType.Int :
                        pars[0].ParameterType == typeof(float) ? ParameterType.Float :
                        pars[0].ParameterType == typeof(string) ? ParameterType.String :
                        pars[0].ParameterType == typeof(bool) ? ParameterType.Bool : ParameterType.None;
                }
                else
                {
                    EventCalls[index].methodName = "";
                    EventCalls[index].parameterType = ParameterType.None;
                }
                UpdateEventsDisplay();
                NotifyChange();
            });
            result.Add(methodDropdown);

            if (selectedIdx > 0 && methodInfos[selectedIdx] != null)
            {
                var pars = methodInfos[selectedIdx].GetParameters();
                if (pars.Length == 1)
                    result.Add(CreateParameterField(index, eventCall, pars[0].ParameterType));
            }

            return result;
        }

        private VisualElement CreateParameterField(int index, DialogueEventCall eventCall, Type paramType)
        {
            var container = new VisualElement { style = { marginTop = 3, paddingLeft = 10 } };

            if (paramType == typeof(string))
            {
                var field = new TextField("Parameter:") { value = eventCall.stringParameter };
                field.RegisterValueChangedCallback(evt => { EventCalls[index].stringParameter = evt.newValue; NotifyChange(); });
                container.Add(field);
            }
            else if (paramType == typeof(int))
            {
                var field = new IntegerField("Parameter:") { value = eventCall.intParameter };
                field.RegisterValueChangedCallback(evt => { EventCalls[index].intParameter = evt.newValue; NotifyChange(); });
                container.Add(field);
            }
            else if (paramType == typeof(float))
            {
                var field = new FloatField("Parameter:") { value = eventCall.floatParameter };
                field.RegisterValueChangedCallback(evt => { EventCalls[index].floatParameter = evt.newValue; NotifyChange(); });
                container.Add(field);
            }
            else if (paramType == typeof(bool))
            {
                var field = new Toggle("Parameter:") { value = eventCall.boolParameter };
                field.RegisterValueChangedCallback(evt => { EventCalls[index].boolParameter = evt.newValue; NotifyChange(); });
                container.Add(field);
            }

            return container;
        }

        // 公共方法
        public void SetEventCalls(List<DialogueEventCall> eventCalls)
        {
            EventCalls = eventCalls ?? new List<DialogueEventCall>();
            UpdateEventsDisplay();
        }

        public Port GetInputPort() => inputPort;
        public Port GetDefaultOutputPort() => defaultOutputPort;
        public string GetId() => nodeId;
        public void SetId(string id) => nodeId = id;
    }
}

namespace DialogueSystem.Editor
{
    public partial class DialogueNode
    {
        private void CreateChoicesSection()
        {
            mainContainer.Add(new Label("Player Choices:")
            {
                style = { marginTop = 10, unityFontStyleAndWeight = FontStyle.Bold }
            });

            choicesContainer = new VisualElement();
            mainContainer.Add(choicesContainer);

            mainContainer.Add(new Button(() =>
            {
                AddChoice(new ChoiceData { text = "New Choice" });
                NotifyChange();
            })
            {
                text = "Add Choice",
                style = { marginTop = 5 }
            });
        }

        private void AddChoice(ChoiceData choiceData)
        {
            int index = ChoicesData.Count;
            ChoicesData.Add(choiceData);
            RebuildChoiceUI(index);
            CreateChoiceOutputPort(index, choiceData.text);
            RefreshExpandedState();
            RefreshPorts();
        }

        private void RemoveChoice(int index)
        {
            if (index < 0 || index >= ChoicesData.Count) return;

            if (index < choiceOutputPorts.Count)
            {
                outputContainer.Remove(choiceOutputPorts[index]);
                choiceOutputPorts.RemoveAt(index);
            }

            ChoicesData.RemoveAt(index);
            choicesContainer.Clear();

            foreach (var port in choiceOutputPorts)
                outputContainer.Remove(port);
            choiceOutputPorts.Clear();

            for (int i = 0; i < ChoicesData.Count; i++)
            {
                RebuildChoiceUI(i);
                CreateChoiceOutputPort(i, ChoicesData[i].text);
            }

            RefreshExpandedState();
            RefreshPorts();
        }

        private void RebuildChoiceUI(int index)
        {
            var container = new VisualElement
            {
                style = {
                    marginTop = 5,
                    backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.5f),
                    paddingTop = 5, paddingBottom = 5, paddingLeft = 5, paddingRight = 5
                }
            };

            // Header row
            var headerRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            var choiceField = new TextField { value = ChoicesData[index].text, style = { flexGrow = 1 } };
            int currentIndex = index;
            choiceField.RegisterValueChangedCallback(evt =>
            {
                if (currentIndex < ChoicesData.Count)
                {
                    ChoicesData[currentIndex].text = evt.newValue;
                    if (currentIndex < choiceOutputPorts.Count)
                        choiceOutputPorts[currentIndex].portName = $"{currentIndex + 1}: {evt.newValue}";
                    NotifyChange();
                }
            });

            var removeButton = new Button(() => { RemoveChoice(currentIndex); NotifyChange(); })
            {
                text = "×",
                style = { width = 20, height = 18 }
            };

            headerRow.Add(choiceField);
            headerRow.Add(removeButton);
            container.Add(headerRow);

            // Conditions section
            container.Add(new VisualElement
            {
                style = {
                    flexDirection = FlexDirection.Row, marginTop = 8, marginBottom = 3, paddingBottom = 3,
                    borderBottomWidth = 1, borderBottomColor = new Color(0.3f, 0.3f, 0.3f)
                }
            }.With(h => h.Add(new Label("Conditions")
            {
                style = {
                    unityFontStyleAndWeight = FontStyle.Bold, fontSize = 10,
                    color = new Color(0.8f, 0.8f, 0.8f)
                }
            })));

            var conditionsContent = new VisualElement
            {
                style = {
                    backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.8f),
                    paddingTop = 8, paddingBottom = 8, paddingLeft = 8, paddingRight = 8, marginTop = 0
                }
            };

            UpdateConditionsDisplay(conditionsContent, currentIndex);
            container.Add(conditionsContent);
            choicesContainer.Add(container);
        }

        public void RefreshConditionsUI()
        {
            choicesContainer.Clear();
            foreach (var port in choiceOutputPorts)
                outputContainer.Remove(port);
            choiceOutputPorts.Clear();

            for (int i = 0; i < ChoicesData.Count; i++)
            {
                RebuildChoiceUI(i);
                CreateChoiceOutputPort(i, ChoicesData[i].text);
            }

            RefreshExpandedState();
            RefreshPorts();
        }

        private void UpdateConditionsDisplay(VisualElement container, int choiceIndex)
        {
            container.Clear();
            if (choiceIndex >= ChoicesData.Count) return;

            var choiceData = ChoicesData[choiceIndex];

            if (choiceData.conditions.Count == 0)
            {
                container.Add(new Label("No conditions (always available)")
                {
                    style = {
                        color = new Color(0.6f, 0.6f, 0.6f), unityFontStyleAndWeight = FontStyle.Italic,
                        fontSize = 10, paddingTop = 5, paddingBottom = 5, unityTextAlign = TextAnchor.MiddleCenter
                    }
                });
            }
            else
            {
                for (int i = 0; i < choiceData.conditions.Count; i++)
                {
                    int condIndex = i;
                    container.Add(CreateConditionUI(choiceIndex, condIndex, choiceData.conditions[i]));
                }

                if (choiceData.conditions.Count > 1)
                    container.Add(CreateLogicSelector(choiceIndex, choiceData));
            }

            container.Add(new Button(() =>
            {
                if (choiceIndex < ChoicesData.Count)
                {
                    ChoicesData[choiceIndex].conditions.Add(new ChoiceCondition());
                    UpdateConditionsDisplay(container, choiceIndex);
                    NotifyChange();
                }
            })
            {
                text = "+ Add Condition",
                style = { marginTop = 5, height = 20, fontSize = 10 }
            });
        }

        private VisualElement CreateConditionUI(int choiceIndex, int condIndex, ChoiceCondition condition)
        {
            var container = new VisualElement
            {
                style = {
                    marginTop = 5, paddingTop = 8, paddingBottom = 8, paddingLeft = 8, paddingRight = 8,
                    backgroundColor = new Color(0.12f, 0.12f, 0.12f),
                    borderTopLeftRadius = 3, borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3, borderBottomRightRadius = 3
                }
            };

            // Header
            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 8 } };
            header.Add(new Label($"Condition {condIndex + 1}")
            {
                style = { flexGrow = 1, unityFontStyleAndWeight = FontStyle.Bold, fontSize = 10, color = new Color(0.9f, 0.9f, 0.9f) }
            });
            header.Add(new Button(() =>
            {
                if (choiceIndex < ChoicesData.Count)
                {
                    ChoicesData[choiceIndex].conditions.RemoveAt(condIndex);
                    UpdateConditionsDisplay(container.parent, choiceIndex);
                    NotifyChange();
                }
            })
            {
                text = "×",
                style = { width = 18, height = 18, fontSize = 12 }
            });
            container.Add(header);

            // Variable selection
            var variables = editorWindow?.GetVariables() ?? new List<DialogueVariable>();
            var varNames = new List<string> { "None" };
            varNames.AddRange(variables.Select(v => v.name));

            int selectedVarIndex = string.IsNullOrEmpty(condition.variableName)
                ? 0 : Math.Max(0, varNames.IndexOf(condition.variableName));

            if (selectedVarIndex > 0)
            {
                var selectedVar = variables[selectedVarIndex - 1];
                var fullRow = CreateFullConditionRow(choiceIndex, condIndex, condition, varNames, selectedVarIndex, selectedVar);
                container.Add(fullRow);
            }
            else
            {
                var varRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
                varRow.Add(new Label("Var:")
                {
                    style = { width = 35, fontSize = 10, color = new Color(0.7f, 0.7f, 0.7f), marginRight = 3 }
                });

                var varDropdown = new PopupField<string>(varNames, selectedVarIndex) { style = { width = 90 } };
                varDropdown.RegisterValueChangedCallback(evt =>
                {
                    if (choiceIndex < ChoicesData.Count && condIndex < ChoicesData[choiceIndex].conditions.Count)
                    {
                        ChoicesData[choiceIndex].conditions[condIndex].variableName = evt.newValue == "None" ? "" : evt.newValue;
                        UpdateConditionsDisplay(container.parent, choiceIndex);
                        NotifyChange();
                    }
                });

                varRow.Add(varDropdown);
                container.Add(varRow);
            }

            return container;
        }

        private VisualElement CreateFullConditionRow(int choiceIndex, int condIndex, ChoiceCondition condition,
            List<string> varNames, int selectedVarIndex, DialogueVariable selectedVar)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

            row.Add(new Label("Var:")
            {
                style = { width = 35, fontSize = 10, color = new Color(0.7f, 0.7f, 0.7f), marginRight = 3 }
            });

            var varDropdown = new PopupField<string>(varNames, selectedVarIndex) { style = { width = 90, marginRight = 5 } };
            varDropdown.RegisterValueChangedCallback(evt =>
            {
                if (choiceIndex < ChoicesData.Count && condIndex < ChoicesData[choiceIndex].conditions.Count)
                {
                    ChoicesData[choiceIndex].conditions[condIndex].variableName = evt.newValue == "None" ? "" : evt.newValue;
                    UpdateConditionsDisplay(row.parent.parent, choiceIndex);
                    NotifyChange();
                }
            });
            row.Add(varDropdown);

            // Comparison operator
            var compTypes = GetComparisonTypesForVariable(selectedVar.type);
            var compNames = compTypes.Select(GetComparisonDisplayName).ToList();
            int selectedCompIndex = Math.Max(0, compTypes.IndexOf(condition.comparison));

            var compDropdown = new PopupField<string>(compNames, selectedCompIndex)
            {
                style = { width = 80, marginRight = 5, fontSize = 10 }
            };
            compDropdown.RegisterValueChangedCallback(evt =>
            {
                if (choiceIndex < ChoicesData.Count && condIndex < ChoicesData[choiceIndex].conditions.Count)
                {
                    ChoicesData[choiceIndex].conditions[condIndex].comparison = compTypes[compNames.IndexOf(evt.newValue)];
                    NotifyChange();
                }
            });
            row.Add(compDropdown);

            // Value field
            row.Add(CreateValueField(choiceIndex, condIndex, condition, selectedVar.type));

            return row;
        }

        private VisualElement CreateValueField(int choiceIndex, int condIndex, ChoiceCondition condition, VariableType varType)
        {
            return varType switch
            {
                VariableType.Bool => CreateBoolValueField(choiceIndex, condIndex, condition),
                VariableType.Int => CreateIntValueField(choiceIndex, condIndex, condition),
                VariableType.Float => CreateFloatValueField(choiceIndex, condIndex, condition),
                VariableType.String => CreateStringValueField(choiceIndex, condIndex, condition),
                _ => new VisualElement()
            };
        }

        private Toggle CreateBoolValueField(int choiceIndex, int condIndex, ChoiceCondition condition)
        {
            var field = new Toggle { value = condition.compareValue == "true", style = { width = 40 } };
            field.RegisterValueChangedCallback(evt =>
            {
                if (choiceIndex < ChoicesData.Count && condIndex < ChoicesData[choiceIndex].conditions.Count)
                {
                    ChoicesData[choiceIndex].conditions[condIndex].compareValue = evt.newValue.ToString().ToLower();
                    NotifyChange();
                }
            });
            return field;
        }

        private IntegerField CreateIntValueField(int choiceIndex, int condIndex, ChoiceCondition condition)
        {
            int.TryParse(condition.compareValue, out int value);
            var field = new IntegerField { value = value, style = { flexGrow = 1, fontSize = 10 } };
            field.RegisterValueChangedCallback(evt =>
            {
                if (choiceIndex < ChoicesData.Count && condIndex < ChoicesData[choiceIndex].conditions.Count)
                {
                    ChoicesData[choiceIndex].conditions[condIndex].compareValue = evt.newValue.ToString();
                    NotifyChange();
                }
            });
            return field;
        }

        private FloatField CreateFloatValueField(int choiceIndex, int condIndex, ChoiceCondition condition)
        {
            float.TryParse(condition.compareValue, out float value);
            var field = new FloatField { value = value, style = { flexGrow = 1, fontSize = 10 } };
            field.RegisterValueChangedCallback(evt =>
            {
                if (choiceIndex < ChoicesData.Count && condIndex < ChoicesData[choiceIndex].conditions.Count)
                {
                    ChoicesData[choiceIndex].conditions[condIndex].compareValue = evt.newValue.ToString();
                    NotifyChange();
                }
            });
            return field;
        }

        private TextField CreateStringValueField(int choiceIndex, int condIndex, ChoiceCondition condition)
        {
            var field = new TextField { value = condition.compareValue, style = { flexGrow = 1, fontSize = 10 } };
            field.RegisterValueChangedCallback(evt =>
            {
                if (choiceIndex < ChoicesData.Count && condIndex < ChoicesData[choiceIndex].conditions.Count)
                {
                    ChoicesData[choiceIndex].conditions[condIndex].compareValue = evt.newValue;
                    NotifyChange();
                }
            });
            return field;
        }

        private VisualElement CreateLogicSelector(int choiceIndex, ChoiceData choiceData)
        {
            var row = new VisualElement
            {
                style = {
                    flexDirection = FlexDirection.Row, marginTop = 8, marginBottom = 3,
                    alignItems = Align.Center, justifyContent = Justify.Center
                }
            };

            var andBtn = new Button(() =>
            {
                if (choiceIndex < ChoicesData.Count)
                {
                    ChoicesData[choiceIndex].conditionLogic = ConditionLogic.AND;
                    UpdateConditionsDisplay(row.parent, choiceIndex);
                    NotifyChange();
                }
            })
            {
                text = "AND",
                style = {
                    width = 60, height = 22, fontSize = 10,
                    unityFontStyleAndWeight = choiceData.conditionLogic == ConditionLogic.AND ? FontStyle.Bold : FontStyle.Normal,
                    backgroundColor = choiceData.conditionLogic == ConditionLogic.AND
                        ? new Color(0.3f, 0.5f, 0.3f) : new Color(0.25f, 0.25f, 0.25f)
                }
            };

            var orBtn = new Button(() =>
            {
                if (choiceIndex < ChoicesData.Count)
                {
                    ChoicesData[choiceIndex].conditionLogic = ConditionLogic.OR;
                    UpdateConditionsDisplay(row.parent, choiceIndex);
                    NotifyChange();
                }
            })
            {
                text = "OR",
                style = {
                    width = 60, height = 22, fontSize = 10, marginLeft = 5,
                    unityFontStyleAndWeight = choiceData.conditionLogic == ConditionLogic.OR ? FontStyle.Bold : FontStyle.Normal,
                    backgroundColor = choiceData.conditionLogic == ConditionLogic.OR
                        ? new Color(0.3f, 0.5f, 0.3f) : new Color(0.25f, 0.25f, 0.25f)
                }
            };

            row.Add(andBtn);
            row.Add(orBtn);
            return row;
        }

        private List<ComparisonType> GetComparisonTypesForVariable(VariableType varType)
        {
            return varType switch
            {
                VariableType.Bool => new() { ComparisonType.Equal, ComparisonType.NotEqual },
                VariableType.Int or VariableType.Float => new()
                {
                    ComparisonType.Equal, ComparisonType.NotEqual, ComparisonType.Greater,
                    ComparisonType.Less, ComparisonType.GreaterOrEqual, ComparisonType.LessOrEqual
                },
                VariableType.String => new()
                {
                    ComparisonType.Equal, ComparisonType.NotEqual, ComparisonType.Contains,
                    ComparisonType.StartsWith, ComparisonType.EndsWith
                },
                _ => new() { ComparisonType.Equal }
            };
        }

        private string GetComparisonDisplayName(ComparisonType comparison)
        {
            return comparison switch
            {
                ComparisonType.Equal => "==",
                ComparisonType.NotEqual => "!=",
                ComparisonType.Greater => ">",
                ComparisonType.Less => "<",
                ComparisonType.GreaterOrEqual => ">=",
                ComparisonType.LessOrEqual => "<=",
                ComparisonType.Contains => "Contains",
                ComparisonType.StartsWith => "Starts With",
                ComparisonType.EndsWith => "Ends With",
                _ => "=="
            };
        }

        private void CreateChoiceOutputPort(int index, string choiceText)
        {
            var port = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
            port.portName = $"{index + 1}: {choiceText}";
            choiceOutputPorts.Add(port);
            outputContainer.Add(port);
        }

        public void SetChoicesData(List<ChoiceData> choicesData)
        {
            ChoicesData.Clear();
            choicesContainer.Clear();

            foreach (var port in choiceOutputPorts)
                outputContainer.Remove(port);
            choiceOutputPorts.Clear();

            for (int i = 0; i < choicesData.Count; i++)
            {
                ChoicesData.Add(choicesData[i]);
                RebuildChoiceUI(i);
                CreateChoiceOutputPort(i, choicesData[i].text);
            }

            RefreshExpandedState();
            RefreshPorts();
        }

        public int GetChoiceIndexForPort(Port port)
        {
            return port == defaultOutputPort ? -1 : choiceOutputPorts.IndexOf(port);
        }

        public Port GetOutputPortByIndex(int index)
        {
            return index >= 0 && index < choiceOutputPorts.Count ? choiceOutputPorts[index] : null;
        }
    }

    // Helper extension method for fluent API
    public static class VisualElementExtensions
    {
        public static T With<T>(this T element, System.Action<T> action) where T : VisualElement
        {
            action(element);
            return element;
        }
    }
}