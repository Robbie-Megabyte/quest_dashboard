using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DisallowMultipleComponent]
public sealed class G1QuestTeleopUiController :
    MonoBehaviour
{
    [Header("References")]

    [SerializeField]
    private G1QuestTeleopModeCoordinator coordinator;

    [SerializeField]
    private HudGridManager gridManager;

    [SerializeField]
    private HudWindowPaletteController paletteController;

    [SerializeField]
    private HudModeManager modeManager;

    [Header("Placement")]

    [Tooltip(
        "Angular distance above the HUD grid to the " +
        "center of the teleop button."
    )]
    [FormerlySerializedAs("buttonGapBelowGridDegrees")]
    [SerializeField]
    private float buttonGapAboveGridDegrees = 3f;

    [SerializeField]
    private float panelYawDegrees;

    [SerializeField]
    private float panelPitchDegrees = -5f;

    [Header("Colors")]

    [SerializeField]
    private Color lockedColor =
        new Color(0.32f, 0.34f, 0.38f, 1f);

    [SerializeField]
    private Color readyColor =
        new Color(0.12f, 0.65f, 0.32f, 1f);

    [SerializeField]
    private Color transitionColor =
        new Color(0.92f, 0.62f, 0.10f, 1f);

    [SerializeField]
    private Color activeColor =
        new Color(0.82f, 0.12f, 0.12f, 1f);

    private GameObject topButtonRoot;
    private XRSimpleInteractable topInteractable;
    private HudButtonLabelCanvas topLabel;
    private Renderer topRenderer;

    private GameObject panelRoot;
    private TMP_Text panelTitle;
    private TMP_Text panelDetail;
    private TMP_Text panelXr;
    private Button enterButton;
    private TMP_Text enterLabel;
    private HudCurvedButtonHitTarget enterHold;
    private Button cancelButton;
    private TMP_Text cancelLabel;

    private bool topSubscribed;
    private bool panelWasOpen;

    private void Awake()
    {
        lockedColor = HudDashboardTheme.ControlHover;
        readyColor = HudDashboardTheme.Green;
        transitionColor = HudDashboardTheme.Amber;
        activeColor = HudDashboardTheme.Red;

        ResolveReferences();
    }

    private void Start()
    {
        ResolveReferences();
        BuildTopButton();
        BuildPanel();
        RefreshUi();
    }

    private void Update()
    {
        if (topButtonRoot == null)
            BuildTopButton();

        if (panelRoot == null)
            BuildPanel();

        RefreshUi();
    }

    private void OnDisable()
    {
        UnsubscribeTopButton();

        if (topButtonRoot != null)
            topButtonRoot.SetActive(false);

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        if (topButtonRoot != null)
        {
            topButtonRoot.SetActive(true);
            SubscribeTopButton();
        }
    }

    private void OnDestroy()
    {
        UnsubscribeTopButton();

        if (topButtonRoot != null)
            Destroy(topButtonRoot);

        if (panelRoot != null)
            Destroy(panelRoot);
    }

    private void ResolveReferences()
    {
        if (coordinator == null)
        {
            coordinator =
                GetComponent<
                    G1QuestTeleopModeCoordinator>();
        }

        if (coordinator == null)
        {
            coordinator =
                Object.FindAnyObjectByType<
                    G1QuestTeleopModeCoordinator>();
        }

        if (gridManager == null)
        {
            gridManager =
                Object.FindAnyObjectByType<
                    HudGridManager>();
        }

        if (paletteController == null)
        {
            paletteController =
                Object.FindAnyObjectByType<
                    HudWindowPaletteController>();
        }

        if (modeManager == null)
        {
            modeManager =
                Object.FindAnyObjectByType<
                    HudModeManager>();
        }
    }

    private void BuildTopButton()
    {
        ResolveReferences();

        if (topButtonRoot != null ||
            gridManager == null)
        {
            return;
        }

        Transform template =
            gridManager.transform.Find(
                "HUD_BottomControls/ModeButton");

        if (template == null)
        {
            Debug.LogError(
                "[G1 Teleop UI] HUD mode-button " +
                "template was not found.",
                this);

            return;
        }

        topButtonRoot =
            Instantiate(
                template.gameObject,
                gridManager.transform,
                false);

        topButtonRoot.name =
            "HUD_TeleopModeButton";

        topButtonRoot.SetActive(true);

        /*
         * The cloned Mode button contains a persistent
         * ToggleMode listener. Runtime RemoveAllListeners
         * cannot remove serialized persistent listeners.
         *
         * Disable the copied interactable and create a clean
         * child interaction target instead.
         */
        XRSimpleInteractable copiedInteractable =
            topButtonRoot.GetComponent<
                XRSimpleInteractable>();

        if (copiedInteractable != null)
            copiedInteractable.enabled = false;

        BoxCollider copiedCollider =
            topButtonRoot.GetComponent<
                BoxCollider>();

        if (copiedCollider != null)
            copiedCollider.enabled = false;

        GameObject interactionTarget =
            new GameObject(
                "TeleopInteractionTarget",
                typeof(BoxCollider),
                typeof(XRSimpleInteractable));

        interactionTarget.layer =
            topButtonRoot.layer;

        interactionTarget.transform.SetParent(
            topButtonRoot.transform,
            false);

        BoxCollider hitCollider =
            interactionTarget.GetComponent<
                BoxCollider>();

        hitCollider.size =
            new Vector3(
                0.34f,
                0.085f,
                0.024f);

        hitCollider.center = Vector3.zero;
        hitCollider.isTrigger = false;

        topInteractable =
            interactionTarget.GetComponent<
                XRSimpleInteractable>();

        topInteractable.colliders.Clear();
        topInteractable.colliders.Add(
            hitCollider);

        topLabel =
            topButtonRoot.GetComponent<
                HudButtonLabelCanvas>();

        if (topLabel != null)
        {
            topLabel.initialText =
                "TELEOP";

            topLabel.ConfigureTypography(
                28f,
                0.34f);
        }

        Transform visual =
            topButtonRoot.transform.Find(
                "ButtonVisual");

        if (visual != null)
        {
            visual.localScale =
                new Vector3(
                    0.34f,
                    0.085f,
                    0.016f);

            topRenderer =
                visual.GetComponent<Renderer>();
        }

        Transform snap =
            topButtonRoot.transform.Find(
                "SnapVolume");

        if (snap != null)
        {
            BoxCollider snapCollider =
                snap.GetComponent<BoxCollider>();

            if (snapCollider != null)
            {
                snapCollider.size =
                    new Vector3(
                        0.42f,
                        0.13f,
                        0.06f);

                snapCollider.isTrigger = true;
            }

            XRInteractableSnapVolume snapVolume =
                snap.GetComponent<
                    XRInteractableSnapVolume>();

            if (snapVolume != null)
            {
                snapVolume.interactable =
                    topInteractable;

                snapVolume.snapCollider =
                    snapCollider;

                snapVolume.snapToCollider =
                    hitCollider;
            }
        }

        HudTopControlsAnchor anchor =
            topButtonRoot.GetComponent<
                HudTopControlsAnchor>();

        if (anchor == null)
        {
            anchor =
                topButtonRoot.AddComponent<
                    HudTopControlsAnchor>();
        }

        anchor.Configure(
            gridManager,
            buttonGapAboveGridDegrees,
            0.03f);

        SubscribeTopButton();
    }

    private void SubscribeTopButton()
    {
        if (topSubscribed ||
            topInteractable == null)
        {
            return;
        }

        topInteractable.selectEntered.AddListener(
            HandleTopSelected);

        topSubscribed = true;
    }

    private void UnsubscribeTopButton()
    {
        if (!topSubscribed ||
            topInteractable == null)
        {
            return;
        }

        topInteractable.selectEntered.RemoveListener(
            HandleTopSelected);

        topSubscribed = false;
    }

    private void HandleTopSelected(
        SelectEnterEventArgs eventArguments)
    {
        if (coordinator == null)
            return;

        coordinator.HandleTopButtonPressed();
    }

    private void BuildPanel()
    {
        ResolveReferences();

        if (panelRoot != null ||
            gridManager == null)
        {
            return;
        }

        Transform windowLayer =
            gridManager.transform.Find(
                "HUD_WindowLayer");

        if (windowLayer == null)
        {
            Debug.LogError(
                "[G1 Teleop UI] HUD_WindowLayer " +
                "was not found.",
                this);

            return;
        }

        Transform paletteTemplate =
            windowLayer.Find(
                "HUD_WindowPalette");

        if (paletteTemplate == null)
        {
            Debug.LogError(
                "[G1 Teleop UI] HUD_WindowPalette " +
                "template was not found.",
                this);

            return;
        }

        panelRoot =
            Instantiate(
                paletteTemplate.gameObject,
                windowLayer,
                false);

        panelRoot.name =
            "HUD_TeleopModePanel";

        DisableCopiedContent("PaletteItems");
        DisableCopiedContent("PresetItems");
        DisableCopiedCloseControl();

        HudWindow window =
            panelRoot.GetComponent<HudWindow>();

        if (window == null ||
            window.windowCanvas == null)
        {
            Debug.LogError(
                "[G1 Teleop UI] Cloned panel is " +
                "missing its HudWindow canvas.",
                this);

            Destroy(panelRoot);
            panelRoot = null;
            return;
        }

        window.windowId = "teleop-mode";
        window.windowTitle = "Teleoperation";
        window.SetTitle("Teleoperation");
        window.SetSizeMeters(0.66f, 0.46f);

        RectTransform content =
            CreateRect(
                "TeleopContent",
                window.windowCanvas,
                Vector2.zero,
                Vector2.one,
                new Vector2(28f, 24f),
                new Vector2(-28f, -70f));

        panelTitle =
            CreateText(
                "StatusTitle",
                content,
                new Vector2(0f, 0.80f),
                new Vector2(1f, 1f),
                28f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        panelDetail =
            CreateText(
                "StatusDetail",
                content,
                new Vector2(0.05f, 0.40f),
                new Vector2(0.95f, 0.80f),
                18f,
                FontStyles.Normal,
                TextAlignmentOptions.Center);

        panelXr =
            CreateText(
                "XrStatus",
                content,
                new Vector2(0f, 0.28f),
                new Vector2(1f, 0.41f),
                18f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        enterButton =
            CreateButton(
                "EnterButton",
                content,
                new Vector2(0.06f, 0.02f),
                new Vector2(0.68f, 0.26f),
                readyColor,
                out enterLabel,
                out enterHold);

        enterButton.onClick.AddListener(
            coordinator
                .ConfirmEntryHoldCompleted);

        enterHold.ConfigureHoldDuration(1f);

        HudCurvedButtonHitTarget cancelHit;

        cancelButton =
            CreateButton(
                "CancelButton",
                content,
                new Vector2(0.72f, 0.02f),
                new Vector2(0.96f, 0.26f),
                lockedColor,
                out cancelLabel,
                out cancelHit);

        cancelLabel.text = "CANCEL";

        cancelButton.onClick.AddListener(
            coordinator.CancelEntry);

        cancelHit.ConfigureHoldDuration(0f);

        window.RefreshCurvedVisuals();

        if (window.sphereConstraint != null)
        {
            window.sphereConstraint.SetVisorAngles(
                panelYawDegrees,
                panelPitchDegrees);
        }

        panelRoot.SetActive(false);
    }

    private void DisableCopiedContent(
        string childName)
    {
        if (panelRoot == null)
            return;

        Transform child =
            panelRoot.transform.Find(
                childName);

        if (child != null)
            child.gameObject.SetActive(false);
    }

    private void DisableCopiedCloseControl()
    {
        if (panelRoot == null)
            return;

        HudWindowCloseController[] closeControllers =
            panelRoot.GetComponentsInChildren<
                HudWindowCloseController>(true);

        foreach (
            HudWindowCloseController closeController
            in closeControllers
        )
        {
            if (closeController != null)
                closeController.enabled = false;
        }

        HudWindowCloseSnapLayout[] closeLayouts =
            panelRoot.GetComponentsInChildren<
                HudWindowCloseSnapLayout>(true);

        foreach (
            HudWindowCloseSnapLayout closeLayout
            in closeLayouts
        )
        {
            if (closeLayout != null)
                closeLayout.enabled = false;
        }

        Transform[] descendants =
            panelRoot.GetComponentsInChildren<
                Transform>(true);

        foreach (Transform descendant in descendants)
        {
            if (
                descendant != null &&
                (
                    descendant.name == "CloseButton" ||
                    descendant.name == "CloseSnapTarget"
                )
            )
            {
                descendant.gameObject.SetActive(false);
            }
        }
    }


    private void RefreshUi()
    {
        if (coordinator == null)
            return;

        /*
         * Teleop entry is available only in Live mode.
         * Once teleop/countdown starts, force Live mode and
         * let HudModeManager reject Edit-mode requests.
         */
        if (
            coordinator.BlocksHudEditing &&
            modeManager != null &&
            !modeManager.IsLiveMode
        )
        {
            modeManager.EnterLiveMode();
        }

        if (
            modeManager != null &&
            !modeManager.IsLiveMode &&
            coordinator.EntryPanelOpen
        )
        {
            coordinator.CancelEntry();
        }

        RefreshTopButton();
        RefreshPanel();
    }

    private void RefreshTopButton()
    {
        if (topButtonRoot == null)
            return;

        bool liveMode =
            modeManager == null ||
            modeManager.IsLiveMode;

        if (topButtonRoot.activeSelf != liveMode)
            topButtonRoot.SetActive(liveMode);

        if (!liveMode)
            return;

        G1QuestTeleopModeCoordinator.ModeState state =
            coordinator.State;

        Color color = lockedColor;
        string label =
            "TELEOP LOCKED\n" +
            coordinator.XrStatusLabel;

        /*
         * The gray button remains clickable in Live mode so
         * it can show the exact lock reason. Only the panel's
         * one-second Enter control is disabled while locked.
         */
        bool enabled = true;

        switch (state)
        {
            case G1QuestTeleopModeCoordinator
                .ModeState.Ready:

                color = readyColor;
                label =
                    "ENTER TELEOP\n" +
                    coordinator.XrStatusLabel;
                enabled = true;
                break;

            case G1QuestTeleopModeCoordinator
                .ModeState.ConfirmEntry:

                color = readyColor;
                label =
                    "CONFIRM TELEOP\n" +
                    coordinator.XrStatusLabel;
                break;

            case G1QuestTeleopModeCoordinator
                .ModeState.Aligning:

                color = transitionColor;
                label =
                    "ALIGN HANDS · " +
                    Mathf.CeilToInt(
                        coordinator
                            .AlignmentSecondsRemaining);
                break;

            case G1QuestTeleopModeCoordinator
                .ModeState.TeleopActive:

                color = activeColor;
                label =
                    "STOP TELEOP\n" +
                    coordinator.XrStatusLabel;
                enabled = true;
                break;

            case G1QuestTeleopModeCoordinator
                .ModeState.PoseHeld:

                color = activeColor;
                label = "STOP TELEOP\nPOSE HELD";
                enabled = true;
                break;

            case G1QuestTeleopModeCoordinator
                .ModeState.Realigning:

                color = transitionColor;
                label = "STOP TELEOP\nALIGN HANDS";
                enabled = true;
                break;

            case G1QuestTeleopModeCoordinator
                .ModeState.Transition:

                color = transitionColor;
                label = coordinator.StatusTitle;
                break;

            case G1QuestTeleopModeCoordinator
                .ModeState.Fault:

                color = activeColor;
                label = "FAULT\nARMS HELD";
                break;
        }

        if (topLabel != null)
            topLabel.SetText(label);

        SetRendererColor(
            topRenderer,
            color);

        if (topInteractable != null &&
            topInteractable.enabled != enabled)
        {
            topInteractable.enabled = enabled;
        }
    }

    private void RefreshPanel()
    {
        if (panelRoot == null)
            return;

        bool shouldOpen =
            coordinator.EntryPanelOpen;

        if (panelRoot.activeSelf != shouldOpen)
            panelRoot.SetActive(shouldOpen);

        if (shouldOpen)
            DisableCopiedCloseControl();

        if (!shouldOpen)
        {
            panelWasOpen = false;
            return;
        }

        if (!panelWasOpen)
        {
            if (paletteController != null &&
                paletteController.menuPanel !=
                panelRoot)
            {
                paletteController.CloseMenu();
            }

            HudSphereWindowConstraint constraint =
                panelRoot.GetComponent<
                    HudSphereWindowConstraint>();

            if (constraint != null)
            {
                constraint.SetVisorAngles(
                    panelYawDegrees,
                    panelPitchDegrees);
            }

            panelWasOpen = true;
        }

        bool faultMode =
            coordinator.State ==
            G1QuestTeleopModeCoordinator.ModeState.Fault;

        if (panelTitle != null)
            panelTitle.text = coordinator.StatusTitle;

        if (panelDetail != null)
            panelDetail.text = coordinator.StatusDetail;

        if (panelXr != null)
        {
            panelXr.text = faultMode
                ? "RELEASE MAY MOVE ARMS · FINGERS WILL OPEN"
                : coordinator.XrStatusLabel;

            panelXr.color = faultMode
                ? transitionColor
                : coordinator.XrStatusLabel == "XR OK"
                    ? readyColor
                    : activeColor;
        }

        bool primaryActionAvailable = faultMode
            ? coordinator.CanReleaseFaultHold
            : coordinator.CanConfirmEntry;

        if (enterButton != null)
        {
            SetButtonColor(
                enterButton,
                faultMode
                    ? transitionColor
                    : readyColor);

            enterButton.interactable =
                primaryActionAvailable;
        }

        if (enterLabel != null)
        {
            float progress =
                enterHold != null
                    ? enterHold.HoldProgress01
                    : 0f;

            if (progress > 0f)
            {
                enterLabel.text = faultMode
                    ? "HOLD RELEASE · " +
                        Mathf.RoundToInt(
                            progress * 100f) +
                        "%"
                    : "HOLD ENTER · " +
                        Mathf.RoundToInt(
                            progress * 100f) +
                        "%";
            }
            else
            {
                enterLabel.text = faultMode
                    ? primaryActionAvailable
                        ? "HOLD RELEASE · 1s"
                        : "RELEASE LOCKED"
                    : primaryActionAvailable
                        ? "HOLD ENTER · 1s"
                        : "ENTRY LOCKED";
            }
        }

        if (cancelLabel != null)
        {
            cancelLabel.text = faultMode
                ? "KEEP HELD"
                : "CANCEL";
        }
    }

    private static RectTransform CreateRect(
        string objectName,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        GameObject target =
            new GameObject(
                objectName,
                typeof(RectTransform));

        RectTransform rect =
            target.GetComponent<RectTransform>();

        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;

        return rect;
    }

    private static TMP_Text CreateText(
        string objectName,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float fontSize,
        FontStyles style,
        TextAlignmentOptions alignment)
    {
        GameObject target =
            new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(TextMeshProUGUI));

        RectTransform rect =
            target.GetComponent<RectTransform>();

        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI text =
            target.GetComponent<TextMeshProUGUI>();

        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = HudDashboardTheme.TextPrimary;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        text.text = string.Empty;

        return text;
    }

    private static Button CreateButton(
        string objectName,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Color color,
        out TMP_Text label,
        out HudCurvedButtonHitTarget hitTarget)
    {
        GameObject target =
            new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));

        RectTransform rect =
            target.GetComponent<RectTransform>();

        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image =
            target.GetComponent<Image>();

        image.color = color;

        Button button =
            target.GetComponent<Button>();

        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor =
            Color.Lerp(color, HudDashboardTheme.TextPrimary, 0.18f);
        colors.pressedColor =
            Color.Lerp(color, HudDashboardTheme.Background, 0.18f);
        colors.selectedColor =
            colors.highlightedColor;
        colors.disabledColor =
            HudDashboardTheme.WithAlpha(
                HudDashboardTheme.Control,
                0.70f);
        button.colors = colors;

        label =
            CreateText(
                "Label",
                rect,
                Vector2.zero,
                Vector2.one,
                23f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        hitTarget =
            target.AddComponent<
                HudCurvedButtonHitTarget>();

        return button;
    }

    private static void SetButtonColor(
        Button button,
        Color color)
    {
        if (button == null)
            return;

        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor =
            Color.Lerp(
                color,
                HudDashboardTheme.TextPrimary,
                0.18f);
        colors.pressedColor =
            Color.Lerp(
                color,
                HudDashboardTheme.Background,
                0.18f);
        colors.selectedColor =
            colors.highlightedColor;
        colors.disabledColor =
            HudDashboardTheme.WithAlpha(
                HudDashboardTheme.Control,
                0.70f);
        button.colors = colors;
    }

    private static void SetRendererColor(
        Renderer renderer,
        Color color)
    {
        if (renderer == null)
            return;

        MaterialPropertyBlock properties =
            new MaterialPropertyBlock();

        renderer.GetPropertyBlock(properties);

        properties.SetColor(
            "_BaseColor",
            color);

        properties.SetColor(
            "_Color",
            color);

        renderer.SetPropertyBlock(properties);
    }
}
