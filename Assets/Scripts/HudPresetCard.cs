using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;


[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(XRSimpleInteractable))]
public sealed class HudPresetCard : MonoBehaviour
{
    [Header("Card Size")]
    [SerializeField]
    private Vector2 cardSizeMeters =
        new Vector2(0.60f, 0.075f);

    [SerializeField]
    private float colliderDepthMeters = 0.025f;


    [Header("Appearance")]
    [SerializeField]
    private Color normalColor =
        new Color(0.18f, 0.21f, 0.25f, 0.96f);

    [SerializeField]
    private Color hoverColor =
        new Color(0.29f, 0.35f, 0.41f, 1.0f);

    [SerializeField]
    private Color nameColor = Color.white;

    [SerializeField]
    private Color badgeColor =
        new Color(0.55f, 0.72f, 0.88f, 1.0f);


    [Header("Runtime")]
    [SerializeField]
    private string presetId;

    [SerializeField]
    private string presetName;

    [SerializeField]
    private bool builtIn;

    [SerializeField]
    private HudEditMenuCoordinator menuCoordinator;


    private XRSimpleInteractable interactable;
    private BoxCollider cardCollider;

    private Canvas cardCanvas;
    private Image backgroundImage;
    private TextMeshProUGUI nameText;
    private TextMeshProUGUI badgeText;

    private bool eventsSubscribed;


    private void Awake()
    {
        ResolveReferences();
        BuildOrFindVisuals();
        ApplyCardData();
    }


    private void OnEnable()
    {
        ResolveReferences();
        BuildOrFindVisuals();
        ApplyCardData();
        SubscribeEvents();
    }


    private void OnDisable()
    {
        UnsubscribeEvents();
    }


    public void Configure(
        HudEditMenuCoordinator coordinator,
        HudPresetDefinition preset)
    {
        menuCoordinator = coordinator;

        if (preset == null)
        {
            presetId = string.Empty;
            presetName = "INVALID PRESET";
            builtIn = false;
        }
        else
        {
            presetId =
                preset.presetId ?? string.Empty;

            presetName =
                string.IsNullOrWhiteSpace(
                    preset.displayName)
                    ? presetId
                    : preset.displayName.Trim();

            builtIn = preset.builtIn;
        }

        gameObject.name =
            string.IsNullOrWhiteSpace(presetId)
                ? "PresetCard_Invalid"
                : "PresetCard_" + presetId;

        ResolveReferences();
        BuildOrFindVisuals();
        ApplyCardData();
    }


    public void Activate()
    {
        ResolveReferences();

        if (menuCoordinator == null)
        {
            Debug.LogError(
                "[HUD Presets] Card has no menu coordinator.",
                this
            );

            return;
        }

        if (string.IsNullOrWhiteSpace(presetId))
        {
            Debug.LogError(
                "[HUD Presets] Card has no preset ID.",
                this
            );

            return;
        }

        menuCoordinator.ApplyPresetAndCloseById(
            presetId
        );
    }


