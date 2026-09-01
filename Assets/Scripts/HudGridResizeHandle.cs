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
    [Min(0.0f)]
    public float cornerOutsetMeters =
        0.025f;


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

    [Header("Live Resize")]
    [Min(0.01f)]
    public float liveResizeIntervalSeconds = 0.03f;

    private float nextLiveResizeTime;


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
            return;
        }

        if (Time.unscaledTime < nextLiveResizeTime)
        {
            return;
        }

        nextLiveResizeTime =
            Time.unscaledTime + liveResizeIntervalSeconds;

        ApplyLiveResize();
    }

    private void ApplyLiveResize()
    {
        if (gridManager == null || windowController == null)
        {
            return;
        }

        /*
        * XR Interaction Toolkit has already moved the handle before
        * LateUpdate runs, so this is the current grabbed position.
        */
        Vector3 dragWorldPoint = transform.position;

        gridManager.ResizeWindowFromWorldPoint(
            windowController,
            resizeEdge,
            dragWorldPoint
        );
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


        beingDragged = true;
        nextLiveResizeTime = 0f;


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


        float radius =
            HudSphereGeometry.GetRadius(
                hudWindow
            );

        float widthDegrees =
            Mathf.Max(
                0.001f,
                HudSphereGeometry.MetersToDegrees(
                    hudWindow.WidthMeters,
                    radius
                )
            );

        float heightDegrees =
            Mathf.Max(
                0.001f,
                HudSphereGeometry.MetersToDegrees(
                    hudWindow.HeightMeters,
                    radius
                )
            );

        float outsetDegrees =
            HudSphereGeometry.MetersToDegrees(
                Mathf.Max(
                    0.0f,
                    cornerOutsetMeters
                ),
                radius
            );

        float outsideX =
            0.5f +
            outsetDegrees / widthDegrees;

        float outsideY =
            0.5f +
            outsetDegrees / heightDegrees;


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
                normalizedX = -outsideX;
                normalizedY = outsideY;
                break;

            case HudGridManager.ResizeEdge.TopRight:
                normalizedX = outsideX;
                normalizedY = outsideY;
                break;

            case HudGridManager.ResizeEdge.BottomLeft:
                normalizedX = -outsideX;
                normalizedY = -outsideY;
                break;

            case HudGridManager.ResizeEdge.BottomRight:
                normalizedX = outsideX;
                normalizedY = -outsideY;
                break;


            // ------------------------------------------------
            // CARDINALS
            //
            // Kept functional in case we ever re-enable them.
            // ------------------------------------------------

            case HudGridManager.ResizeEdge.Left:
                normalizedX = -outsideX;
                normalizedY = 0.0f;
                break;

            case HudGridManager.ResizeEdge.Right:
                normalizedX = outsideX;
                normalizedY = 0.0f;
                break;

            case HudGridManager.ResizeEdge.Top:
                normalizedX = 0.0f;
                normalizedY = outsideY;
                break;

            case HudGridManager.ResizeEdge.Bottom:
                normalizedX = 0.0f;
                normalizedY = -outsideY;
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