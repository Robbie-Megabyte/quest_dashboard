using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(G1AgentsWindowView))]
public sealed class G1AgentsGoalPicker :
    MonoBehaviour,
    IPointerDownHandler,
    IDragHandler,
    IPointerUpHandler
{
    public enum TargetAgent
    {
        Robot,
        Vehicle
    }

    [Header("References")]
    [SerializeField] private G1AgentsWindowView agentsView;

    [Header("Selection Test")]
    [Tooltip("This stage only selects and displays a goal. It sends no command.")]
    [SerializeField] private bool selectionEnabled = true;
    [SerializeField] private TargetAgent targetAgent = TargetAgent.Robot;
    [SerializeField, Min(0.01f)] private float minimumHeadingDragMetres = 0.15f;

    public bool HasSelection { get; private set; }
    public Vector2 SelectedMapPosition { get; private set; }
    public float SelectedYawDegrees { get; private set; }
    public TargetAgent SelectedAgent => targetAgent;
    public bool SelectionEnabled => selectionEnabled;

    public event Action<G1AgentsGoalPicker>
        SelectionCompleted;

    private bool dragging;
    private Vector2 dragOrigin;

    private void Awake()
    {
        ResolveReferences();
    }

    public void SetSelectionEnabled(bool enabled)
    {
        selectionEnabled = enabled;
        if (!enabled)
            dragging = false;
    }

    public void SelectRobotTarget()
    {
        targetAgent = TargetAgent.Robot;
        RefreshMarker();
    }

    public void SelectVehicleTarget()
    {
        targetAgent = TargetAgent.Vehicle;
        RefreshMarker();
    }

    public void ClearSelection()
    {
        dragging = false;
        HasSelection = false;
        if (agentsView != null)
            agentsView.ClearGoalMarker();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!selectionEnabled || agentsView == null)
            return;

        if (!TryGetMapPoint(eventData, out Vector2 point))
            return;

        dragging = true;
        dragOrigin = point;
        SelectedMapPosition = point;
        SelectedYawDegrees = 0.0f;
        HasSelection = true;
        RefreshMarker();
        eventData.Use();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging || agentsView == null)
            return;

        UpdateHeading(eventData);
        eventData.Use();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!dragging)
            return;

        UpdateHeading(eventData);
        dragging = false;

        Debug.Log(
            "[G1 Agents Goal] agent=" + targetAgent +
            " x=" + SelectedMapPosition.x.ToString("F2") +
            " y=" + SelectedMapPosition.y.ToString("F2") +
            " yaw_deg=" + SelectedYawDegrees.ToString("F1"),
            this
        );

        SelectionCompleted?.Invoke(this);

        eventData.Use();
    }

    private void UpdateHeading(PointerEventData eventData)
    {
        if (!TryGetMapPoint(eventData, out Vector2 point))
            return;

        Vector2 direction = point - dragOrigin;
        if (direction.magnitude >= minimumHeadingDragMetres)
        {
            SelectedYawDegrees = Mathf.Atan2(
                direction.y,
                direction.x
            ) * Mathf.Rad2Deg;
        }

        RefreshMarker();
    }

    private bool TryGetMapPoint(
        PointerEventData eventData,
        out Vector2 point)
    {
        Camera eventCamera =
            eventData.pressEventCamera != null
                ? eventData.pressEventCamera
                : eventData.enterEventCamera;

        if (eventData is TrackedDeviceEventData trackedData &&
            trackedData.rayPoints != null &&
            trackedData.rayPoints.Count >= 2)
        {
            int hitIndex = Mathf.Clamp(
                trackedData.rayHitIndex,
                1,
                trackedData.rayPoints.Count - 1
            );

            Vector3 rayOrigin =
                trackedData.rayPoints[hitIndex - 1];

            Vector3 rayDirection =
                trackedData.rayPoints[hitIndex] -
                rayOrigin;

            if (rayDirection.sqrMagnitude > 0.000001f &&
                agentsView.TryViewportWorldRayToMap(
                    new Ray(rayOrigin, rayDirection),
                    eventCamera,
                    out point
                ))
            {
                return true;
            }
        }

        return agentsView.TryViewportScreenPointToMap(
            eventData.position,
            eventCamera,
            out point
        );
    }

    private void RefreshMarker()
    {
        if (!HasSelection || agentsView == null)
            return;

        Color color = targetAgent == TargetAgent.Robot
            ? new Color32(80, 220, 255, 255)
            : new Color32(255, 190, 65, 255);

        agentsView.SetGoalMarker(
            SelectedMapPosition.x,
            SelectedMapPosition.y,
            SelectedYawDegrees,
            color
        );
    }

    private void ResolveReferences()
    {
        if (agentsView == null)
            agentsView = GetComponent<G1AgentsWindowView>();
    }
}
