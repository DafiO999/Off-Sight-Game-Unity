using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class ServerInteraction : MonoBehaviour
{
    [Header("Player Interaction")]
    [SerializeField]
    private Transform player;

    [SerializeField]
    private PlayerMovement playerMovement;

    [SerializeField, Min(0f)]
    [Tooltip("The player may press E anywhere inside this radius, regardless of facing direction.")]
    private float interactionRadius = 6f;

    [Header("Server Display")]
    [SerializeField]
    private Camera serverCamera;

    [SerializeField]
    private RenderTexture firstDisplayTexture;

    [SerializeField]
    private CameraDisplayZoom mainCameraZoom;

    private readonly List<Camera> disabledDisplayCameras = new();
    private RenderTexture originalServerTarget;
    private bool originalServerCameraEnabled;
    private bool playerMovementWasEnabled;
    private bool isInteracting;

    public bool IsInteracting => isInteracting;

    private void Update()
    {
        bool isInRange = IsPlayerInRange();
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.eKey.wasPressedThisFrame)
            return;

        if (isInteracting)
            SetInteracting(false);
        else if (isInRange)
            SetInteracting(true);
    }

    private void OnDisable()
    {
        if (isInteracting)
            SetInteracting(false, true);
    }

    private void OnValidate()
    {
        interactionRadius = Mathf.Max(0f, interactionRadius);
    }

    private bool IsPlayerInRange()
    {
        if (player == null)
            return false;

        float radiusSquared = interactionRadius * interactionRadius;
        return (player.position - transform.position).sqrMagnitude <= radiusSquared;
    }

    private void SetInteracting(bool value, bool snapCameraHome = false)
    {
        if (isInteracting == value)
            return;

        isInteracting = value;

        if (isInteracting)
            BeginInteraction();
        else
            EndInteraction(snapCameraHome);
    }

    private void BeginInteraction()
    {
        RouteServerCameraToFirstDisplay();

        if (mainCameraZoom != null)
            mainCameraZoom.SetFocused(true);

        if (playerMovement != null)
        {
            playerMovementWasEnabled = playerMovement.enabled;
            playerMovement.enabled = false;
        }
    }

    private void EndInteraction(bool snapCameraHome)
    {
        RestoreFirstDisplayFeed();

        if (mainCameraZoom != null)
        {
            if (snapCameraHome)
                mainCameraZoom.SnapHome();
            else
                mainCameraZoom.SetFocused(false);
        }

        if (playerMovement != null)
            playerMovement.enabled = playerMovementWasEnabled;
    }

    private void RouteServerCameraToFirstDisplay()
    {
        if (serverCamera == null || firstDisplayTexture == null)
            return;

        originalServerTarget = serverCamera.targetTexture;
        originalServerCameraEnabled = serverCamera.enabled;
        disabledDisplayCameras.Clear();

        Camera[] sceneCameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (Camera sceneCamera in sceneCameras)
        {
            if (sceneCamera == serverCamera ||
                sceneCamera.targetTexture != firstDisplayTexture ||
                !sceneCamera.enabled)
            {
                continue;
            }

            sceneCamera.enabled = false;
            disabledDisplayCameras.Add(sceneCamera);
        }

        serverCamera.targetTexture = firstDisplayTexture;
        serverCamera.enabled = true;
    }

    private void RestoreFirstDisplayFeed()
    {
        if (serverCamera != null)
        {
            serverCamera.targetTexture = originalServerTarget;
            serverCamera.enabled = originalServerCameraEnabled;
        }

        foreach (Camera displayCamera in disabledDisplayCameras)
        {
            if (displayCamera != null)
                displayCamera.enabled = true;
        }

        disabledDisplayCameras.Clear();
    }
}
