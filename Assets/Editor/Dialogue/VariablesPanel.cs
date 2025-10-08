using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

namespace DialogueSystem.Editor
{
    public class VariablesPanel : VisualElement
    {
        private DialogueTreeEditor editorWindow;
        private List<DialogueVariable> variables;
        private VisualElement listContainer;

        public VariablesPanel(DialogueTreeEditor editor)
        {
            editorWindow = editor;
            style.backgroundColor = new Color(0.22f, 0.22f, 0.22f);
            style.paddingTop = style.paddingBottom = style.paddingLeft = style.paddingRight = 5;

            CreateHeader();
            CreateScrollView();
        }

        private void CreateHeader()
        {
            Add(new Label("Variables")
            {
                style = {
                    fontSize = 14,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 5,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            });

            var addButton = new Button(ShowAddVariableMenu) { text = "+ Add Variable" };
            addButton.style.marginBottom = 8;
            Add(addButton);
        }

        private void ShowAddVariableMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Bool"), false, () => AddVariable(VariableType.Bool));
            menu.AddItem(new GUIContent("Int"), false, () => AddVariable(VariableType.Int));
            menu.AddItem(new GUIContent("Float"), false, () => AddVariable(VariableType.Float));
            menu.AddItem(new GUIContent("String"), false, () => AddVariable(VariableType.String));
            menu.ShowAsContext();
        }

        private void AddVariable(VariableType type)
        {
            string baseName = type.ToString().ToLower() + "Var";
            string name = baseName;
            int counter = 1;
            while (variables.Any(v => v.name == name))
                name = baseName + counter++;

            variables.Add(new DialogueVariable
            {
                name = name,
                type = type,
                defaultValue = GetDefaultValue(type)
            });

            editorWindow.MarkAsChanged();
            editorWindow.NotifyVariablesChanged();
            RefreshDisplay();
        }

        private string GetDefaultValue(VariableType type)
        {
            return type switch
            {
                VariableType.Bool => "false",
                VariableType.Int => "0",
                VariableType.Float => "0.0",
                _ => ""
            };
        }

        private void CreateScrollView()
        {
            var scrollView = new ScrollView { style = { flexGrow = 1 } };
            listContainer = new VisualElement();
            scrollView.Add(listContainer);
            Add(scrollView);
        }

        public void SetVariables(List<DialogueVariable> vars)
        {
            variables = vars;
            RefreshDisplay();
        }

        private void RefreshDisplay()
        {
            listContainer.Clear();
            foreach (var variable in variables)
                AddVariableUI(variable);
        }

        private void AddVariableUI(DialogueVariable variable)
        {
            var row = new VisualElement
            {
                style = {
                    flexDirection = FlexDirection.Row,
                    marginBottom = 3,
                    paddingTop = 3, paddingBottom = 3, paddingLeft = 3, paddingRight = 3,
                    backgroundColor = new Color(0.18f, 0.18f, 0.18f)
                }
            };

            // Type icon
            row.Add(new Label(GetTypeIcon(variable.type))
            {
                style = {
                    width = 20, unityTextAlign = TextAnchor.MiddleCenter,
                    fontSize = 12, unityFontStyleAndWeight = FontStyle.Bold,
                    marginRight = 5, color = GetTypeColor(variable.type)
                }
            });

            // Name field
            var nameField = new TextField { value = variable.name };
            nameField.style.flexGrow = 1;
            nameField.style.minWidth = 60;
            nameField.style.marginRight = 5;
            nameField.RegisterValueChangedCallback(evt =>
            {
                if (!string.IsNullOrEmpty(evt.newValue) && !variables.Any(v => v != variable && v.name == evt.newValue))
                {
                    variable.name = evt.newValue;
                    editorWindow.MarkAsChanged();
                    editorWindow.NotifyVariablesChanged();
                }
                else
                {
                    nameField.SetValueWithoutNotify(variable.name);
                }
            });
            row.Add(nameField);

            // Value field
            row.Add(CreateValueField(variable));

            // Delete button
            var deleteButton = new Button(() =>
            {
                variables.Remove(variable);
                editorWindow.MarkAsChanged();
                editorWindow.NotifyVariablesChanged();
                RefreshDisplay();
            })
            {
                text = "×",
                style = { width = 20, height = 20, marginLeft = 5, fontSize = 14 }
            };
            row.Add(deleteButton);

            listContainer.Add(row);
        }

        private VisualElement CreateValueField(DialogueVariable variable)
        {
            VisualElement field = variable.type switch
            {
                VariableType.Bool => CreateBoolField(variable),
                VariableType.Int => CreateIntField(variable),
                VariableType.Float => CreateFloatField(variable),
                VariableType.String => CreateStringField(variable),
                _ => new VisualElement()
            };
            return field;
        }

        private Toggle CreateBoolField(DialogueVariable variable)
        {
            var field = new Toggle { value = variable.defaultValue == "true", style = { width = 40 } };
            field.RegisterValueChangedCallback(evt =>
            {
                variable.defaultValue = evt.newValue.ToString().ToLower();
                editorWindow.MarkAsChanged();
            });
            return field;
        }

        private IntegerField CreateIntField(DialogueVariable variable)
        {
            int.TryParse(variable.defaultValue, out int value);
            var field = new IntegerField { value = value, style = { width = 60 } };
            field.RegisterValueChangedCallback(evt =>
            {
                variable.defaultValue = evt.newValue.ToString();
                editorWindow.MarkAsChanged();
            });
            return field;
        }

        private FloatField CreateFloatField(DialogueVariable variable)
        {
            float.TryParse(variable.defaultValue, out float value);
            var field = new FloatField { value = value, style = { width = 60 } };
            field.RegisterValueChangedCallback(evt =>
            {
                variable.defaultValue = evt.newValue.ToString();
                editorWindow.MarkAsChanged();
            });
            return field;
        }

        private TextField CreateStringField(DialogueVariable variable)
        {
            var field = new TextField { value = variable.defaultValue, style = { width = 80 } };
            field.RegisterValueChangedCallback(evt =>
            {
                variable.defaultValue = evt.newValue;
                editorWindow.MarkAsChanged();
            });
            return field;
        }

        private string GetTypeIcon(VariableType type) => type switch
        {
            VariableType.Bool => "B",
            VariableType.Int => "I",
            VariableType.Float => "F",
            VariableType.String => "S",
            _ => "?"
        };

        private Color GetTypeColor(VariableType type) => type switch
        {
            VariableType.Bool => new Color(0.8f, 0.4f, 0.4f),
            VariableType.Int => new Color(0.4f, 0.7f, 1f),
            VariableType.Float => new Color(0.5f, 1f, 0.5f),
            VariableType.String => new Color(1f, 0.8f, 0.4f),
            _ => Color.white
        };
    }
}