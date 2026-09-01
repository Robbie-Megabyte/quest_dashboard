using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class HudWindow : MonoBehaviour
{
    public enum WindowLayoutState
    {
        Free,
        Tiled
    }
    [Header("Curved Visual Surface")]
    public bool bendAllWindowGraphics = true;

    [Range(1, 8)]
    public int graphicBendSubdivisions = 8;

    public float visualSurfaceOffsetTowardUser = 0.0f;

    [Header("Identity")]
    public string windowId = "window";
    public string windowTitle = "WINDOW";

    [Header("Required References")]
    public RectTransform windowCanvas;
    public Transform grabHandle;
    public BoxCollider grabHandleCollider;
    public TMP_Text titleText;
    public HudSphereWindowConstraint sphereConstraint;

    [Header("Window Size Limits")]
    public Vector2 minimumSizeMeters =
        new Vector2(0.24f, 0.16f);

    public Vector2 maximumSizeMeters =
        new Vector2(1.20f, 0.80f);

    [Header("Header Geometry")]
    [Tooltip("Physical height of the header.")]
    public float headerHeightMeters = 0.07f;

    [Tooltip(
        "Header space reserved on the right for " +
        "buttons such as Pin / Close."
    )]
    public float reservedRightHeaderMeters = 0.14f;

    [Tooltip("Depth of the invisible grab collider.")]
    public float grabColliderDepthMeters = 0.025f;

    [Header("Runtime")]
    [SerializeField]
    private WindowLayoutState layoutState =
        WindowLayoutState.Free;

    /*
     * Future tiling/layout systems can subscribe to this.
     */
    public event Action<HudWindow> SizeChanged;


    public WindowLayoutState LayoutState
    {
        get
        {
            return layoutState;
        }
    }


    public bool IsTiled
    {
        get
        {
            return layoutState ==
                   WindowLayoutState.Tiled;
        }
    }


    public float WidthMeters
    {
        get
        {
            if (windowCanvas == null)
                return 0.0f;

            return
                windowCanvas.rect.width *
                Mathf.Abs(
                    windowCanvas.localScale.x
                );
        }
    }


    public float HeightMeters
    {
        get
        {
            if (windowCanvas == null)
                return 0.0f;

            return
                windowCanvas.rect.height *
                Mathf.Abs(
                    windowCanvas.localScale.y
                );
        }
    }


    public Vector2 SizeMeters
    {
        get
        {
            return new Vector2(
                WidthMeters,
                HeightMeters
            );
        }
    }


    void Awake()
{
    ValidateReferences();
    EnsureCurvedVisuals();

    transform.localScale =
        Vector3.one;

    SetTitle(
        windowTitle
    );

    UpdateWindowGeometry();
}


    private void ValidateReferences()
    {
        if (windowCanvas == null)
        {
            Debug.LogError(
                $"HudWindow '{name}': " +
                "Window Canvas is missing."
            );
        }

        if (grabHandle == null)
        {
            Debug.LogError(
                $"HudWindow '{name}': " +
                "Grab Handle is missing."
            );
        }

        if (grabHandleCollider == null)
        {
            Debug.LogError(
                $"HudWindow '{name}': " +
                "Grab Handle Collider is missing."
            );
        }

        if (sphereConstraint == null)
        {
            Debug.LogWarning(
                $"HudWindow '{name}': " +
                "Sphere Constraint is missing."
            );
        }
    }



    public void SetTitle(
        string newTitle)
    {
        windowTitle =
            newTitle;

        if (titleText != null)
        {
            titleText.text =
                newTitle;
        }
    }


    public void SetSizeMeters(
        float widthMeters,
        float heightMeters)
    {
        if (windowCanvas == null)
            return;

        widthMeters =
            Mathf.Clamp(
                widthMeters,
                minimumSizeMeters.x,
                maximumSizeMeters.x
            );

        heightMeters =
            Mathf.Clamp(
                heightMeters,
                minimumSizeMeters.y,
                maximumSizeMeters.y
            );

        float canvasScaleX =
            Mathf.Abs(
                windowCanvas.localScale.x
            );

        float canvasScaleY =
            Mathf.Abs(
                windowCanvas.localScale.y
            );

        if (canvasScaleX <
            0.000001f)
        {
            canvasScaleX =
                0.001f;
        }

        if (canvasScaleY <
            0.000001f)
        {
            canvasScaleY =
                0.001f;
        }

        float widthCanvasUnits =
            widthMeters /
            canvasScaleX;

        float heightCanvasUnits =
            heightMeters /
            canvasScaleY;

        windowCanvas.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            widthCanvasUnits
        );

        windowCanvas.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            heightCanvasUnits
        );

        UpdateWindowGeometry();

        SizeChanged?.Invoke(
            this
        );
    }


    public void SetSizeMeters(
        Vector2 sizeMeters)
    {
        SetSizeMeters(
            sizeMeters.x,
            sizeMeters.y
        );
    }

    public void RefreshCurvedVisuals()
    {
        EnsureCurvedVisuals();
    }


    private void EnsureCurvedVisuals()
    {
        if (!bendAllWindowGraphics)
            return;

        Graphic[] graphics =
            GetComponentsInChildren<Graphic>(true);

        foreach (Graphic childGraphic in graphics)
        {
            if (childGraphic == null)
                continue;

            TextMeshProUGUI text =
                childGraphic as TextMeshProUGUI;

            if (text != null)
            {
                HudSphereTextBender textBender =
                    text.GetComponent<HudSphereTextBender>();

                if (textBender == null)
                {
                    textBender =
                        text.gameObject.AddComponent
                        <HudSphereTextBender>();
                }

                textBender.hudWindow =
                    this;

                textBender.surfaceOffsetTowardUser =
                    visualSurfaceOffsetTowardUser;

                text.SetVerticesDirty();
                continue;
            }
            // Transparent graphics are interaction surfaces, not visible curved content.
            if (childGraphic.color.a <= 0.001f)
            {
                continue;
            }

            HudSphereGraphicBender graphicBender =
                childGraphic.GetComponent
                <HudSphereGraphicBender>();

            if (graphicBender == null)
            {
                graphicBender =
                    childGraphic.gameObject.AddComponent
                    <HudSphereGraphicBender>();
            }

            graphicBender.hudWindow =
                this;

            graphicBender.subdivisions =
                graphicBendSubdivisions;

            graphicBender.surfaceOffsetTowardUser =
                visualSurfaceOffsetTowardUser;

            childGraphic.SetVerticesDirty();
        }
    }


    /*
 * Keeps the window grab collider aligned with the complete
 * physical window surface.
 */
