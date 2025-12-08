using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;
using DialogueSystem;

public static class DialogueEventExecutor
{
    private static readonly Dictionary<string, Type> typeCache = new Dictionary<string, Type>();

    private static readonly string[] commonNamespaces = {
        "",
        "UnityEngine.",
        "UnityEngine.UI.",
        "TMPro."
    };

    public static void Execute(List<DialogueEventCall> eventCalls)
    {
        if (eventCalls == null || eventCalls.Count == 0) return;

        foreach (var eventCall in eventCalls)
        {
            if (!IsValidEventCall(eventCall))
            {
                LogError($"Invalid event call: missing required fields");
                continue;
            }

            ExecuteSingleEvent(eventCall);
        }
    }

    public static void ExecuteSingleEvent(DialogueEventCall eventCall)
    {
        try
        {
            // 只使用ID查找
            if (string.IsNullOrEmpty(eventCall.targetObjectID))
            {
                LogError($"Event call missing target object ID (targetObjectName: '{eventCall.targetObjectName}')");
                return;
            }

            var targetObject = FindTargetObject(eventCall.targetObjectID, eventCall.targetObjectName);
            if (targetObject == null) return;

            var component = GetTargetComponent(targetObject, eventCall.componentTypeName);
            if (component == null) return;

            InvokeMethod(component, eventCall);
        }
        catch (Exception e)
        {
            LogError($"Failed to execute event call: {e.Message}");
        }
    }

    public static bool IsValidEventCall(DialogueEventCall eventCall)
    {
        return !string.IsNullOrEmpty(eventCall.targetObjectID) &&
               !string.IsNullOrEmpty(eventCall.componentTypeName) &&
               !string.IsNullOrEmpty(eventCall.methodName);
    }

    private static GameObject FindTargetObject(string objectID, string objectName)
    {
        // 只通过ID查找
        var targetObject = DialogueReference.FindByID(objectID);

        if (targetObject == null)
        {
            string displayName = !string.IsNullOrEmpty(objectName) ? $"'{objectName}'" : "(unnamed)";
            LogError($"GameObject {displayName} with ID '{objectID}' not found. Please ensure the object exists in the scene and has a DialogueReference component with this ID.");
        }

        return targetObject;
    }

    private static Component GetTargetComponent(GameObject targetObject, string componentTypeName)
    {
        var componentType = GetComponentType(componentTypeName);
        if (componentType == null)
        {
            LogError($"Component type '{componentTypeName}' not found");
            return null;
        }

        var component = targetObject.GetComponent(componentType);
        if (component == null)
        {
            LogError($"Component '{componentTypeName}' not found on GameObject '{targetObject.name}'");
        }
        return component;
    }

    private static Type GetComponentType(string typeName)
    {
        if (typeCache.TryGetValue(typeName, out Type cachedType))
        {
            return cachedType;
        }

        Type foundType = null;

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var assembly in assemblies)
        {
            foreach (var nameSpace in commonNamespaces)
            {
                var fullTypeName = nameSpace + typeName;
                foundType = assembly.GetType(fullTypeName);
                if (foundType != null) break;
            }
            if (foundType != null) break;
        }

        typeCache[typeName] = foundType;
        return foundType;
    }

    private static void InvokeMethod(Component component, DialogueEventCall eventCall)
    {
        if (eventCall.methodName.Contains("|"))
        {
            var parts = eventCall.methodName.Split('|');
            string paramTypeName = parts.Length > 1 ? parts[1] : "";
            if (paramTypeName == "Int32") eventCall.parameterType = ParameterType.Int;
            else if (paramTypeName == "Single") eventCall.parameterType = ParameterType.Float;
            else if (paramTypeName == "String") eventCall.parameterType = ParameterType.String;
            else if (paramTypeName == "Boolean") eventCall.parameterType = ParameterType.Bool;
            else eventCall.parameterType = ParameterType.None;
        }

        // 从方法名解析基础名称
        string baseName = eventCall.methodName;
        if (eventCall.methodName.Contains("|"))
        {
            baseName = eventCall.methodName.Split('|')[0];
        }

        // 根据参数类型构建 Type[] 和参数值
        Type[] paramTypes;
        object parameter = null;

        switch (eventCall.parameterType)
        {
            case ParameterType.Int:
                paramTypes = new Type[] { typeof(int) };
                parameter = eventCall.intParameter;
                break;
            case ParameterType.Float:
                paramTypes = new Type[] { typeof(float) };
                parameter = eventCall.floatParameter;
                break;
            case ParameterType.String:
                paramTypes = new Type[] { typeof(string) };
                parameter = eventCall.stringParameter;
                break;
            case ParameterType.Bool:
                paramTypes = new Type[] { typeof(bool) };
                parameter = eventCall.boolParameter;
                break;
            case ParameterType.None:
            default:
                paramTypes = Type.EmptyTypes;
                break;
        }

        // 查找方法
        var method = component.GetType().GetMethod(
            baseName,
            BindingFlags.Public | BindingFlags.Instance,
            null,
            paramTypes,
            null);

        if (method == null)
        {
            var allMethods = component.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == baseName)
                .ToList();

            if (allMethods.Count > 0)
            {
                LogError($"Method '{baseName}' with signature ({string.Join(", ", paramTypes.Select(t => t.Name))}) not found on component '{component.GetType().Name}'");
            }
            else
            {
                LogError($"Method '{baseName}' not found on component '{component.GetType().Name}'");
            }
            return;
        }

        // 调用方法
        try
        {
            if (paramTypes.Length == 0)
                method.Invoke(component, null);
            else
                method.Invoke(component, new object[] { parameter });
        }
        catch (Exception e)
        {
            LogError($"Error invoking method {baseName} on GameObject '{component.gameObject.name}': {e.InnerException?.Message ?? e.Message}");
        }
    }

    public static void ClearTypeCache()
    {
        typeCache.Clear();
    }

    public static void LogWarning(string message)
    {
        Debug.LogWarning($"[DialogueEvent] {message}");
    }

    private static void LogError(string message)
    {
        Debug.LogError($"[DialogueEvent] {message}");
    }
}