using UnityEditor.Experimental.GraphView;
using System.Collections.Generic;
using DialogueSystem;

/// <summary>
/// 条件连线 - 不显示标签
/// </summary>
public class ConditionalEdge : Edge
{
    private DialogueGraphView graphView;
    private DialogueTreeEditor editorWindow;

    public int branchPriority;
    public List<ChoiceCondition> conditions = new List<ChoiceCondition>();
    public ConditionLogic conditionLogic = ConditionLogic.AND;

    public ConditionalEdge(DialogueGraphView graphView, DialogueTreeEditor editorWindow)
    {
        this.graphView = graphView;
        this.editorWindow = editorWindow;
    }

    public void UpdateLabel()
    {
        // 不再显示标签，因为节点内部已有 Conditional Branches 显示
    }
}