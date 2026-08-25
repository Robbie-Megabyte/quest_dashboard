using UnityEngine;

[DisallowMultipleComponent]
public class HudBottomControlsAnchor : MonoBehaviour
{
    [Header("Automatically Resolved")]
    [SerializeField]
    private HudGridManager gridManager;


    [Header("Placement")]

    [Tooltip(
        "Angular gap between the bottom of the HUD grid " +
        "and the controls."
    )]
    public float gapBelowGridDegrees = 1.5f;


    [Tooltip(
        "Moves the controls slightly toward the user's eyes."
    )]
    public float surfaceOffsetTowardUser = 0.030f;


    [Header("Orientation")]
    public Vector3 rotationOffsetEuler = Vector3.zero;


    [Header("Runtime")]
    public bool followGridEveryFrame = false;


    void Awake()
    {
        RebindReferences();
    }


    void Start()
    {
        RebindReferences();
        ApplyPose();
    }


    void LateUpdate()
    {
        if (followGridEveryFrame)
        {
            ApplyPose();
        }
    }


    private void RebindReferences()
    {
        if (gridManager == null)
        {
            gridManager =
                Object.FindAnyObjectByType<HudGridManager>();
        }
    }


    [ContextMenu("Apply Pose")]
    public void ApplyPose()
    {
        RebindReferences();

        if (gridManager == null)
            return;


        float yawDegrees =
            gridManager.gridCenterYawDegrees;


        float pitchDegrees =
            gridManager.gridCenterPitchDegrees
            -
            gridManager.verticalSpanDegrees * 0.5f
            -
            gapBelowGridDegrees;


        float yawRadians =
            yawDegrees * Mathf.Deg2Rad;

        float pitchRadians =
            pitchDegrees * Mathf.Deg2Rad;


        float cosPitch =
            Mathf.Cos(pitchRadians);


        /*
         * Direction from the center of our HUD sphere
         * outward toward the user-facing surface.
         */
        Vector3 surfaceNormalLocal =
            new Vector3(
                Mathf.Sin(yawRadians) * cosPitch,
                Mathf.Sin(pitchRadians),
                Mathf.Cos(yawRadians) * cosPitch
            );


        float sphereRadius =
            Mathf.Max(
                0.1f,
                gridManager.sphereRadius
            );


        Vector3 sphereCenterLocal =
            new Vector3(
                0.0f,
                0.0f,
                -sphereRadius
            );


        /*
         * Smaller radius = slightly toward the user.
         */
        float controlRadius =
            Mathf.Max(
                0.1f,
                sphereRadius -
                surfaceOffsetTowardUser
            );


        Vector3 pointLocal =
            sphereCenterLocal
            +
            surfaceNormalLocal * controlRadius;


        Vector3 worldPosition =
            gridManager.transform.TransformPoint(
                pointLocal
            );


        /*
         * IMPORTANT:
         * The old version used sphereCenter - point here,
         * which pointed the controls INTO the sphere.
         *
         * We want their +Z/front side facing outward,
         * toward the user.
         */
        Vector3 forwardWorld =
            gridManager.transform.TransformDirection(
                surfaceNormalLocal
            );


        Vector3 upReferenceWorld =
            gridManager.transform.TransformDirection(
                Vector3.up
            );


        /*
         * Project the HUD up vector onto the tangent plane
         * so the buttons remain level on the curved visor.
         */
        Vector3 tangentUp =
            Vector3.ProjectOnPlane(
                upReferenceWorld,
                forwardWorld
            ).normalized;


        if (tangentUp.sqrMagnitude < 0.0001f)
        {
            tangentUp = Vector3.up;
        }


        Quaternion worldRotation =
            Quaternion.LookRotation(
                forwardWorld.normalized,
                tangentUp
            );


        worldRotation *=
            Quaternion.Euler(
                rotationOffsetEuler
            );


        transform.SetPositionAndRotation(
            worldPosition,
            worldRotation
        );
    }
}