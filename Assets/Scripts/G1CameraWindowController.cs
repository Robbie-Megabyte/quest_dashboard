using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class G1CameraWindowController : MonoBehaviour
{
    [Header("Automatically Resolved")]
    [SerializeField] private RectTransform windowCanvas;
    [SerializeField] private RawImage originalCameraFeed;
    [SerializeField] private G1CameraUdpReceiver receiver;

    [Header("Layout")]
    [SerializeField] private float toggleBarTop = 74f;
    [SerializeField] private float toggleBarHeight = 64f;
    [SerializeField] private float viewAreaTop = 148f;
    [SerializeField] private float toggleBarToViewGap = 16f;
    [SerializeField] private float outerMargin = 12f;
    [SerializeField] private float viewGap = 8f;

    private sealed class ViewUi
    {
        public G1CameraView View;
        public Toggle Toggle;
        public RectTransform Tile;
        public RawImage Image;
    }

    private static readonly G1CameraView[] ViewOrder =
    {
        G1CameraView.Rgb,
        G1CameraView.Depth,
        G1CameraView.Overlay,
        G1CameraView.Near,
        G1CameraView.Disparity,
        G1CameraView.PointCloud,
        G1CameraView.TopDown
    };

    private static readonly string[] ButtonLabels =
    {
        "RGB",
        "DEPTH",
        "OVERLAY",
        "NEAR",
        "DISP",
        "POINT",
        "TOP"
    };

    private readonly List<ViewUi> views = new List<ViewUi>();

    private RectTransform viewArea;
    private TMP_FontAsset fontAsset;
    private Toggle yoloToggle;
    private bool built;
    private bool subscribed;

    private void Awake()
    {
        ResolveReferences();
        BuildInterface();
    }

    private void OnEnable()
    {
        ResolveReferences();
        BuildInterface();
        Subscribe();
        ApplySelection();
    }

    private void OnDisable()
    {
        if (receiver != null)
            receiver.RemoveConsumer(this);

        Unsubscribe();
    }

    private void OnDestroy()
    {
        if (receiver != null)
            receiver.RemoveConsumer(this);

        Unsubscribe();
    }

    private void ResolveReferences()
    {
        if (windowCanvas == null)
        {
            Transform found = transform.Find("WindowCanvas");

            if (found != null)
                windowCanvas = found as RectTransform;
        }

        if (originalCameraFeed == null &&
            windowCanvas != null)
        {
            Transform found =
                windowCanvas.Find("CameraFeed");

            if (found != null)
                originalCameraFeed =
                    found.GetComponent<RawImage>();
        }

        if (receiver == null)
        {
            receiver =
                G1CameraUdpReceiver.Instance;

            if (receiver == null)
            {
                receiver =
                    Object.FindAnyObjectByType
                        <G1CameraUdpReceiver>();
            }
        }

        if (fontAsset == null &&
            windowCanvas != null)
        {
            TMP_Text existingText =
                windowCanvas.GetComponentInChildren
                    <TMP_Text>(true);

            if (existingText != null)
                fontAsset = existingText.font;
        }
    }

    private void BuildInterface()
    {
        if (built)
            return;

        if (windowCanvas == null ||
            originalCameraFeed == null)
        {
            Debug.LogError(
                "[G1 Camera Window] WindowCanvas or " +
                "CameraFeed could not be found.");
            return;
        }

        float effectiveToggleBarHeight =
            Mathf.Max(
                64f,
                toggleBarHeight);

        float effectiveViewAreaTop =
            Mathf.Max(
                viewAreaTop,
                toggleBarTop +
                effectiveToggleBarHeight +
                Mathf.Max(0f, toggleBarToViewGap));

        viewArea = CreateRect(
            "CameraViewArea_Runtime",
            windowCanvas);

        viewArea.anchorMin = Vector2.zero;
        viewArea.anchorMax = Vector2.one;
        viewArea.offsetMin =
            new Vector2(outerMargin, outerMargin);
        viewArea.offsetMax =
            new Vector2(
                -outerMargin,
                -effectiveViewAreaTop);

        RectTransform toggleBar = CreateRect(
            "CameraToggleBar_Runtime",
            windowCanvas);

        toggleBar.anchorMin = new Vector2(0f, 1f);
        toggleBar.anchorMax = new Vector2(1f, 1f);
        toggleBar.pivot = new Vector2(0.5f, 1f);
        toggleBar.anchoredPosition =
            new Vector2(0f, -toggleBarTop);
        toggleBar.sizeDelta =
            new Vector2(
                -2f * outerMargin,
                effectiveToggleBarHeight);

        HorizontalLayoutGroup layout =
            toggleBar.gameObject.AddComponent
                <HorizontalLayoutGroup>();

        layout.padding = new RectOffset(2, 2, 2, 2);
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        for (int i = 0; i < ViewOrder.Length; i++)
        {
            G1CameraView view = ViewOrder[i];

            RectTransform tile =
                CreateTile(view);

            RawImage image;

            if (view == G1CameraView.Rgb)
            {
                image = originalCameraFeed;
                image.transform.SetParent(tile, false);
            }
            else
            {
                image = CreateRawImage(
                    "Image_" + view,
                    tile);
            }

            ConfigureImage(image);

            Toggle toggle =
                CreateToggle(
                    ButtonLabels[i],
                    toggleBar);

            toggle.SetIsOnWithoutNotify(
                view == G1CameraView.Rgb);

            toggle.onValueChanged.AddListener(
                _ => ApplySelection());

            views.Add(
                new ViewUi
                {
                    View = view,
                    Toggle = toggle,
                    Tile = tile,
                    Image = image
                });
        }

        yoloToggle = CreateToggle(
            "YOLO",
            toggleBar);

        yoloToggle.SetIsOnWithoutNotify(
            receiver != null &&
            receiver.YoloRequested);

        yoloToggle.onValueChanged.AddListener(
            HandleYoloToggleChanged);

        built = true;
        LayoutSelectedViews();

        // The camera controls and view tiles are generated at runtime,
        // after HudWindow may already have scanned its original graphics.
        // Enrol them in the exact same spherical surface as the window.
        Canvas.ForceUpdateCanvases();

        HudWindow hudWindow =
            GetComponent<HudWindow>();

        if (hudWindow != null)
            hudWindow.RefreshCurvedVisuals();
    }

    private RectTransform CreateTile(
        G1CameraView view)
    {
        GameObject tileObject = new GameObject(
            "Tile_" + view,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(RectMask2D));

        tileObject.transform.SetParent(viewArea, false);

        Image background =
            tileObject.GetComponent<Image>();

        background.color =
            new Color(0f, 0f, 0f, 0.85f);
        background.raycastTarget = false;

        return tileObject.GetComponent<RectTransform>();
    }

    private static RawImage CreateRawImage(
        string objectName,
        Transform parent)
    {
        GameObject imageObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage));

        imageObject.transform.SetParent(parent, false);

        return imageObject.GetComponent<RawImage>();
    }

    private static void ConfigureImage(
    RawImage image)
    {
        /*
        * The image mesh must remain exactly inside its tile.
        *
        * AspectRatioFitter.EnvelopeParent expands the mesh beyond
        * the tile. A flat RectMask2D cannot correctly clip that
        * expanded mesh after HudSphereGraphicBender curves it.
        */
        AspectRatioFitter oldAspect =
            image.GetComponent<AspectRatioFitter>();

        if (oldAspect != null)
            oldAspect.enabled = false;

        RectTransform rect =
            image.rectTransform;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;

        image.color = Color.white;
        image.raycastTarget = false;
        image.uvRect =
            new Rect(0f, 0f, 1f, 1f);
    }

    private Toggle CreateToggle(
        string labelText,
        Transform parent)
    {
        GameObject targetObject = new GameObject(
            "CameraToggleTarget_" + labelText,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Toggle),
            typeof(LayoutElement));

        targetObject.transform.SetParent(parent, false);

        Image hitTarget =
            targetObject.GetComponent<Image>();

        hitTarget.color =
            new Color(0f, 0f, 0f, 0.01f);
        hitTarget.raycastTarget = true;

        LayoutElement element =
            targetObject.GetComponent<LayoutElement>();

        // The transparent outer Toggle is intentionally larger
        // than its visible button, giving hand rays/pokes a forgiving
        // snap target without visually crowding adjacent controls.
        element.minWidth = 64f;
        element.preferredWidth = 86f;
        element.minHeight = 56f;
        element.preferredHeight = 60f;

        RectTransform visual = CreateRect(
            "Visual",
            targetObject.transform);

        visual.anchorMin = Vector2.zero;
        visual.anchorMax = Vector2.one;
        visual.offsetMin = new Vector2(4f, 6f);
        visual.offsetMax = new Vector2(-4f, -6f);

        Image background =
            visual.gameObject.AddComponent<Image>();

        background.color =
            new Color(0.08f, 0.10f, 0.13f, 0.96f);
        background.raycastTarget = false;

        RectTransform checkmark = CreateRect(
            "Selected",
            visual);

        checkmark.anchorMin = Vector2.zero;
        checkmark.anchorMax = Vector2.one;
        checkmark.offsetMin = new Vector2(2f, 2f);
        checkmark.offsetMax = new Vector2(-2f, -2f);

        Image selectedImage =
            checkmark.gameObject.AddComponent<Image>();

        selectedImage.color =
            new Color(0f, 0.65f, 1f, 0.75f);
        selectedImage.raycastTarget = false;

        RectTransform labelRect = CreateRect(
            "Label",
            visual);

        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label =
            labelRect.gameObject.AddComponent
                <TextMeshProUGUI>();

        label.text = labelText;
        label.fontSize = 18f;
        label.fontStyle = FontStyles.Bold;
        label.alignment =
            TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        label.overflowMode =
            TextOverflowModes.Ellipsis;
        label.textWrappingMode =
            TextWrappingModes.NoWrap;

        if (fontAsset != null)
            label.font = fontAsset;

        Toggle toggle =
            targetObject.GetComponent<Toggle>();

        toggle.targetGraphic = background;
        toggle.graphic = selectedImage;

        ColorBlock colors = toggle.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor =
            new Color(0.80f, 0.92f, 1f, 1f);
        colors.pressedColor =
            new Color(0.45f, 0.75f, 1f, 1f);
        colors.selectedColor = Color.white;
        toggle.colors = colors;

        return toggle;
    }

    private static RectTransform CreateRect(
        string objectName,
        Transform parent)
    {
        GameObject obj = new GameObject(
            objectName,
            typeof(RectTransform));

        obj.transform.SetParent(parent, false);

        return obj.GetComponent<RectTransform>();
    }

    private void Subscribe()
    {
        if (subscribed)
            return;

        ResolveReferences();

        if (receiver == null)
        {
            Debug.LogError(
                "[G1 Camera Window] Global receiver missing.");
            return;
        }

        receiver.ViewTextureUpdated +=
            HandleTextureUpdated;

        receiver.YoloRequestChanged +=
            HandleYoloRequestChanged;

        if (yoloToggle != null)
        {
            yoloToggle.SetIsOnWithoutNotify(
                receiver.YoloRequested);
        }

        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || receiver == null)
            return;

        receiver.ViewTextureUpdated -=
            HandleTextureUpdated;

        receiver.YoloRequestChanged -=
            HandleYoloRequestChanged;

        subscribed = false;
    }

    private void HandleYoloToggleChanged(
        bool requested)
    {
        if (receiver == null)
            ResolveReferences();

        if (receiver != null)
        {
            receiver.SetYoloRequested(
                requested);
        }
    }

    private void HandleYoloRequestChanged(
        bool requested)
    {
        if (yoloToggle != null)
        {
            yoloToggle.SetIsOnWithoutNotify(
                requested);
        }
    }

    private void ApplySelection()
    {
        if (!built)
            return;

        ushort mask = 0;

        foreach (ViewUi view in views)
        {
            bool selected =
                view.Toggle != null &&
                view.Toggle.isOn;

            view.Tile.gameObject.SetActive(selected);

            if (!selected)
                continue;

            mask |= G1CameraViewInfo.Bit(view.View);

            if (receiver != null)
            {
                Texture2D texture =
                    receiver.GetTexture(view.View);

                if (texture != null)
                    ApplyTexture(view, texture);
            }
        }

        LayoutSelectedViews();

        if (isActiveAndEnabled &&
            receiver != null)
        {
            receiver.SetConsumerMask(this, mask);
        }
    }

    private void LayoutSelectedViews()
    {
        var active =
            new List<ViewUi>();

        foreach (ViewUi view in views)
        {
            if (view.Toggle != null &&
                view.Toggle.isOn)
            {
                active.Add(view);
            }
        }

        int count = active.Count;

        if (count == 0)
            return;

        int columns =
            count == 1
                ? 1
                : count <= 4
                    ? 2
                    : 3;

        int rows =
            Mathf.CeilToInt(
                (float)count / columns);

        for (int i = 0; i < count; i++)
        {
            int column = i % columns;
            int row = i / columns;

            float minX =
                (float)column / columns;
            float maxX =
                (float)(column + 1) / columns;

            float maxY =
                1f - (float)row / rows;
            float minY =
                1f - (float)(row + 1) / rows;

            RectTransform tile =
                active[i].Tile;

            tile.anchorMin =
                new Vector2(minX, minY);
            tile.anchorMax =
                new Vector2(maxX, maxY);
            tile.offsetMin =
                new Vector2(
                    viewGap * 0.5f,
                    viewGap * 0.5f);
            tile.offsetMax =
                new Vector2(
                    -viewGap * 0.5f,
                    -viewGap * 0.5f);
        }
    }

    private void HandleTextureUpdated(
        G1CameraView view,
        Texture2D texture)
    {
        foreach (ViewUi candidate in views)
        {
            if (candidate.View == view)
            {
                ApplyTexture(candidate, texture);
                return;
            }
        }
    }

    private static void ApplyTexture(
        ViewUi view,
        Texture2D texture)
    {
        if (view.Image == null ||
            texture == null)
        {
            return;
        }

        view.Image.texture = texture;

        ApplyAspectFillCrop(
            view.Image,
            texture);
    }


    private static void ApplyAspectFillCrop(
        RawImage image,
        Texture texture)
    {
        if (image == null ||
            texture == null ||
            texture.width <= 0 ||
            texture.height <= 0)
        {
            return;
        }

        Rect imageRect =
            image.rectTransform.rect;

        if (imageRect.width <= 0.01f ||
            imageRect.height <= 0.01f)
        {
            image.uvRect =
                new Rect(0f, 0f, 1f, 1f);

            return;
        }

        float viewportAspect =
            imageRect.width /
            imageRect.height;

        float textureAspect =
            (float)texture.width /
            texture.height;

        if (textureAspect > viewportAspect)
        {
            /*
            * The texture is wider than the viewport.
            * Crop equal amounts from its left and right sides.
            */
            float visibleWidth =
                viewportAspect /
                textureAspect;

            float horizontalOffset =
                (1f - visibleWidth) *
                0.5f;

            image.uvRect =
                new Rect(
                    horizontalOffset,
                    0f,
                    visibleWidth,
                    1f);
        }
        else
        {
            /*
            * The texture is taller than the viewport.
            * Crop equal amounts from its top and bottom.
            */
            float visibleHeight =
                textureAspect /
                viewportAspect;

            float verticalOffset =
                (1f - visibleHeight) *
                0.5f;

            image.uvRect =
                new Rect(
                    0f,
                    verticalOffset,
                    1f,
                    visibleHeight);
        }
    }
}