    private void ResolveReferences()
    {
        if (interactable == null)
        {
            interactable =
                GetComponent<XRSimpleInteractable>();
        }

        if (cardCollider == null)
        {
            cardCollider =
                GetComponent<BoxCollider>();
        }

        if (menuCoordinator == null)
        {
            menuCoordinator =
                GetComponentInParent
                    <HudEditMenuCoordinator>(true);
        }

        if (menuCoordinator == null)
        {
            menuCoordinator =
                UnityEngine.Object.FindAnyObjectByType
                    <HudEditMenuCoordinator>();
        }

        if (cardCollider != null)
        {
            cardCollider.center =
                Vector3.zero;

            cardCollider.size =
                new Vector3(
                    cardSizeMeters.x,
                    cardSizeMeters.y,
                    colliderDepthMeters
                );

            cardCollider.isTrigger = false;
        }

        /*
         * The old Operations object contains a disabled
         * MeshRenderer. CardCanvas now supplies the visual.
         */
        MeshRenderer oldRenderer =
            GetComponent<MeshRenderer>();

        if (oldRenderer != null)
        {
            oldRenderer.enabled = false;
        }
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

        cardCanvas.overrideSorting =
            true;

        cardCanvas.sortingOrder =
            210;

        RectTransform canvasRect =
            canvasObject.GetComponent<RectTransform>();

        const float canvasScale = 0.001f;

        canvasRect.sizeDelta =
            new Vector2(
                cardSizeMeters.x / canvasScale,
                cardSizeMeters.y / canvasScale
            );

        canvasRect.localPosition =
            Vector3.zero;

        canvasRect.localRotation =
            Quaternion.identity;

        canvasRect.localScale =
            Vector3.one * canvasScale;


        backgroundImage =
            GetOrCreateImage(
                canvasRect,
                "Background"
            );

        RectTransform backgroundRect =
            backgroundImage.rectTransform;

        StretchToParent(backgroundRect);

        backgroundImage.raycastTarget = false;


        nameText =
            GetOrCreateText(
                canvasRect,
                "PresetName"
            );

        RectTransform nameRect =
            nameText.rectTransform;

        nameRect.anchorMin =
            Vector2.zero;

        nameRect.anchorMax =
            Vector2.one;

        nameRect.offsetMin =
            new Vector2(22.0f, 8.0f);

        nameRect.offsetMax =
            new Vector2(-150.0f, -8.0f);

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
                "PresetBadge"
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
            new Vector2(135.0f, 0.0f);

        badgeRect.anchoredPosition =
            new Vector2(-14.0f, 0.0f);

        badgeText.alignment =
            TextAlignmentOptions.Center;

        badgeText.fontSize = 19.0f;
        badgeText.color = badgeColor;
        badgeText.raycastTarget = false;
        badgeText.textWrappingMode =
            TextWrappingModes.NoWrap;

        badgeText.overflowMode =
            TextOverflowModes.Ellipsis;


        HudWindow window =
            GetComponentInParent<HudWindow>(true);

        if (window != null)
        {
            window.RefreshCurvedVisuals();
        }
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

        return textObject.GetComponent<TextMeshProUGUI>();
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


    private void ApplyCardData()
    {
        if (backgroundImage != null)
        {
            backgroundImage.color =
                normalColor;
        }

        if (nameText != null)
        {
            nameText.text =
                string.IsNullOrWhiteSpace(presetName)
                    ? "UNNAMED PRESET"
                    : presetName;
        }

        if (badgeText != null)
        {
            badgeText.text =
                builtIn
                    ? "BUILT-IN"
                    : "CUSTOM";
        }
    }


    private void SubscribeEvents()
    {
        if (eventsSubscribed ||
            interactable == null)
        {
            return;
        }

        interactable.selectEntered.AddListener(
            HandleSelected
        );

        interactable.hoverEntered.AddListener(
            HandleHoverEntered
        );

        interactable.hoverExited.AddListener(
            HandleHoverExited
        );

        eventsSubscribed = true;
    }


    private void UnsubscribeEvents()
    {
        if (!eventsSubscribed ||
            interactable == null)
        {
            return;
        }

        interactable.selectEntered.RemoveListener(
            HandleSelected
        );

        interactable.hoverEntered.RemoveListener(
            HandleHoverEntered
        );

        interactable.hoverExited.RemoveListener(
            HandleHoverExited
        );

        eventsSubscribed = false;
    }


    private void HandleSelected(
        SelectEnterEventArgs eventArguments)
    {
        Activate();
    }


    private void HandleHoverEntered(
        HoverEnterEventArgs eventArguments)
    {
        if (backgroundImage != null)
        {
            backgroundImage.color =
                hoverColor;
        }
    }


    private void HandleHoverExited(
        HoverExitEventArgs eventArguments)
    {
        if (backgroundImage != null)
        {
            backgroundImage.color =
                normalColor;
        }
    }
}