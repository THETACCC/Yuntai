using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class changebound : MonoBehaviour
{

    public GameObject boundingarea_1;
    public GameObject boundingarea_2;
    public GameObject virtualcamera;
    public GameObject InteractIndicator;

    [Header("Direction Gates (默认都允许)")]
    [SerializeField] private bool allow1to2 = true;
    [SerializeField] private bool allow2to1 = true;

    [Header("当方向被禁止时要触发的事件（可选）")]
    [SerializeField] private UnityEvent onBlocked1to2; // 在 bound_1 时按 E 但 1->2 被禁用
    [SerializeField] private UnityEvent onBlocked2to1; // 在 bound_2 时按 E 但 2->1 被禁用

    private CinemachineConfiner2D confiner;
    private PolygonCollider2D bound_1;
    private PolygonCollider2D bound_2;

    private bool isInTrigger = false;

    void Start()
    {
        virtualcamera = GameObject.FindGameObjectWithTag("VirtualCam");

        bound_1 = boundingarea_1.GetComponent<PolygonCollider2D>();
        bound_2 = boundingarea_2.GetComponent<PolygonCollider2D>();
        confiner = virtualcamera.GetComponent<CinemachineConfiner2D>();
        InteractIndicator.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        isInTrigger = true;
        InteractIndicator.SetActive(true);
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        isInTrigger = false;
        InteractIndicator.SetActive(false);
    }

    private void Update()
    {
        if (isInTrigger && Input.GetKeyDown(KeyCode.E))
        {
            HandlePress();
        }
    }

    private void HandlePress()
    {
        var current = confiner.m_BoundingShape2D;

        // 在 bound_1 内按 E
        if (current == bound_1)
        {
            if (allow1to2)
            {
                confiner.m_BoundingShape2D = bound_2;
            }
            else
            {
                // 禁止 1->2：触发事件（比如调用 LevelManager 的某个 public 方法）
                onBlocked1to2?.Invoke();
            }
            return;
        }

        // 在 bound_2 内按 E
        if (current == bound_2)
        {
            if (allow2to1)
            {
                confiner.m_BoundingShape2D = bound_1;
            }
            else
            {
                onBlocked2to1?.Invoke();
            }
            return;
        }
    }
    /*
    private void SwitchBoundingArea()
    {
        if (confiner.m_BoundingShape2D == bound_1)
        {
            confiner.m_BoundingShape2D = bound_2;
        }
        else
        {
            confiner.m_BoundingShape2D = bound_1;
        }
    }
    */
}
