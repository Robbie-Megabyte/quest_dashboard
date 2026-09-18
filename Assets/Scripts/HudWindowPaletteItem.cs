using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;


[DisallowMultipleComponent]
public class HudWindowPaletteItem : MonoBehaviour
{
    [Header("Target Window")]
    public GameObject targetWindow;

    [Header("HUD")]
    public Transform hudWindowLayer;


    [Header("List Card")]
    [SerializeField]
    private Vector2 cardSizeMeters =
        new Vector2(0.90f, 0.075f);

    [SerializeField]
    private float colliderDepthMeters = 0.025f;

    [SerializeField]
    private Color normalColor =
        new Color(0.18f, 0.21f, 0.25f, 0.96f);

    [SerializeField]
    private Color hoverColor =
        new Color(0.29f, 0.35f, 0.41f, 1.0f);

    [SerializeField]
    private Color openColor =
        new Color(0.16f, 0.34f, 0.30f, 1.0f);

    [SerializeField]
    private Color nameColor = Color.white;

    [SerializeField]
    private Color availableBadgeColor =
        new Color(0.55f, 0.72f, 0.88f, 1.0f);

    [SerializeField]
    private Color openBadgeColor =
        new Color(0.35f, 0.95f, 0.66f, 1.0f);


    [Header("Automatically Resolved")]
    [SerializeField]
    private HudGridWindowController targetGridController;

    [SerializeField]
    private HudSphereWindowConstraint targetSphereConstraint;

    [SerializeField]
    private HudGridManager gridManager;

    [SerializeField]
    private HudWindowPaletteController paletteController;


    private XRGrabInteractable grabInteractable;
    private BoxCollider cardCollider;

    private Canvas cardCanvas;
    private Image backgroundImage;
    private TextMeshProUGUI nameText;
    private TextMeshProUGUI badgeText;

    private Vector3 homeLocalPosition;
    private Quaternion homeLocalRotation;
    private Vector3 homeLocalScale;

    private bool homePoseStored;
    private bool eventsSubscribed;
    private bool hovered;
    private bool listActivationMode = true;


    public int ListSortOrder
    {
        get
        {
            string identifier =
                ResolveWindowIdentifier();

            switch (identifier)
            {
                case "slam":
                    return 0;

                case "robot":
                    return 1;

                case "camera":
                    return 2;

                case "telemetry":
                    return 3;

                case "agents":
                    return 4;

                default:
                    return
                        100 +
                        transform.GetSiblingIndex();
            }
        }
    }


    private void Awake()
    {
        ResolveReferences();
        ConfigureListInteraction();
        BuildOrFindVisuals();
        StoreHomePose();
        ApplyAppearance();
    }


    private void Start()
    {
        ResolveReferences();
        ConfigureListInteraction();
        BuildOrFindVisuals();

        if (!homePoseStored)
        {
            StoreHomePose();
        }

        ApplyAppearance();
    }


    private void OnEnable()
    {
        ResolveReferences();
        ConfigureListInteraction();
        BuildOrFindVisuals();
        SubscribeEvents();
        ApplyAppearance();
    }


    private void OnDisable()
    {
        hovered = false;
        UnsubscribeEvents();
    }


    public void ConfigureAsListItem(
        HudWindowPaletteController controller,
        Vector3 localPosition)
    {
        paletteController = controller;
        listActivationMode = true;

        transform.localPosition = localPosition;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;

        StoreHomePose();

        ResolveReferences();
        ConfigureListInteraction();
        BuildOrFindVisuals();
        ApplyAppearance();
    }


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

        if (paletteController == null)
        {
            paletteController =
                Object.FindFirstObjectByType
                    <HudWindowPaletteController>();
        }

        if (grabInteractable == null)
        {
            grabInteractable =
                GetComponent<XRGrabInteractable>();
        }

