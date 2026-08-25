using UnityEngine;

[DisallowMultipleComponent]
public class RobotWindowContentFitter : MonoBehaviour
{
    [Header("References")]
    public HudWindow hudWindow;

    [Tooltip(
        "Root containing the actual G1 model."
    )]
    public Transform robotContentRoot;


    [Header("Window Fit")]
    [Range(0.1f, 1.0f)]
    [Tooltip(
        "Maximum fraction of the window width used by the robot."
    )]
    public float widthFill = 0.72f;

    [Range(0.1f, 1.0f)]
    [Tooltip(
        "Maximum fraction of the usable content height."
    )]
    public float heightFill = 0.82f;


    [Header("Header")]
    [Tooltip(
        "Physical header height. This area is excluded from " +
        "the robot's available vertical space."
    )]
    public float headerHeightMeters = 0.07f;


    [Header("Placement")]
    [Tooltip(
        "Moves the robot slightly toward the user's side " +
        "of the panel."
    )]
    public float depthTowardUser = -0.045f;

    [Tooltip(
        "Additional vertical adjustment after centering."
    )]
    public float verticalOffset = -0.01f;

    [Tooltip(
        "Additional horizontal adjustment after centering."
    )]
    public float horizontalOffset = 0.0f;


    [Header("Orientation")]
    public Vector3 robotEulerAngles =
        Vector3.zero;


    private Bounds modelBounds;

    private bool hasBounds = false;


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

        CalculateModelBounds();

        if (hudWindow != null)
        {
            hudWindow.SizeChanged +=
                HandleWindowSizeChanged;
        }

        FitRobot();
    }


    void OnDestroy()
    {
        if (hudWindow != null)
        {
            hudWindow.SizeChanged -=
                HandleWindowSizeChanged;
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


        if (robotContentRoot == null)
        {
            Transform found =
                transform.Find(
                    "RobotContentRoot"
                );

            if (found != null)
            {
                robotContentRoot =
                    found;
            }
        }


        if (hudWindow == null)
        {
            Debug.LogError(
                $"RobotWindowContentFitter '{name}': " +
                "HudWindow reference missing."
            );
        }


        if (robotContentRoot == null)
        {
            Debug.LogError(
                $"RobotWindowContentFitter '{name}': " +
                "RobotContentRoot reference missing."
            );
        }
    }


    // ========================================================
    // WINDOW EVENTS
    // ========================================================

    private void HandleWindowSizeChanged(
        HudWindow window)
    {
        FitRobot();
    }


    // ========================================================
    // MODEL BOUNDS
    // ========================================================

    public void CalculateModelBounds()
    {
        if (robotContentRoot == null)
            return;


        Renderer[] renderers =
            robotContentRoot
                .GetComponentsInChildren
                <Renderer>(true);


        if (renderers == null ||
            renderers.Length == 0)
        {
            hasBounds =
                false;

            Debug.LogWarning(
                $"RobotWindowContentFitter '{name}': " +
                "no Renderers found."
            );

            return;
        }


        bool initialized =
            false;


        Bounds combined =
            new Bounds();


        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;


            Bounds localBounds =
                renderer.localBounds;


            Vector3 center =
                localBounds.center;


            Vector3 extents =
                localBounds.extents;


            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 rendererLocalCorner =
                            center +
                            Vector3.Scale(
                                extents,
                                new Vector3(
                                    x,
                                    y,
                                    z
                                )
                            );


                        Vector3 worldCorner =
                            renderer.transform
                                .TransformPoint(
                                    rendererLocalCorner
                                );


                        Vector3 rootLocalCorner =
                            robotContentRoot
                                .InverseTransformPoint(
                                    worldCorner
                                );


                        if (!initialized)
                        {
                            combined =
                                new Bounds(
                                    rootLocalCorner,
                                    Vector3.zero
                                );

                            initialized =
                                true;
                        }
                        else
                        {
                            combined.Encapsulate(
                                rootLocalCorner
                            );
                        }
                    }
                }
            }
        }


        if (!initialized)
        {
            hasBounds =
                false;

            return;
        }


        modelBounds =
            combined;

        hasBounds =
            true;
    }


    // ========================================================
    // FIT
    // ========================================================

    public void FitRobot()
    {
        if (hudWindow == null ||
            robotContentRoot == null)
        {
            return;
        }


        if (!hasBounds)
        {
            CalculateModelBounds();

            if (!hasBounds)
                return;
        }


        float availableWidth =
            Mathf.Max(
                0.05f,
                hudWindow.WidthMeters *
                widthFill
            );


        float contentHeight =
            Mathf.Max(
                0.05f,
                hudWindow.HeightMeters -
                headerHeightMeters
            );


        float availableHeight =
            Mathf.Max(
                0.05f,
                contentHeight *
                heightFill
            );


        float boundsWidth =
            Mathf.Max(
                0.0001f,
                modelBounds.size.x
            );


        float boundsHeight =
            Mathf.Max(
                0.0001f,
                modelBounds.size.y
            );


        float scaleFromWidth =
            availableWidth /
            boundsWidth;


        float scaleFromHeight =
            availableHeight /
            boundsHeight;


        float uniformScale =
            Mathf.Min(
                scaleFromWidth,
                scaleFromHeight
            );


        robotContentRoot.localScale =
            Vector3.one *
            uniformScale;


        robotContentRoot.localRotation =
            Quaternion.Euler(
                robotEulerAngles
            );


        float contentCenterY =
            -headerHeightMeters *
            0.5f;


        Vector3 scaledBoundsCenter =
            modelBounds.center *
            uniformScale;


        robotContentRoot.localPosition =
            new Vector3(
                horizontalOffset -
                scaledBoundsCenter.x,

                contentCenterY +
                verticalOffset -
                scaledBoundsCenter.y,

                depthTowardUser -
                scaledBoundsCenter.z
            );
    }


    // ========================================================
    // MANUAL REFRESH
    // ========================================================

    public void RefreshFit()
    {
        CalculateModelBounds();
        FitRobot();
    }
}