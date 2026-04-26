using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.Experimental.GraphView;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using DialogueSystem;
using UnityEditor.UIElements;

/// <summary>
/// 对话节点 - 角色系统版本
/// </summary>
public partial class DialogueNodeEditor : Node
{
    private DialogueTreeEditor editorWindow;
    private VisualElement characterContainer;
    private VisualElement avatarPreview;
    private Label characterLabel;
    private TextField dialogueTextField;
    private TextField dialogueIdField;
    private Label dialoguePreviewLabel;
    private DropdownField contentModeDropdown;
    private VisualElement dialogueInputContainer;

    private bool useContentId = true;
    private string contentId = "";
    private VisualElement eventsContainer;
    private Button addEventButton;
    private VisualElement choicesContainer;
    private Button addChoiceButton;
    private VisualElement conditionalBranchesContainer;
    private Port inputPort;
    private Port defaultOutputPort;
    private List<Port> choiceOutputPorts = new List<Port>();
    private List<TextField> choiceIdFields = new List<TextField>();
    private List<Label> choicePreviewLabels = new List<Label>();
    private List<Port> conditionalPorts = new List<Port>();
    private Dictionary<int, ConditionalBranchData> conditionalBranchesData = new Dictionary<int, ConditionalBranchData>();

    private string nodeId;
    private int nodeIndex;
    private bool isStartNode = false;
    private bool isConditionalMode = false;
    private int nextBranchPriority = 1;

    // 文本对齐
    private TextAlignmentType contentAlignment = TextAlignmentType.Left;
    private Button alignLeftBtn;
    private Button alignCenterBtn;
    private Button alignRightBtn;

    public string CharacterId { get; private set; }
    public string CharacterName => GetCharacterName();
    public Sprite AvatarSprite => GetCharacterAvatar();
    public bool UseContentId => useContentId;
    public string ContentId => contentId;
    public LocalizedText DialogueText { get; private set; } = new LocalizedText();
    public List<ChoiceData> ChoicesData { get; private set; } = new List<ChoiceData>();
    public List<DialogueEventCall> EventCalls { get; private set; } = new List<DialogueEventCall>();
    public int NodeIndex => nodeIndex;
    public TextAlignmentType ContentAlignment => contentAlignment;

    public event System.Action OnNodeChanged;

    public DialogueNodeEditor(string characterName = "Character", Sprite avatarSprite = null,
                       string dialogueText = "New Dialogue", int index = 0,
                       DialogueTreeEditor editor = null)
    {
        this.CharacterId = "";
        this.DialogueText = new LocalizedText(dialogueText);
        this.nodeIndex = index;
        this.nodeId = System.Guid.NewGuid().ToString();
        this.editorWindow = editor;

        UpdateTitle();
        CreateInputPort();
        CreateOutputPortWithAddButton();
        CreateCharacterSection();
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
        title = isStartNode ? "START" : "Node";
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

        var addBranchButton = new Button(OnAddBranch) { text = "+" };
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

    #region Character Section
    private void CreateCharacterSection()
    {
        characterContainer = new VisualElement();
        characterContainer.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.25f, 0.5f));
        characterContainer.style.marginTop = 5;
        characterContainer.style.marginBottom = 5;
        characterContainer.style.paddingTop = 5;
        characterContainer.style.paddingBottom = 5;
        characterContainer.style.paddingLeft = 8;
        characterContainer.style.paddingRight = 8;
        characterContainer.style.borderTopLeftRadius = 4;
        characterContainer.style.borderTopRightRadius = 4;
        characterContainer.style.borderBottomLeftRadius = 4;
        characterContainer.style.borderBottomRightRadius = 4;
        characterContainer.style.minWidth = 310;
        characterContainer.style.maxWidth = 310;

        var contentRow = new VisualElement();
        contentRow.style.flexDirection = FlexDirection.Row;
        contentRow.style.alignItems = Align.Center;
        contentRow.style.justifyContent = Justify.SpaceBetween;

        characterLabel = new Label();
        characterLabel.style.fontSize = 11;
        characterLabel.style.flexGrow = 1;

        var clearButton = new Button(() => { CharacterId = ""; UpdateCharacterDisplay(); NotifyChange(); }) { text = "Clear" };
        clearButton.style.width = 50;
        clearButton.style.height = 18;
        clearButton.style.fontSize = 10;

        contentRow.Add(characterLabel);
        contentRow.Add(clearButton);
        characterContainer.Add(contentRow);

        characterContainer.RegisterCallback<DragUpdatedEvent>(OnCharacterDragUpdate);
        characterContainer.RegisterCallback<DragPerformEvent>(OnCharacterDragPerform);
        characterContainer.RegisterCallback<DragExitedEvent>(OnCharacterDragExit);