        if (cardCollider == null)
        {
            cardCollider =
                GetComponent<BoxCollider>();
        }
    }


    private void ConfigureListInteraction()
    {
        if (!listActivationMode)
            return;

        if (grabInteractable != null)
        {
            grabInteractable.trackPosition = false;
            grabInteractable.trackRotation = false;
            grabInteractable.trackScale = false;
            grabInteractable.throwOnDetach = false;
        }

        if (cardCollider != null)
        {
            cardCollider.center = Vector3.zero;

            cardCollider.size =
                new Vector3(
                    cardSizeMeters.x,
                    cardSizeMeters.y,
                    colliderDepthMeters
                );

            cardCollider.isTrigger = false;
        }

        MeshRenderer oldRenderer =
            GetComponent<MeshRenderer>();

        if (oldRenderer != null)
        {
            oldRenderer.enabled = false;
        }

        Transform oldLabel =
            transform.Find("Label");

        if (oldLabel != null)
        {
            oldLabel.gameObject.SetActive(false);
        }
    }


    private void StoreHomePose()
    {
        homeLocalPosition = transform.localPosition;
        homeLocalRotation = transform.localRotation;
        homeLocalScale = transform.localScale;
        homePoseStored = true;
    }


    private void ReturnToMenu()
    {
        if (!homePoseStored)
            return;

        transform.localPosition = homeLocalPosition;
        transform.localRotation = homeLocalRotation;
        transform.localScale = homeLocalScale;
    }


    public void NotifyGrabStarted()
    {
        ResolveReferences();

        if (listActivationMode)
        {
            ReturnToMenu();
        }
    }


    public void NotifyGrabEnded()
    {
        Vector3 dropWorldPoint =
            transform.position;

        ReturnToMenu();

        if (listActivationMode)
        {
            OpenTargetWindowFromList();
            return;
        }

        OpenTargetWindowAt(
            dropWorldPoint
        );
    }


    public void OpenTargetWindowFromList()
    {
        if (!PrepareTargetWindow())
            return;

        gridManager.RegisterWindow(
            targetGridController
        );

        ApplyAppearance();

        if (paletteController != null)
        {
            paletteController.NotifyItemActivated(
                this
            );
        }
    }


    public void OpenTargetWindowAt(
        Vector3 worldPoint)
    {
        if (!PrepareTargetWindow())
            return;

        gridManager.RegisterWindow(
            targetGridController
        );

        gridManager.BeginWindowMove(
            targetGridController
        );

        targetWindow.transform.position =
            worldPoint;

        gridManager.EndWindowMove(
            targetGridController
        );

        ApplyAppearance();
    }


    private bool PrepareTargetWindow()
    {
        ResolveReferences();

        if (targetWindow == null)
        {
            Debug.LogError(
                $"Palette item '{name}': " +
                "Target Window is missing."
            );

            return false;
        }

        if (targetGridController == null)
        {
            Debug.LogError(
                $"Palette item '{name}': " +
                "Target Window has no " +
                "HudGridWindowController."
            );

            return false;
        }

        if (gridManager == null)
        {
            Debug.LogError(
                $"Palette item '{name}': " +
                "HudGridManager is missing."
            );

            return false;
        }

        targetWindow.SetActive(true);

        if (hudWindowLayer != null &&
            targetWindow.transform.parent !=
            hudWindowLayer)
        {
            targetWindow.transform.SetParent(
                hudWindowLayer,
                true
            );
        }

        targetGridController.SetHudGridEnabled(
            true
        );

        if (targetSphereConstraint != null)
        {
            targetSphereConstraint
                .EnableHudConstraint();
        }

        return true;
    }


    private void BuildOrFindVisuals()
    {
        Transform existingCanvas =
            transform.Find("CardCanvas");

        GameObject canvasObject;

        if (existingCanvas == null)
        {
            canvasObject =
                new GameObject(
                    "CardCanvas",
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
                existingCanvas.gameObject;
        }

        cardCanvas =
            canvasObject.GetComponent<Canvas>();

        cardCanvas.renderMode =
            RenderMode.WorldSpace;

        cardCanvas.worldCamera =
            Camera.main;

        cardCanvas.overrideSorting = true;
        cardCanvas.sortingOrder = 210;

        RectTransform canvasRect =
            canvasObject.GetComponent<RectTransform>();

        const float canvasScale = 0.001f;

        canvasRect.sizeDelta =
            new Vector2(
                cardSizeMeters.x / canvasScale,
                cardSizeMeters.y / canvasScale
            );

        canvasRect.localPosition = Vector3.zero;
        canvasRect.localRotation = Quaternion.identity;
        canvasRect.localScale =
            Vector3.one * canvasScale;


        backgroundImage =
            GetOrCreateImage(
                canvasRect,
                "Background"
            );

        StretchToParent(
            backgroundImage.rectTransform
        );

        backgroundImage.raycastTarget = false;


        nameText =
            GetOrCreateText(
                canvasRect,
                "WindowName"
            );

        RectTransform nameRect =
            nameText.rectTransform;

        nameRect.anchorMin = Vector2.zero;
        nameRect.anchorMax = Vector2.one;

        nameRect.offsetMin =
            new Vector2(22.0f, 8.0f);

        nameRect.offsetMax =
            new Vector2(-170.0f, -8.0f);

        nameText.alignment =
            TextAlignmentOptions.MidlineLeft;

        nameText.fontSize = 28.0f;
        nameText.color = nameColor;
        nameText.raycastTarget = false;

        nameText.textWrappingMode =
            TextWrappingModes.NoWrap;

        nameText.overflowMode =
            TextOverflowModes.Ellipsis;


        badgeText =
            GetOrCreateText(
                canvasRect,
                "WindowBadge"
            );

        RectTransform badgeRect =
            badgeText.rectTransform;

        badgeRect.anchorMin =
            new Vector2(1.0f, 0.0f);

        badgeRect.anchorMax =
            new Vector2(1.0f, 1.0f);

        badgeRect.pivot =
            new Vector2(1.0f, 0.5f);

        badgeRect.sizeDelta =
            new Vector2(155.0f, 0.0f);

        badgeRect.anchoredPosition =
            new Vector2(-14.0f, 0.0f);

        badgeText.alignment =
            TextAlignmentOptions.Center;

        badgeText.fontSize = 18.0f;
        badgeText.raycastTarget = false;

        badgeText.textWrappingMode =
            TextWrappingModes.NoWrap;

        badgeText.overflowMode =
            TextOverflowModes.Ellipsis;
    }


    private string ResolveWindowIdentifier()
    {
        if (targetWindow != null)
        {
            HudWindow window =
                targetWindow.GetComponent<HudWindow>();

            if (window != null &&
                !string.IsNullOrWhiteSpace(
                    window.windowId))
            {
                return
                    window.windowId
                        .Trim()
                        .ToLowerInvariant();
            }
        }

        return
            name.Replace("Palette_", string.Empty)
                .Trim()
                .ToLowerInvariant();
    }


    private string ResolveDisplayName()
    {
        if (targetWindow != null)
        {
            HudWindow window =
                targetWindow.GetComponent<HudWindow>();

            if (window != null &&
                !string.IsNullOrWhiteSpace(
                    window.windowTitle))
            {
                return window.windowTitle.Trim();
            }
        }

        string fallback =
            name.Replace(
                "Palette_",
                string.Empty
            );

        return
            string.IsNullOrWhiteSpace(fallback)
                ? "WINDOW"
                : fallback.Trim();
    }


    private void ApplyAppearance()
    {
        bool windowOpen =
            targetWindow != null &&
            targetWindow.activeInHierarchy;

        if (backgroundImage != null)
        {
            backgroundImage.color =
                hovered
                    ? hoverColor
                    : windowOpen
                        ? openColor
                        : normalColor;
        }

        if (nameText != null)
        {
            nameText.text =
                ResolveDisplayName();
        }

        if (badgeText != null)
        {
            badgeText.text =
                windowOpen
                    ? "OPEN"
                    : "AVAILABLE";

            badgeText.color =
                windowOpen
                    ? openBadgeColor
                    : availableBadgeColor;
        }
    }


    private void SubscribeEvents()
    {
        if (eventsSubscribed ||
            grabInteractable == null)
        {
            return;
        }

        grabInteractable.hoverEntered.AddListener(
            HandleHoverEntered
        );

        grabInteractable.hoverExited.AddListener(
            HandleHoverExited
        );

        eventsSubscribed = true;
    }


    private void UnsubscribeEvents()
    {
        if (!eventsSubscribed ||
            grabInteractable == null)
        {
            return;
        }

        grabInteractable.hoverEntered.RemoveListener(
            HandleHoverEntered
        );

        grabInteractable.hoverExited.RemoveListener(
            HandleHoverExited
        );

        eventsSubscribed = false;
    }


    private void HandleHoverEntered(
        HoverEnterEventArgs eventArguments)
    {
        hovered = true;
        ApplyAppearance();
    }


    private void HandleHoverExited(
        HoverExitEventArgs eventArguments)
    {
        hovered = false;
        ApplyAppearance();
    }


    private static Image GetOrCreateImage(
        Transform parent,
        string objectName)
    {
        Transform existing =
            parent.Find(objectName);

        GameObject imageObject;

        if (existing == null)
        {
            imageObject =
                new GameObject(
                    objectName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image)
                );

            imageObject.transform.SetParent(
                parent,
                false
            );
        }
        else
        {
            imageObject =
                existing.gameObject;
        }

        return imageObject.GetComponent<Image>();
    }


    private static TextMeshProUGUI GetOrCreateText(
        Transform parent,
        string objectName)
    {
        Transform existing =
            parent.Find(objectName);

        GameObject textObject;

        if (existing == null)
        {
            textObject =
                new GameObject(
                    objectName,
                    typeof(RectTransform),
                    typeof(TextMeshProUGUI)
                );

            textObject.transform.SetParent(
                parent,
                false
            );
        }
        else
        {
            textObject =
                existing.gameObject;
        }

        return
            textObject.GetComponent
                <TextMeshProUGUI>();
    }


    private static void StretchToParent(
        RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        rect.localPosition = Vector3.zero;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
    }
}
