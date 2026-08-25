using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class HudWindowCloseController : MonoBehaviour
{
    [Header("Automatically Resolved")]
    [SerializeField]
    private HudGridWindowController gridController;

    [SerializeField]
    private HudGridManager gridManager;

    [SerializeField]
    private HudGridVisualizer gridVisualizer;

    [SerializeField]
    private Button closeButton;

    [SerializeField]
    private RectTransform closeButtonRect;

    [SerializeField]
    private TMP_Text closeButtonText;


    [Header("Close Button Layout")]
    public Vector2 buttonSize =
        new Vector2(46.0f, 46.0f);

    [Tooltip(
        "Offset from the top-right corner of WindowCanvas."
    )]
    public Vector2 topRightOffset =
        new Vector2(-12.0f, -12.0f);


    // ========================================================
    // UNITY
    // ========================================================

    void Awake()
    {
        ResolveReferences();
        ConfigureCloseButton();
        ApplyButtonLayout();
    }


    void OnEnable()
    {
        ResolveReferences();
        ConfigureCloseButton();
        ApplyButtonLayout();
    }


    void OnValidate()
    {
        ResolveReferences();
        ApplyButtonLayout();
    }


    // ========================================================
    // REFERENCES
    // ========================================================

    private void ResolveReferences()
    {
        if (gridController == null)
        {
            gridController =
                GetComponent<HudGridWindowController>();
        }


        if (gridController != null)
        {
            if (gridManager == null)
            {
                gridManager =
                    gridController.gridManager;
            }

            if (gridVisualizer == null)
            {
                gridVisualizer =
                    gridController.gridVisualizer;
            }
        }


        if (gridManager == null)
        {
            gridManager =
                Object.FindAnyObjectByType
                <HudGridManager>();
        }


        if (gridVisualizer == null)
        {
            gridVisualizer =
                Object.FindAnyObjectByType
                <HudGridVisualizer>();
        }


        /*
         * Find CloseButton anywhere below this window.
         *
         * This is intentionally hierarchy-based so duplicated
         * windows do not preserve stale references to another
         * window's button.
         */
        if (closeButton == null)
        {
            Button[] buttons =
                GetComponentsInChildren
                <Button>(true);


            foreach (Button candidate in buttons)
            {
                if (candidate != null &&
                    candidate.name == "CloseButton")
                {
                    closeButton =
                        candidate;

                    break;
                }
            }
        }


        if (closeButton != null)
        {
            closeButtonRect =
                closeButton.GetComponent
                <RectTransform>();


            closeButtonText =
                closeButton.GetComponentInChildren
                <TMP_Text>(true);
        }
    }


    // ========================================================
    // BUTTON
    // ========================================================

    private void ConfigureCloseButton()
    {
        if (closeButton == null)
            return;


        /*
         * Replace copied/stale UnityEvent targets.
         *
         * Every normal window's close button will therefore
         * always close ITS OWN GameObject.
         */
        closeButton.onClick =
            new Button.ButtonClickedEvent();


        closeButton.onClick.AddListener(
            CloseWindow
        );


        if (closeButtonText != null)
        {
            closeButtonText.text =
                "X";
        }
    }


    private void ApplyButtonLayout()
    {
        if (closeButtonRect == null)
            return;


        closeButtonRect.anchorMin =
            new Vector2(
                1.0f,
                1.0f
            );


        closeButtonRect.anchorMax =
            new Vector2(
                1.0f,
                1.0f
            );


        closeButtonRect.pivot =
            new Vector2(
                1.0f,
                1.0f
            );


        closeButtonRect.sizeDelta =
            buttonSize;


        closeButtonRect.anchoredPosition =
            topRightOffset;


        closeButtonRect.localScale =
            Vector3.one;


        closeButtonRect.localRotation =
            Quaternion.identity;
    }


    // ========================================================
    // CLOSE
    // ========================================================

    public void CloseWindow()
    {
        ResolveReferences();


        if (gridController != null)
        {
            if (gridVisualizer != null)
            {
                gridVisualizer.EndWindowPreview(
                    gridController
                );
            }


            if (gridManager != null)
            {
                gridManager.UnregisterWindow(
                    gridController
                );
            }


            gridController.SetGridPlaced(
                false
            );


            gridController.SetHudGridEnabled(
                false
            );
        }


        gameObject.SetActive(
            false
        );
    }
}