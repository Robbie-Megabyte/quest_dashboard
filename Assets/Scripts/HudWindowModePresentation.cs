using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.UI;

[DisallowMultipleComponent]
public class HudWindowModePresentation : MonoBehaviour
{
    // ========================================================
    // LIVE MODE POLICY
    // ========================================================

    [Header("Live Mode")]

    [Tooltip(
        "Enable this for text-heavy windows such as " +
        "Telemetry and Debug. Disable it for Robot/Camera."
    )]
    public bool keepBackgroundInLive = false;


    [Tooltip(
        "Normally false. Headers are editing chrome."
    )]
    public bool keepHeaderInLive = false;


    [Header("Automatically Resolved")]
    [SerializeField]
    private TrackedDeviceGraphicRaycaster windowRaycaster;

    [SerializeField]
    private GameObject background;

    [SerializeField]
    private GameObject header;

    [SerializeField]
    private GameObject title;

    [SerializeField]
    private GameObject closeSnapTarget;

    [SerializeField]
    private GameObject resizeHandlesRoot;

    [SerializeField]
    private GameObject grabHandle;

    [SerializeField]
    private HudGridWindowController gridController;

    [SerializeField]
    private G1SlamWindowView slamWindowView;

    [SerializeField]
    private G1RobotWindowView robotWindowView;


    [Header("Optional Extra Edit-Only Objects")]

    [Tooltip(
        "Anything else belonging to this specific window " +
        "that should disappear in Live Mode."
    )]
    public GameObject[] additionalEditOnlyObjects;


    // ========================================================
    // UNITY
    // ========================================================

    void Awake()
    {
        RebindReferences();
    }


    void OnEnable()
    {
        RebindReferences();
    }


    void OnValidate()
    {
        RebindReferences();
    }


    // ========================================================
    // REFERENCE BINDING
    // ========================================================

    [ContextMenu("Rebind References")]
    public void RebindReferences()
    {
        Transform found;


        found =
            transform.Find(
                "WindowCanvas/Background"
            );

        background =
            found != null
                ? found.gameObject
                : null;


        found =
            transform.Find(
                "WindowCanvas/Header"
            );

        header =
            found != null
                ? found.gameObject
                : null;


        found =
            transform.Find(
                "WindowCanvas/Title"
            );

        title =
            found != null
                ? found.gameObject
                : null;


        found =
            transform.Find(
                "CloseSnapTarget"
            );

        closeSnapTarget =
            found != null
                ? found.gameObject
                : null;


        found =
            transform.Find(
                "Resize_Handles"
            );

        resizeHandlesRoot =
            found != null
                ? found.gameObject
                : null;


        found =
            transform.Find(
                "GrabHandle"
            );

        grabHandle =
            found != null
                ? found.gameObject
                : null;


        gridController =
            GetComponent<HudGridWindowController>();

        slamWindowView =
        GetComponentInChildren<G1SlamWindowView>(
            true
        );
        robotWindowView =
        GetComponentInChildren<G1RobotWindowView>(
            true
        );
    }


    // ========================================================
    // MODE
    // ========================================================

    public void ApplyMode(
        HudModeManager.HudMode mode)
    {
        RebindReferences();


        bool editMode =
            mode ==
            HudModeManager.HudMode.Edit;
        // ----------------------------------------------------
        // CANVAS INTERACTION
        //
        // In Edit mode, XR rays must pass through the Canvas
        // graphics and reach the whole-window grab collider.
        // Close and resize controls use separate XR physics
        // interactables, so they remain available.
        // ----------------------------------------------------

        if (windowRaycaster != null)
        {
            windowRaycaster.enabled =
                !editMode;
        }

        // ----------------------------------------------------
        // BACKGROUND
        // ----------------------------------------------------

        if (background != null)
        {
            background.SetActive(
                editMode ||
                keepBackgroundInLive
            );
        }


        // ----------------------------------------------------
        // HEADER + TITLE
        // ----------------------------------------------------

        bool showHeader =
            editMode ||
            keepHeaderInLive;


        if (header != null)
        {
            header.SetActive(
                showHeader
            );
        }


        if (title != null)
        {
            title.SetActive(
                showHeader
            );
        }


        // ----------------------------------------------------
        // CLOSE CONTROL
        // ----------------------------------------------------

        if (closeSnapTarget != null)
        {
            closeSnapTarget.SetActive(
                editMode
            );
        }


        // ----------------------------------------------------
        // RESIZE CONTROLS
        // ----------------------------------------------------

        if (resizeHandlesRoot != null)
        {
            resizeHandlesRoot.SetActive(
                editMode
            );
        }


        // ----------------------------------------------------
        // WINDOW MOVEMENT
        //
        // Our window grabbing is based on the GrabHandle.
        // Hiding it removes the physical grab target.
        // ----------------------------------------------------

        if (grabHandle != null)
        {
            grabHandle.SetActive(
                editMode
            );
        }


        // ----------------------------------------------------
        // GRID LOGIC
        //
        // Keep the placement itself registered.
        // Only stop it from participating in editing.
        // ----------------------------------------------------

        if (gridController != null)
        {
            gridController.SetHudGridEnabled(
                editMode
            );
        }


        // ----------------------------------------------------
        // OPTIONAL WINDOW-SPECIFIC CHROME
        // ----------------------------------------------------

        if (additionalEditOnlyObjects != null)
        {
            foreach (
                GameObject obj
                in additionalEditOnlyObjects)
            {
                if (obj != null)
                {
                    obj.SetActive(
                        editMode
                    );
                }
            }
        }
        // ----------------------------------------------------
        // WINDOW CONTENT INTERACTION
        //
        // Edit mode reserves dragging for window placement.
        // Live mode restores SLAM pan, rotation and zoom.
        // ----------------------------------------------------

        if (slamWindowView != null)
        {
            slamWindowView.SetInteractionEnabled(
                !editMode
            );
        }

        if (robotWindowView != null)
        {
            robotWindowView.SetInteractionEnabled(
                !editMode
            );
        }
    }
}