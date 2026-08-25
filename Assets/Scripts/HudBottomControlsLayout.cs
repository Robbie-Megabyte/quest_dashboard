using UnityEngine;

[DisallowMultipleComponent]
public class HudBottomControlsLayout : MonoBehaviour
{
    [Header("Automatically Resolved")]
    [SerializeField]
    private Transform menuButton;

    [SerializeField]
    private Transform modeButton;


    [Header("Layout")]
    [Tooltip("Physical width of each bottom button.")]
    public float buttonWidthMeters = 0.110f;

    [Tooltip("Empty space between MENU and EDIT.")]
    public float spacingMeters = 0.020f;

    public float localY = 0.0f;
    public float localZ = 0.0f;


    private bool initialized = false;
    private bool lastMenuActive;
    private bool lastModeActive;


    // ========================================================
    // UNITY
    // ========================================================

    void Awake()
    {
        RebindReferences();
        ApplyLayout();
    }


    void OnEnable()
    {
        RebindReferences();
        ApplyLayout();
    }


    void LateUpdate()
    {
        bool menuActive =
            menuButton != null &&
            menuButton.gameObject.activeSelf;


        bool modeActive =
            modeButton != null &&
            modeButton.gameObject.activeSelf;


        if (!initialized ||
            menuActive != lastMenuActive ||
            modeActive != lastModeActive)
        {
            ApplyLayout();
        }
    }


    void OnValidate()
    {
        RebindReferences();

        if (!Application.isPlaying)
        {
            ApplyLayout();
        }
    }


    // ========================================================
    // REFERENCES
    // ========================================================

    [ContextMenu("Rebind References")]
    public void RebindReferences()
    {
        Transform found;


        found =
            transform.Find("MenuButton");

        menuButton =
            found;


        found =
            transform.Find("ModeButton");

        modeButton =
            found;
    }


    // ========================================================
    // LAYOUT
    // ========================================================

    [ContextMenu("Apply Layout")]
    public void ApplyLayout()
    {
        RebindReferences();


        bool menuActive =
            menuButton != null &&
            menuButton.gameObject.activeSelf;


        bool modeActive =
            modeButton != null &&
            modeButton.gameObject.activeSelf;


        if (menuActive &&
            modeActive)
        {
            float halfCenterDistance =
                (
                    buttonWidthMeters +
                    spacingMeters
                ) *
                0.5f;


            SetButtonPose(
                menuButton,
                -halfCenterDistance
            );


            SetButtonPose(
                modeButton,
                halfCenterDistance
            );
        }
        else if (menuActive)
        {
            SetButtonPose(
                menuButton,
                0.0f
            );
        }
        else if (modeActive)
        {
            SetButtonPose(
                modeButton,
                0.0f
            );
        }


        lastMenuActive =
            menuActive;


        lastModeActive =
            modeActive;


        initialized =
            true;
    }


    private void SetButtonPose(
        Transform button,
        float localX)
    {
        if (button == null)
            return;


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