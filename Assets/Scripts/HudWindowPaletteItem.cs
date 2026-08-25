using UnityEngine;

[DisallowMultipleComponent]
public class HudWindowPaletteItem : MonoBehaviour
{
    [Header("Target Window")]
    public GameObject targetWindow;

    [Header("HUD")]
    public Transform hudWindowLayer;


    [Header("Automatically Resolved")]
    [SerializeField]
    private HudGridWindowController
        targetGridController;

    [SerializeField]
    private HudSphereWindowConstraint
        targetSphereConstraint;

    [SerializeField]
    private HudGridManager
        gridManager;


    private Vector3 homeLocalPosition;
    private Quaternion homeLocalRotation;
    private Vector3 homeLocalScale;

    private bool homePoseStored = false;


    // ========================================================
    // UNITY
    // ========================================================

    void Awake()
    {
        ResolveReferences();
        StoreHomePose();
    }


    void Start()
    {
        ResolveReferences();

        if (!homePoseStored)
        {
            StoreHomePose();
        }
    }


    // ========================================================
    // REFERENCES
    // ========================================================

    private void ResolveReferences()
    {
        if (targetWindow != null)
        {
            targetGridController =
                targetWindow.GetComponent
                <HudGridWindowController>();


            targetSphereConstraint =
                targetWindow.GetComponent
                <HudSphereWindowConstraint>();
        }


        if (gridManager == null)
        {
            gridManager =
                Object.FindFirstObjectByType
                <HudGridManager>();
        }
    }


    // ========================================================
    // HOME POSITION
    // ========================================================

    private void StoreHomePose()
    {
        homeLocalPosition =
            transform.localPosition;

        homeLocalRotation =
            transform.localRotation;

        homeLocalScale =
            transform.localScale;

        homePoseStored =
            true;
    }


    private void ReturnToMenu()
    {
        transform.localPosition =
            homeLocalPosition;

        transform.localRotation =
            homeLocalRotation;

        transform.localScale =
            homeLocalScale;
    }


    // ========================================================
    // XR GRAB EVENTS
    // ========================================================

    /*
     * XR Grab Interactable
     * -> First Select Entered
     */
    public void NotifyGrabStarted()
    {
        ResolveReferences();
    }


    /*
     * XR Grab Interactable
     * -> Last Select Exited
     */
    public void NotifyGrabEnded()
    {
        /*
         * Capture where the user dropped the launcher tile
         * BEFORE snapping the tile itself back into the menu.
         */
        Vector3 dropWorldPoint =
            transform.position;


        ReturnToMenu();


        OpenTargetWindowAt(
            dropWorldPoint
        );
    }


    // ========================================================
    // WINDOW CREATION / REOPEN
    // ========================================================

    public void OpenTargetWindowAt(
        Vector3 worldPoint)
    {
        ResolveReferences();


        if (targetWindow == null)
        {
            Debug.LogError(
                $"Palette item '{name}': " +
                "Target Window is missing."
            );

            return;
        }


        if (targetGridController == null)
        {
            Debug.LogError(
                $"Palette item '{name}': " +
                "Target Window has no " +
                "HudGridWindowController."
            );

            return;
        }


        if (gridManager == null)
        {
            Debug.LogError(
                $"Palette item '{name}': " +
                "HudGridManager is missing."
            );

            return;
        }


        // ----------------------------------------------------
        // ACTIVATE WINDOW
        // ----------------------------------------------------

        targetWindow.SetActive(
            true
        );


        // ----------------------------------------------------
        // MAKE SURE IT BELONGS TO THE HUD WINDOW LAYER
        // ----------------------------------------------------

        if (hudWindowLayer != null &&
            targetWindow.transform.parent !=
            hudWindowLayer)
        {
            targetWindow.transform.SetParent(
                hudWindowLayer,
                true
            );
        }


        // ----------------------------------------------------
        // RESTORE HUD BEHAVIOUR
        // ----------------------------------------------------

        targetGridController
            .SetHudGridEnabled(
                true
            );


        if (targetSphereConstraint != null)
        {
            targetSphereConstraint
                .EnableHudConstraint();
        }


        /*
         * RegisterWindow gives the object legal occupancy.
         *
         * If this window was already active/registered,
         * RegisterWindow safely ignores the duplicate call.
         */
        gridManager.RegisterWindow(
            targetGridController
        );


        /*
         * Now immediately treat it as though the user grabbed
         * the actual window, moved it to the palette drop
         * location, then released it.
         *
         * This lets us reuse the exact same trusted grid
         * snapping code instead of implementing another
         * placement system just for the menu.
         */
        gridManager.BeginWindowMove(
            targetGridController
        );


        targetWindow.transform.position =
            worldPoint;


        gridManager.EndWindowMove(
            targetGridController
        );
    }
}