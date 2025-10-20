using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using DialogueSystem;

/// <summary>
/// Specialized class for executing dialogue event calls, separated from DialogueManager
/// </summary>
public static class DialogueEventExecutor
{
    // Cache for looked-up component types to avoid repeated reflection
    private static readonly Dictionary<string, Type> typeCache = new Dictionary<string, Type>();

    // Predefined common namespaces, sorted by priority
    private static readonly string[] commonNamespaces = {
        "", // No namespace (types in current assembly)
        "UnityEngine.",
        "UnityEngine.UI.",
        "TMPro."
    };

    /// <summary>
    /// Execute a list of dialogue event calls
    /// </summary>
    /// <param name="eventCalls">List of event calls</param>
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
    /// Execute a single event call
    /// </summary>
    /// <param name="eventCall">Event call data</param>
    public static void ExecuteSingleEvent(DialogueEventCall eventCall)
    {
        try
        {
            // 1. Find target GameObject
            var targetObject = FindTargetObject(eventCall.targetObjectName);
            if (targetObject == null) return;

            // 2. Get target component
            var component = GetTargetComponent(targetObject, eventCall.componentTypeName);
            if (component == null) return;

            // 3. Invoke method
            InvokeMethod(component, eventCall);
        }
        catch (Exception e)
        {
            LogError($"Failed to execute event call on '{eventCall.targetObjectName}': {e.Message}");
        }
    }

    /// <summary>
    /// Validate if event call data is valid
    /// </summary>
    public static bool IsValidEventCall(DialogueEventCall eventCall)
    {
        return !string.IsNullOrEmpty(eventCall.targetObjectName) &&
               !string.IsNullOrEmpty(eventCall.componentTypeName) &&
               !string.IsNullOrEmpty(eventCall.methodName);
    }

    /// <summary>
    /// Find target GameObject
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
    /// Get target component
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
    /// Get component type with caching for performance optimization
    /// </summary>
    private static Type GetComponentType(string typeName)
    {
        // Check cache first
        if (typeCache.TryGetValue(typeName, out Type cachedType))
        {
            return cachedType;
        }

        // Try to find type from different namespaces
        Type foundType = null;
        foreach (var nameSpace in commonNamespaces)
        {
            var fullTypeName = nameSpace + typeName;
            foundType = Type.GetType(fullTypeName) ??
                       Assembly.GetExecutingAssembly().GetType(fullTypeName);

            if (foundType != null) break;
        }

        // Cache result (even if null, to avoid repeated lookups)
        typeCache[typeName] = foundType;
        return foundType;
    }

    /// <summary>
    /// Invoke method via reflection
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
    /// Prepare method parameters
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
    /// Find method with support for exact matching and name matching
    /// </summary>
    private static MethodInfo FindMethod(Type componentType, string methodName, Type[] parameterTypes)
    {
        // Try exact parameter type matching first
        var method = componentType.GetMethod(methodName, parameterTypes);

        // If exact matching fails, try name matching (for overloaded methods or parameterless methods)
        if (method == null)
        {
            method = componentType.GetMethod(methodName);
        }

        return method;
    }

    /// <summary>
    /// Clear type cache (optional, for memory management)
    /// </summary>
    public static void ClearTypeCache()
    {
        typeCache.Clear();
    }

    // Logging methods - can be customized based on project needs
    private static void LogSuccess(string message)
    {
        Debug.Log($"[DialogueEvent] {message}");
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