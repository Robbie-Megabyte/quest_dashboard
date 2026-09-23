using TMPro;
using UnityEngine;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class HudButtonLabelCanvas : MonoBehaviour
{
    [Header("Text")]
    public string initialText = "MENU";

    public Color textColor = Color.black;

    [Min(1.0f)]
    public float fontSize = 28.0f;


    [Header("Canvas Size")]
    public Vector2 canvasSizePixels =
        new Vector2(110.0f, 45.0f);

    [Min(0.0001f)]
    public float canvasWorldScale = 0.001f;


    [Header("Placement")]
    [Tooltip(
        "How far the text canvas sits in front of the " +
        "physical button toward the XR camera."
    )]
    public float distanceTowardCamera = 0.012f;


    [Header("Rendering")]
    public int sortingOrder = 200;


    [Header("Automatically Resolved")]
    [SerializeField]
    private Camera xrCamera;

    [SerializeField]
    private Canvas labelCanvas;

    [SerializeField]
    private TextMeshProUGUI label;


    public TMP_Text Label
    {
        get
        {
            return label;
        }
    }


    // ========================================================
    // UNITY
    // ========================================================

    void Awake()
    {
        textColor = HudDashboardTheme.TextPrimary;
        BuildOrFindLabel();
        ApplyAppearance();
        ApplyPose();
    }


    void OnEnable()
    {
        BuildOrFindLabel();
        ApplyAppearance();
        ApplyPose();
    }


    void LateUpdate()
    {
        ApplyPose();
    }


    // ========================================================
    // BUILD
    // ========================================================

    private void BuildOrFindLabel()
    {
        if (xrCamera == null)
        {
            xrCamera =
                Camera.main;
        }


        Transform existing =
            transform.Find("LabelCanvas");


        GameObject canvasObject;


        if (existing == null)
        {
            canvasObject =
                new GameObject(
                    "LabelCanvas",
                    typeof(RectTransform),
                    typeof(Canvas)
                );


            canvasObject.transform.SetParent(
                transform,
                false
            );
        }
        else
        {
            canvasObject =
                existing.gameObject;
        }


        labelCanvas =
            canvasObject.GetComponent<Canvas>();


        labelCanvas.renderMode =
            RenderMode.WorldSpace;


        labelCanvas.worldCamera =
            xrCamera;


        labelCanvas.sortingOrder =
            sortingOrder;


        RectTransform canvasRect =
            canvasObject.GetComponent<RectTransform>();


        canvasRect.sizeDelta =
            canvasSizePixels;


        canvasRect.localScale =
            Vector3.one *
            canvasWorldScale;


        Transform existingText =
            canvasObject.transform.Find("Text");


        GameObject textObject;


        if (existingText == null)
        {
            textObject =
                new GameObject(
                    "Text",
                    typeof(RectTransform),
                    typeof(TextMeshProUGUI)
                );


            textObject.transform.SetParent(
                canvasObject.transform,
                false
            );
        }
        else
        {
            textObject =
                existingText.gameObject;
        }


        label =
            textObject.GetComponent<TextMeshProUGUI>();


        RectTransform textRect =
            textObject.GetComponent<RectTransform>();


        textRect.anchorMin =
            Vector2.zero;

        textRect.anchorMax =
            Vector2.one;

        textRect.offsetMin =
            Vector2.zero;

        textRect.offsetMax =
            Vector2.zero;

        textRect.localPosition =
            Vector3.zero;

        textRect.localRotation =
            Quaternion.identity;

        textRect.localScale =
            Vector3.one;
    }


    // ========================================================
    // RUNTIME CONFIGURATION
    // ========================================================

    public void ConfigureTypography(
        float uniformFontSize,
        float buttonWidthMeters)
    {
        fontSize =
            Mathf.Max(
                1.0f,
                uniformFontSize);

        float safeScale =
            Mathf.Max(
                0.0001f,
                canvasWorldScale);

        canvasSizePixels =
            new Vector2(
                Mathf.Max(
                    1.0f,
                    buttonWidthMeters / safeScale),
                canvasSizePixels.y);

        BuildOrFindLabel();
        ApplyAppearance();
        ApplyPose();
    }


    // ========================================================
    // APPEARANCE
    // ========================================================

    private void ApplyAppearance()
    {
        if (label == null)
            return;


        /*
         * Only set the initial text when the label has not
         * already been given runtime text by another system.
         */
        if (string.IsNullOrEmpty(label.text))
        {
            label.text =
                initialText;
        }


        label.color =
            textColor;


        label.fontSize =
            fontSize;


        label.alignment =
            TextAlignmentOptions.Center;

        label.fontStyle =
            FontStyles.Bold;


        label.enableWordWrapping =
            false;


        label.overflowMode =
            TextOverflowModes.Overflow;


        /*
         * This label is visual only.
         * The XR Simple Interactable / BoxCollider handles
         * interaction.
         */
        label.raycastTarget =
            false;


        if (labelCanvas != null)
        {
            labelCanvas.sortingOrder =
                sortingOrder;
        }
    }


    // ========================================================
    // POSE
    // ========================================================

    private void ApplyPose()
    {
        if (labelCanvas == null)
        {
            BuildOrFindLabel();
        }


        if (xrCamera == null)
        {
            xrCamera =
                Camera.main;
        }


        if (labelCanvas == null ||
            xrCamera == null)
        {
            return;
        }


        Transform canvasTransform =
            labelCanvas.transform;


        Vector3 towardCamera =
            xrCamera.transform.position
            -
            transform.position;


        if (towardCamera.sqrMagnitude <
            0.000001f)
        {
            return;
        }


        towardCamera.Normalize();


        // ----------------------------------------------------
        // POSITION
        //
        // Always physically put the label on the user's side
        // of the button, regardless of HUD curvature.
        // ----------------------------------------------------

        canvasTransform.position =
            transform.position
            +
            towardCamera *
            distanceTowardCamera;


        // ----------------------------------------------------
        // ROTATION
        //
        // Unity UI lies in the XY plane. We orient the Canvas
        // so its readable/front side faces the camera.
        // ----------------------------------------------------

        Vector3 forwardAwayFromCamera =
            -towardCamera;


        Vector3 desiredUp =
            Vector3.ProjectOnPlane(
                transform.up,
                forwardAwayFromCamera
            );


        if (desiredUp.sqrMagnitude <
            0.000001f)
        {
            desiredUp =
                xrCamera.transform.up;
        }


        desiredUp.Normalize();


        canvasTransform.rotation =
            Quaternion.LookRotation(
                forwardAwayFromCamera,
                desiredUp
            );


        /*
         * We set world position/rotation above, but preserve
         * a tiny Canvas scale relative to the button.
         */
        canvasTransform.localScale =
            Vector3.one *
            canvasWorldScale;
    }


    // ========================================================
    // PUBLIC
    // ========================================================

    public void SetText(
        string newText)
    {
        BuildOrFindLabel();


        if (label != null)
        {
            label.text =
                newText;
        }
    }
}