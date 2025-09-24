using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 专门用于执行对话事件调用的类，从 DialogueManager 中分离出来
/// </summary>
public static class DialogueEventExecutor
{
    // 缓存已查找的组件类型，避免重复反射
    private static readonly Dictionary<string, Type> typeCache = new Dictionary<string, Type>();

    // 预定义的常用命名空间，按优先级排序
    private static readonly string[] commonNamespaces = {
        "", // 无命名空间 (当前程序集的类型)
        "UnityEngine.",
        "UnityEngine.UI.",
        "TMPro."
    };

    /// <summary>
    /// 执行对话事件调用列表
    /// </summary>
    /// <param name="eventCalls">事件调用列表</param>
    public static void Execute(List<DialogueEventCall> eventCalls)
    {
        if (eventCalls == null || eventCalls.Count == 0) return;

        foreach (var eventCall in eventCalls)
        {
            if (!IsValidEventCall(eventCall))
            {
                LogWarning($"Invalid event call: missing required fields");
                continue;
            }

            ExecuteSingleEvent(eventCall);
        }
    }

    /// <summary>
    /// 执行单个事件调用
    /// </summary>
    /// <param name="eventCall">事件调用数据</param>
    private static void ExecuteSingleEvent(DialogueEventCall eventCall)
    {
        try
        {
            // 1. 查找目标GameObject
            var targetObject = FindTargetObject(eventCall.targetObjectName);
            if (targetObject == null) return;

            // 2. 获取目标组件
            var component = GetTargetComponent(targetObject, eventCall.componentTypeName);
            if (component == null) return;

            // 3. 调用方法
            InvokeMethod(component, eventCall);
        }
        catch (Exception e)
        {
            LogError($"Failed to execute event call on '{eventCall.targetObjectName}': {e.Message}");
        }
    }

    /// <summary>
    /// 验证事件调用数据是否有效
    /// </summary>
    private static bool IsValidEventCall(DialogueEventCall eventCall)
    {
        return !string.IsNullOrEmpty(eventCall.targetObjectName) &&
               !string.IsNullOrEmpty(eventCall.componentTypeName) &&
               !string.IsNullOrEmpty(eventCall.methodName);
    }

    /// <summary>
    /// 查找目标GameObject
    /// </summary>
    private static GameObject FindTargetObject(string objectName)
    {
        var targetObject = GameObject.Find(objectName);
        if (targetObject == null)
        {
            LogWarning($"GameObject '{objectName}' not found");
        }
        return targetObject;
    }

    /// <summary>
    /// 获取目标组件
    /// </summary>
    private static Component GetTargetComponent(GameObject targetObject, string componentTypeName)
    {
        var componentType = GetComponentType(componentTypeName);
        if (componentType == null)
        {
            LogWarning($"Component type '{componentTypeName}' not found");
            return null;
        }

        var component = targetObject.GetComponent(componentType);
        if (component == null)
        {
            LogWarning($"Component '{componentTypeName}' not found on GameObject '{targetObject.name}'");
        }
        return component;
    }

    /// <summary>
    /// 获取组件类型，使用缓存优化性能
    /// </summary>
    private static Type GetComponentType(string typeName)
    {
        // 先检查缓存
        if (typeCache.TryGetValue(typeName, out Type cachedType))
        {
            return cachedType;
        }

        // 尝试从不同命名空间查找类型
        Type foundType = null;
        foreach (var nameSpace in commonNamespaces)
        {
            var fullTypeName = nameSpace + typeName;
            foundType = Type.GetType(fullTypeName) ??
                       Assembly.GetExecutingAssembly().GetType(fullTypeName);

            if (foundType != null) break;
        }

        // 缓存结果（即使是 null 也缓存，避免重复查找）
        typeCache[typeName] = foundType;
        return foundType;
    }

    /// <summary>
    /// 通过反射调用方法
    /// </summary>
    private static void InvokeMethod(Component component, DialogueEventCall eventCall)
    {
        var (parameters, parameterTypes) = PrepareMethodParameters(eventCall);
        var method = FindMethod(component.GetType(), eventCall.methodName, parameterTypes);

        if (method == null)
        {
            LogWarning($"Method '{eventCall.methodName}' not found on component '{component.GetType().Name}'");
            return;
        }

        try
        {
            method.Invoke(component, parameters);
            LogSuccess($"Successfully called {component.GetType().Name}.{eventCall.methodName}() on {component.gameObject.name}");
        }
        catch (Exception e)
        {
            LogError($"Error invoking method {eventCall.methodName}: {e.Message}");
        }
    }

    /// <summary>
    /// 准备方法参数
    /// </summary>
    private static (object[] parameters, Type[] parameterTypes) PrepareMethodParameters(DialogueEventCall eventCall)
    {
        return eventCall.parameterType switch
        {
            ParameterType.None => (new object[0], new Type[0]),
            ParameterType.String => (new object[] { eventCall.stringParameter }, new Type[] { typeof(string) }),
            ParameterType.Int => (new object[] { eventCall.intParameter }, new Type[] { typeof(int) }),
            ParameterType.Float => (new object[] { eventCall.floatParameter }, new Type[] { typeof(float) }),
            ParameterType.Bool => (new object[] { eventCall.boolParameter }, new Type[] { typeof(bool) }),
            _ => (new object[0], new Type[0])
        };
    }

    /// <summary>
    /// 查找方法，支持精确匹配和名称匹配
    /// </summary>
    private static MethodInfo FindMethod(Type componentType, string methodName, Type[] parameterTypes)
    {
        // 先尝试精确匹配参数类型
        var method = componentType.GetMethod(methodName, parameterTypes);

        // 如果精确匹配失败，尝试按名称匹配（适用于重载方法或无参数方法）
        if (method == null)
        {
            method = componentType.GetMethod(methodName);
        }

        return method;
    }

    /// <summary>
    /// 清除类型缓存（可选，用于内存管理）
    /// </summary>
    public static void ClearTypeCache()
    {
        typeCache.Clear();
    }

    // 日志方法 - 可以根据项目需求自定义
    private static void LogSuccess(string message)
    {
        Debug.Log($"[DialogueEvent] {message}");
    }

    private static void LogWarning(string message)
    {
        Debug.LogWarning($"[DialogueEvent] {message}");
    }

    private static void LogError(string message)
    {
        Debug.LogError($"[DialogueEvent] {message}");
    }
}