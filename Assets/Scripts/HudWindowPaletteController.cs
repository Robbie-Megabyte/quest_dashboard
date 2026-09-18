using System.Collections.Generic;
using UnityEngine;


[DisallowMultipleComponent]
public class HudWindowPaletteController : MonoBehaviour
{
    [Header("Menu")]
    public GameObject menuPanel;

    [Header("Placement")]
    public HudSphereWindowConstraint sphereConstraint;

    [Tooltip(
        "Horizontal visor angle used whenever the menu opens."
    )]
    public float openYawDegrees = 0.0f;

    [Tooltip(
        "Vertical visor angle used whenever the menu opens. " +
        "Negative moves it lower in the field of view."
    )]
    public float openPitchDegrees = -5.0f;

    [Tooltip(
        "Return the palette to its preset position every time " +
        "it is opened."
    )]
    public bool resetPositionWhenOpened = true;


    [Header("Window List")]
    [SerializeField]
    private Transform itemContainer;

    [Range(1, 8)]
    [SerializeField]
    private int visibleItemCount = 5;

    [SerializeField]
    private Vector3 firstItemLocalPosition =
        new Vector3(
            0.12f,
            0.18f,
            -0.02f
        );

    [Min(0.01f)]
    [SerializeField]
    private float verticalSpacingMeters =
        0.09f;

    [SerializeField]
    private bool closeAfterSelection = true;


    [Header("Startup")]
    public bool startOpen = true;


    [Header("Runtime")]
    [SerializeField]
    private int firstVisibleItemIndex;


    private readonly List<HudWindowPaletteItem>
        paletteItems =
            new List<HudWindowPaletteItem>();


    public bool CanScrollUp
    {
        get
        {
            return firstVisibleItemIndex > 0;
        }
    }


    public bool CanScrollDown
    {
        get
        {
            return
                firstVisibleItemIndex +
                visibleItemCount <
                paletteItems.Count;
        }
    }


    private void Awake()
    {
        ResolveReferences();
    }


    private void Start()
    {
        ResolveReferences();

        if (startOpen)
        {
            OpenMenu();
        }
        else
        {
            CloseMenu();
        }
    }


    private void OnValidate()
    {
        visibleItemCount =
            Mathf.Clamp(
                visibleItemCount,
                1,
                8
            );

        verticalSpacingMeters =
            Mathf.Max(
                0.01f,
                verticalSpacingMeters
            );

        ResolveReferences();
    }


    private void ResolveReferences()
    {
        if (menuPanel != null &&
            sphereConstraint == null)
        {
            sphereConstraint =
                menuPanel.GetComponent
                    <HudSphereWindowConstraint>();
        }

        if (itemContainer == null &&
            menuPanel != null)
        {
            Transform found =
                menuPanel.transform.Find(
                    "PaletteItems"
                );

            if (found != null)
            {
                itemContainer = found;
            }
        }
    }


    public void ToggleMenu()
    {
        if (menuPanel == null)
            return;

        if (menuPanel.activeSelf)
        {
            CloseMenu();
        }
        else
        {
            OpenMenu();
        }
    }


    public void OpenMenu()
    {
        if (menuPanel == null)
            return;

        menuPanel.SetActive(true);

        ResolveReferences();
        RebuildList();

        if (resetPositionWhenOpened &&
            sphereConstraint != null)
        {
            sphereConstraint.SetVisorAngles(
                openYawDegrees,
                openPitchDegrees
            );
        }
    }


    public void CloseMenu()
    {
        if (menuPanel == null)
            return;

        menuPanel.SetActive(false);
    }


    public void NotifyItemActivated(
        HudWindowPaletteItem item)
    {
        if (closeAfterSelection)
        {
            CloseMenu();
        }
        else
        {
            RebuildList();
        }
    }


    public void RebuildList()
    {
        ResolveReferences();

        if (itemContainer == null)
            return;

        paletteItems.Clear();

        for (
            int childIndex = 0;
            childIndex < itemContainer.childCount;
            ++childIndex)
        {
            Transform child =
                itemContainer.GetChild(
                    childIndex
                );

            HudWindowPaletteItem item =
                child.GetComponent
                    <HudWindowPaletteItem>();

            if (item != null)
            {
                paletteItems.Add(item);
            }
        }

        paletteItems.Sort(
            ComparePaletteItems
        );

        int maximumStartIndex =
            Mathf.Max(
                0,
                paletteItems.Count -
                visibleItemCount
            );

        firstVisibleItemIndex =
            Mathf.Clamp(
                firstVisibleItemIndex,
                0,
                maximumStartIndex
            );

        for (
            int index = 0;
            index < paletteItems.Count;
            ++index)
        {
            HudWindowPaletteItem item =
                paletteItems[index];

            int visibleSlot =
                index -
                firstVisibleItemIndex;

            bool visible =
                visibleSlot >= 0 &&
                visibleSlot <
                visibleItemCount;

            item.gameObject.SetActive(
                visible
            );

            if (!visible)
                continue;

            Vector3 localPosition =
                firstItemLocalPosition +
                Vector3.down *
                (
                    verticalSpacingMeters *
                    visibleSlot
                );

            item.ConfigureAsListItem(
                this,
                localPosition
            );
        }

        HudWindow sharedWindow =
            menuPanel != null
                ? menuPanel.GetComponent<HudWindow>()
                : null;

        if (sharedWindow != null)
        {
            sharedWindow.RefreshCurvedVisuals();
        }
    }


    public void ScrollUp()
    {
        if (!CanScrollUp)
            return;

        firstVisibleItemIndex--;
        RebuildList();
    }


    public void ScrollDown()
    {
        if (!CanScrollDown)
            return;

        firstVisibleItemIndex++;
        RebuildList();
    }


    public void ResetScroll()
    {
        firstVisibleItemIndex = 0;
        RebuildList();
    }


    private static int ComparePaletteItems(
        HudWindowPaletteItem left,
        HudWindowPaletteItem right)
    {
        if (ReferenceEquals(left, right))
            return 0;

        if (left == null)
            return 1;

        if (right == null)
            return -1;

        int orderComparison =
            left.ListSortOrder.CompareTo(
                right.ListSortOrder
            );

        if (orderComparison != 0)
            return orderComparison;

        return
            left.transform
                .GetSiblingIndex()
                .CompareTo(
                    right.transform
                        .GetSiblingIndex()
                );
    }
}
