using UnityEngine;

[DisallowMultipleComponent]
public sealed class HudTopControlsAnchor : MonoBehaviour
{
    [SerializeField]
    private HudGridManager gridManager;

    [Tooltip(
        "Angular distance from the top of the HUD grid " +
        "to the center of the teleop button."
    )]
    [SerializeField]
    private float gapAboveGridDegrees = 3f;

    [SerializeField]
    private float surfaceOffsetTowardUser = 0.03f;

    [SerializeField]
    private bool followGridEveryFrame;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        ApplyPose();
    }

    private void LateUpdate()
    {
        if (followGridEveryFrame)
            ApplyPose();
    }

    public void Configure(
        HudGridManager manager,
        float gapDegrees,
        float surfaceOffset)
    {
        gridManager = manager;
        gapAboveGridDegrees = gapDegrees;
        surfaceOffsetTowardUser = surfaceOffset;

        ApplyPose();
    }

    [ContextMenu("Apply Pose")]
    public void ApplyPose()
    {
        ResolveReferences();

        if (gridManager == null)
            return;

        float yawDegrees =
            gridManager.gridCenterYawDegrees;

        float pitchDegrees =
            gridManager.gridCenterPitchDegrees +
            gridManager.verticalSpanDegrees * 0.5f +
            gapAboveGridDegrees;

        float yaw =
            yawDegrees * Mathf.Deg2Rad;

        float pitch =
            pitchDegrees * Mathf.Deg2Rad;

        float cosPitch =
            Mathf.Cos(pitch);

        Vector3 normalLocal =
            new Vector3(
                Mathf.Sin(yaw) * cosPitch,
                Mathf.Sin(pitch),
                Mathf.Cos(yaw) * cosPitch
            ).normalized;

        float radius =
            Mathf.Max(
                0.1f,
                gridManager.sphereRadius);

        Vector3 centerLocal =
            new Vector3(
                0f,
                0f,
                -radius);

        float drawingRadius =
            Mathf.Max(
                0.1f,
                radius -
                surfaceOffsetTowardUser);

        Vector3 positionLocal =
            centerLocal +
            normalLocal * drawingRadius;

        Vector3 worldPosition =
            gridManager.transform.TransformPoint(
                positionLocal);

        Vector3 forward =
            gridManager.transform.TransformDirection(
                normalLocal);

        Vector3 up =
            Vector3.ProjectOnPlane(
                gridManager.transform.up,
                forward).normalized;

        if (up.sqrMagnitude < 0.0001f)
            up = Vector3.up;

        transform.SetPositionAndRotation(
            worldPosition,
            Quaternion.LookRotation(
                forward,
                up));
    }

    private void ResolveReferences()
    {
        if (gridManager == null)
        {
            gridManager =
                Object.FindAnyObjectByType<
                    HudGridManager>();
        }
    }
}
