using UnityEngine;

[DisallowMultipleComponent]
public sealed class HudBottomControlsLayout : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private GameObject menuButton;
    [SerializeField] private GameObject presetsButton;
    [SerializeField] private GameObject modeButton;
    [SerializeField] private GameObject viewControlsButton;

    [Header("Layout")]
    [Min(0.01f)]
    [SerializeField] private float buttonWidth = 0.110f;

    [Min(0f)]
    [SerializeField] private float spacing = 0.020f;

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
        bool viewControlsActive = IsActive(viewControlsButton);

        if (menuActive == lastMenuActive &&
            presetsActive == lastPresetsActive &&
            modeActive == lastModeActive &&
            viewControlsActive == lastViewControlsActive)
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

        int activeCount = 0;

        for (int i = 0; i < orderedButtons.Length; i++)
        {
            if (IsActive(orderedButtons[i]))
                activeCount++;
        }

        if (activeCount > 0)
        {
            float step = buttonWidth + spacing;
            float firstX = -0.5f * step * (activeCount - 1);
            int visibleIndex = 0;

            for (int i = 0; i < orderedButtons.Length; i++)
            {
                GameObject button = orderedButtons[i];

                if (!IsActive(button))
                    continue;

                Transform buttonTransform = button.transform;
                Vector3 localPosition = buttonTransform.localPosition;

                localPosition.x = firstX + visibleIndex * step;
                buttonTransform.localPosition = localPosition;

                visibleIndex++;
            }
        }

        lastMenuActive = IsActive(menuButton);
        lastPresetsActive = IsActive(presetsButton);
        lastModeActive = IsActive(modeButton);
        lastViewControlsActive = IsActive(viewControlsButton);
    }

    private void RebindReferences()
    {
        Transform root = transform;

        if (menuButton == null)
        {
            Transform found = root.Find("MenuButton");

            if (found != null)
                menuButton = found.gameObject;
        }

        if (presetsButton == null)
        {
            Transform found = root.Find("PresetsButton");

            if (found != null)
                presetsButton = found.gameObject;
        }

        if (modeButton == null)
        {
            Transform found = root.Find("ModeButton");

            if (found != null)
                modeButton = found.gameObject;
        }

        if (viewControlsButton == null)
        {
            Transform found = root.Find("ViewControlsButton");

            if (found != null)
                viewControlsButton = found.gameObject;
        }
    }

    private static bool IsActive(GameObject target)
    {
        return target != null && target.activeSelf;
    }
}