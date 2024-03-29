﻿using Enderlook.Unity.Pathfinding.Steerings;

using UnityEngine;
using UnityEngine.UIElements;

[DefaultExecutionOrder(100)]
public class GameManager : MonoBehaviour
{
    [SerializeField]
    private PathFollower agent;
    [SerializeField]
    private PathFollower agent2;

    [SerializeField]
    private Transform target;

    [SerializeField]
    private Collider plane;

    //[System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
    //private void Awake() => agent.SetDestinationSync(target.position);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
    private void Update()
    {
        if (Input.GetMouseButtonDown((int)MouseButton.LeftMouse))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit) && hit.collider == plane)
            {
                agent.SetDestination(hit.point);
                agent2.SetDestination(hit.point);
            }
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
    private void OnDrawGizmos()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(hit.point, 1);
        }
    }
}