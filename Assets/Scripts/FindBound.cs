using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FindBound : MonoBehaviour
{
    [Header("Options")]
    [Tooltip("If true, automatically find & assign the nearest bound in Start().")]
    public bool autoAssignOnStart = true;

    [Tooltip("Optional: only consider bounds with this tag. Leave empty to consider all.")]
    public string boundsTag = ""; // e.g., "ConfinerBound"

    [Tooltip("Reference point used to choose the nearest bound. Defaults to this transform.")]
    public Transform reference;

    private CinemachineConfiner2D confiner;

    private void Awake()
    {
        confiner = GetComponent<CinemachineConfiner2D>();
        if (reference == null) reference = transform;
    }

    private void Start()
    {
        if (!autoAssignOnStart) return;
        AssignNearestBound(reference, boundsTag);
    }

    /// <summary>
    /// Finds the nearest valid Collider2D (PolygonCollider2D / CompositeCollider2D)
    /// to the given reference and assigns it to this object's CinemachineConfiner2D.
    /// Returns true on success, false if none found or no confiner present.
    /// </summary>
    public bool AssignNearestBound(Transform refPoint = null, string filterTag = "")
    {
        if (confiner == null)
        {
            Debug.LogError("FindBound: CinemachineConfiner2D not found on this GameObject.");
            return false;
        }

        var target = FindNearestBound(refPoint ?? reference ?? transform, filterTag);
        if (target == null)
        {
            Debug.LogWarning("FindBound: No suitable bound found to assign.");
            return false;
        }

        confiner.m_BoundingShape2D = target;
        InvalidateConfinerCache(confiner);
        // Optional: if you tweak confiner settings at runtime, you can also set confiner.m_Damping, etc.

        // Debug.Log($"FindBound: Assigned nearest confiner '{target.name}' to {name}.");
        return true;
    }

    /// <summary>
    /// Returns the nearest Collider2D (PolygonCollider2D or CompositeCollider2D)
    /// to the given reference point, optionally filtered by tag. Does not assign.
    /// </summary>
    public Collider2D FindNearestBound(Transform refPoint, string filterTag = "")
    {
        if (refPoint == null) refPoint = transform;

        Collider2D[] candidates;

        if (!string.IsNullOrWhiteSpace(filterTag))
        {
            var tagged = GameObject.FindGameObjectsWithTag(filterTag);
            candidates = tagged
                .Select(go => go.GetComponent<Collider2D>())
                .Where(c => c != null)
                .ToArray();
        }
        else
        {
            // Includes inactive objects (Unity 2020.1+)
            candidates = Object.FindObjectsOfType<Collider2D>(true);
        }

        // Keep only polygonal shapes supported by Confiner2D
        var polygonal = candidates.Where(c => c is PolygonCollider2D || c is CompositeCollider2D);

        Collider2D nearest = null;
        float bestSqr = float.PositiveInfinity;
        Vector3 origin = refPoint.position;

        foreach (var c in polygonal)
        {
            // Use bounds.ClosestPoint for a stable distance metric
            var cp = c.bounds.ClosestPoint(origin);
            float d2 = (cp - origin).sqrMagnitude;
            if (d2 < bestSqr)
            {
                bestSqr = d2;
                nearest = c;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Confiner caches its computed polygon. Invalidate to apply new bounds immediately.
    /// Supports both Confiner2D API variants across Cinemachine versions.
    /// </summary>
    private static void InvalidateConfinerCache(CinemachineConfiner2D conf)
    {
        var t = conf.GetType();
        var m = t.GetMethod("InvalidateCache") ?? t.GetMethod("InvalidateBoundingShapeCache");
        if (m != null) m.Invoke(conf, null);
    }
}