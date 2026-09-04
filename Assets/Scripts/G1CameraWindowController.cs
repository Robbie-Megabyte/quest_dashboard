using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class G1CameraWindowController :
    MonoBehaviour
{
    [Header("Automatically Resolved")]
    [SerializeField]
    private RectTransform windowCanvas;

    [SerializeField]
    private RawImage originalCameraFeed;

    [SerializeField]
    private G1CameraUdpReceiver receiver;

    [Header("Main View")]
    [SerializeField]
    private float mainViewTop = 74f;

    [Header("Hotbar")]
    [SerializeField]
    private float hotbarHeight = 104f;

    [SerializeField]
    private float hotbarBottom = 12f;

    [SerializeField]
    private float mainToHotbarGap = 10f;

    [SerializeField]
    private float outerMargin = 12f;

    [SerializeField]
    private float thumbnailGap = 7f;

    private sealed class ViewUi
    {
        public G1CameraView View;
        public RawImage MainImage;
        public RawImage ThumbnailImage;
        public Toggle Selector;
        public G1CameraPointCloudView PointCloudView;
    }

    private static readonly G1CameraView[] ViewOrder =
    {
        G1CameraView.Rgb,
        G1CameraView.Depth,
        G1CameraView.Overlay,
        G1CameraView.Disparity,
        G1CameraView.PointCloud,
        G1CameraView.LifeCam
    };

    private readonly List<ViewUi> views =
        new List<ViewUi>();

    private RectTransform mainViewport;
    private RectTransform hotbar;
    private ToggleGroup cameraToggleGroup;
    private Toggle yoloToggle;
    private TMP_FontAsset fontAsset;

    private G1CameraView selectedMainView =
        G1CameraView.Rgb;

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
        SelectMainView(selectedMainView);
        RequestAllViews();
    }

    private void OnDisable()
    {
        if (receiver != null)
            receiver.RemoveConsumer(this);

        Unsubscribe();

        foreach (ViewUi view in views)
        {
            if (view.PointCloudView != null)
                view.PointCloudView.SetMainView(false);
        }
    }

    private void OnDestroy()
    {
        if (receiver != null)
            receiver.RemoveConsumer(this);

        Unsubscribe();
    }

    private void Update()
    {
        /*
         * Point Cloud is rendered locally into a RenderTexture.
         * Keep its hotbar thumbnail attached if that texture is
         * recreated when switching between thumbnail/main quality.
         */
        foreach (ViewUi view in views)
        {
            if (view.PointCloudView == null ||
                view.ThumbnailImage == null)
            {
                continue;
            }

            Texture output =
                view.PointCloudView.OutputTexture;

            if (output != null &&
                view.ThumbnailImage.texture != output)
            {
                view.ThumbnailImage.texture = output;

                ApplyAspectFillCrop(
                    view.ThumbnailImage,
                    output);
            }
        }
    }

    private void ResolveReferences()
    {
        if (windowCanvas == null)
        {
            Transform found =
                transform.Find("WindowCanvas");

            if (found != null)
                windowCanvas = found as RectTransform;
        }

        if (originalCameraFeed == null &&
            windowCanvas != null)
        {
            Transform found =
                windowCanvas.Find("CameraFeed");

            if (found != null)
            {
                originalCameraFeed =
                    found.GetComponent<RawImage>();
            }
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

        mainViewport = CreateRectWithBackground(
            "CameraMainViewport_Runtime",
            windowCanvas,
            new Color(0f, 0f, 0f, 0.92f),
            true);

        mainViewport.anchorMin = Vector2.zero;
        mainViewport.anchorMax = Vector2.one;

        mainViewport.offsetMin =
            new Vector2(
                outerMargin,
                hotbarBottom +
                hotbarHeight +
                mainToHotbarGap);

        mainViewport.offsetMax =
            new Vector2(
                -outerMargin,
                -mainViewTop);

        hotbar = CreateRect(
            "CameraHotbar_Runtime",
            windowCanvas);

        hotbar.anchorMin =
            new Vector2(0f, 0f);

        hotbar.anchorMax =
            new Vector2(1f, 0f);

        hotbar.pivot =
            new Vector2(0.5f, 0f);

        hotbar.anchoredPosition =
            new Vector2(0f, hotbarBottom);

        hotbar.sizeDelta =
            new Vector2(
                -2f * outerMargin,
                hotbarHeight);

        HorizontalLayoutGroup layout =
            hotbar.gameObject.AddComponent
                <HorizontalLayoutGroup>();

        layout.padding =
            new RectOffset(0, 0, 0, 0);

        layout.spacing = thumbnailGap;

        layout.childAlignment =
            TextAnchor.MiddleCenter;

        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        cameraToggleGroup =
            hotbar.gameObject.AddComponent
                <ToggleGroup>();

        cameraToggleGroup.allowSwitchOff = false;

        for (int index = 0;
             index < ViewOrder.Length;
             index++)
        {
            G1CameraView view = ViewOrder[index];

            RawImage mainImage;

            if (view == G1CameraView.Rgb)
            {
                mainImage = originalCameraFeed;

                mainImage.transform.SetParent(
                    mainViewport,
                    false);
            }
            else
            {
                mainImage = CreateRawImage(
                    "Main_" + view,
                    mainViewport);
            }

            ConfigureImage(mainImage);

            G1CameraPointCloudView pointCloudView =
                null;

            if (view == G1CameraView.PointCloud)
            {
                pointCloudView =
                    mainImage.gameObject.AddComponent
                        <G1CameraPointCloudView>();

                pointCloudView.Configure(
                    receiver,
                    GetComponentInParent
                        <HudWindow>(true));
            }

            CreateThumbnailSelector(
                view,
                out Toggle selector,
                out RawImage thumbnailImage);

            selector.group = cameraToggleGroup;

            selector.SetIsOnWithoutNotify(
                view == selectedMainView);

            G1CameraView capturedView = view;

            selector.onValueChanged.AddListener(
                enabled =>
                {
                    if (enabled)
                    {
                        SelectMainView(
                            capturedView);
                    }
                });

            views.Add(
                new ViewUi
                {
                    View = view,
                    MainImage = mainImage,
                    ThumbnailImage = thumbnailImage,
                    Selector = selector,
                    PointCloudView = pointCloudView
                });
        }

        yoloToggle =
            CreateYoloToggle(hotbar);

        yoloToggle.SetIsOnWithoutNotify(
            receiver != null &&
            receiver.YoloRequested);

        yoloToggle.onValueChanged.AddListener(
            HandleYoloToggleChanged);

        built = true;

        SelectMainView(
            selectedMainView);

        Canvas.ForceUpdateCanvases();

        HudWindow hudWindow =
            GetComponent<HudWindow>();

        if (hudWindow != null)
            hudWindow.RefreshCurvedVisuals();
    }

    private void CreateThumbnailSelector(
        G1CameraView view,
        out Toggle toggle,
        out RawImage thumbnail)
    {
        GameObject targetObject =
            new GameObject(
                "CameraHotbar_" + view,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Toggle),
                typeof(LayoutElement));

        targetObject.transform.SetParent(
            hotbar,
            false);

        Image targetImage =
            targetObject.GetComponent<Image>();

        targetImage.color = Color.clear;
        targetImage.raycastTarget = false;

        LayoutElement element =
            targetObject.GetComponent
                <LayoutElement>();

        element.minWidth = 58f;
        element.preferredWidth = 92f;
        element.flexibleWidth = 1f;
        element.minHeight = 72f;
        element.preferredHeight = hotbarHeight;
        element.flexibleHeight = 1f;

        RectTransform visual =
            CreateRectWithBackground(
                "Visual",
                targetObject.transform,
                new Color(
                    0.035f,
                    0.055f,
                    0.075f,
                    1f),
                false);

        visual.anchorMin = Vector2.zero;
        visual.anchorMax = Vector2.one;
        visual.offsetMin = new Vector2(2f, 2f);
        visual.offsetMax = new Vector2(-2f, -2f);

        thumbnail = CreateRawImage(
            "LivePreview",
            visual);

        ConfigureImage(thumbnail);

        thumbnail.rectTransform.offsetMin =
            new Vector2(3f, 3f);

        thumbnail.rectTransform.offsetMax =
            new Vector2(-3f, -3f);

        RectTransform selectedRect =
            CreateRect(
                "Selected",
                visual);

        selectedRect.anchorMin = Vector2.zero;
        selectedRect.anchorMax = Vector2.one;
        selectedRect.offsetMin = Vector2.zero;
        selectedRect.offsetMax = Vector2.zero;

        Image selectedImage =
            selectedRect.gameObject.AddComponent
                <Image>();

        selectedImage.color =
            new Color(
                0f,
                0.68f,
                1f,
                0.28f);

        selectedImage.raycastTarget = false;

        toggle =
            targetObject.GetComponent<Toggle>();

        toggle.targetGraphic =
            visual.GetComponent<Image>();

        toggle.graphic = selectedImage;

        ColorBlock colors = toggle.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor =
            new Color(0.82f, 0.94f, 1f, 1f);
        colors.pressedColor =
            new Color(0.45f, 0.78f, 1f, 1f);
        colors.selectedColor = Color.white;
        toggle.colors = colors;

        HudCurvedToggleHitTarget hitTarget =
            targetObject.AddComponent
                <HudCurvedToggleHitTarget>();

        hitTarget.Configure(
            GetComponentInParent
                <HudWindow>(true),
            toggle);
    }

    private Toggle CreateYoloToggle(
        Transform parent)
    {
        GameObject targetObject =
            new GameObject(
                "CameraHotbar_YOLO",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Toggle),
                typeof(LayoutElement));

        targetObject.transform.SetParent(
            parent,
            false);

        Image targetImage =
            targetObject.GetComponent<Image>();

        targetImage.color = Color.clear;
        targetImage.raycastTarget = false;

        LayoutElement element =
            targetObject.GetComponent
                <LayoutElement>();

        element.minWidth = 66f;
        element.preferredWidth = 74f;
        element.flexibleWidth = 0f;
        element.minHeight = 72f;
        element.preferredHeight = hotbarHeight;

        RectTransform visual =
            CreateRectWithBackground(
                "Visual",
                targetObject.transform,
                new Color(
                    0.055f,
                    0.075f,
                    0.095f,
                    1f),
                false);

        visual.anchorMin = Vector2.zero;
        visual.anchorMax = Vector2.one;
        visual.offsetMin = new Vector2(2f, 2f);
        visual.offsetMax = new Vector2(-2f, -2f);

        RectTransform selectedRect =
            CreateRect(
                "Selected",
                visual);

        selectedRect.anchorMin = Vector2.zero;
        selectedRect.anchorMax = Vector2.one;
        selectedRect.offsetMin = Vector2.zero;
        selectedRect.offsetMax = Vector2.zero;

        Image selectedImage =
            selectedRect.gameObject.AddComponent
                <Image>();

        selectedImage.color =
            new Color(
                0f,
                0.68f,
                1f,
                0.30f);

        selectedImage.raycastTarget = false;

        RectTransform labelRect =
            CreateRect("Label", visual);

        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label =
            labelRect.gameObject.AddComponent
                <TextMeshProUGUI>();

        label.text = "YOLO";
        label.fontSize = 15f;
        label.fontStyle = FontStyles.Bold;
        label.alignment =
            TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        label.textWrappingMode =
            TextWrappingModes.NoWrap;

        if (fontAsset != null)
            label.font = fontAsset;

        Toggle toggle =
            targetObject.GetComponent<Toggle>();

        toggle.targetGraphic =
            visual.GetComponent<Image>();

        toggle.graphic = selectedImage;

        ColorBlock colors = toggle.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor =
            new Color(0.82f, 0.94f, 1f, 1f);
        colors.pressedColor =
            new Color(0.45f, 0.78f, 1f, 1f);
        colors.selectedColor = Color.white;
        toggle.colors = colors;

        HudCurvedToggleHitTarget hitTarget =
            targetObject.AddComponent
                <HudCurvedToggleHitTarget>();

        hitTarget.Configure(
            GetComponentInParent
                <HudWindow>(true),
            toggle);

        return toggle;
    }

    private void SelectMainView(
        G1CameraView selected)
    {
        selectedMainView = selected;

        foreach (ViewUi view in views)
        {
            bool isSelected =
                view.View == selected;

            if (view.Selector != null)
            {
                view.Selector.SetIsOnWithoutNotify(
                    isSelected);
            }

            if (view.MainImage != null)
            {
                view.MainImage.enabled =
                    isSelected;

                if (view.PointCloudView == null)
                {
                    view.MainImage.raycastTarget =
                        false;
                }
            }

            if (view.PointCloudView != null)
            {
                view.PointCloudView.SetMainView(
                    isSelected);
            }

            if (receiver != null &&
                view.View !=
                    G1CameraView.PointCloud)
            {
                Texture2D texture =
                    receiver.GetTexture(view.View);

                if (texture != null)
                    ApplyTexture(view, texture);
            }
        }

        Canvas.ForceUpdateCanvases();

        foreach (ViewUi view in views)
        {
            if (view.MainImage != null &&
                view.MainImage.texture != null)
            {
                ApplyAspectFillCrop(
                    view.MainImage,
                    view.MainImage.texture);
            }

            if (view.ThumbnailImage != null &&
                view.ThumbnailImage.texture != null)
            {
                ApplyAspectFillCrop(
                    view.ThumbnailImage,
                    view.ThumbnailImage.texture);
            }
        }
    }

    private void RequestAllViews()
    {
        if (!isActiveAndEnabled ||
            receiver == null)
        {
            return;
        }

        ushort mask = 0;

        foreach (G1CameraView view in ViewOrder)
            mask |= G1CameraViewInfo.Bit(view);

        receiver.SetConsumerMask(this, mask);
    }

    private void Subscribe()
    {
        if (subscribed)
            return;

        ResolveReferences();

        if (receiver == null)
        {
            Debug.LogError(
                "[G1 Camera Window] " +
                "Global receiver missing.");
            return;
        }

        foreach (ViewUi view in views)
        {
            if (view.PointCloudView != null)
            {
                view.PointCloudView.Configure(
                    receiver,
                    GetComponentInParent
                        <HudWindow>(true));
            }
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
        if (!subscribed ||
            receiver == null)
        {
            return;
        }

        receiver.ViewTextureUpdated -=
            HandleTextureUpdated;

        receiver.YoloRequestChanged -=
            HandleYoloRequestChanged;

        subscribed = false;
    }

    private void HandleTextureUpdated(
        G1CameraView view,
        Texture2D texture)
    {
        foreach (ViewUi candidate in views)
        {
            if (candidate.View != view)
                continue;

            ApplyTexture(candidate, texture);
            return;
        }
    }

    private void ApplyTexture(
        ViewUi view,
        Texture texture)
    {
        if (texture == null)
            return;

        if (view.MainImage != null)
        {
            view.MainImage.texture = texture;

            ApplyAspectFillCrop(
                view.MainImage,
                texture);
        }

        if (view.ThumbnailImage != null)
        {
            view.ThumbnailImage.texture = texture;

            ApplyAspectFillCrop(
                view.ThumbnailImage,
                texture);
        }
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

    private static RectTransform
        CreateRectWithBackground(
            string objectName,
            Transform parent,
            Color color,
            bool addMask)
    {
        GameObject obj =
            new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

        obj.transform.SetParent(parent, false);

        Image image = obj.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        if (addMask)
            obj.AddComponent<RectMask2D>();

        return obj.GetComponent<RectTransform>();
    }

    private static RectTransform CreateRect(
        string objectName,
        Transform parent)
    {
        GameObject obj =
            new GameObject(
                objectName,
                typeof(RectTransform));

        obj.transform.SetParent(parent, false);

        return obj.GetComponent<RectTransform>();
    }

    private static RawImage CreateRawImage(
        string objectName,
        Transform parent)
    {
        GameObject obj =
            new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage));

        obj.transform.SetParent(parent, false);

        return obj.GetComponent<RawImage>();
    }

    private static void ConfigureImage(
        RawImage image)
    {
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
            float visibleWidth =
                viewportAspect /
                textureAspect;

            image.uvRect =
                new Rect(
                    (1f - visibleWidth) * 0.5f,
                    0f,
                    visibleWidth,
                    1f);
        }
        else
        {
            float visibleHeight =
                textureAspect /
                viewportAspect;

            image.uvRect =
                new Rect(
                    0f,
                    (1f - visibleHeight) * 0.5f,
                    1f,
                    visibleHeight);
        }
    }
}
