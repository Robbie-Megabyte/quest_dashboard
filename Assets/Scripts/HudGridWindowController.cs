using UnityEngine;

[DisallowMultipleComponent]
public class HudGridWindowController : MonoBehaviour
{
    [Header("References")]
    public HudWindow hudWindow;

    public HudGridManager gridManager;

    public HudGridVisualizer gridVisualizer;


    [Header("Initial Grid Placement")]
    [Tooltip(
        "Column counting from the left. " +
        "Column 0 is the far-left grid column."
    )]
    public int startColumn = 0;

    [Tooltip(
        "Row counting from the top. " +
        "Row 0 is the top grid row."
    )]
    public int startRow = 0;


    [Header("Grid Size")]
    [Min(1)]
    public int columnSpan = 2;

    [Min(1)]
    public int rowSpan = 2;


    [Header("HUD Grid State")]
    [SerializeField]
    private bool hudGridEnabled = true;


    [Header("Runtime")]
    [SerializeField]
    private bool isGridPlaced = false;

    [SerializeField]
    private int currentColumn = -1;

    [SerializeField]
    private int currentRow = -1;


    // ========================================================
    // PROPERTIES
    // ========================================================

    public HudWindow HudWindow
    {
        get
        {
            return hudWindow;
        }
    }


    public int StartColumn
    {
        get
        {
            return startColumn;
        }
    }


    public int StartRow
    {
        get
        {
            return startRow;
        }
    }


    public int ColumnSpan
    {
        get
        {
            return Mathf.Max(
                1,
                columnSpan
            );
        }
    }


    public int RowSpan
    {
        get
        {
            return Mathf.Max(
                1,
                rowSpan
            );
        }
    }


    public bool IsGridPlaced
    {
        get
        {
            return isGridPlaced;
        }
    }


    public int CurrentColumn
    {
        get
        {
            return currentColumn;
        }
    }


    public int CurrentRow
    {
        get
        {
            return currentRow;
        }
    }


    public bool HudGridEnabled
    {
        get
        {
            return hudGridEnabled;
        }
    }


    // ========================================================
    // UNITY
    // ========================================================

    void Awake()
    {
        ResolveReferences();
    }


    void Start()
    {
        ResolveReferences();


        if (hudGridEnabled &&
            gridManager != null)
        {
            gridManager.RegisterWindow(
                this
            );
        }
    }


    void OnDestroy()
    {
        if (gridVisualizer != null)
        {
            gridVisualizer.EndWindowPreview(
                this
            );
        }


        if (gridManager != null)
        {
            gridManager.UnregisterWindow(
                this
            );
        }
    }


    // ========================================================
    // REFERENCES
    // ========================================================

    private void ResolveReferences()
    {
        if (hudWindow == null)
        {
            hudWindow =
                GetComponent<HudWindow>();
        }


        if (gridManager == null)
        {
            gridManager =
                Object.FindFirstObjectByType
                <HudGridManager>();
        }


        if (gridVisualizer == null)
        {
            gridVisualizer =
                Object.FindFirstObjectByType
                <HudGridVisualizer>();
        }


        if (hudWindow == null)
        {
            Debug.LogError(
                $"HudGridWindowController '{name}': " +
                "HudWindow is missing."
            );
        }


        if (gridManager == null)
        {
            Debug.LogError(
                $"HudGridWindowController '{name}': " +
                "HudGridManager could not be found."
            );
        }
    }


    // ========================================================
    // GRID ENABLE / DISABLE
    // ========================================================

    public void SetHudGridEnabled(
        bool enabled)
    {
        hudGridEnabled =
            enabled;


        if (!enabled &&
            gridVisualizer != null)
        {
            gridVisualizer.EndWindowPreview(
                this
            );
        }
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
        if (!hudGridEnabled)
            return;


        ResolveReferences();


        if (gridManager != null)
        {
            gridManager.BeginWindowMove(
                this
            );
        }


        if (gridVisualizer != null)
        {
            gridVisualizer.BeginWindowPreview(
                this
            );
        }
    }


    /*
     * XR Grab Interactable
     * -> Last Select Exited
     */
    public void NotifyGrabEnded()
    {
        if (!hudGridEnabled)
            return;


        ResolveReferences();


        if (gridManager != null)
        {
            gridManager.EndWindowMove(
                this
            );
        }


        if (gridVisualizer != null)
        {
            gridVisualizer.EndWindowPreview(
                this
            );
        }
    }


    // ========================================================
    // CALLED BY GRID MANAGER
    // ========================================================

    public void ApplyGridPlacement(
        int column,
        int row,
        int newColumnSpan,
        int newRowSpan)
    {
        currentColumn =
            column;

        currentRow =
            row;


        columnSpan =
            Mathf.Max(
                1,
                newColumnSpan
            );


        rowSpan =
            Mathf.Max(
                1,
                newRowSpan
            );


        isGridPlaced =
            true;
    }


    public void SetGridPlaced(
        bool placed)
    {
        isGridPlaced =
            placed;


        if (!placed)
        {
            currentColumn =
                -1;

            currentRow =
                -1;
        }
    }


    public void SetGridSpan(
        int newColumnSpan,
        int newRowSpan)
    {
        columnSpan =
            Mathf.Max(
                1,
                newColumnSpan
            );


        rowSpan =
            Mathf.Max(
                1,
                newRowSpan
            );
    }
}