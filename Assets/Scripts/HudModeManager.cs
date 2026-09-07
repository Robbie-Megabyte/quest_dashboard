using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class HudModeManager : MonoBehaviour
{
    public enum HudMode
    {
        Edit,
        Live
    }


    // ========================================================
    // MODE
    // ========================================================

    [Header("Mode")]

    public HudMode startMode = HudMode.Edit;


    [SerializeField]
    private HudMode currentMode = HudMode.Edit;


    // ========================================================
    // HUD REFERENCES
    // ========================================================

    [Header("HUD References")]

    [SerializeField]
    private Transform windowLayer;


    [SerializeField]
    private GameObject menuButtonRoot;

    [SerializeField]
    private GameObject presetsButtonRoot;


    [SerializeField]
    private GameObject modeButtonRoot;


    [SerializeField]
    private HudWindowPaletteController paletteController;

    [SerializeField]
    private G1QuestTeleopModeCoordinator
        teleopCoordinator;


    [SerializeField]
    private GameObject gridVisualizerRoot;


    [SerializeField]
    private TMP_Text modeButtonLabel;



    // ========================================================
    // BUTTON LABELS
    // ========================================================

    [Header("Mode Toggle Labels")]

    [Tooltip(
        "Shown while currently in Edit Mode. " +
        "Pressing it ENTERS Live Mode."
    )]
    public string enterLiveModeLabel = "LIVE";


    [Tooltip(
        "Shown while currently in Live Mode. " +
        "Pressing it ENTERS Edit Mode."
    )]
    public string enterEditModeLabel = "EDIT";


    [Header("Diagnostics")]
    public bool logModeChanges = true;


    // ========================================================
    // PUBLIC STATE
    // ========================================================

    public HudMode CurrentMode
    {
        get
        {
            return currentMode;
        }
    }


    public bool IsEditMode
    {
        get
        {
            return currentMode == HudMode.Edit;
        }
    }


    public bool IsLiveMode
    {
        get
        {
            return currentMode == HudMode.Live;
        }
    }


    // ========================================================
    // UNITY
    // ========================================================

    void Awake()
    {
        RebindReferences();

        currentMode =
            startMode;
    }


    void Start()
    {
        ApplyCurrentMode();
    }


    void OnValidate()
    {
        RebindReferences();
    }


    void Update()
    {
        /*
         * A real or simulated teleop transition must remain
         * in Live mode. This also catches transitions started
         * externally by the robot/controller.
         */
        if (
            currentMode == HudMode.Edit &&
            IsEditModeBlocked()
        )
        {
            currentMode = HudMode.Live;
            ApplyCurrentMode();
        }
    }


    // ========================================================
    // REFERENCES
    // ========================================================

    [ContextMenu("Rebind References")]
    public void RebindReferences()
    {
        Transform hudRoot =
            transform.parent;


        if (hudRoot == null)
        {
            hudRoot = transform;
        }


        Transform found;


        found =
            hudRoot.Find(
                "HUD_WindowLayer"
            );


        if (found != null)
        {
            windowLayer =
                found;
        }


        found =
            hudRoot.Find(
                "HUD_GridVisualizer"
            );


        if (found != null)
        {
            gridVisualizerRoot =
                found.gameObject;
        }


        found =
            hudRoot.Find(
                "HUD_BottomControls/MenuButton"
            );
        
        if (found != null)
        {
            menuButtonRoot =
                found.gameObject;
        }

        found =
            hudRoot.Find(
                "HUD_BottomControls/PresetsButton"
            );
        


        if (found != null)
        {
            presetsButtonRoot =
                found.gameObject;
        }


        found =
            hudRoot.Find(
                "HUD_BottomControls/ModeButton"
            );


        if (found != null)
        {
            modeButtonRoot =
                found.gameObject;


            modeButtonLabel =
                found.GetComponentInChildren
                <TMP_Text>(true);
        }


        paletteController =
            hudRoot.GetComponentInChildren
            <HudWindowPaletteController>(true);


        if (teleopCoordinator == null)
        {
            teleopCoordinator =
                Object.FindAnyObjectByType
                <G1QuestTeleopModeCoordinator>();
        }
    }


    // ========================================================
    // COMMANDS
    // ========================================================

    public void ToggleMode()
    {
        if (currentMode ==
            HudMode.Edit)
        {
            SetMode(
                HudMode.Live
            );
        }
        else
        {
            SetMode(
                HudMode.Edit
            );
        }
    }


    public void EnterEditMode()
    {
        SetMode(
            HudMode.Edit
        );
    }


    public void EnterLiveMode()
    {
        SetMode(
            HudMode.Live
        );
    }


    public void SetMode(
        HudMode newMode)
    {
        if (
            newMode == HudMode.Edit &&
            IsEditModeBlocked()
        )
        {
            if (logModeChanges)
            {
                Debug.LogWarning(
                    "HUD MODE: Edit is blocked during " +
                    "teleoperation or a safety transition.",
                    this
                );
            }

            ApplyCurrentMode();
            return;
        }


        currentMode =
            newMode;


        ApplyCurrentMode();
    }


    private bool IsEditModeBlocked()
    {
        return
            teleopCoordinator != null &&
            teleopCoordinator.BlocksHudEditing;
    }


    // ========================================================
    // APPLY
    // ========================================================

    public void ApplyCurrentMode()
    {
        RebindReferences();


        bool editMode =
            currentMode ==
            HudMode.Edit;


        // ----------------------------------------------------
        // CLOSE EDIT PALETTE WHEN ENTERING LIVE
        // ----------------------------------------------------

        if (!editMode &&
            paletteController != null)
        {
            paletteController.CloseMenu();
        }


        // ----------------------------------------------------
        // MENU EXISTS ONLY IN EDIT MODE
        // ----------------------------------------------------

        if (menuButtonRoot != null)
        {
            menuButtonRoot.SetActive(
                editMode
            );
        }

        if (presetsButtonRoot != null)
        {
            presetsButtonRoot.SetActive(editMode);
        }

        if (modeButtonRoot != null)
        {
            modeButtonRoot.SetActive(
                true
            );
        }

        // ----------------------------------------------------
        // GRID VISUALIZER
        // ----------------------------------------------------

        if (gridVisualizerRoot != null)
        {
            gridVisualizerRoot.SetActive(
                editMode
            );
        }


        // ----------------------------------------------------
        // WINDOW PRESENTATION
        // ----------------------------------------------------

        if (windowLayer != null)
        {
            HudWindowModePresentation[]
                presentations =
                    windowLayer
                    .GetComponentsInChildren
                    <HudWindowModePresentation>(
                        true
                    );


            foreach (
                HudWindowModePresentation presentation
                in presentations)
            {
                if (presentation != null)
                {
                    presentation.ApplyMode(
                        currentMode
                    );
                }
            }
        }


        // ----------------------------------------------------
        // LABEL DESCRIBES WHAT PRESSING THE BUTTON DOES
        //
        // EDIT MODE -> [ LIVE ]
        // LIVE MODE -> [ EDIT ]
        // ----------------------------------------------------

        if (modeButtonLabel != null)
        {
            modeButtonLabel.text =
                IsEditModeBlocked()
                    ? "TELEOP"
                    : (
                        editMode
                            ? enterLiveModeLabel
                            : enterEditModeLabel
                    );
        }


        // ----------------------------------------------------
        // RECENTER BOTTOM CONTROLS AFTER MENU HIDES/APPEARS
        // ----------------------------------------------------

        HudBottomControlsLayout layout =
            GetComponentInParent<Transform>()
            != null
                ? Object.FindAnyObjectByType
                    <HudBottomControlsLayout>()
                : null;


        if (layout != null)
        {
            layout.ApplyLayout();
        }


        if (logModeChanges)
        {
            Debug.Log(
                $"HUD MODE: {currentMode}"
            );
        }
    }
}