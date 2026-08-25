using UnityEngine;

[DisallowMultipleComponent]
public class HudWindowCloseSnapLayout : MonoBehaviour
{
    [Header("Automatically Resolved")]
    [SerializeField]
    private HudWindow hudWindow;

    [SerializeField]
    private HudSphereAnchor sphereAnchor;


    [Header("Header Placement")]
    [Tooltip(
        "Distance from the physical right edge of the window " +
        "to the center of the close button."
    )]
    public float rightInsetMeters =
        0.040f;

    [Tooltip(
        "Distance from the physical top edge of the window " +
        "to the center of the close button."
    )]
    public float topInsetMeters =
        0.035f;


    [Header("Surface")]
    public float surfaceOffsetTowardUser =
        0.018f;


    [Header("Old Canvas Button")]
    public bool disableOldCanvasCloseButton =
        true;

    [SerializeField]
    private GameObject oldCanvasCloseButton;


    private bool subscribed =
        false;


    // ========================================================
    // UNITY
    // ========================================================

    void Awake()
    {
        RebindReferences();
        ApplyLayout();
    }


    void OnEnable()
    {
        RebindReferences();
        Subscribe();
        ApplyLayout();
    }


    void Start()
    {
        RebindReferences();
        ApplyLayout();
    }


    void OnDisable()
    {
        Unsubscribe();
    }


    void OnValidate()
    {
        RebindReferences();

        if (!Application.isPlaying)
        {
            ApplyLayout();
        }
    }


    void OnTransformParentChanged()
    {
        Unsubscribe();

        RebindReferences();

        Subscribe();

        ApplyLayout();
    }


    // ========================================================
    // REFERENCES
    // ========================================================

    private void RebindReferences()
    {
        /*
         * Always take the nearest HudWindow from the CURRENT
         * hierarchy.
         *
         * This prevents duplicated CloseSnapTargets from
         * retaining Camera/Robot/etc. references.
         */
        hudWindow =
            GetComponentInParent
            <HudWindow>(true);


        sphereAnchor =
            GetComponent
            <HudSphereAnchor>();


        oldCanvasCloseButton =
            null;


        if (hudWindow != null)
        {
            Transform found =
                hudWindow.transform.Find(
                    "WindowCanvas/CloseButton"
                );


            if (found != null)
            {
                oldCanvasCloseButton =
                    found.gameObject;
            }
        }
    }


    // ========================================================
    // WINDOW SIZE EVENT
    // ========================================================

    private void Subscribe()
    {
        if (subscribed ||
            hudWindow == null)
        {
            return;
        }


        hudWindow.SizeChanged +=
            HandleWindowSizeChanged;


        subscribed =
            true;
    }


    private void Unsubscribe()
    {
        if (!subscribed ||
            hudWindow == null)
        {
            return;
        }


        hudWindow.SizeChanged -=
            HandleWindowSizeChanged;


        subscribed =
            false;
    }


    private void HandleWindowSizeChanged(
        HudWindow window)
    {
        ApplyLayout();
    }


    // ========================================================
    // LAYOUT
    // ========================================================

    public void ApplyLayout()
    {
        RebindReferences();


        if (hudWindow == null ||
            sphereAnchor == null)
        {
            return;
        }


        float width =
            Mathf.Max(
                0.001f,
                hudWindow.WidthMeters
            );


        float height =
            Mathf.Max(
                0.001f,
                hudWindow.HeightMeters
            );


        float normalizedX =
            0.5f -
            rightInsetMeters /
            width;


        float normalizedY =
            0.5f -
            topInsetMeters /
            height;


        sphereAnchor.normalizedX =
            Mathf.Clamp(
                normalizedX,
                -0.5f,
                0.5f
            );


        sphereAnchor.normalizedY =
            Mathf.Clamp(
                normalizedY,
                -0.5f,
                0.5f
            );


        sphereAnchor.surfaceOffsetTowardUser =
            surfaceOffsetTowardUser;


        sphereAnchor.ApplyPose();


        if (disableOldCanvasCloseButton &&
            oldCanvasCloseButton != null)
        {
            oldCanvasCloseButton.SetActive(
                false
            );
        }
    }
}