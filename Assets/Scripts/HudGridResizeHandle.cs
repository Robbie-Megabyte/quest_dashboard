using UnityEngine;

[DisallowMultipleComponent]
public class HudGridResizeHandle : MonoBehaviour
{
    [Header("Handle")]
    public HudGridManager.ResizeEdge resizeEdge =
        HudGridManager.ResizeEdge.BottomRight;


    [Header("Curved HUD Placement")]
    [Tooltip(
        "Moves the handle slightly outside the visual window " +
        "corner. This also keeps the TopRight handle away " +
        "from the close button."
    )]
    public float cornerOutsetNormalized =
        0.035f;


    [Tooltip(
        "Moves the handle slightly toward the user's eyes."
    )]
    public float surfaceOffsetTowardUser =
        0.018f;


    [Header("Automatically Resolved")]
    [SerializeField]
    private HudGridWindowController
        windowController;

    [SerializeField]
    private HudWindow hudWindow;

    [SerializeField]
    private HudGridManager gridManager;

    [SerializeField]
    private HudGridVisualizer gridVisualizer;


    [Header("Runtime")]
    [SerializeField]
    private bool beingDragged =
        false;


    // ========================================================
    // UNITY
    // ========================================================

    void Awake()
    {
        RebindReferences();
    }


    void Start()
    {
        RebindReferences();
        UpdateRestingPose();
    }


    void LateUpdate()
    {
        if (!beingDragged)
        {
            UpdateRestingPose();
        }
    }


    void OnValidate()
    {
        RebindReferences();

        if (!Application.isPlaying)
        {
            UpdateRestingPose();
        }
    }


    void OnTransformParentChanged()
    {
        RebindReferences();

        if (!beingDragged)
        {
            UpdateRestingPose();
        }
    }


    // ========================================================
    // REFERENCES
    // ========================================================

    private void RebindReferences()
    {
        windowController =
            GetComponentInParent
            <HudGridWindowController>(true);


        hudWindow =
            GetComponentInParent
            <HudWindow>(true);


        if (gridManager == null)
        {
            gridManager =
                Object.FindAnyObjectByType
                <HudGridManager>();
        }


        if (gridVisualizer == null)
        {
            gridVisualizer =
                Object.FindAnyObjectByType
                <HudGridVisualizer>();
        }
    }


    // ========================================================
    // XR EVENTS
    // ========================================================

    public void NotifyGrabStarted()
    {
        RebindReferences();


        beingDragged =
            true;


        if (gridVisualizer != null &&
            windowController != null)
        {
            gridVisualizer.BeginResizePreview(
                windowController
            );
        }
    }


    public void NotifyGrabEnded()
    {
        RebindReferences();


        /*
         * Capture released point BEFORE putting the handle
         * back on the window.
         */
        Vector3 releasedWorldPoint =
            transform.position;


        if (gridManager != null &&
            windowController != null)
        {
            gridManager.ResizeWindowFromWorldPoint(
                windowController,
                resizeEdge,
                releasedWorldPoint
            );
        }


        beingDragged =
            false;


        UpdateRestingPose();


        if (gridVisualizer != null &&
            windowController != null)
        {
            gridVisualizer.EndResizePreview(
                windowController
            );
        }
    }


    // ========================================================
    // CURVED RESTING POSITION
    // ========================================================

    public void UpdateRestingPose()
    {
        if (hudWindow == null)
        {
            RebindReferences();


            if (hudWindow == null)
                return;
        }


        float outside =
            0.5f +
            Mathf.Max(
                0.0f,
                cornerOutsetNormalized
            );


        float normalizedX =
            0.0f;


        float normalizedY =
            0.0f;


        switch (resizeEdge)
        {
            // ------------------------------------------------
            // CORNERS
            // ------------------------------------------------

            case HudGridManager.ResizeEdge.TopLeft:

                normalizedX =
                    -outside;

                normalizedY =
                    outside;

                break;


            case HudGridManager.ResizeEdge.TopRight:

                normalizedX =
                    outside;

                normalizedY =
                    outside;

                break;


            case HudGridManager.ResizeEdge.BottomLeft:

                normalizedX =
                    -outside;

                normalizedY =
                    -outside;

                break;


            case HudGridManager.ResizeEdge.BottomRight:

                normalizedX =
                    outside;

                normalizedY =
                    -outside;

                break;


            // ------------------------------------------------
            // CARDINALS
            //
            // Kept functional in case we ever re-enable them.
            // ------------------------------------------------

            case HudGridManager.ResizeEdge.Left:

                normalizedX =
                    -outside;

                normalizedY =
                    0.0f;

                break;


            case HudGridManager.ResizeEdge.Right:

                normalizedX =
                    outside;

                normalizedY =
                    0.0f;

                break;


            case HudGridManager.ResizeEdge.Top:

                normalizedX =
                    0.0f;

                normalizedY =
                    outside;

                break;


            case HudGridManager.ResizeEdge.Bottom:

                normalizedX =
                    0.0f;

                normalizedY =
                    -outside;

                break;
        }


        Pose pose =
            HudSphereGeometry.GetSurfacePose(
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