        mainContainer.Add(characterContainer);
        UpdateCharacterDisplay();
    }

    public void RefreshCharacterDisplay() { UpdateCharacterDisplay(); }

    private void OnCharacterDragUpdate(DragUpdatedEvent evt)
    {
        if (DragAndDrop.GetGenericData("CharacterData") is CharacterData)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            evt.StopPropagation();
        }
    }

    private void OnCharacterDragPerform(DragPerformEvent evt)
    {
        var characterData = DragAndDrop.GetGenericData("CharacterData") as CharacterData;
        if (characterData != null)
        {
            CharacterId = characterData.id;
            UpdateCharacterDisplay();
            NotifyChange();
            DragAndDrop.AcceptDrag();
            evt.StopPropagation();
        }
    }

    private void OnCharacterDragExit(DragExitedEvent evt) { }

    private void UpdateCharacterDisplay()
    {
        if (string.IsNullOrEmpty(CharacterId))
        {
            characterLabel.text = "Character: Drag from Manager";
            characterLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            characterLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        }
        else
        {
            characterLabel.text = $"Character: {GetCharacterName()}";
            characterLabel.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f));
            characterLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        }
    }

    private string GetCharacterName()
    {
        if (string.IsNullOrEmpty(CharacterId)) return "No Character";
        var character = GetCharacterById(CharacterId);
        if (character != null)
            return !string.IsNullOrEmpty(character.character) ? character.character : character.characterName?.en ?? "";
        return "Unknown Character";
    }

    private Sprite GetCharacterAvatar()
    {
        if (string.IsNullOrEmpty(CharacterId)) return null;
        var character = GetCharacterById(CharacterId);
        if (character != null && !string.IsNullOrEmpty(character.avatarAssetPath))
            return AssetDatabase.LoadAssetAtPath<Sprite>(character.avatarAssetPath);
        return null;
    }

    private CharacterData GetCharacterById(string id)
    {
        string libraryPath = GetCharacterLibraryPath();
        if (File.Exists(libraryPath))
        {
            try
            {
                string json = File.ReadAllText(libraryPath);
                var library = JsonUtility.FromJson<CharacterLibraryData>(json);
                if (library?.characters != null)
                    return System.Array.Find(library.characters, c => c.id == id);
            }
            catch { }
        }
        return null;
    }

    private string GetCharacterLibraryPath() => "Assets/Dialogue/Editor/Data/CharacterLibrary.json";

    public void SetCharacterId(string characterId) { CharacterId = characterId; UpdateCharacterDisplay(); }
    public void SetEditorWindow(DialogueTreeEditor editor) { this.editorWindow = editor; }

    public void SetDialogueText(LocalizedText localizedText)
    {
        if (localizedText != null)
        {
            this.DialogueText = localizedText;
            if (dialogueTextField != null && editorWindow != null)
                dialogueTextField.SetValueWithoutNotify(localizedText.GetText(editorWindow.GetCurrentLanguage()));
        }
    }

    public void SetContentId(string id)
    {
        this.contentId = id ?? "";
        if (dialogueIdField != null)
        {
            dialogueIdField.SetValueWithoutNotify(this.contentId);
            UpdateDialoguePreview();
        }
    }

    public void SetContentMode(bool useId, string id)
    {
        useContentId = useId;
        contentId = id ?? "";
        if (contentModeDropdown != null)
            contentModeDropdown.SetValueWithoutNotify(useId ? "Use ID" : "Direct Input");
        UpdateDialogueInputFields();
    }

    public void SetContentAlignment(TextAlignmentType alignment)
    {
        contentAlignment = alignment;
        UpdateAlignmentButtonStyles();
    }

    private void UpdateAlignmentButtonStyles()
    {
        var activeColor  = new StyleColor(new Color(0.3f, 0.5f, 0.8f));
        var inactiveColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
        if (alignLeftBtn   != null) alignLeftBtn.style.backgroundColor   = contentAlignment == TextAlignmentType.Left   ? activeColor : inactiveColor;
        if (alignCenterBtn != null) alignCenterBtn.style.backgroundColor = contentAlignment == TextAlignmentType.Center ? activeColor : inactiveColor;
        if (alignRightBtn  != null) alignRightBtn.style.backgroundColor  = contentAlignment == TextAlignmentType.Right  ? activeColor : inactiveColor;
    }
    #endregion

    #region Dialogue TextField
    private void CreateDialogueTextField()
    {
        dialogueInputContainer = new VisualElement();
        dialogueInputContainer.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f, 0.3f));
        dialogueInputContainer.style.paddingTop = 5;
        dialogueInputContainer.style.paddingBottom = 5;
        dialogueInputContainer.style.paddingLeft = 5;
        dialogueInputContainer.style.paddingRight = 5;
        dialogueInputContainer.style.marginBottom = 10;

        contentModeDropdown = new DropdownField("Input Mode:", new List<string> { "Direct Input", "Use ID" }, useContentId ? 1 : 0);
        contentModeDropdown.RegisterValueChangedCallback(evt =>
        {
            bool newUseId = (evt.newValue == "Use ID");
            if (useContentId && !newUseId)
            {
                bool isEmpty = DialogueText == null ||
                              (string.IsNullOrEmpty(DialogueText.en) && string.IsNullOrEmpty(DialogueText.zh) && string.IsNullOrEmpty(DialogueText.ja));
                if (isEmpty && !string.IsNullOrEmpty(contentId) && DialogueLocalization.IsLoaded)
                {
                    var locData = DialogueLocalization.GetAllLanguages(contentId);
                    if (locData != null)
                    {
                        if (DialogueText == null) DialogueText = new LocalizedText();
                        DialogueText.en = locData.ContainsKey(Language.English) ? locData[Language.English] : "";
                        DialogueText.zh = locData.ContainsKey(Language.ChineseSimplified) ? locData[Language.ChineseSimplified] : "";
                        DialogueText.ja = locData.ContainsKey(Language.Japanese) ? locData[Language.Japanese] : "";
                    }
                }
            }
            useContentId = newUseId;
            UpdateDialogueInputFields();
            NotifyChange();
        });
        dialogueInputContainer.Add(contentModeDropdown);

        dialogueTextField = new TextField("Dialogue:") { multiline = true };
        dialogueTextField.style.minWidth = 290;
        dialogueTextField.style.maxWidth = 290;
        dialogueTextField.style.minHeight = 60;
        dialogueTextField.RegisterValueChangedCallback(evt =>
        {
            if (DialogueText == null) DialogueText = new LocalizedText();
            if (editorWindow != null) DialogueText.SetText(editorWindow.GetCurrentLanguage(), evt.newValue);
            NotifyChange();
        });

        dialogueIdField = new TextField("Dialogue ID:");
        dialogueIdField.style.minWidth = 290;
        dialogueIdField.style.maxWidth = 290;
        dialogueIdField.RegisterValueChangedCallback(evt =>
        {
            contentId = evt.newValue.Trim();
            UpdateDialoguePreview();
            if (!string.IsNullOrEmpty(contentId) && DialogueLocalization.IsLoaded)
            {
                var locData = DialogueLocalization.GetAllLanguages(contentId);
                if (locData != null)
                {
                    DialogueText.en = locData.ContainsKey(Language.English) ? locData[Language.English] : "";
                    DialogueText.zh = locData.ContainsKey(Language.ChineseSimplified) ? locData[Language.ChineseSimplified] : "";
                    DialogueText.ja = locData.ContainsKey(Language.Japanese) ? locData[Language.Japanese] : "";
                }
            }
            NotifyChange();
        });

        dialoguePreviewLabel = new Label("");
        dialoguePreviewLabel.style.marginTop = 5;
        dialoguePreviewLabel.style.paddingLeft = 5;
        dialoguePreviewLabel.style.paddingRight = 5;
        dialoguePreviewLabel.style.paddingTop = 5;
        dialoguePreviewLabel.style.paddingBottom = 5;
        dialoguePreviewLabel.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f, 0.8f));
        dialoguePreviewLabel.style.borderTopWidth = 1;
        dialoguePreviewLabel.style.borderBottomWidth = 1;
        dialoguePreviewLabel.style.borderLeftWidth = 1;
        dialoguePreviewLabel.style.borderRightWidth = 1;
        dialoguePreviewLabel.style.borderTopColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
        dialoguePreviewLabel.style.borderBottomColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
        dialoguePreviewLabel.style.borderLeftColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
        dialoguePreviewLabel.style.borderRightColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
        dialoguePreviewLabel.style.whiteSpace = WhiteSpace.Normal;
        dialoguePreviewLabel.style.minHeight = 40;
        dialoguePreviewLabel.style.maxWidth = 290;

        UpdateDialogueInputFields();

        // ====== Text Alignment Row ======
        var alignRow = new VisualElement();
        alignRow.style.flexDirection = FlexDirection.Row;
        alignRow.style.alignItems = Align.Center;
        alignRow.style.marginTop = 6;
        alignRow.style.marginBottom = 2;

        var alignLabel = new Label("Align:");
        alignLabel.style.minWidth = 42;
        alignLabel.style.fontSize = 10;
        alignLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
        alignRow.Add(alignLabel);

        alignLeftBtn   = new Button(() => { contentAlignment = TextAlignmentType.Left;   UpdateAlignmentButtonStyles(); NotifyChange(); }) { text = "◀ Left"  };
        alignCenterBtn = new Button(() => { contentAlignment = TextAlignmentType.Center; UpdateAlignmentButtonStyles(); NotifyChange(); }) { text = "Center"  };
        alignRightBtn  = new Button(() => { contentAlignment = TextAlignmentType.Right;  UpdateAlignmentButtonStyles(); NotifyChange(); }) { text = "Right ▶" };

        foreach (var btn in new[] { alignLeftBtn, alignCenterBtn, alignRightBtn })
        {
            btn.style.height = 20;
            btn.style.fontSize = 10;
            btn.style.marginRight = 3;
            btn.style.paddingLeft = 5;
            btn.style.paddingRight = 5;
        }
        alignRow.Add(alignLeftBtn);
        alignRow.Add(alignCenterBtn);
        alignRow.Add(alignRightBtn);
        UpdateAlignmentButtonStyles();
        dialogueInputContainer.Add(alignRow);
        // ================================

        mainContainer.Add(dialogueInputContainer);
    }

    private void UpdateDialogueInputFields()
    {
        dialogueTextField.RemoveFromHierarchy();
        dialogueIdField.RemoveFromHierarchy();
        dialoguePreviewLabel.RemoveFromHierarchy();

        if (contentModeDropdown.parent != dialogueInputContainer)
            dialogueInputContainer.Insert(0, contentModeDropdown);

        if (useContentId)
        {
            dialogueInputContainer.Add(dialogueIdField);
            dialogueInputContainer.Add(dialoguePreviewLabel);
            dialogueIdField.SetValueWithoutNotify(contentId ?? "");
            UpdateDialoguePreview();
        }
        else
        {
            dialogueInputContainer.Add(dialogueTextField);
            if (editorWindow != null && DialogueText != null)
                dialogueTextField.SetValueWithoutNotify(DialogueText.GetText(editorWindow.GetCurrentLanguage()));
        }
    }

    private void UpdateDialoguePreview()
    {
        if (dialoguePreviewLabel == null) return;
        if (string.IsNullOrEmpty(contentId))
        {
            dialoguePreviewLabel.text = "[未设置ID]";
            dialoguePreviewLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
            return;
        }
        if (!DialogueLocalization.IsLoaded)
        {
            dialoguePreviewLabel.text = "[本地化数据未加载]";
            dialoguePreviewLabel.style.color = new StyleColor(new Color(0.7f, 0.5f, 0.2f));
            return;
        }
        Language currentLang = editorWindow?.GetCurrentLanguage() ?? Language.English;
        string previewText = DialogueLocalization.GetText(contentId, currentLang);
        if (previewText == null)
        {
            dialoguePreviewLabel.text = $"[错误: ID '{contentId}' 不存在]";
            dialoguePreviewLabel.style.color = new StyleColor(new Color(0.8f, 0.2f, 0.2f));
        }
        else
        {
            dialoguePreviewLabel.text = string.IsNullOrEmpty(previewText) ? "[空文本]" : previewText;
            dialoguePreviewLabel.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.8f));
        }
    }

    private void UpdateChoicePreview(int index)
    {
        if (index >= choicePreviewLabels.Count || choicePreviewLabels[index] == null) return;
        if (index >= ChoicesData.Count) return;
        var previewLabel = choicePreviewLabels[index];
        string choiceId = ChoicesData[index].textId;
        if (string.IsNullOrEmpty(choiceId)) { previewLabel.text = "[未设置ID]"; previewLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f)); return; }
        if (!DialogueLocalization.IsLoaded) { previewLabel.text = "[本地化数据未加载]"; previewLabel.style.color = new StyleColor(new Color(0.7f, 0.5f, 0.2f)); return; }
        Language currentLang = editorWindow?.GetCurrentLanguage() ?? Language.English;
        string previewText = DialogueLocalization.GetText(choiceId, currentLang);
        if (previewText == null) { previewLabel.text = $"[错误: ID '{choiceId}' 不存在]"; previewLabel.style.color = new StyleColor(new Color(0.8f, 0.2f, 0.2f)); }
        else { previewLabel.text = string.IsNullOrEmpty(previewText) ? "[空文本]" : previewText; previewLabel.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.8f)); }
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
        eventsContainer.style.borderTopWidth = 1; eventsContainer.style.borderBottomWidth = 1;
        eventsContainer.style.borderLeftWidth = 1; eventsContainer.style.borderRightWidth = 1;
        eventsContainer.style.borderTopColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
        eventsContainer.style.borderBottomColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
        eventsContainer.style.borderLeftColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
        eventsContainer.style.borderRightColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
        eventsContainer.style.paddingTop = 5; eventsContainer.style.paddingBottom = 5;
        eventsContainer.style.paddingLeft = 5; eventsContainer.style.paddingRight = 5;
        eventsContainer.style.marginTop = 2;
        mainContainer.Add(eventsContainer);

        addEventButton = new Button(() => { AddEventCall(); NotifyChange(); }) { text = "+ Add Event" };
        addEventButton.style.marginTop = 2;
        mainContainer.Add(addEventButton);
        UpdateEventsDisplay();
    }

    private void AddEventCall() { EventCalls.Add(new DialogueEventCall()); UpdateEventsDisplay(); }

    private void RemoveEventCall(int index)
    {
        if (index >= 0 && index < EventCalls.Count) { EventCalls.RemoveAt(index); UpdateEventsDisplay(); }
    }

    private void UpdateEventsDisplay()
    {
        eventsContainer.Clear();
        if (EventCalls.Count == 0)
        {
            var lbl = new Label("List is Empty");
            lbl.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
            lbl.style.unityFontStyleAndWeight = FontStyle.Italic;
            lbl.style.paddingLeft = 10; lbl.style.paddingTop = 5; lbl.style.paddingBottom = 5;
            eventsContainer.Add(lbl);
            return;
        }
        for (int i = 0; i < EventCalls.Count; i++)
        {
            int currentIndex = i;
            var eventCall = EventCalls[i];
            if (!string.IsNullOrEmpty(eventCall.methodName) && eventCall.methodName.Contains("|"))
            {
                var parts = eventCall.methodName.Split('|');
                if (parts.Length == 2)
                {
                    string pt = parts[1];
                    if (pt == "Int32") eventCall.parameterType = ParameterType.Int;
                    else if (pt == "Single") eventCall.parameterType = ParameterType.Float;
                    else if (pt == "String") eventCall.parameterType = ParameterType.String;
                    else if (pt == "Boolean") eventCall.parameterType = ParameterType.Bool;
                }
            }
            var ec = new VisualElement();
            ec.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f, 0.5f));
            ec.style.marginTop = 3; ec.style.paddingTop = 5; ec.style.paddingBottom = 5;
            ec.style.paddingLeft = 5; ec.style.paddingRight = 5;
            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row; titleRow.style.alignItems = Align.Center;
            var titleLabel = new Label($"Event {i}"); titleLabel.style.flexGrow = 1; titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            var removeBtn = new Button(() => { RemoveEventCall(currentIndex); NotifyChange(); }) { text = "×" };
            removeBtn.style.width = 20; removeBtn.style.height = 18; removeBtn.style.fontSize = 12;
            titleRow.Add(titleLabel); titleRow.Add(removeBtn); ec.Add(titleRow);
            var triggerEnum = new EnumField("Event Timing:", eventCall.triggerTiming);
            triggerEnum.style.marginTop = 3;
            triggerEnum.RegisterValueChangedCallback(evt =>
            {
                if (currentIndex < EventCalls.Count) { EventCalls[currentIndex].triggerTiming = (EventTriggerTiming)evt.newValue; UpdateEventsDisplay(); NotifyChange(); }
            });
            ec.Add(triggerEnum);
            bool hasNextNode = (ChoicesData.Count > 0) || (conditionalBranchesData.Count > 0) || (defaultOutputPort != null && defaultOutputPort.connected);
            if (eventCall.triggerTiming == EventTriggerTiming.OnDialogueDisappear && hasNextNode)
            {
                var wb = new VisualElement();
                wb.style.marginTop = 2; wb.style.backgroundColor = new StyleColor(new Color(0.8f, 0.4f, 0.2f, 0.3f));
                wb.style.paddingTop = 2; wb.style.paddingBottom = 2; wb.style.paddingLeft = 5; wb.style.paddingRight = 5;
                wb.style.borderTopLeftRadius = 2; wb.style.borderTopRightRadius = 2; wb.style.borderBottomLeftRadius = 2; wb.style.borderBottomRightRadius = 2;
                var wl = new Label("⚠ This event will NOT run (node has next dialogue)");
                wl.style.fontSize = 9; wl.style.color = new StyleColor(new Color(1f, 0.8f, 0.3f)); wl.style.whiteSpace = WhiteSpace.Normal;
                wb.Add(wl); ec.Add(wb);
            }
            GameObject currentGameObject = null;
            if (!string.IsNullOrEmpty(eventCall.targetObjectID)) currentGameObject = DialogueEventTarget.FindByID(eventCall.targetObjectID);
            if (currentGameObject != null && currentIndex < EventCalls.Count)
            {
                string cn = currentGameObject.name;
                if (string.IsNullOrEmpty(eventCall.targetObjectName) || eventCall.targetObjectName == cn)
                    if (EventCalls[currentIndex].targetObjectName != cn) { EventCalls[currentIndex].targetObjectName = cn; NotifyChange(); }
            }
            var goField = new ObjectField("Target GameObject:") { objectType = typeof(GameObject), value = currentGameObject, allowSceneObjects = true };
            goField.style.marginTop = 3; goField.style.maxWidth = 240; goField.style.overflow = Overflow.Hidden;
            goField.RegisterCallback<GeometryChangedEvent>(evt => { var lbl = goField.Q<Label>(); if (lbl != null) { lbl.style.overflow = Overflow.Hidden; lbl.style.textOverflow = TextOverflow.Ellipsis; } });
            goField.RegisterValueChangedCallback(evt =>
            {
                if (currentIndex < EventCalls.Count)
                {
                    var selectedGO = evt.newValue as GameObject;
                    if (selectedGO != null)
                    {
                        if (PrefabUtility.IsPartOfPrefabAsset(selectedGO))
                        {
                            EditorUtility.DisplayDialog("Invalid Target", "Cannot use prefab as event target. Please drag the prefab into the scene first.", "OK");
                            gameObjectField_Clear(goField, currentIndex);
                            return;
                        }
                        var rc = DialogueEventTarget.GetOrCreate(selectedGO);
                        EventCalls[currentIndex].targetObjectID = rc.UniqueID;
                        EventCalls[currentIndex].targetObjectName = selectedGO.name;
                        EventCalls[currentIndex].targetSceneName = selectedGO.scene.name;
                    }
                    else { EventCalls[currentIndex].targetObjectID = ""; EventCalls[currentIndex].targetObjectName = ""; EventCalls[currentIndex].targetSceneName = ""; }
                    EditorApplication.delayCall += () => { if (this != null) UpdateEventsDisplay(); };
                    NotifyChange();
                }
            });
            ec.Add(goField);
            if (currentGameObject != null)
            {
                BuildEventComponentUI(ec, currentGameObject, currentIndex, eventCall);
            }
            else
            {
                bool hasSaved = !string.IsNullOrEmpty(eventCall.targetObjectID) || !string.IsNullOrEmpty(eventCall.targetObjectName);
                if (hasSaved)
                {
                    if (!string.IsNullOrEmpty(eventCall.targetSceneName))
                    {
                        var sl = new Label($"Object {eventCall.targetObjectName} from Scene {eventCall.targetSceneName}");
                        sl.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f)); sl.style.unityFontStyleAndWeight = FontStyle.Italic;
                        sl.style.marginTop = 3; sl.style.paddingLeft = 10; sl.style.fontSize = 10; ec.Add(sl);
                        var hl = new Label("Object not found in current scene.");
                        hl.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f)); hl.style.unityFontStyleAndWeight = FontStyle.Italic;
                        hl.style.marginTop = 1; hl.style.paddingLeft = 10; hl.style.fontSize = 9; hl.style.whiteSpace = WhiteSpace.Normal; hl.style.maxWidth = 280; ec.Add(hl);
                    }
                    else
                    {
                        var wc = new VisualElement();
                        wc.style.marginTop = 3; wc.style.marginLeft = 10; wc.style.backgroundColor = new StyleColor(new Color(0.6f, 0.3f, 0.2f, 0.4f));
                        wc.style.paddingTop = 3; wc.style.paddingBottom = 3; wc.style.paddingLeft = 5; wc.style.paddingRight = 5;
                        wc.style.borderTopLeftRadius = 2; wc.style.borderTopRightRadius = 2; wc.style.borderBottomLeftRadius = 2; wc.style.borderBottomRightRadius = 2; wc.style.maxWidth = 270;
                        var wl2 = new Label($"⚠ Object {eventCall.targetObjectName} not found in any scene");
                        wl2.style.color = new StyleColor(new Color(1f, 0.9f, 0.7f)); wl2.style.fontSize = 10; wl2.style.whiteSpace = WhiteSpace.Normal;
                        wc.Add(wl2); ec.Add(wc);
                    }
                }
                else
                {
                    var hl = new Label("Select a GameObject first");
                    hl.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f)); hl.style.unityFontStyleAndWeight = FontStyle.Italic;
                    hl.style.marginTop = 3; hl.style.paddingLeft = 10; ec.Add(hl);
                }
            }
            eventsContainer.Add(ec);
        }
    }

    private void gameObjectField_Clear(ObjectField field, int idx)
    {
        field.value = null;
        EventCalls[idx].targetObjectID = ""; EventCalls[idx].targetObjectName = ""; EventCalls[idx].targetSceneName = "";
        EditorApplication.delayCall += () => { if (this != null) UpdateEventsDisplay(); };
        NotifyChange();
    }

    private void BuildEventComponentUI(VisualElement ec, GameObject go, int currentIndex, DialogueEventCall eventCall)
    {
        var components = go.GetComponents<Component>();
        var componentNames = new List<string> { "None" };
        var componentTypes = new List<System.Type> { null };
        foreach (var comp in components) { if (comp != null) { componentNames.Add(comp.GetType().Name); componentTypes.Add(comp.GetType()); } }
        int selCompIdx = 0;
        if (!string.IsNullOrEmpty(eventCall.componentTypeName)) { selCompIdx = componentNames.IndexOf(eventCall.componentTypeName); if (selCompIdx < 0) selCompIdx = 0; }
        var compDrop = new PopupField<string>("Component:", componentNames, selCompIdx);
        compDrop.style.marginTop = 3;
        compDrop.RegisterValueChangedCallback(evt =>
        {
            if (currentIndex < EventCalls.Count) { EventCalls[currentIndex].componentTypeName = evt.newValue != "None" ? evt.newValue : ""; UpdateEventsDisplay(); NotifyChange(); }
        });
        ec.Add(compDrop);
        if (selCompIdx > 0 && componentTypes[selCompIdx] != null)
        {
            var comp = go.GetComponent(componentTypes[selCompIdx]);
            if (comp == null) return;
            var methods = componentTypes[selCompIdx].GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName && m.GetParameters().Length <= 1).ToList();
            var methodNames = new List<string> { "None" };
            var methodInfos = new List<System.Reflection.MethodInfo> { null };
            foreach (var m in methods)
            {
                var ps = m.GetParameters();
                if (ps.Length == 0) { methodNames.Add(m.Name + " ()"); methodInfos.Add(m); }
                else if (ps.Length == 1)
                {
                    var pt = ps[0].ParameterType;
                    if (pt == typeof(int) || pt == typeof(float) || pt == typeof(string) || pt == typeof(bool))
                    { methodNames.Add($"{m.Name} ({pt.Name})"); methodInfos.Add(m); }
                }
            }
            int selMethIdx = 0;
            if (!string.IsNullOrEmpty(eventCall.methodName))
            {
                string baseName = eventCall.methodName.Contains("|") ? eventCall.methodName.Split('|')[0] : eventCall.methodName;
                for (int j = 0; j < methodInfos.Count; j++)
                {
                    if (methodInfos[j] != null && methodInfos[j].Name == baseName)
                    {
                        var mp = methodInfos[j].GetParameters();
                        bool match = (mp.Length == 0 && eventCall.parameterType == ParameterType.None) ||
                                     (mp.Length == 1 && ((mp[0].ParameterType == typeof(int) && eventCall.parameterType == ParameterType.Int) ||
                                                          (mp[0].ParameterType == typeof(float) && eventCall.parameterType == ParameterType.Float) ||
                                                          (mp[0].ParameterType == typeof(string) && eventCall.parameterType == ParameterType.String) ||
                                                          (mp[0].ParameterType == typeof(bool) && eventCall.parameterType == ParameterType.Bool)));
                        if (match) { selMethIdx = j; break; }
                    }
                }
            }
            var methDrop = new PopupField<string>("Function:", methodNames, selMethIdx);
            methDrop.style.marginTop = 3;
            methDrop.RegisterValueChangedCallback(evt =>
            {
                if (currentIndex < EventCalls.Count)
                {
                    int idx2 = methodNames.IndexOf(evt.newValue);
                    if (idx2 > 0 && methodInfos[idx2] != null)
                    {
                        var m2 = methodInfos[idx2]; var ps2 = m2.GetParameters();
                        EventCalls[currentIndex].methodName = ps2.Length == 1 ? $"{m2.Name}|{ps2[0].ParameterType.Name}" : m2.Name;
                        EventCalls[currentIndex].parameterType = ps2.Length == 0 ? ParameterType.None :
                            ps2[0].ParameterType == typeof(int) ? ParameterType.Int :
                            ps2[0].ParameterType == typeof(float) ? ParameterType.Float :
                            ps2[0].ParameterType == typeof(string) ? ParameterType.String :
                            ps2[0].ParameterType == typeof(bool) ? ParameterType.Bool : ParameterType.None;
                    }
                    else { EventCalls[currentIndex].methodName = ""; EventCalls[currentIndex].parameterType = ParameterType.None; }
                    UpdateEventsDisplay(); NotifyChange();
                }
            });
            ec.Add(methDrop);
            if (selMethIdx > 0 && methodInfos[selMethIdx] != null)
            {
                var ps3 = methodInfos[selMethIdx].GetParameters();
                if (ps3.Length == 1)
                {
                    var pc = new VisualElement(); pc.style.marginTop = 3; pc.style.paddingLeft = 10;
                    var pt3 = ps3[0].ParameterType;
                    if (pt3 == typeof(string)) { var f = new TextField("Parameter:") { value = eventCall.stringParameter }; f.RegisterValueChangedCallback(e => { if (currentIndex < EventCalls.Count) { EventCalls[currentIndex].stringParameter = e.newValue; NotifyChange(); } }); pc.Add(f); }
                    else if (pt3 == typeof(int)) { var f = new IntegerField("Parameter:") { value = eventCall.intParameter }; f.RegisterValueChangedCallback(e => { if (currentIndex < EventCalls.Count) { EventCalls[currentIndex].intParameter = e.newValue; NotifyChange(); } }); pc.Add(f); }
                    else if (pt3 == typeof(float)) { var f = new FloatField("Parameter:") { value = eventCall.floatParameter }; f.RegisterValueChangedCallback(e => { if (currentIndex < EventCalls.Count) { EventCalls[currentIndex].floatParameter = e.newValue; NotifyChange(); } }); pc.Add(f); }
                    else if (pt3 == typeof(bool)) { var f = new Toggle("Parameter:") { value = eventCall.boolParameter }; f.RegisterValueChangedCallback(e => { if (currentIndex < EventCalls.Count) { EventCalls[currentIndex].boolParameter = e.newValue; NotifyChange(); } }); pc.Add(f); }
                    ec.Add(pc);
                }
            }
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
        if (!isConditionalMode || conditionalBranchesData.Count <= 1) return;
        var lbl = new Label("Conditional Branches:"); lbl.style.unityFontStyleAndWeight = FontStyle.Bold; lbl.style.marginBottom = 5;
        conditionalBranchesContainer.Add(lbl);
        foreach (var kvp in conditionalBranchesData.OrderByDescending(b => b.Key))
        {
            if (kvp.Key == 0) continue;
            CreateBranchConditionEditor(kvp.Key, kvp.Value);
        }
    }

    private void CreateBranchConditionEditor(int priority, ConditionalBranchData branchData)
    {
        var bc = new VisualElement();
        bc.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f, 0.5f));
        bc.style.marginTop = 5; bc.style.paddingTop = 8; bc.style.paddingBottom = 8; bc.style.paddingLeft = 8; bc.style.paddingRight = 8;
        bc.style.borderTopLeftRadius = 4; bc.style.borderTopRightRadius = 4; bc.style.borderBottomLeftRadius = 4; bc.style.borderBottomRightRadius = 4;
        bc.style.borderLeftWidth = 3; bc.style.borderLeftColor = GetPriorityColor(priority);
        var hr = new VisualElement(); hr.style.flexDirection = FlexDirection.Row; hr.style.alignItems = Align.Center; hr.style.marginBottom = 8;
        var bl = new Label($"Priority {priority}"); bl.style.flexGrow = 1; bl.style.unityFontStyleAndWeight = FontStyle.Bold; bl.style.fontSize = 11; bl.style.color = GetPriorityColor(priority);
        var acb = new Button(() => { branchData.conditions.Add(new ChoiceCondition()); UpdateConditionalBranchesDisplay(); NotifyChange(); }) { text = "+ Condition" };
        acb.style.marginLeft = 5; hr.Add(bl); hr.Add(acb); bc.Add(hr);
        for (int i = 0; i < branchData.conditions.Count; i++)
        {
            int ci = i; var cond = branchData.conditions[i];
            var cui = CreateConditionUI(cond, () => { branchData.conditions.RemoveAt(ci); UpdateConditionalBranchesDisplay(); NotifyChange(); }, nc => { branchData.conditions[ci] = nc; NotifyChange(); });
            bc.Add(cui);
        }
        if (branchData.conditions.Count > 1)
        {
            var lr = new VisualElement(); lr.style.flexDirection = FlexDirection.Row; lr.style.marginTop = 8; lr.style.justifyContent = Justify.Center;
            var andBtn = new Button(() => { branchData.conditionLogic = ConditionLogic.AND; UpdateConditionalBranchesDisplay(); NotifyChange(); }) { text = "AND" };
            andBtn.style.width = 60; andBtn.style.backgroundColor = branchData.conditionLogic == ConditionLogic.AND ? new Color(0.3f, 0.5f, 0.3f) : new Color(0.25f, 0.25f, 0.25f);
            var orBtn = new Button(() => { branchData.conditionLogic = ConditionLogic.OR; UpdateConditionalBranchesDisplay(); NotifyChange(); }) { text = "OR" };
            orBtn.style.width = 60; orBtn.style.marginLeft = 5; orBtn.style.backgroundColor = branchData.conditionLogic == ConditionLogic.OR ? new Color(0.3f, 0.5f, 0.3f) : new Color(0.25f, 0.25f, 0.25f);
            lr.Add(andBtn); lr.Add(orBtn); bc.Add(lr);
        }
        conditionalBranchesContainer.Add(bc);
    }

    private VisualElement CreateConditionUI(ChoiceCondition condition, Action onRemove, Action<ChoiceCondition> onUpdate)
    {
        var cc = new VisualElement();
        cc.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
        cc.style.marginTop = 5; cc.style.paddingTop = 5; cc.style.paddingBottom = 5; cc.style.paddingLeft = 5; cc.style.paddingRight = 5;
        cc.style.borderTopLeftRadius = 3; cc.style.borderTopRightRadius = 3; cc.style.borderBottomLeftRadius = 3; cc.style.borderBottomRightRadius = 3;
        var hr = new VisualElement(); hr.style.flexDirection = FlexDirection.Row; hr.style.alignItems = Align.Center; hr.style.marginBottom = 5;
        var cl = new Label("Condition"); cl.style.flexGrow = 1; cl.style.fontSize = 9; cl.style.unityFontStyleAndWeight = FontStyle.Bold;
        var rb = new Button(() => onRemove()) { text = "×" }; rb.style.width = 18; rb.style.height = 18; rb.style.fontSize = 11;
        hr.Add(cl); hr.Add(rb); cc.Add(hr);
        GameObject currentGO = null;
        if (!string.IsNullOrEmpty(condition.targetObjectID)) currentGO = DialogueEventTarget.FindByID(condition.targetObjectID);
        else if (!string.IsNullOrEmpty(condition.targetObjectName)) { var all = Resources.FindObjectsOfTypeAll<GameObject>(); currentGO = System.Array.Find(all, o => o.name == condition.targetObjectName && o.scene.IsValid()); }
        var gof = new ObjectField("GameObject:") { objectType = typeof(GameObject), value = currentGO, allowSceneObjects = true };
        gof.style.fontSize = 9; gof.style.maxWidth = 240; gof.style.overflow = Overflow.Hidden;
        gof.RegisterCallback<GeometryChangedEvent>(evt => { var lbl = gof.Q<Label>(); if (lbl != null) { lbl.style.overflow = Overflow.Hidden; lbl.style.textOverflow = TextOverflow.Ellipsis; } });
        gof.RegisterValueChangedCallback(evt => { var sg = evt.newValue as GameObject; condition.targetObjectName = sg != null ? sg.name : ""; condition.componentTypeName = ""; condition.variableName = ""; onUpdate(condition); if (cc.parent != null) UpdateConditionalBranchesDisplay(); });
        cc.Add(gof);
        if (currentGO != null)
        {
            var comps = currentGO.GetComponents<Component>();
            var cn = new List<string> { "None" }; var ct = new List<System.Type> { null };
            foreach (var c in comps) { if (c != null) { cn.Add(c.GetType().Name); ct.Add(c.GetType()); } }
            int sci = 0; if (!string.IsNullOrEmpty(condition.componentTypeName)) { sci = cn.IndexOf(condition.componentTypeName); if (sci < 0) sci = 0; }
            var cd = new PopupField<string>("Component:", cn, sci); cd.style.marginTop = 3; cd.style.maxWidth = 240; cd.style.fontSize = 9;
            cd.RegisterValueChangedCallback(evt => { condition.componentTypeName = evt.newValue != "None" ? evt.newValue : ""; condition.variableName = ""; onUpdate(condition); UpdateConditionalBranchesDisplay(); });
            cc.Add(cd);
            if (sci > 0 && ct[sci] != null)
            {
                var compType = ct[sci];
                var fields = compType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).Where(f => f.FieldType == typeof(int) || f.FieldType == typeof(float) || f.FieldType == typeof(bool)).ToList();
                var props = compType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).Where(p => (p.PropertyType == typeof(int) || p.PropertyType == typeof(float) || p.PropertyType == typeof(bool)) && p.CanRead).ToList();
                var vn = new List<string> { "None" }; vn.AddRange(fields.Select(f => f.Name)); vn.AddRange(props.Select(p => p.Name));
                if (vn.Count > 1)
                {
                    int svi = string.IsNullOrEmpty(condition.variableName) ? 0 : vn.IndexOf(condition.variableName); if (svi < 0) svi = 0;
                    string dvv = vn[svi];
                    var vr = new VisualElement(); vr.style.flexDirection = FlexDirection.Row; vr.style.alignItems = Align.Center; vr.style.marginTop = 5; vr.style.maxWidth = 240;
                    var vl = new Label("Var:"); vl.style.width = 30; vl.style.fontSize = 9; vl.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f)); vl.style.marginRight = 2; vl.style.flexShrink = 0;
                    var vd = new PopupField<string>(vn, dvv); vd.style.width = 70; vd.style.marginRight = 2; vd.style.fontSize = 9; vd.style.flexShrink = 0;
                    vd.RegisterValueChangedCallback(evt => { condition.variableName = evt.newValue == "None" ? "" : evt.newValue; onUpdate(condition); UpdateConditionalBranchesDisplay(); });
                    vr.Add(vl); vr.Add(vd);
                    if (svi > 0)
                    {
                        System.Type svt = null; string svn = dvv;
                        var f2 = fields.FirstOrDefault(f => f.Name == svn); if (f2 != null) svt = f2.FieldType; else { var p2 = props.FirstOrDefault(p => p.Name == svn); if (p2 != null) svt = p2.PropertyType; }
                        var compTypes = svt == typeof(bool) ? new List<ComparisonType> { ComparisonType.Equal, ComparisonType.NotEqual } :
                            new List<ComparisonType> { ComparisonType.Equal, ComparisonType.NotEqual, ComparisonType.Greater, ComparisonType.Less, ComparisonType.GreaterOrEqual, ComparisonType.LessOrEqual };
                        var compNames = compTypes.Select(c2 => GetComparisonDisplayName(c2)).ToList();
                        int sci2 = compTypes.IndexOf(condition.comparison); if (sci2 < 0) sci2 = 0;
                        var compDrop2 = new PopupField<string>(compNames, compNames[sci2]); compDrop2.style.width = 50; compDrop2.style.marginRight = 2; compDrop2.style.fontSize = 9; compDrop2.style.flexShrink = 0;
                        compDrop2.RegisterValueChangedCallback(evt => { int idx3 = compNames.IndexOf(evt.newValue); condition.comparison = compTypes[idx3]; onUpdate(condition); });
                        vr.Add(compDrop2);
                        if (svt == typeof(bool))
                        {
                            var bv = new List<string> { "True", "False" }; string dbv = "True";
                            if (!string.IsNullOrEmpty(condition.compareValue) && condition.compareValue.Equals("False", System.StringComparison.OrdinalIgnoreCase)) dbv = "False"; else condition.compareValue = "True";
                            var bd = new PopupField<string>(bv, dbv); bd.style.flexGrow = 1; bd.style.flexShrink = 1; bd.style.minWidth = 40; bd.style.fontSize = 9;
                            bd.RegisterValueChangedCallback(evt => { condition.compareValue = evt.newValue; onUpdate(condition); }); vr.Add(bd);
                        }
                        else
                        {
                            var vf2 = new TextField(); vf2.value = condition.compareValue; vf2.style.flexGrow = 1; vf2.style.flexShrink = 1; vf2.style.minWidth = 40; vf2.style.fontSize = 9;
                            vf2.RegisterValueChangedCallback(evt => { condition.compareValue = evt.newValue; onUpdate(condition); }); vr.Add(vf2);
                        }
                    }
                    cc.Add(vr);
                }
                else { var nvl = new Label("No public int/float/bool variables found"); nvl.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f)); nvl.style.unityFontStyleAndWeight = FontStyle.Italic; nvl.style.marginTop = 5; nvl.style.paddingLeft = 10; nvl.style.fontSize = 9; cc.Add(nvl); }
            }
        }
        else { var hl = new Label("Select a GameObject first"); hl.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f)); hl.style.unityFontStyleAndWeight = FontStyle.Italic; hl.style.marginTop = 5; hl.style.paddingLeft = 10; hl.style.fontSize = 9; cc.Add(hl); }
        return cc;
    }

    private Color GetPriorityColor(int priority)
    {
        var colors = new Color[] { new Color(0.4f, 0.7f, 1f), new Color(0.5f, 1f, 0.5f), new Color(1f, 0.8f, 0.4f), new Color(1f, 0.5f, 0.8f), new Color(0.8f, 0.5f, 1f) };
        return colors[(priority - 1) % colors.Length];
    }
    #endregion

    #region Choices Section
    private void CreateChoicesSection()
    {
        var lbl = new Label("Player Choices:"); lbl.style.marginTop = 10; lbl.style.unityFontStyleAndWeight = FontStyle.Bold; mainContainer.Add(lbl);
        choicesContainer = new VisualElement(); mainContainer.Add(choicesContainer);
        addChoiceButton = new Button(() => { AddChoice(new ChoiceData { text = new LocalizedText("New Choice") }); NotifyChange(); }) { text = "Add Choice" };
        addChoiceButton.style.marginTop = 5; mainContainer.Add(addChoiceButton);
    }

    private void AddChoice(ChoiceData choiceData) { int index = ChoicesData.Count; ChoicesData.Add(choiceData); RebuildChoiceUI(index); RefreshExpandedState(); RefreshPorts(); }

    private void RemoveChoice(int index)
    {
        if (index < 0 || index >= ChoicesData.Count) return;
        var graphView = GetFirstAncestorOfType<DialogueGraphViewEditor>();
        var connectionData = new Dictionary<int, Port>();
        for (int i = 0; i < choiceOutputPorts.Count; i++)
        {
            if (i == index) continue;
            var port = choiceOutputPorts[i];
            if (port != null && port.connected) { var edge = port.connections.FirstOrDefault(); if (edge != null && edge.input != null) connectionData[i] = edge.input; }
        }
        if (index < choiceOutputPorts.Count)
        {
            var port = choiceOutputPorts[index];
            if (graphView != null && port != null) { foreach (var edge in port.connections.ToList()) graphView.RemoveElement(edge); }
        }
        ChoicesData.RemoveAt(index);
        choicesContainer.Clear(); choiceOutputPorts.Clear();
        for (int i = 0; i < ChoicesData.Count; i++) RebuildChoiceUI(i);
        foreach (var kvp in connectionData)
        {
            int newIndex = kvp.Key > index ? kvp.Key - 1 : kvp.Key;
            if (newIndex >= 0 && newIndex < choiceOutputPorts.Count && graphView != null)
            {
                var newEdge = choiceOutputPorts[newIndex].ConnectTo(kvp.Value);
                graphView.AddElement(newEdge);
            }
        }
        RefreshExpandedState(); RefreshPorts();
    }

    private void RebuildAllChoicesUI()
    {
        var graphView = GetFirstAncestorOfType<DialogueGraphViewEditor>();
        var savedConnections = new List<(int choiceIndex, DialogueNodeEditor targetNode)>();
        if (graphView != null)
        {
            var edges = graphView.edges.ToList();
            foreach (var edge in edges) { if (edge.output.node == this) { int ci = GetChoiceIndexForPort(edge.output); if (ci >= 0 && edge.input.node is DialogueNodeEditor tn) savedConnections.Add((ci, tn)); } }
            foreach (var edge in edges) { if (edge.output.node == this) graphView.RemoveElement(edge); }
        }
        choicesContainer.Clear(); choiceOutputPorts.Clear(); choiceIdFields.Clear();
        for (int i = 0; i < ChoicesData.Count; i++) RebuildChoiceUI(i);
        RefreshExpandedState(); RefreshPorts();
        if (graphView != null) { foreach (var (ci, tn) in savedConnections) { if (ci < choiceOutputPorts.Count) { var ip = tn.inputContainer.Q<Port>(); if (ip != null) graphView.AddElement(choiceOutputPorts[ci].ConnectTo(ip)); } } }
    }

    private void RebuildChoiceUI(int index)
    {
        var cc = new VisualElement();
        cc.style.marginTop = 10; cc.style.marginBottom = 5; cc.style.borderLeftWidth = 3; cc.style.borderLeftColor = GetChoiceColor(index);
        cc.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f, 0.5f));
        cc.style.paddingTop = 8; cc.style.paddingBottom = 8; cc.style.paddingLeft = 8; cc.style.paddingRight = 8;
        cc.style.borderTopLeftRadius = 4; cc.style.borderTopRightRadius = 4; cc.style.borderBottomLeftRadius = 4; cc.style.borderBottomRightRadius = 4;
        var op = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
        op.portName = ChoicesData[index].text != null ? ChoicesData[index].text.GetText(editorWindow?.GetCurrentLanguage() ?? Language.English) : "";
        op.userData = -2 - index; op.portColor = GetChoiceColorRaw(index); choiceOutputPorts.Add(op);
        var portRow = new VisualElement(); portRow.style.flexDirection = FlexDirection.Row; portRow.style.alignItems = Align.Center; portRow.style.justifyContent = Justify.SpaceBetween; portRow.style.marginBottom = 8;
        var portLabel = new Label($"Choice {index + 1}:"); portLabel.style.fontSize = 11; portLabel.style.color = GetChoiceColor(index); portLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        portRow.Add(portLabel); portRow.Add(op); cc.Add(portRow);
        var modeRow = new VisualElement(); modeRow.style.marginBottom = 5;
        var choiceModeDropdown = new DropdownField("Input Mode:", new List<string> { "Direct Input", "Use ID" }, ChoicesData[index].useTextId ? 1 : 0);
        choiceModeDropdown.style.fontSize = 9;
        int currentIndex = index;
        choiceModeDropdown.RegisterValueChangedCallback(evt =>
        {
            if (currentIndex < ChoicesData.Count)
            {
                bool oldUseId = ChoicesData[currentIndex].useTextId; bool newUseId = (evt.newValue == "Use ID");
                if (oldUseId && !newUseId)
                {
                    bool isEmpty = ChoicesData[currentIndex].text == null || (string.IsNullOrEmpty(ChoicesData[currentIndex].text.en) && string.IsNullOrEmpty(ChoicesData[currentIndex].text.zh) && string.IsNullOrEmpty(ChoicesData[currentIndex].text.ja));
                    if (isEmpty && !string.IsNullOrEmpty(ChoicesData[currentIndex].textId) && DialogueLocalization.IsLoaded)
                    {
                        var ld = DialogueLocalization.GetAllLanguages(ChoicesData[currentIndex].textId);
                        if (ld != null) { if (ChoicesData[currentIndex].text == null) ChoicesData[currentIndex].text = new LocalizedText(); ChoicesData[currentIndex].text.en = ld.ContainsKey(Language.English) ? ld[Language.English] : ""; ChoicesData[currentIndex].text.zh = ld.ContainsKey(Language.ChineseSimplified) ? ld[Language.ChineseSimplified] : ""; ChoicesData[currentIndex].text.ja = ld.ContainsKey(Language.Japanese) ? ld[Language.Japanese] : ""; }
                    }
                }
                ChoicesData[currentIndex].useTextId = newUseId; RebuildAllChoicesUI(); NotifyChange();
            }
        });
        modeRow.Add(choiceModeDropdown); cc.Add(modeRow);
        var inputRow = new VisualElement(); inputRow.style.flexDirection = FlexDirection.Row; inputRow.style.alignItems = Align.Center; inputRow.style.marginBottom = 5;
        if (ChoicesData[index].useTextId)
        {
            var idLbl = new Label("Text ID:"); idLbl.style.minWidth = 50; idLbl.style.maxWidth = 50; idLbl.style.fontSize = 10; idLbl.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f)); idLbl.style.marginRight = 5;
            var tidField = new TextField(); tidField.value = ChoicesData[index].textId ?? ""; tidField.style.flexGrow = 1; tidField.style.flexShrink = 1; tidField.style.minWidth = 80;
            while (choiceIdFields.Count <= index) choiceIdFields.Add(null); choiceIdFields[index] = tidField;
            tidField.RegisterValueChangedCallback(evt =>
            {
                if (currentIndex < ChoicesData.Count)
                {
                    ChoicesData[currentIndex].textId = evt.newValue.Trim(); UpdateChoicePreview(currentIndex);
                    if (!string.IsNullOrEmpty(evt.newValue) && DialogueLocalization.IsLoaded)
                    {
                        var ld = DialogueLocalization.GetAllLanguages(evt.newValue);
                        if (ld != null) { ChoicesData[currentIndex].text.en = ld.ContainsKey(Language.English) ? ld[Language.English] : ""; ChoicesData[currentIndex].text.zh = ld.ContainsKey(Language.ChineseSimplified) ? ld[Language.ChineseSimplified] : ""; ChoicesData[currentIndex].text.ja = ld.ContainsKey(Language.Japanese) ? ld[Language.Japanese] : ""; if (currentIndex < choiceOutputPorts.Count && editorWindow != null) choiceOutputPorts[currentIndex].portName = ChoicesData[currentIndex].text.GetText(editorWindow.GetCurrentLanguage()); }
                    }
                    NotifyChange();
                }
            });
            inputRow.Add(idLbl); inputRow.Add(tidField);
        }
        else
        {
            var txtLbl = new Label("Text:"); txtLbl.style.minWidth = 40; txtLbl.style.maxWidth = 40; txtLbl.style.fontSize = 10; txtLbl.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f)); txtLbl.style.marginRight = 5;
            var cf = new TextField(); cf.value = ChoicesData[index].text != null && editorWindow != null ? ChoicesData[index].text.GetText(editorWindow.GetCurrentLanguage()) : ""; cf.style.flexGrow = 1; cf.style.flexShrink = 1; cf.style.minWidth = 80;
            cf.RegisterValueChangedCallback(evt =>
            {
                if (currentIndex < ChoicesData.Count) { if (ChoicesData[currentIndex].text == null) ChoicesData[currentIndex].text = new LocalizedText(); if (editorWindow != null) ChoicesData[currentIndex].text.SetText(editorWindow.GetCurrentLanguage(), evt.newValue); if (currentIndex < choiceOutputPorts.Count) choiceOutputPorts[currentIndex].portName = evt.newValue; NotifyChange(); }
            });
            inputRow.Add(txtLbl); inputRow.Add(cf);
        }
        var removeBtn2 = new Button(() => { RemoveChoice(currentIndex); NotifyChange(); }) { text = "×" };
        removeBtn2.style.minWidth = 22; removeBtn2.style.maxWidth = 22; removeBtn2.style.minHeight = 22; removeBtn2.style.maxHeight = 22;
        removeBtn2.style.marginLeft = 5; removeBtn2.style.fontSize = 14; removeBtn2.style.backgroundColor = new StyleColor(new Color(0.6f, 0.2f, 0.2f)); removeBtn2.style.flexShrink = 0;
        inputRow.Add(removeBtn2); cc.Add(inputRow);
        Label previewLabel = null;
        if (ChoicesData[index].useTextId)
        {
            previewLabel = new Label("");
            previewLabel.style.marginTop = 5; previewLabel.style.marginBottom = 5; previewLabel.style.paddingLeft = 5; previewLabel.style.paddingRight = 5; previewLabel.style.paddingTop = 3; previewLabel.style.paddingBottom = 3;
            previewLabel.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f, 0.8f));
            previewLabel.style.borderTopWidth = 1; previewLabel.style.borderBottomWidth = 1; previewLabel.style.borderLeftWidth = 1; previewLabel.style.borderRightWidth = 1;
            previewLabel.style.borderTopColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f)); previewLabel.style.borderBottomColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
            previewLabel.style.borderLeftColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f)); previewLabel.style.borderRightColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
            previewLabel.style.whiteSpace = WhiteSpace.Normal; previewLabel.style.fontSize = 10; cc.Add(previewLabel);
        }
        while (choicePreviewLabels.Count <= index) choicePreviewLabels.Add(null); choicePreviewLabels[index] = previewLabel;
        if (ChoicesData[index].useTextId) UpdateChoicePreview(index);
        var condHdr = new VisualElement(); condHdr.style.flexDirection = FlexDirection.Row; condHdr.style.alignItems = Align.Center; condHdr.style.marginTop = 5;
        var condLbl = new Label("Conditions:"); condLbl.style.fontSize = 9; condLbl.style.color = new Color(0.7f, 0.7f, 0.7f); condLbl.style.flexGrow = 1;
        var acb2 = new Button(() => { if (currentIndex < ChoicesData.Count) { ChoicesData[currentIndex].conditions.Add(new ChoiceCondition()); UpdateChoiceConditionsDisplay(cc, currentIndex); NotifyChange(); } }) { text = "+" };
        acb2.style.minWidth = 18; acb2.style.maxWidth = 18; acb2.style.minHeight = 18; acb2.style.maxHeight = 18; acb2.style.fontSize = 12; acb2.style.flexShrink = 0;
        condHdr.Add(condLbl); condHdr.Add(acb2); cc.Add(condHdr);
        var condContent = new VisualElement(); condContent.name = "conditionsContent"; cc.Add(condContent);
        UpdateChoiceConditionsDisplay(cc, currentIndex);
        choicesContainer.Add(cc);
    }

    private void UpdateChoiceConditionsDisplay(VisualElement choiceContainer, int choiceIndex)
    {
        if (choiceIndex >= ChoicesData.Count) return;
        var cc2 = choiceContainer.Q("conditionsContent"); if (cc2 == null) return;
        cc2.Clear();
        var cd = ChoicesData[choiceIndex];
        if (cd.conditions.Count == 0) { var el = new Label("(no conditions)"); el.style.fontSize = 8; el.style.color = new Color(0.5f, 0.5f, 0.5f); el.style.unityFontStyleAndWeight = FontStyle.Italic; el.style.marginTop = 2; el.style.marginLeft = 10; cc2.Add(el); return; }
        cc2.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f, 0.8f));
        cc2.style.paddingTop = 8; cc2.style.paddingBottom = 8; cc2.style.paddingLeft = 8; cc2.style.paddingRight = 8; cc2.style.marginTop = 3;
        cc2.style.borderTopLeftRadius = 3; cc2.style.borderTopRightRadius = 3; cc2.style.borderBottomLeftRadius = 3; cc2.style.borderBottomRightRadius = 3;
        for (int i = 0; i < cd.conditions.Count; i++)
        {
            int ci = i; var cond = cd.conditions[i];
            var condBox = new VisualElement();
            condBox.style.marginTop = i > 0 ? 5 : 0; condBox.style.paddingTop = 8; condBox.style.paddingBottom = 8; condBox.style.paddingLeft = 8; condBox.style.paddingRight = 8;
            condBox.style.backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.12f));
            condBox.style.borderTopLeftRadius = 3; condBox.style.borderTopRightRadius = 3; condBox.style.borderBottomLeftRadius = 3; condBox.style.borderBottomRightRadius = 3;
            var ch = new VisualElement(); ch.style.flexDirection = FlexDirection.Row; ch.style.alignItems = Align.Center; ch.style.marginBottom = 8;
            var clbl = new Label($"Condition {i + 1}"); clbl.style.flexGrow = 1; clbl.style.unityFontStyleAndWeight = FontStyle.Bold; clbl.style.fontSize = 10; clbl.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f));
            var rcb = new Button(() => { if (choiceIndex < ChoicesData.Count) { ChoicesData[choiceIndex].conditions.RemoveAt(ci); UpdateChoiceConditionsDisplay(choiceContainer, choiceIndex); NotifyChange(); } }) { text = "×" };
            rcb.style.width = 18; rcb.style.height = 18; rcb.style.fontSize = 12;
            ch.Add(clbl); ch.Add(rcb); condBox.Add(ch);
            GameObject cgo = null;
            if (!string.IsNullOrEmpty(cond.targetObjectID)) cgo = DialogueEventTarget.FindByID(cond.targetObjectID);
            else if (!string.IsNullOrEmpty(cond.targetObjectName)) { var all = Resources.FindObjectsOfTypeAll<GameObject>(); cgo = System.Array.Find(all, o => o.name == cond.targetObjectName && o.scene.IsValid()); }
            var cgof = new ObjectField("GameObject:") { objectType = typeof(GameObject), value = cgo, allowSceneObjects = true };
            cgof.style.marginTop = 3; cgof.style.maxWidth = 240; cgof.style.overflow = Overflow.Hidden;
            cgof.RegisterCallback<GeometryChangedEvent>(evt => { var lbl2 = cgof.Q<Label>(); if (lbl2 != null) { lbl2.style.overflow = Overflow.Hidden; lbl2.style.textOverflow = TextOverflow.Ellipsis; } });
            cgof.RegisterValueChangedCallback(evt => { if (choiceIndex < ChoicesData.Count && ci < ChoicesData[choiceIndex].conditions.Count) { var sg = evt.newValue as GameObject; ChoicesData[choiceIndex].conditions[ci].targetObjectName = sg != null ? sg.name : ""; ChoicesData[choiceIndex].conditions[ci].componentTypeName = ""; ChoicesData[choiceIndex].conditions[ci].variableName = ""; UpdateChoiceConditionsDisplay(choiceContainer, choiceIndex); NotifyChange(); } });
            condBox.Add(cgof);
            if (cgo != null)
            {
                var comps2 = cgo.GetComponents<Component>(); var cn2 = new List<string> { "None" }; var ct2 = new List<System.Type> { null };
                foreach (var c2 in comps2) { if (c2 != null) { cn2.Add(c2.GetType().Name); ct2.Add(c2.GetType()); } }
                int sci2 = 0; if (!string.IsNullOrEmpty(cond.componentTypeName)) { sci2 = cn2.IndexOf(cond.componentTypeName); if (sci2 < 0) sci2 = 0; }
                var cd2 = new PopupField<string>("Component:", cn2, sci2); cd2.style.marginTop = 3; cd2.style.maxWidth = 240;
                cd2.RegisterValueChangedCallback(evt => { if (choiceIndex < ChoicesData.Count && ci < ChoicesData[choiceIndex].conditions.Count) { ChoicesData[choiceIndex].conditions[ci].componentTypeName = evt.newValue != "None" ? evt.newValue : ""; ChoicesData[choiceIndex].conditions[ci].variableName = ""; UpdateChoiceConditionsDisplay(choiceContainer, choiceIndex); NotifyChange(); } });
                condBox.Add(cd2);
                if (sci2 > 0 && ct2[sci2] != null)
                {
                    var compType2 = ct2[sci2];
                    var fields2 = compType2.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).Where(f => f.FieldType == typeof(int) || f.FieldType == typeof(float) || f.FieldType == typeof(bool)).ToList();
                    var props2 = compType2.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).Where(p => (p.PropertyType == typeof(int) || p.PropertyType == typeof(float) || p.PropertyType == typeof(bool)) && p.CanRead).ToList();
                    var vn2 = new List<string> { "None" }; vn2.AddRange(fields2.Select(f => f.Name)); vn2.AddRange(props2.Select(p => p.Name));
                    if (vn2.Count > 1)
                    {
                        int svi2 = string.IsNullOrEmpty(cond.variableName) ? 0 : vn2.IndexOf(cond.variableName); if (svi2 < 0) svi2 = 0; string dvv2 = vn2[svi2];
                        var vr2 = new VisualElement(); vr2.style.flexDirection = FlexDirection.Row; vr2.style.alignItems = Align.Center; vr2.style.marginTop = 5; vr2.style.maxWidth = 240;
                        var vl2 = new Label("Var:"); vl2.style.width = 30; vl2.style.fontSize = 9; vl2.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f)); vl2.style.marginRight = 2; vl2.style.flexShrink = 0;
                        var vd2 = new PopupField<string>(vn2, dvv2); vd2.style.width = 70; vd2.style.marginRight = 2; vd2.style.fontSize = 9; vd2.style.flexShrink = 0;
                        vd2.RegisterValueChangedCallback(evt => { if (choiceIndex < ChoicesData.Count && ci < ChoicesData[choiceIndex].conditions.Count) { ChoicesData[choiceIndex].conditions[ci].variableName = evt.newValue == "None" ? "" : evt.newValue; UpdateChoiceConditionsDisplay(choiceContainer, choiceIndex); NotifyChange(); } });
                        vr2.Add(vl2); vr2.Add(vd2);
                        if (svi2 > 0)
                        {
                            System.Type svt2 = null; string svn2 = dvv2;
                            var f3 = fields2.FirstOrDefault(f => f.Name == svn2); if (f3 != null) svt2 = f3.FieldType; else { var p3 = props2.FirstOrDefault(p => p.Name == svn2); if (p3 != null) svt2 = p3.PropertyType; }
                            var compTypes2 = svt2 == typeof(bool) ? new List<ComparisonType> { ComparisonType.Equal, ComparisonType.NotEqual } :
                                new List<ComparisonType> { ComparisonType.Equal, ComparisonType.NotEqual, ComparisonType.Greater, ComparisonType.Less, ComparisonType.GreaterOrEqual, ComparisonType.LessOrEqual };
                            var compNames2 = compTypes2.Select(c3 => GetComparisonDisplayName(c3)).ToList();
                            int scc2 = compTypes2.IndexOf(cond.comparison); if (scc2 < 0) scc2 = 0;
                            var compDrop3 = new PopupField<string>(compNames2, compNames2[scc2]); compDrop3.style.width = 50; compDrop3.style.marginRight = 2; compDrop3.style.fontSize = 9; compDrop3.style.flexShrink = 0;
                            compDrop3.RegisterValueChangedCallback(evt => { if (choiceIndex < ChoicesData.Count && ci < ChoicesData[choiceIndex].conditions.Count) { int idx4 = compNames2.IndexOf(evt.newValue); ChoicesData[choiceIndex].conditions[ci].comparison = compTypes2[idx4]; NotifyChange(); } });
                            vr2.Add(compDrop3);
                            if (svt2 == typeof(bool))
                            {
                                var bv2 = new List<string> { "True", "False" }; string dbv2 = "True";
                                if (!string.IsNullOrEmpty(cond.compareValue) && cond.compareValue.Equals("False", System.StringComparison.OrdinalIgnoreCase)) dbv2 = "False"; else ChoicesData[choiceIndex].conditions[ci].compareValue = "True";
                                var bd2 = new PopupField<string>(bv2, dbv2); bd2.style.flexGrow = 1; bd2.style.flexShrink = 1; bd2.style.minWidth = 40; bd2.style.fontSize = 9;
                                bd2.RegisterValueChangedCallback(evt => { if (choiceIndex < ChoicesData.Count && ci < ChoicesData[choiceIndex].conditions.Count) { ChoicesData[choiceIndex].conditions[ci].compareValue = evt.newValue; NotifyChange(); } });
                                vr2.Add(bd2);
                            }
                            else
                            {
                                var vf3 = new TextField(); vf3.value = cond.compareValue; vf3.style.flexGrow = 1; vf3.style.flexShrink = 1; vf3.style.minWidth = 40; vf3.style.fontSize = 9;
                                vf3.RegisterValueChangedCallback(evt => { if (choiceIndex < ChoicesData.Count && ci < ChoicesData[choiceIndex].conditions.Count) { ChoicesData[choiceIndex].conditions[ci].compareValue = evt.newValue; NotifyChange(); } });
                                vr2.Add(vf3);
                            }
                        }
                        condBox.Add(vr2);
                    }
                    else { var nvl2 = new Label("No public int/float/bool variables found"); nvl2.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f)); nvl2.style.unityFontStyleAndWeight = FontStyle.Italic; nvl2.style.marginTop = 5; nvl2.style.paddingLeft = 10; nvl2.style.fontSize = 9; condBox.Add(nvl2); }
                }
            }
            else { var hl2 = new Label("Select a GameObject first"); hl2.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f)); hl2.style.unityFontStyleAndWeight = FontStyle.Italic; hl2.style.marginTop = 5; hl2.style.paddingLeft = 10; hl2.style.fontSize = 9; condBox.Add(hl2); }
            cc2.Add(condBox);
        }
        if (cd.conditions.Count > 1)
        {
            var lr2 = new VisualElement(); lr2.style.flexDirection = FlexDirection.Row; lr2.style.marginTop = 8; lr2.style.alignItems = Align.Center; lr2.style.justifyContent = Justify.Center;
            var andBtn2 = new Button(() => { if (choiceIndex < ChoicesData.Count) { ChoicesData[choiceIndex].conditionLogic = ConditionLogic.AND; UpdateChoiceConditionsDisplay(choiceContainer, choiceIndex); NotifyChange(); } }) { text = "AND" };
            andBtn2.style.width = 60; andBtn2.style.height = 22; andBtn2.style.fontSize = 10;
            andBtn2.style.unityFontStyleAndWeight = cd.conditionLogic == ConditionLogic.AND ? FontStyle.Bold : FontStyle.Normal;
            andBtn2.style.backgroundColor = cd.conditionLogic == ConditionLogic.AND ? new StyleColor(new Color(0.3f, 0.5f, 0.3f)) : new StyleColor(new Color(0.25f, 0.25f, 0.25f));
            var orBtn2 = new Button(() => { if (choiceIndex < ChoicesData.Count) { ChoicesData[choiceIndex].conditionLogic = ConditionLogic.OR; UpdateChoiceConditionsDisplay(choiceContainer, choiceIndex); NotifyChange(); } }) { text = "OR" };
            orBtn2.style.width = 60; orBtn2.style.height = 22; orBtn2.style.fontSize = 10; orBtn2.style.marginLeft = 5;
            orBtn2.style.unityFontStyleAndWeight = cd.conditionLogic == ConditionLogic.OR ? FontStyle.Bold : FontStyle.Normal;
            orBtn2.style.backgroundColor = cd.conditionLogic == ConditionLogic.OR ? new StyleColor(new Color(0.3f, 0.5f, 0.3f)) : new StyleColor(new Color(0.25f, 0.25f, 0.25f));
            lr2.Add(andBtn2); lr2.Add(orBtn2); cc2.Add(lr2);
        }
    }

    private StyleColor GetChoiceColor(int index) => new StyleColor(GetChoiceColorRaw(index));
    private Color GetChoiceColorRaw(int index)
    {
        var colors = new Color[] { new Color(0.4f, 0.7f, 1f), new Color(0.5f, 1f, 0.5f), new Color(1f, 0.8f, 0.4f), new Color(1f, 0.5f, 0.8f), new Color(0.8f, 0.5f, 1f), new Color(0.5f, 1f, 1f) };
        return colors[index % colors.Length];
    }
    #endregion

    #region Conditional Branch Methods
    private void OnAddBranch() { if (!isConditionalMode) ConvertToConditionalMode(); AddConditionalBranch(nextBranchPriority++); NotifyChange(); }

    private void ConvertToConditionalMode()
    {
        isConditionalMode = true;
        var graphView = GetFirstAncestorOfType<DialogueGraphViewEditor>();
        Port existingTargetPort = null;
        if (defaultOutputPort != null && defaultOutputPort.connected) { var e = defaultOutputPort.connections.FirstOrDefault(); if (e != null) { existingTargetPort = e.input; graphView?.RemoveElement(e); } }
        outputContainer.Remove(defaultOutputPort.parent);
        var outRow = new VisualElement(); outRow.style.flexDirection = FlexDirection.Row; outRow.style.alignItems = Align.Center; outRow.style.justifyContent = Justify.SpaceBetween; outRow.style.width = Length.Percent(100);
        defaultOutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
        defaultOutputPort.portName = "Default"; defaultOutputPort.userData = 0; defaultOutputPort.style.flexGrow = 1;
        var abtn = new Button(OnAddBranch) { text = "+" }; abtn.style.width = 20; abtn.style.height = 20; abtn.style.fontSize = 14; abtn.style.unityFontStyleAndWeight = FontStyle.Bold; abtn.style.flexShrink = 0;
        outRow.Add(defaultOutputPort); outRow.Add(abtn); outputContainer.Insert(0, outRow);
        conditionalBranchesData[0] = new ConditionalBranchData { priority = 0, conditions = new List<ChoiceCondition>(), conditionLogic = ConditionLogic.AND };
        if (existingTargetPort != null && graphView != null) graphView.AddElement(defaultOutputPort.ConnectTo(existingTargetPort));
        UpdateConditionalBranchesDisplay(); RefreshExpandedState(); RefreshPorts();
    }

    private void AddConditionalBranch(int priority)
    {
        var container2 = new VisualElement(); container2.style.flexDirection = FlexDirection.Row; container2.style.alignItems = Align.Center; container2.style.justifyContent = Justify.SpaceBetween; container2.style.width = Length.Percent(100);
        var port2 = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
        port2.portName = $"Priority {priority}"; port2.userData = priority; port2.style.flexGrow = 1;
        var rmvBtn = new Button(() => { if (port2 != null && port2.userData is int p) RemoveBranch(p); }) { text = "×" };
        rmvBtn.style.width = 20; rmvBtn.style.height = 20; rmvBtn.style.fontSize = 14; rmvBtn.style.backgroundColor = new StyleColor(new Color(0.6f, 0.2f, 0.2f)); rmvBtn.style.flexShrink = 0;
        container2.Add(port2); container2.Add(rmvBtn);
        outputContainer.Insert(outputContainer.childCount - 1, container2); conditionalPorts.Add(port2);
        conditionalBranchesData[priority] = new ConditionalBranchData { priority = priority, conditions = new List<ChoiceCondition>(), conditionLogic = ConditionLogic.AND };
        UpdateConditionalBranchesDisplay(); RefreshExpandedState(); RefreshPorts();
    }

    private void RemoveBranch(int priorityToRemove)
    {
        var graphView = GetFirstAncestorOfType<DialogueGraphViewEditor>();
        var portToRemove = conditionalPorts.FirstOrDefault(p => p != null && (int)p.userData == priorityToRemove);
        if (portToRemove != null)
        {
            if (portToRemove.connected) { foreach (var edge in portToRemove.connections.ToList()) { if (edge == null) continue; edge.output?.Disconnect(edge); edge.input?.Disconnect(edge); graphView?.RemoveElement(edge); } portToRemove.DisconnectAll(); }
            portToRemove.parent?.parent?.Remove(portToRemove.parent); conditionalPorts.Remove(portToRemove);
        }
        if (conditionalBranchesData.ContainsKey(priorityToRemove)) conditionalBranchesData.Remove(priorityToRemove);
        if (conditionalPorts.Count == 0) { ConvertToDefaultMode(); return; }
        var remaining = conditionalPorts.Where(p => p != null).OrderByDescending(p => (int)p.userData).ToList();
        conditionalPorts.Clear();
        var newBD = new Dictionary<int, ConditionalBranchData>();
        if (conditionalBranchesData.ContainsKey(0)) newBD[0] = conditionalBranchesData[0];
        int newPri = 1;
        foreach (var p in remaining)
        {
            int op2 = (int)p.userData;
            if (op2 > priorityToRemove) { int ap = op2 - 1; p.userData = ap; p.portName = $"Priority {ap}"; if (conditionalBranchesData.ContainsKey(op2)) { newBD[ap] = conditionalBranchesData[op2]; newBD[ap].priority = ap; } }
            else { if (conditionalBranchesData.ContainsKey(op2)) newBD[op2] = conditionalBranchesData[op2]; }
            conditionalPorts.Add(p); newPri = Math.Max(newPri, (int)p.userData + 1);
        }
        conditionalBranchesData = newBD; nextBranchPriority = newPri;
        UpdateConditionalBranchesDisplay(); RefreshExpandedState(); RefreshPorts(); NotifyChange();
    }

    private void ConvertToDefaultMode()
    {
        isConditionalMode = false;
        var graphView = GetFirstAncestorOfType<DialogueGraphViewEditor>();
        Port existingTargetPort = null;
        if (defaultOutputPort != null && defaultOutputPort.connected) { var e = defaultOutputPort.connections.FirstOrDefault(); if (e != null) { existingTargetPort = e.input; graphView?.RemoveElement(e); } }
        conditionalBranchesData.Clear(); nextBranchPriority = 1;
        outputContainer.Remove(defaultOutputPort.parent);
        CreateOutputPortWithAddButton();
        if (existingTargetPort != null && graphView != null) graphView.AddElement(defaultOutputPort.ConnectTo(existingTargetPort));
        UpdateConditionalBranchesDisplay(); RefreshExpandedState(); RefreshPorts();
    }

    public bool IsConditionalMode() => isConditionalMode;

    public int GetBranchPriorityForPort(Port port)
    {
        if (port == defaultOutputPort) return isConditionalMode ? 0 : -1;
        if (port.userData is int priority) return priority;
        return -1;
    }

    public Port GetConditionalPort(int priority) { if (priority == 0) return defaultOutputPort; return conditionalPorts.FirstOrDefault(p => (int)p.userData == priority); }
    public ConditionalBranchData GetConditionalBranchData(int priority) => conditionalBranchesData.ContainsKey(priority) ? conditionalBranchesData[priority] : null;

    public void UpdateConditionalBranchData(int priority, List<ChoiceCondition> conditions, ConditionLogic logic)
    {
        if (conditionalBranchesData.ContainsKey(priority)) { conditionalBranchesData[priority].conditions = new List<ChoiceCondition>(conditions); conditionalBranchesData[priority].conditionLogic = logic; }
    }

    public List<ConditionalBranchData> GetAllConditionalBranches() => conditionalBranchesData.Values.OrderByDescending(b => b.priority).ToList();

    public void LoadConditionalBranches(List<ConditionalBranchData> branches)
    {
        if (branches == null || branches.Count == 0) return;
        var nonDefault = branches.Where(b => b.priority > 0).OrderByDescending(b => b.priority).ToList();
        if (nonDefault.Count > 0)
        {
            if (!isConditionalMode) ConvertToConditionalMode();
            foreach (var branch in nonDefault) { AddConditionalBranch(branch.priority); conditionalBranchesData[branch.priority] = new ConditionalBranchData { priority = branch.priority, conditions = new List<ChoiceCondition>(branch.conditions), conditionLogic = branch.conditionLogic }; }
            nextBranchPriority = nonDefault.Max(b => b.priority) + 1;
        }
        UpdateConditionalBranchesDisplay();
    }
    #endregion

    #region Public Methods
    public void SetChoicesData(List<ChoiceData> choicesData)
    {
        ChoicesData.Clear(); choicesContainer.Clear(); choiceOutputPorts.Clear();
        for (int i = 0; i < choicesData.Count; i++) { ChoicesData.Add(choicesData[i]); RebuildChoiceUI(i); }
        RefreshExpandedState(); RefreshPorts();
    }

    public void SetEventCalls(List<DialogueEventCall> eventCalls) { EventCalls = eventCalls ?? new List<DialogueEventCall>(); UpdateEventsDisplay(); }
    public int GetChoiceIndexForPort(Port port) { if (port.userData is int value && value <= -2) return -2 - value; return -1; }
    public Port GetOutputPortByIndex(int index) => index >= 0 && index < choiceOutputPorts.Count ? choiceOutputPorts[index] : null;
    public Port GetDefaultOutputPort() => defaultOutputPort;
    public Port GetInputPort() => inputPort;
    public string GetId() => nodeId;
    public void SetId(string id) => nodeId = id;
    #endregion

    #region Helper Methods
    private string GetComparisonDisplayName(ComparisonType ct)
    {
        switch (ct)
        {
            case ComparisonType.Equal: return "==";
            case ComparisonType.NotEqual: return "!=";
            case ComparisonType.Greater: return ">";
            case ComparisonType.Less: return "<";
            case ComparisonType.GreaterOrEqual: return ">=";
            case ComparisonType.LessOrEqual: return "<=";
            default: return "==";
        }
    }

    public void RefreshLanguageDisplay()
    {
        if (editorWindow == null) return;
        UpdateDialoguePreview();
        RefreshChoicesLanguage();
    }

    private void RefreshChoicesLanguage()
    {
        if (editorWindow == null) return;
        for (int i = 0; i < ChoicesData.Count; i++)
        {
            UpdateChoicePreview(i);
            if (i < choiceOutputPorts.Count && choiceOutputPorts[i] != null)
            {
                Language currentLang = editorWindow.GetCurrentLanguage();
                string previewText = DialogueLocalization.GetText(ChoicesData[i].textId, currentLang);
                choiceOutputPorts[i].portName = previewText ?? ChoicesData[i].textId;
            }
        }
    }
    #endregion

    #region Start Node Management
    public void SetAsStartNode(bool isStart)
    {
        isStartNode = isStart; UpdateTitle();
        titleContainer.style.backgroundColor = isStart ? new StyleColor(new Color(0.2f, 0.6f, 0.3f, 0.8f)) : new StyleColor(new Color(0.24f, 0.24f, 0.24f, 0.8f));
    }

    public bool IsStartNode() => isStartNode;
    #endregion
}
