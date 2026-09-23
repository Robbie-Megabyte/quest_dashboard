using UnityEngine;

[DisallowMultipleComponent]
public sealed class HudBottomControlsLayout : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField]
    private GameObject menuButton;

    [SerializeField]
    private GameObject presetsButton;

    [SerializeField]
    private GameObject modeButton;

    [SerializeField]
    private GameObject viewControlsButton;


    [Header("Individual Widths")]
    [Min(0.01f)]
    [SerializeField]
    private float menuButtonWidth = 0.100f;

    [Min(0.01f)]
    [SerializeField]
    private float presetsButtonWidth = 0.135f;

    [Min(0.01f)]
    [SerializeField]
    private float modeButtonWidth = 0.100f;

    [Min(0.01f)]
    [SerializeField]
    private float viewControlsButtonWidth = 0.125f;


    [Header("Layout")]
    [Min(0.0f)]
    [SerializeField]
    private float spacing = 0.020f;


    private bool lastMenuActive;
    private bool lastPresetsActive;
    private bool lastModeActive;
    private bool lastViewControlsActive;


    private void Awake()
    {
        RebindReferences();
        ApplyLayout();
    }


    private void OnEnable()
    {
        RebindReferences();
        ApplyLayout();
    }


    private void LateUpdate()
    {
        bool menuActive = IsActive(menuButton);
        bool presetsActive = IsActive(presetsButton);
        bool modeActive = IsActive(modeButton);
        bool viewControlsActive =
            IsActive(viewControlsButton);

        if (
            menuActive == lastMenuActive &&
            presetsActive == lastPresetsActive &&
            modeActive == lastModeActive &&
            viewControlsActive ==
                lastViewControlsActive)
        {
            return;
        }

        ApplyLayout();
    }


    public void ApplyLayout()
    {
        RebindReferences();

        GameObject[] orderedButtons =
        {
            menuButton,
            presetsButton,
            modeButton,
            viewControlsButton
        };

        float[] orderedWidths =
        {
            menuButtonWidth,
            presetsButtonWidth,
            modeButtonWidth,
            viewControlsButtonWidth
        };

        int activeCount = 0;
        float totalWidth = 0.0f;

        for (int i = 0; i < orderedButtons.Length; i++)
        {
            if (!IsActive(orderedButtons[i]))
                continue;

            totalWidth += orderedWidths[i];
            activeCount++;
        }

        if (activeCount > 1)
        {
            totalWidth +=
                spacing *
                (activeCount - 1);
        }

        float cursor =
            -0.5f * totalWidth;

        for (int i = 0; i < orderedButtons.Length; i++)
        {
            GameObject button =
                orderedButtons[i];

            if (!IsActive(button))
                continue;

            float width =
                orderedWidths[i];

            Vector3 localPosition =
                button.transform.localPosition;

            localPosition.x =
                cursor +
                width * 0.5f;

            button.transform.localPosition =
                localPosition;

            cursor +=
                width +
                spacing;
        }

        lastMenuActive = IsActive(menuButton);
        lastPresetsActive = IsActive(presetsButton);
        lastModeActive = IsActive(modeButton);
        lastViewControlsActive =
            IsActive(viewControlsButton);
    }


    private void RebindReferences()
    {
        Transform root = transform;

        if (menuButton == null)
        {
            Transform found =
                root.Find("MenuButton");

            if (found != null)
                menuButton = found.gameObject;
        }

        if (presetsButton == null)
        {
            Transform found =
                root.Find("PresetsButton");

            if (found != null)
                presetsButton = found.gameObject;
        }

        if (modeButton == null)
        {
            Transform found =
                root.Find("ModeButton");

            if (found != null)
                modeButton = found.gameObject;
        }

        if (viewControlsButton == null)
        {
            Transform found =
                root.Find("ViewControlsButton");

            if (found != null)
            {
                viewControlsButton =
                    found.gameObject;
            }
        }
    }


    private static bool IsActive(
        GameObject target)
    {
        return
            target != null &&
            target.activeSelf;
    }
}