public void UpdateWindowGeometry()
{
    if (windowCanvas == null ||
        grabHandle == null ||
        grabHandleCollider == null)
    {
        return;
    }

    float width =
        WidthMeters;

    float height =
        HeightMeters;

    if (width <= 0.0f ||
        height <= 0.0f)
    {
        return;
    }

    /*
     * Place the proven GrabHandle at the center of the
     * complete window instead of only over the header.
     */
    Vector3 handlePosition =
        grabHandle.localPosition;

    handlePosition.x = 0.0f;
    handlePosition.y = 0.0f;

    grabHandle.localPosition =
        handlePosition;

    grabHandleCollider.center =
        Vector3.zero;

    grabHandleCollider.size =
        new Vector3(
            width,
            height,
            grabColliderDepthMeters
        );
}


    /*
     * Called later by the tiling system.
     *
     * A tiled window is not allowed to independently decide
     * its position or size. Its tile group owns those values.
     */
    public void SetLayoutState(
        WindowLayoutState newState)
    {
        layoutState =
            newState;
    }


    public void SetTiled(
        bool tiled)
    {
        layoutState =
            tiled
                ? WindowLayoutState.Tiled
                : WindowLayoutState.Free;
    }


    /*
     * Future resize handles call this for free windows.
     *
     * Once tiled, resize handles will instead ask the tile
     * layout manager to move the appropriate shared split.
     */
    public bool CanResizeIndependently()
    {
        return
            layoutState ==
            WindowLayoutState.Free;
    }
}
