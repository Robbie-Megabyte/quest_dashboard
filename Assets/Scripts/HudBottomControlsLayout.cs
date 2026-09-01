using UnityEngine;

[DisallowMultipleComponent]
public class HudBottomControlsLayout : MonoBehaviour
{
    [Header("Automatically Resolved")]
    [SerializeField]
    private Transform menuButton;

    [SerializeField]
    private Transform modeButton;

    [SerializeField]
    private Transform viewControlsButton;

    [Header("Layout")]
    [Tooltip("Physical width of each bottom button.")]
    public float buttonWidthMeters = 0.110f;

    [Tooltip("Empty space between adjacent buttons.")]
    public float spacingMeters = 0.020f;

    public float localY = 0.0f;
    public float localZ = 0.0f;

    private bool initialized;
    private bool lastMenuActive;
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
        bool menuActive =
            IsActive(menuButton);

        bool modeActive =
            IsActive(modeButton);

        bool viewControlsActive =
            IsActive(viewControlsButton);

        if (!initialized ||
            menuActive != lastMenuActive ||
            modeActive != lastModeActive ||
            viewControlsActive != lastViewControlsActive)
        {
            ApplyLayout();
        }
    }

    private void OnValidate()
    {
        RebindReferences();

        if (!Application.isPlaying)
        {
            ApplyLayout();
        }
    }

    [ContextMenu("Rebind References")]
    public void RebindReferences()
    {
        menuButton =
            transform.Find("MenuButton");

        modeButton =
            transform.Find("ModeButton");

        viewControlsButton =
            transform.Find("ViewControlsButton");
    }

    [ContextMenu("Apply Layout")]
    public void ApplyLayout()
    {
        RebindReferences();

        Transform[] buttons =
        {
            menuButton,
            modeButton,
            viewControlsButton
        };

        int activeCount = 0;

        foreach (Transform button in buttons)
        {
            if (IsActive(button))
            {
                activeCount++;
            }
        }

        float step =
            buttonWidthMeters +
            spacingMeters;

        float firstX =
            -0.5f *
            (activeCount - 1) *
            step;

        int activeIndex = 0;

        foreach (Transform button in buttons)
        {
            if (!IsActive(button))
                continue;

            SetButtonPose(
                button,
                firstX + activeIndex * step
            );

            activeIndex++;
        }

        lastMenuActive =
            IsActive(menuButton);

        lastModeActive =
            IsActive(modeButton);

        lastViewControlsActive =
            IsActive(viewControlsButton);

        initialized = true;
    }

    private static bool IsActive(
        Transform button)
    {
        return
            button != null &&
            button.gameObject.activeSelf;
    }

    private void SetButtonPose(
        Transform button,
        float localX)
    {
        button.localPosition =
            new Vector3(
                localX,
                localY,
                localZ
            );

        button.localRotation =
            Quaternion.identity;

        button.localScale =
            Vector3.one;
    }
}