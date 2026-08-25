using UnityEngine;

[DisallowMultipleComponent]
public class HudSphereAnchor : MonoBehaviour
{
    [Header("Automatically Resolved")]
    [SerializeField]
    private HudWindow hudWindow;


    [Header("Position On Window")]
    [Range(-0.5f, 0.5f)]
    public float normalizedX = 0.0f;

    [Range(-0.5f, 0.5f)]
    public float normalizedY = 0.0f;


    [Header("Depth")]
    [Tooltip(
        "Moves the object slightly toward the user's eyes."
    )]
    public float surfaceOffsetTowardUser =
        0.015f;


    [Header("Runtime")]
    public bool followEveryFrame =
        true;


    // ========================================================
    // UNITY
    // ========================================================

    void Awake()
    {
        RebindToCurrentWindow();
    }


    void OnEnable()
    {
        RebindToCurrentWindow();
        ApplyPose();
    }


    void Start()
    {
        RebindToCurrentWindow();
        ApplyPose();
    }


    void LateUpdate()
    {
        if (followEveryFrame)
        {
            ApplyPose();
        }
    }


    void OnValidate()
    {
        RebindToCurrentWindow();

        if (!Application.isPlaying)
        {
            ApplyPose();
        }
    }


    void OnTransformParentChanged()
    {
        RebindToCurrentWindow();
        ApplyPose();
    }


    // ========================================================
    // OWNER
    // ========================================================

    private void RebindToCurrentWindow()
    {
        /*
         * IMPORTANT:
         *
         * Always resolve from the CURRENT hierarchy.
         *
         * Do not preserve a serialized HudWindow reference
         * copied from another window.
         */
        hudWindow =
            GetComponentInParent
            <HudWindow>(true);
    }


    // ========================================================
    // POSITION
    // ========================================================

    public void ApplyPose()
    {
        RebindToCurrentWindow();


        if (hudWindow == null)
            return;


        Pose pose =
            HudSphereGeometry
                .GetSurfacePose(
                    hudWindow,
                    normalizedX,
                    normalizedY,
                    surfaceOffsetTowardUser
                );


        transform.SetPositionAndRotation(
            pose.position,
            pose.rotation
        );
    }
}