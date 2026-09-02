using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider), typeof(Highlight))]
public sealed class RJDragConnector : MonoBehaviour
{
    private static readonly Dictionary<Transform, RJDragConnector> PortOccupants = new();

    [Header("Dragging")]
    [SerializeField]
    [Tooltip("Camera used to drag this RJ. If empty, the Main Camera is used.")]
    private Camera interactionCamera;

    [SerializeField]
    [Tooltip("Only colliders on these layers can be selected by the cursor raycast.")]
    private LayerMask dragLayers = Physics.DefaultRaycastLayers;

    [SerializeField, Min(0f)]
    private float maximumDistance = 1000f;

    [SerializeField]
    private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.UseGlobal;

    [Header("Connection")]
    [SerializeField]
    [Tooltip("Every server port this RJ is allowed to connect to.")]
    private Transform[] connectionPoints;

    [SerializeField]
    [Tooltip("Port occupied by this RJ when Starts Connected is enabled.")]
    private Transform startingConnectionPoint;

    [SerializeField]
    private bool startsConnected;

    [SerializeField, Min(0f)]
    [Tooltip("On LMB release, the RJ connects when its center is this close to a free port.")]
    private float autoConnectDistance = 0.4f;

    private Highlight highlight;
    private InteractionHighlight interactionHighlight;
    private Plane dragPlane;
    private Vector3 grabOffset;
    private bool isDragging;
    private bool isConnected;
    private Transform occupiedConnectionPoint;

    public bool IsConnected => isConnected;
    public bool IsDragging => isDragging;

    private Camera CameraToUse => interactionCamera != null ? interactionCamera : Camera.main;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPortOccupants()
    {
        PortOccupants.Clear();
    }

    private void Awake()
    {
        highlight = GetComponent<Highlight>();
        interactionHighlight = GetComponent<InteractionHighlight>();
    }

    private void OnEnable()
    {
        if (highlight == null)
            highlight = GetComponent<Highlight>();

        if (interactionHighlight == null)
            interactionHighlight = GetComponent<InteractionHighlight>();

        isDragging = false;
        InitializeConnectionState();
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        Camera cameraToUse = CameraToUse;
        if (mouse == null || cameraToUse == null)
            return;

        Vector2 cursorPosition = mouse.position.ReadValue();

        if (!isDragging && mouse.leftButton.wasPressedThisFrame)
            TryBeginDrag(cameraToUse, cursorPosition);

        if (!isDragging)
            return;

        if (mouse.leftButton.isPressed)
            MoveOnCameraPlane(cameraToUse, cursorPosition);

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            isDragging = false;
            TryConnectToNearestAvailablePort();
        }
    }

    private void OnDisable()
    {
        isDragging = false;
        ReleaseOccupiedPort();
    }

    private void OnValidate()
    {
        maximumDistance = Mathf.Max(0f, maximumDistance);
        autoConnectDistance = Mathf.Max(0f, autoConnectDistance);
    }

    private void TryBeginDrag(Camera cameraToUse, Vector2 cursorPosition)
    {
        Ray ray = cameraToUse.ScreenPointToRay(cursorPosition);
        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                maximumDistance,
                dragLayers,
                triggerInteraction) ||
            (hit.transform != transform && !hit.transform.IsChildOf(transform)))
        {
            return;
        }

        // The plane faces the interaction camera and passes through the RJ, so cursor
        // movement changes only the two screen-space axes and never changes depth.
        dragPlane = new Plane(cameraToUse.transform.forward, transform.position);
        if (!dragPlane.Raycast(ray, out float enter))
            return;

        grabOffset = transform.position - ray.GetPoint(enter);
        isDragging = true;
        ReleaseOccupiedPort();
        SetConnected(false);
    }

    private void MoveOnCameraPlane(Camera cameraToUse, Vector2 cursorPosition)
    {
        Ray ray = cameraToUse.ScreenPointToRay(cursorPosition);
        if (dragPlane.Raycast(ray, out float enter))
            transform.position = ray.GetPoint(enter) + grabOffset;
    }

    private void InitializeConnectionState()
    {
        ReleaseOccupiedPort();

        if (startsConnected &&
            startingConnectionPoint != null &&
            TryOccupyPort(startingConnectionPoint))
        {
            transform.SetPositionAndRotation(
                startingConnectionPoint.position,
                startingConnectionPoint.rotation);
            SetConnected(true);
            return;
        }

        SetConnected(false);
    }

    private bool TryConnectToNearestAvailablePort()
    {
        Transform nearestPort = null;
        float nearestDistance = autoConnectDistance;

        if (connectionPoints == null)
            return false;

        foreach (Transform port in connectionPoints)
        {
            if (port == null || !IsPortAvailable(port))
                continue;

            float distance = Vector3.Distance(transform.position, port.position);
            if (distance <= nearestDistance)
            {
                nearestPort = port;
                nearestDistance = distance;
            }
        }

        if (nearestPort == null || !TryOccupyPort(nearestPort))
            return false;

        transform.SetPositionAndRotation(nearestPort.position, nearestPort.rotation);
        SetConnected(true);
        return true;
    }

    private bool IsPortAvailable(Transform port)
    {
        if (!PortOccupants.TryGetValue(port, out RJDragConnector occupant))
            return true;

        if (occupant != null && occupant != this)
            return false;

        PortOccupants.Remove(port);
        return true;
    }

    private bool TryOccupyPort(Transform port)
    {
        if (!IsPortAvailable(port))
            return false;

        PortOccupants[port] = this;
        occupiedConnectionPoint = port;
        return true;
    }

    private void ReleaseOccupiedPort()
    {
        if (occupiedConnectionPoint == null)
            return;

        if (PortOccupants.TryGetValue(
                occupiedConnectionPoint,
                out RJDragConnector occupant) &&
            occupant == this)
        {
            PortOccupants.Remove(occupiedConnectionPoint);
        }

        occupiedConnectionPoint = null;
    }

    private void SetConnected(bool value)
    {
        isConnected = value;

        if (highlight != null)
            highlight.SetHighlighted(!isConnected);

        if (interactionHighlight != null)
            interactionHighlight.enabled = !isConnected;
    }
}
