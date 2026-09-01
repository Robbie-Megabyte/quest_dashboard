using System.Collections.Generic;
using UnityEngine;


[DisallowMultipleComponent]
public sealed class HudPresetMenuView : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private HudPresetManager presetManager;

    [SerializeField]
    private HudEditMenuCoordinator menuCoordinator;

    [SerializeField]
    private HudWindow sharedMenuWindow;

    [SerializeField]
    private HudPresetCard cardTemplate;

    [SerializeField]
    private Transform cardContainer;


    [Header("Visible List")]
    [Range(1, 6)]
    [SerializeField]
    private int visibleCardCount = 4;

    [SerializeField]
    private Vector3 firstCardLocalPosition =
        new Vector3(0.0f, 0.145f, 0.0f);

    [Min(0.01f)]
    [SerializeField]
    private float verticalSpacingMeters = 0.09f;


    [Header("Runtime")]
    [SerializeField]
    private int firstVisiblePresetIndex;


    private readonly List<HudPresetCard>
        cardSlots =
            new List<HudPresetCard>();

    private bool subscribed;


    public bool CanScrollUp
    {
        get
        {
            return firstVisiblePresetIndex > 0;
        }
    }


    public bool CanScrollDown
    {
        get
        {
            if (presetManager == null)
                return false;

            return
                firstVisiblePresetIndex +
                visibleCardCount <
                presetManager.AllPresets.Count;
        }
    }


    private void Awake()
    {
        ResolveReferences();
    }


    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
        Rebuild();
    }


    private void OnDisable()
    {
        Unsubscribe();
    }


    private void OnValidate()
    {
        visibleCardCount =
            Mathf.Clamp(
                visibleCardCount,
                1,
                6
            );

        verticalSpacingMeters =
            Mathf.Max(
                0.01f,
                verticalSpacingMeters
            );

        ResolveReferences();
    }


    public void Rebuild()
    {
        ResolveReferences();

        if (presetManager == null ||
            cardTemplate == null ||
            cardContainer == null)
        {
            return;
        }

        EnsureCardSlots();

        int presetCount =
            presetManager.AllPresets.Count;

        int maximumStartIndex =
            Mathf.Max(
                0,
                presetCount -
                visibleCardCount
            );

        firstVisiblePresetIndex =
            Mathf.Clamp(
                firstVisiblePresetIndex,
                0,
                maximumStartIndex
            );

        for (
            int slotIndex = 0;
            slotIndex < cardSlots.Count;
            ++slotIndex)
        {
            HudPresetCard card =
                cardSlots[slotIndex];

            int presetIndex =
                firstVisiblePresetIndex +
                slotIndex;

            if (presetIndex >= presetCount)
            {
                card.gameObject.SetActive(false);
                continue;
            }

            HudPresetDefinition preset =
                presetManager.AllPresets[
                    presetIndex
                ];

            card.transform.localPosition =
                firstCardLocalPosition +
                Vector3.down *
                (
                    verticalSpacingMeters *
                    slotIndex
                );

            card.transform.localRotation =
                Quaternion.identity;

            card.transform.localScale =
                Vector3.one;

            card.Configure(
                menuCoordinator,
                preset
            );

            card.gameObject.SetActive(true);
        }

        if (sharedMenuWindow != null)
        {
            sharedMenuWindow.RefreshCurvedVisuals();
        }
    }


    public void ScrollUp()
    {
        if (!CanScrollUp)
            return;

        firstVisiblePresetIndex--;
        Rebuild();
    }


    public void ScrollDown()
    {
        if (!CanScrollDown)
            return;

        firstVisiblePresetIndex++;
        Rebuild();
    }


    public void ResetScroll()
    {
        firstVisiblePresetIndex = 0;
        Rebuild();
    }


    private void EnsureCardSlots()
    {
        while (
            cardSlots.Count <
            visibleCardCount
        )
        {
            HudPresetCard card =
                Instantiate(
                    cardTemplate,
                    cardContainer
                );

            card.gameObject.SetActive(false);

            cardSlots.Add(card);
        }

        for (
            int index = visibleCardCount;
            index < cardSlots.Count;
            ++index)
        {
            cardSlots[index]
                .gameObject
                .SetActive(false);
        }
    }


    private void Subscribe()
    {
        if (subscribed ||
            presetManager == null)
        {
            return;
        }

        presetManager.PresetsChanged +=
            Rebuild;

        subscribed = true;
    }


    private void Unsubscribe()
    {
        if (!subscribed ||
            presetManager == null)
        {
            return;
        }

        presetManager.PresetsChanged -=
            Rebuild;

        subscribed = false;
    }


    private void ResolveReferences()
    {
        if (presetManager == null)
        {
            presetManager =
                UnityEngine.Object.FindAnyObjectByType
                    <HudPresetManager>();
        }

        if (menuCoordinator == null)
        {
            menuCoordinator =
                UnityEngine.Object.FindAnyObjectByType
                    <HudEditMenuCoordinator>();
        }

        if (sharedMenuWindow == null)
        {
            sharedMenuWindow =
                GetComponentInParent<HudWindow>(true);
        }

        if (cardContainer == null)
        {
            cardContainer = transform;
        }
    }
}