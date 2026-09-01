using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class G1TelemetryWindowView :
    MonoBehaviour
{
    private const string Good = "#45E6A1";
    private const string Warning = "#FFD166";
    private const string Bad = "#FF5D73";
    private const string Muted = "#86A2B6";
    private const string Value = "#FFFFFF";

    [Header("Data")]
    [SerializeField]
    private G1DashboardTelemetryClient client;

    [Header("Typography")]
    [SerializeField]
    private TMP_FontAsset fontAsset;

    [Header("Responsive Layout")]
    [SerializeField]
    [Min(300.0f)]
    private float twoColumnThreshold = 500.0f;

    [SerializeField]
    [Min(0.1f)]
    private float displayRefreshSeconds = 0.25f;

    private RectTransform root;
    private RectTransform viewport;
    private RectTransform content;

    private Image statusBackground;
    private TMP_Text statusText;
    private GridLayoutGroup grid;
    private ScrollRect scrollRect;

    private TMP_Text connectionBody;
    private TMP_Text motionBody;
    private TMP_Text armsBody;
    private TMP_Text motorsBody;
    private TMP_Text handsBody;
    private TMP_Text computerBody;

    private bool interfaceBuilt;
    private float lastViewportWidth = -1.0f;
    private float nextDisplayRefresh;

    private static readonly Color CardColor =
        new Color32(12, 29, 42, 235);

    private static readonly Color GoodBackground =
        new Color32(10, 67, 54, 235);

    private static readonly Color WarningBackground =
        new Color32(76, 59, 16, 235);

    private static readonly Color BadBackground =
        new Color32(76, 24, 34, 235);

    private static readonly Color OfflineBackground =
        new Color32(31, 44, 55, 235);

    private void Awake()
    {
        root = GetComponent<RectTransform>();

        TMP_Text existingText =
            GetComponent<TMP_Text>();

        if (
            fontAsset == null &&
            existingText != null
        )
        {
            fontAsset = existingText.font;
        }

        if (fontAsset == null)
        {
            fontAsset =
                TMP_Settings.defaultFontAsset;
        }

        if (existingText != null)
        {
            existingText.enabled = false;
        }

        BuildInterface();
    }

    private void OnEnable()
    {
        nextDisplayRefresh = 0.0f;

        if (interfaceBuilt)
        {
            RefreshDisplay();
        }
    }

    private void Update()
    {
        if (!interfaceBuilt)
            return;

        if (
            Time.unscaledTime >=
            nextDisplayRefresh
        )
        {
            nextDisplayRefresh =
                Time.unscaledTime +
                Mathf.Max(
                    0.1f,
                    displayRefreshSeconds
                );

            RefreshDisplay();
        }
    }

    private void LateUpdate()
    {
        if (
            !interfaceBuilt ||
            viewport == null
        )
        {
            return;
        }

        float width =
            viewport.rect.width;

        if (
            width > 1.0f &&
            Mathf.Abs(
                width -
                lastViewportWidth
            ) > 0.5f
        )
        {
            ApplyResponsiveLayout();
        }
    }

    private void BuildInterface()
    {
        if (interfaceBuilt)
            return;

        interfaceBuilt = true;

        CreateStatusStrip();
        CreateScrollArea();

        connectionBody =
            CreateCard("CONNECTION");

        motionBody =
            CreateCard("MOTION");

        armsBody =
            CreateCard("ARMS");

        motorsBody =
            CreateCard("MOTORS");

        handsBody =
            CreateCard("HANDS & TRACKING");

        computerBody =
            CreateCard("COMPUTER");

        Canvas.ForceUpdateCanvases();
        ApplyResponsiveLayout();
        RefreshDisplay();

        // Cards and labels are created during Awake, so HudWindow's own
        // Awake may have completed before these graphics existed. Apply
        // the shared window/grid curvature after the interface is built.
        HudWindow hudWindow =
            GetComponentInParent<HudWindow>(true);

        if (hudWindow != null)
            hudWindow.RefreshCurvedVisuals();
    }

    private void CreateStatusStrip()
    {
        GameObject strip =
            new GameObject(
                "TelemetryStatus",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );

        strip.transform.SetParent(
            root,
            false
        );

        RectTransform rect =
            strip.GetComponent<RectTransform>();

        rect.anchorMin =
            new Vector2(0.0f, 1.0f);

        rect.anchorMax =
            new Vector2(1.0f, 1.0f);

        rect.pivot =
            new Vector2(0.5f, 1.0f);

        rect.anchoredPosition =
            Vector2.zero;

        rect.sizeDelta =
            new Vector2(0.0f, 44.0f);

        statusBackground =
            strip.GetComponent<Image>();

        statusBackground.color =
            OfflineBackground;

        statusBackground.raycastTarget =
            false;

        statusText =
            CreateText(
                "StatusText",
                strip.transform,
                18.0f,
                13.0f,
                20.0f,
                TextAlignmentOptions.MidlineLeft,
                false
            );

        Stretch(
            statusText.rectTransform,
            12.0f,
            8.0f,
            12.0f,
            8.0f
        );

        statusText.text =
            "WAITING FOR TELEMETRY";
    }

    private void CreateScrollArea()
    {
        GameObject viewportObject =
            new GameObject(
                "TelemetryViewport",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(RectMask2D),
                typeof(ScrollRect)
            );

        viewportObject.transform.SetParent(
            root,
            false
        );

        viewport =
            viewportObject.GetComponent
                <RectTransform>();

        Stretch(
            viewport,
            0.0f,
            0.0f,
            0.0f,
            52.0f
        );

        Image viewportImage =
            viewportObject.GetComponent<Image>();

        viewportImage.color =
            new Color(0.0f, 0.0f, 0.0f, 0.01f);

        viewportImage.raycastTarget =
            true;

        GameObject contentObject =
            new GameObject(
                "TelemetryCards",
                typeof(RectTransform),
                typeof(GridLayoutGroup)
            );

        contentObject.transform.SetParent(
            viewport,
            false
        );

        content =
            contentObject.GetComponent
                <RectTransform>();

        content.anchorMin =
            new Vector2(0.0f, 1.0f);

        content.anchorMax =
            new Vector2(1.0f, 1.0f);

        content.pivot =
            new Vector2(0.5f, 1.0f);

        content.anchoredPosition =
            Vector2.zero;

        content.sizeDelta =
            Vector2.zero;

        grid =
            contentObject.GetComponent
                <GridLayoutGroup>();

        grid.startCorner =
            GridLayoutGroup.Corner.UpperLeft;

        grid.startAxis =
            GridLayoutGroup.Axis.Horizontal;

        grid.childAlignment =
            TextAnchor.UpperLeft;

        grid.constraint =
            GridLayoutGroup.Constraint
                .FixedColumnCount;

        grid.constraintCount = 2;

        grid.padding =
            new RectOffset(8, 8, 8, 8);

        grid.spacing =
            new Vector2(10.0f, 10.0f);

        scrollRect =
            viewportObject.GetComponent
                <ScrollRect>();

        scrollRect.content = content;
        scrollRect.viewport = viewport;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        scrollRect.movementType =
            ScrollRect.MovementType.Clamped;

        scrollRect.inertia = true;
        scrollRect.decelerationRate = 0.12f;
        scrollRect.scrollSensitivity = 32.0f;
        scrollRect.verticalNormalizedPosition = 1.0f;
    }

    private TMP_Text CreateCard(
        string heading)
    {
        GameObject card =
            new GameObject(
                heading.Replace(" ", "") +
                "Card",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );

        card.transform.SetParent(
            content,
            false
        );

        Image image =
            card.GetComponent<Image>();

        image.color = CardColor;
        image.raycastTarget = false;

        TMP_Text title =
            CreateText(
                "Heading",
                card.transform,
                18.0f,
                14.0f,
                19.0f,
                TextAlignmentOptions.TopLeft,
                false
            );

        RectTransform titleRect =
            title.rectTransform;

        titleRect.anchorMin =
            new Vector2(0.0f, 1.0f);

        titleRect.anchorMax =
            new Vector2(1.0f, 1.0f);

        titleRect.pivot =
            new Vector2(0.5f, 1.0f);

        titleRect.anchoredPosition =
            new Vector2(0.0f, -8.0f);

        titleRect.sizeDelta =
            new Vector2(-20.0f, 24.0f);

        title.text =
            $"<color={Muted}>{heading}</color>";

        TMP_Text body =
            CreateText(
                "Values",
                card.transform,
                16.0f,
                11.0f,
                17.0f,
                TextAlignmentOptions.TopLeft,
                true
            );

        Stretch(
            body.rectTransform,
            10.0f,
            8.0f,
            10.0f,
            36.0f
        );

        body.text =
            Row("State", "WAITING");

        return body;
    }

    private TMP_Text CreateText(
        string objectName,
        Transform parent,
        float size,
        float minimumSize,
        float maximumSize,
        TextAlignmentOptions alignment,
        bool wrapping)
    {
        GameObject textObject =
            new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI)
            );

        textObject.transform.SetParent(
            parent,
            false
        );

        TextMeshProUGUI text =
            textObject.GetComponent
                <TextMeshProUGUI>();

        if (fontAsset != null)
            text.font = fontAsset;

        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMin = minimumSize;
        text.fontSizeMax = maximumSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.richText = true;
        text.raycastTarget = false;

        text.textWrappingMode =
            wrapping
                ? TextWrappingModes.Normal
                : TextWrappingModes.NoWrap;

        text.overflowMode =
            TextOverflowModes.Ellipsis;

        return text;
    }

    private void ApplyResponsiveLayout()
    {
        if (
            viewport == null ||
            grid == null ||
            content == null
        )
        {
            return;
        }

        float width =
            viewport.rect.width;

        if (width <= 1.0f)
            return;

        lastViewportWidth = width;

        int columns =
            width >= twoColumnThreshold
                ? 2
                : 1;

        float horizontalPadding =
            grid.padding.left +
            grid.padding.right;

        float spacing =
            grid.spacing.x *
            Mathf.Max(0, columns - 1);

        float cellWidth =
            (
                width -
                horizontalPadding -
                spacing
            ) /
            columns;

        float cellHeight =
            columns == 1
                ? 150.0f
                : 142.0f;

        grid.constraintCount = columns;

        grid.cellSize =
            new Vector2(
                Mathf.Max(100.0f, cellWidth),
                cellHeight
            );

        const int cardCount = 6;

        int rows =
            Mathf.CeilToInt(
                cardCount /
                (float)columns
            );

        float contentHeight =
            grid.padding.top +
            grid.padding.bottom +
            rows * cellHeight +
            Mathf.Max(0, rows - 1) *
            grid.spacing.y;

        content.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            contentHeight
        );

        LayoutRebuilder
            .ForceRebuildLayoutImmediate(content);
    }

    private void RefreshDisplay()
    {
        if (client == null)
        {
            SetStatus(
                "TELEMETRY CLIENT NOT ASSIGNED",
                BadBackground
            );

            SetEveryCardWaiting(
                "Assign the client from G1_ViewerManager."
            );

            return;
        }

        if (
            !client.TransportFresh ||
            !client.HasData
        )
        {
            string reason =
                string.IsNullOrWhiteSpace(
                    client.LastError
                )
                    ? "Waiting for dashboard"
                    : client.LastError;

            SetStatus(
                "DASHBOARD TELEMETRY OFFLINE",
                BadBackground
            );

            SetEveryCardWaiting(reason);
            return;
        }

        G1DashboardTelemetryClient.Envelope data =
            client.Latest;

        bool robotOnline =
            data.connection != null &&
            data.connection.online;

        bool computerOnline =
            data.computer != null &&
            data.computer.online;

        RefreshStatus(
            data,
            robotOnline,
            computerOnline
        );

        RefreshConnection(
            data,
            robotOnline
        );

        RefreshMotion(
            data,
            robotOnline
        );

        RefreshArms(
            data,
            robotOnline
        );

        RefreshMotors(
            data,
            robotOnline
        );

        RefreshHands(
            data,
            robotOnline
        );

        RefreshComputer(
            data,
            computerOnline
        );
    }

    private void RefreshStatus(
        G1DashboardTelemetryClient.Envelope data,
        bool robotOnline,
        bool computerOnline)
    {
        G1DashboardTelemetryClient.Control control =
            data.control;

        if (
            control != null &&
            !string.IsNullOrWhiteSpace(
                control.safety_fault
            )
        )
        {
            SetStatus(
                "SAFETY FAULT  •  " +
                ShortText(
                    control.safety_fault,
                    58
                ),
                BadBackground
            );

            return;
        }

        if (
            control != null &&
            (
                control.tracking_guard ||
                !string.IsNullOrWhiteSpace(
                    control.tracking_hold
                )
            )
        )
        {
            SetStatus(
                "TRACKING HOLD  •  " +
                Friendly(
                    control.state
                ),
                WarningBackground
            );

            return;
        }

        if (robotOnline)
        {
            float ageMilliseconds =
                Mathf.Max(
                    0.0f,
                    data.connection.packet_age_s *
                    1000.0f
                );

            SetStatus(
                $"ROBOT ONLINE  •  " +
                $"{Friendly(control?.state)}  •  " +
                $"{ageMilliseconds:0} ms",
                GoodBackground
            );

            return;
        }

        if (computerOnline)
        {
            SetStatus(
                "ROBOT TELEMETRY OFFLINE  •  COMPUTER ONLINE",
                WarningBackground
            );

            return;
        }

        SetStatus(
            "ROBOT AND COMPUTER OFFLINE",
            BadBackground
        );
    }

    private void RefreshConnection(
        G1DashboardTelemetryClient.Envelope data,
        bool online)
    {
        if (!online)
        {
            connectionBody.text =
                Row("Robot link", "OFFLINE", Bad) +
                "\n" +
                Row("Control", "—") +
                "\n" +
                Row("LowState", "—") +
                "\n" +
                Row("Packets", "—");

            return;
        }

        G1DashboardTelemetryClient.Control control =
            data.control;

        string lowState =
            control != null &&
            control.lowstate_ok
                ? $"OK · " +
                  $"{control.lowstate_age_s * 1000.0f:0} ms"
                : "FAULT";

        connectionBody.text =
            Row("Robot link", "ONLINE", Good) +
            "\n" +
            Row(
                "Control",
                Friendly(control?.state)
            ) +
            "\n" +
            Row(
                "LowState",
                lowState,
                control != null &&
                control.lowstate_ok
                    ? Good
                    : Bad
            ) +
            "\n" +
            Row(
                "Loop",
                $"{control?.main_loop_hz ?? 0.0f:0.0} Hz"
            ) +
            "\n" +
            Row(
                "Packets",
                data.connection.packet_count
                    .ToString("N0")
            );
    }

    private void RefreshMotion(
        G1DashboardTelemetryClient.Envelope data,
        bool online)
    {
        if (
            !online ||
            data.motion == null
        )
        {
            motionBody.text =
                Row("State", "NO ROBOT DATA");
            return;
        }

        motionBody.text =
            Row(
                "Base speed",
                $"{data.motion.base_speed_mps:0.00} m/s"
            ) +
            "\n" +
            Row(
                "Yaw rate",
                $"{data.motion.yaw_rate_rps * Mathf.Rad2Deg:0.0}°/s"
            ) +
            "\n" +
            Row(
                "Max joint speed",
                $"{data.motion.max_joint_speed_rps:0.00} rad/s"
            );
    }

    private void RefreshArms(
        G1DashboardTelemetryClient.Envelope data,
        bool online)
    {
        if (
            !online ||
            data.arms == null ||
            data.control == null
        )
        {
            armsBody.text =
                Row("State", "NO ROBOT DATA");
            return;
        }

        armsBody.text =
            Row(
                "Ownership",
                $"{data.control.arm_ownership * 100.0f:0}%"
            ) +
            "\n" +
            Row(
                "Tracking error",
                $"{data.arms.max_tracking_error_deg:0.0}°"
            ) +
            "\n" +
            Row(
                "Estimated torque",
                $"{data.arms.max_estimated_torque_nm:0.00} Nm"
            ) +
            "\n" +
            Row(
                "Publisher",
                data.arms.publisher_ok
                    ? "OK"
                    : "FAULT",
                data.arms.publisher_ok
                    ? Good
                    : Bad
            );
    }

    private void RefreshMotors(
        G1DashboardTelemetryClient.Envelope data,
        bool online)
    {
        if (
            !online ||
            data.motors == null
        )
        {
            motorsBody.text =
                Row("State", "NO ROBOT DATA");
            return;
        }

        float temperature =
            data.motors.hottest_temperature_c;

        string temperatureColor =
            temperature >= 75.0f
                ? Bad
                : temperature >= 65.0f
                    ? Warning
                    : Good;

        string faultColor =
            data.motors.fault_count == 0
                ? Good
                : Bad;

        motorsBody.text =
            Row(
                "Hottest",
                FriendlyJoint(
                    data.motors.hottest_joint
                )
            ) +
            "\n" +
            Row(
                "Temperature",
                $"{temperature:0.0}°C",
                temperatureColor
            ) +
            "\n" +
            Row(
                "Motor faults",
                data.motors.fault_count
                    .ToString(),
                faultColor
            );
    }

    private void RefreshHands(
        G1DashboardTelemetryClient.Envelope data,
        bool online)
    {
        if (
            !online ||
            data.hands == null ||
            data.control == null
        )
        {
            handsBody.text =
                Row("State", "NO ROBOT DATA");
            return;
        }

        string xrState =
            data.control.xr_ok
                ? "READY"
                : ShortText(
                    data.control.xr_reason,
                    34
                );

        string feedback =
            data.hands.feedback_fault
                ? "FAULT"
                : $"OK · " +
                  $"{data.hands.feedback_age_s * 1000.0f:0} ms";

        handsBody.text =
            Row(
                "Hand mode",
                Friendly(data.hands.mode)
            ) +
            "\n" +
            Row(
                "Hand tracking",
                data.hands.tracking_valid
                    ? "VALID"
                    : "WAITING",
                data.hands.tracking_valid
                    ? Good
                    : Warning
            ) +
            "\n" +
            Row(
                "Feedback",
                feedback,
                data.hands.feedback_fault
                    ? Bad
                    : Good
            ) +
            "\n" +
            Row(
                "XR",
                xrState,
                data.control.xr_ok
                    ? Good
                    : Warning
            ) +
            "\n" +
            Row(
                "Guard",
                data.control.tracking_guard
                    ? "ACTIVE"
                    : "CLEAR",
                data.control.tracking_guard
                    ? Warning
                    : Good
            );
    }

    private void RefreshComputer(
        G1DashboardTelemetryClient.Envelope data,
        bool online)
    {
        if (
            !online ||
            data.computer == null
        )
        {
            computerBody.text =
                Row("Computer", "OFFLINE", Bad);
            return;
        }

        G1DashboardTelemetryClient.Computer computer =
            data.computer;

        computerBody.text =
            Row(
                "CPU",
                $"{computer.cpu_used_pct:0.0}%",
                UsageColor(
                    computer.cpu_used_pct,
                    80.0f,
                    95.0f
                )
            ) +
            "\n" +
            Row(
                "RAM",
                $"{computer.ram_used_pct:0.0}%",
                UsageColor(
                    computer.ram_used_pct,
                    80.0f,
                    92.0f
                )
            ) +
            "\n" +
            Row(
                "Storage",
                $"{computer.disk_used_pct:0.0}%",
                UsageColor(
                    computer.disk_used_pct,
                    80.0f,
                    92.0f
                )
            ) +
            "\n" +
            Row(
                "Jetson maximum",
                $"{computer.maximum_temperature_c:0.0}°C",
                UsageColor(
                    computer.maximum_temperature_c,
                    70.0f,
                    82.0f
                )
            ) +
            "\n" +
            Row(
                "IMU",
                $"{computer.imu_temperature_c:0.0}°C",
                UsageColor(
                    computer.imu_temperature_c,
                    80.0f,
                    90.0f
                )
            ) +
            "\n" +
            Row(
                "Network",
                Friendly(
                    computer.network_state
                ),
                string.Equals(
                    computer.network_state,
                    "up",
                    System.StringComparison
                        .OrdinalIgnoreCase
                )
                    ? Good
                    : Bad
            );
    }

    private void SetEveryCardWaiting(
        string reason)
    {
        string text =
            Row(
                "State",
                ShortText(reason, 72),
                Warning
            );

        connectionBody.text = text;
        motionBody.text = text;
        armsBody.text = text;
        motorsBody.text = text;
        handsBody.text = text;
        computerBody.text = text;
    }

    private void SetStatus(
        string message,
        Color background)
    {
        statusBackground.color = background;
        statusText.text = Escape(message);
    }

    private static string Row(
        string label,
        string value,
        string valueColor = Value)
    {
        return
            $"<color={Muted}>" +
            $"{Escape(label)}</color>  " +
            $"<color={valueColor}>" +
            $"{Escape(value)}</color>";
    }

    private static string Friendly(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "—";

        return value
            .Replace('_', ' ')
            .Trim()
            .ToUpperInvariant();
    }

    private static string FriendlyJoint(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "—";

        return value
            .Replace('_', ' ')
            .Trim();
    }

    private static string ShortText(
        string value,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "—";

        string clean =
            value.Replace('\n', ' ').Trim();

        if (clean.Length <= maximumLength)
            return clean;

        return
            clean.Substring(
                0,
                Mathf.Max(
                    1,
                    maximumLength - 1
                )
            ) +
            "…";
    }

    private static string Escape(
        string value)
    {
        if (string.IsNullOrEmpty(value))
            return "—";

        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    private static string UsageColor(
        float value,
        float warningThreshold,
        float badThreshold)
    {
        if (value >= badThreshold)
            return Bad;

        if (value >= warningThreshold)
            return Warning;

        return Good;
    }

    private static void Stretch(
        RectTransform rect,
        float left,
        float bottom,
        float right,
        float top)
    {
        rect.anchorMin =
            Vector2.zero;

        rect.anchorMax =
            Vector2.one;

        rect.offsetMin =
            new Vector2(
                left,
                bottom
            );

        rect.offsetMax =
            new Vector2(
                -right,
                -top
            );
    }
}
