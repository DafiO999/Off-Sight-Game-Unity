using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class CameraDisplayZoom : MonoBehaviour
{
    [Header("Target")]
    [SerializeField]
    private Transform targetDisplay;

    [SerializeField, Min(0f)]
    [Tooltip("Extra space around the display when it is framed by the camera.")]
    private float framingPadding = 1.15f;

    [SerializeField, Min(0.01f)]
    private float minimumFocusDistance = 1f;

    [Header("Animation")]
    [SerializeField, Min(0f)]
    private float zoomDuration = 0.8f;

    [SerializeField, Range(1f, 179f)]
    private float focusedFieldOfView = 50f;

    private Camera controlledCamera;
    private Vector3 homePosition;
    private Quaternion homeRotation;
    private float homeFieldOfView;
    private Coroutine transition;

    public bool IsFocused { get; private set; }

    private void Awake()
    {
        controlledCamera = GetComponent<Camera>();
        homePosition = transform.position;
        homeRotation = transform.rotation;
        homeFieldOfView = controlledCamera.fieldOfView;
    }

    private void OnValidate()
    {
        framingPadding = Mathf.Max(0f, framingPadding);
        minimumFocusDistance = Mathf.Max(0.01f, minimumFocusDistance);
        zoomDuration = Mathf.Max(0f, zoomDuration);
        focusedFieldOfView = Mathf.Clamp(focusedFieldOfView, 1f, 179f);
    }

    public void SetFocused(bool value)
    {
        if (value && targetDisplay == null)
        {
            Debug.LogWarning($"{nameof(CameraDisplayZoom)} has no target display.", this);
            return;
        }

        if (transition != null)
            StopCoroutine(transition);

        IsFocused = value;
        transition = StartCoroutine(AnimateTo(value));
    }

    public void SnapHome()
    {
        if (transition != null)
            StopCoroutine(transition);

        transition = null;
        IsFocused = false;
        transform.SetPositionAndRotation(homePosition, homeRotation);
        controlledCamera.fieldOfView = homeFieldOfView;
    }

    private IEnumerator AnimateTo(bool focus)
    {
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        float startFieldOfView = controlledCamera.fieldOfView;

        Vector3 endPosition = homePosition;
        Quaternion endRotation = homeRotation;
        float endFieldOfView = homeFieldOfView;

        if (focus)
        {
            CalculateFocusPose(out endPosition, out endRotation);
            endFieldOfView = focusedFieldOfView;
        }

        if (zoomDuration <= 0f)
        {
            ApplyPose(endPosition, endRotation, endFieldOfView);
            transition = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < zoomDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / zoomDuration));
            ApplyPose(
                Vector3.Lerp(startPosition, endPosition, t),
                Quaternion.Slerp(startRotation, endRotation, t),
                Mathf.Lerp(startFieldOfView, endFieldOfView, t));
            yield return null;
        }

        ApplyPose(endPosition, endRotation, endFieldOfView);
        transition = null;
    }

    private void CalculateFocusPose(out Vector3 position, out Quaternion rotation)
    {
        Renderer displayRenderer = targetDisplay.GetComponent<Renderer>();
        Vector3 center = displayRenderer != null
            ? displayRenderer.bounds.center
            : targetDisplay.position;

        Vector3 normal = GetDisplayNormal();
        if (Vector3.Dot(normal, homePosition - center) < 0f)
            normal = -normal;

        rotation = Quaternion.LookRotation(-normal, Vector3.up);
        float distance = CalculateFramingDistance(displayRenderer, rotation);
        position = center + normal * distance;
    }

    private Vector3 GetDisplayNormal()
    {
        MeshFilter meshFilter = targetDisplay.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
            return targetDisplay.forward;

        Vector3 size = meshFilter.sharedMesh.bounds.size;
        Vector3 localNormal;

        if (size.x <= size.y && size.x <= size.z)
            localNormal = Vector3.right;
        else if (size.y <= size.z)
            localNormal = Vector3.up;
        else
            localNormal = Vector3.forward;

        return targetDisplay.TransformDirection(localNormal).normalized;
    }

    private float CalculateFramingDistance(Renderer displayRenderer, Quaternion focusRotation)
    {
        if (displayRenderer == null)
            return minimumFocusDistance;

        Vector3 cameraRight = focusRotation * Vector3.right;
        Vector3 cameraUp = focusRotation * Vector3.up;
        Vector3 extents = displayRenderer.bounds.extents;

        float halfWidth =
            Mathf.Abs(cameraRight.x) * extents.x +
            Mathf.Abs(cameraRight.y) * extents.y +
            Mathf.Abs(cameraRight.z) * extents.z;
        float halfHeight =
            Mathf.Abs(cameraUp.x) * extents.x +
            Mathf.Abs(cameraUp.y) * extents.y +
            Mathf.Abs(cameraUp.z) * extents.z;

        float verticalHalfAngle = focusedFieldOfView * Mathf.Deg2Rad * 0.5f;
        float aspect = Mathf.Max(0.01f, controlledCamera.aspect);
        float horizontalHalfAngle = Mathf.Atan(Mathf.Tan(verticalHalfAngle) * aspect);

        float verticalDistance = halfHeight / Mathf.Tan(verticalHalfAngle);
        float horizontalDistance = halfWidth / Mathf.Tan(horizontalHalfAngle);
        return Mathf.Max(
            minimumFocusDistance,
            Mathf.Max(verticalDistance, horizontalDistance) * framingPadding);
    }

    private void ApplyPose(Vector3 position, Quaternion rotation, float fieldOfView)
    {
        transform.SetPositionAndRotation(position, rotation);
        controlledCamera.fieldOfView = fieldOfView;
    }
}
