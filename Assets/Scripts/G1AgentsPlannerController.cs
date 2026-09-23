using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(G1AgentsGoalPicker))]
[RequireComponent(typeof(G1AgentsLivePoseClient))]
public sealed class G1AgentsPlannerController :
    MonoBehaviour
{
    [Serializable]
    private sealed class RobotPreviewRequest
    {
        public float x;
        public float y;
        public float yaw_deg;
        public float speed;
    }

    [Serializable]
    private sealed class RobotGoalRequest
    {
        public float x;
        public float y;
        public float yaw_deg;
        public float speed;
        public string preview_id;
    }

    [Serializable]
    private sealed class CarGoalRequest
    {
        public float x;
        public float y;
        public float yaw_deg;
    }

    [Serializable]
    private sealed class BasicResponse
    {
        public bool success;
        public string error;
    }

    [Serializable]
    private sealed class RobotPreviewResponse
    {
        public bool success;
        public string error;
        public string preview_id;
        public string request_id;
        public float expires_in;
    }

    [Serializable]
    private sealed class CarPreviewResponse
    {
        public bool success;
        public string error;
        public string request_id;
    }

    [Header("References")]
    [SerializeField]
    private G1AgentsGoalPicker goalPicker;

    [SerializeField]
    private G1AgentsLivePoseClient livePoseClient;

    [SerializeField]
    private G1AgentsWindowView agentsView;

    private G1AgentsWorldClient worldClient;

    [SerializeField]
    private HudWindow hudWindow;

    [Header("G1 navigation")]
    [SerializeField, Range(0.1f, 1.0f)]
    private float robotSpeed = 0.22f;

    [SerializeField, Min(10.0f)]
    private float robotTimeoutSeconds = 120.0f;

    [Header("Safety")]
    [Tooltip(
        "Leave disabled for the first preview test. " +
        "When disabled, HOLD GO cannot move either agent."
    )]
    [SerializeField]
    private bool commandConfirmationEnabled;

    [SerializeField, Min(0.5f)]
    private float confirmationHoldSeconds = 1.0f;

    [SerializeField, Min(1)]
    private int requestTimeoutSeconds = 20;

    [Header("Diagnostics")]
    [SerializeField]
    private bool verboseLogging = true;

    private RectTransform uiRoot;
    private RectTransform viewUiRoot;
    private RectTransform mapUiRoot;
    private TMP_Text statusLabel;

    private Button robotButton;
    private Button vehicleButton;
    private Button setGoalButton;
    private Button previewButton;
    private Button confirmButton;
    private Button clearButton;
    private Button viewModeButton;
    private Button zoomInButton;
    private Button zoomOutButton;
    private Button resetViewButton;
    private Button pointCloudToggleButton;
    private Button worldMapToggleButton;

    private TMP_Text robotLabel;
    private TMP_Text vehicleLabel;
    private TMP_Text setGoalLabel;
    private TMP_Text previewLabel;
    private TMP_Text confirmLabel;
    private TMP_Text clearLabel;
    private TMP_Text viewModeLabel;
    private TMP_Text zoomInLabel;
    private TMP_Text zoomOutLabel;
    private TMP_Text resetViewLabel;
    private TMP_Text pointCloudToggleLabel;
    private TMP_Text worldMapToggleLabel;

    private Image robotImage;
    private Image vehicleImage;
    private Image setGoalImage;
    private Image previewImage;
    private Image confirmImage;
    private Image clearImage;
    private Image viewModeImage;
    private Image zoomInImage;
    private Image zoomOutImage;
    private Image resetViewImage;
    private Image pointCloudToggleImage;
    private Image worldMapToggleImage;

    private HudCurvedButtonHitTarget confirmHit;

    private bool eventSubscribed;
    private bool requestBusy;
    private string robotPreviewId;
    private RobotPreviewRequest robotPreviewGoal;
    private string carPreviewRequestId;
    private bool carPreviewReady;
    private bool carPreviewAcknowledged;
    private int carPreviewBaselineRevision;

    private readonly Color robotColor =
        HudDashboardTheme.Green;

    private readonly Color vehicleColor =
        HudDashboardTheme.Orange;

    private readonly Color actionColor =
        HudDashboardTheme.GreenDim;

    private readonly Color readyColor =
        HudDashboardTheme.Green;

    private readonly Color dangerColor =
        HudDashboardTheme.Red;

    private readonly Color disabledColor =
        HudDashboardTheme.Control;

    private readonly Color viewControlColor =
        HudDashboardTheme.GreenDim;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribePicker();
    }

    private void Start()
    {
        ResolveReferences();
        SubscribePicker();

        goalPicker.SetSelectionEnabled(false);
        goalPicker.SelectRobotTarget();

        BuildUi();
        SetStatus(
            "Choose an agent, select its goal, then request a preview."
        );

        StartCoroutine(RefreshCurvatureNextFrame());
    }

    private void Update()
    {
        UpdateVehiclePreviewReadiness();
        RefreshUi();
    }

    private void OnDisable()
    {
        UnsubscribePicker();
        goalPicker?.SetSelectionEnabled(false);
    }

    private void OnDestroy()
    {
        UnsubscribePicker();

        if (uiRoot != null)
            Destroy(uiRoot.gameObject);

        if (viewUiRoot != null)
            Destroy(viewUiRoot.gameObject);

        if (mapUiRoot != null)
            Destroy(mapUiRoot.gameObject);
    }

    private void ResolveReferences()
    {
        if (goalPicker == null)
            goalPicker = GetComponent<G1AgentsGoalPicker>();

        if (livePoseClient == null)
            livePoseClient = GetComponent<G1AgentsLivePoseClient>();

        if (agentsView == null)
            agentsView = GetComponent<G1AgentsWindowView>();

        if (worldClient == null)
        {
            worldClient = GetComponent<G1AgentsWorldClient>();
            if (worldClient == null)
                worldClient = gameObject.AddComponent<G1AgentsWorldClient>();
        }

        if (hudWindow == null)
            hudWindow = GetComponentInParent<HudWindow>(true);
    }

    private void SubscribePicker()
    {
        if (eventSubscribed || goalPicker == null)
            return;

        goalPicker.SelectionCompleted += HandleSelectionCompleted;
        eventSubscribed = true;
    }

    private void UnsubscribePicker()
    {
        if (!eventSubscribed || goalPicker == null)
            return;

        goalPicker.SelectionCompleted -= HandleSelectionCompleted;
        eventSubscribed = false;
    }

    private void SelectRobot()
    {
        ChangeAgent(G1AgentsGoalPicker.TargetAgent.Robot);
    }

    private void SelectVehicle()
    {
        ChangeAgent(G1AgentsGoalPicker.TargetAgent.Vehicle);
    }

    private void ChangeAgent(
        G1AgentsGoalPicker.TargetAgent agent)
    {
        CancelPendingRobotPreview();
        InvalidatePreview();

        goalPicker.ClearSelection();
        goalPicker.SetSelectionEnabled(false);

        if (agent == G1AgentsGoalPicker.TargetAgent.Robot)
            goalPicker.SelectRobotTarget();
        else
            goalPicker.SelectVehicleTarget();

        SetStatus(
            agent == G1AgentsGoalPicker.TargetAgent.Robot
                ? "G1 selected. Press SET GOAL."
                : "Vehicle selected. Press SET GOAL."
        );
    }

    private void BeginGoalSelection()
    {
        if (requestBusy)
            return;

        CancelPendingRobotPreview();
        InvalidatePreview();
        goalPicker.ClearSelection();
        goalPicker.SetSelectionEnabled(true);
        agentsView?.SetInteractionEnabled(false);

        SetStatus(
            "Click the floor and drag toward the desired heading."
        );
    }

    private void HandleSelectionCompleted(
        G1AgentsGoalPicker picker)
    {
        picker.SetSelectionEnabled(false);
        agentsView?.SetInteractionEnabled(true);
        InvalidatePreview();

        SetStatus(
            picker.SelectedAgent + " goal: " +
            picker.SelectedMapPosition.x.ToString("F2") + ", " +
            picker.SelectedMapPosition.y.ToString("F2") +
            "  heading " +
            picker.SelectedYawDegrees.ToString("F1") + " deg. " +
            "Press PREVIEW."
        );
    }

    private void BeginPreview()
    {
        if (requestBusy)
            return;

        if (!goalPicker.HasSelection)
        {
            SetStatus(
                "Select a goal before requesting a preview."
            );
            return;
        }

        if (!SelectedAgentOnline())
        {
            SetStatus(
                goalPicker.SelectedAgent +
                " is offline; preview is unavailable."
            );
            return;
        }

        if (!HasConnection())
        {
            SetStatus("Agents dashboard connection is unavailable.");
            return;
        }

        InvalidatePreview();

        if (goalPicker.SelectedAgent ==
                G1AgentsGoalPicker.TargetAgent.Vehicle &&
            worldClient != null)
        {
            carPreviewBaselineRevision =
                worldClient.VehiclePathRevision;
        }

        requestBusy = true;
        SetStatus("Requesting planner preview...");

        if (goalPicker.SelectedAgent ==
            G1AgentsGoalPicker.TargetAgent.Robot)
        {
            RobotPreviewRequest payload =
                CurrentRobotGoal();

            robotPreviewGoal = payload;

            StartCoroutine(
                PostJson(
                    "/api/robot/path/preview",
                    JsonUtility.ToJson(payload),
                    HandleRobotPreview
                )
            );
        }
        else
        {
            CarGoalRequest payload = CurrentCarGoal();

            StartCoroutine(
                PostJson(
                    "/api/car/path/preview",
                    JsonUtility.ToJson(payload),
                    HandleCarPreview
                )
            );
        }
    }

    private void HandleRobotPreview(
        bool transportSuccess,
        string json,
        string transportError)
    {
        requestBusy = false;

        RobotPreviewResponse response =
            ParseResponse<RobotPreviewResponse>(json);

        string previewId =
            !string.IsNullOrWhiteSpace(
                response?.preview_id)
                ? response.preview_id
                : response?.request_id;

        if (!transportSuccess ||
            response == null ||
            !response.success ||
            string.IsNullOrWhiteSpace(previewId))
        {
            robotPreviewId = null;
            robotPreviewGoal = null;

            SetStatus(
                "G1 preview failed: " +
                ResponseError(response?.error, transportError)
            );
            LogWarning(statusLabel.text);
            return;
        }

        List<Vector2> route =
            ParseRobotRoutePoints(json);

        float routeLength = 0.0f;

        for (int index = 1;
             index < route.Count;
             index++)
        {
            routeLength +=
                Vector2.Distance(
                    route[index - 1],
                    route[index]);
        }

        if (route.Count < 2)
        {
            robotPreviewId = null;
            robotPreviewGoal = null;
            agentsView?.ClearPlannerPath();

            SetStatus(
                "G1 preview failed: planner returned " +
                "fewer than two valid route points."
            );
            LogWarning(statusLabel.text);
            return;
        }

        robotPreviewId = previewId;
        agentsView?.SetPlannerPath(
            route,
            HudDashboardTheme.Green
        );

        SetStatus(
            "G1 route ready: " +
            route.Count + " points, " +
            routeLength.ToString("F2") +
            " m." +
            (commandConfirmationEnabled
                ? " Hold GO to confirm."
                : " GO remains safety-locked.")
        );

        Log(
            "G1 preview ready id=" +
            robotPreviewId +
            " points=" +
            route.Count
        );
    }

    private static List<Vector2> ParseRobotRoutePoints(
        string json)
    {
        List<Vector2> points =
            new List<Vector2>();

        if (string.IsNullOrWhiteSpace(json))
            return points;

        int routeKey =
            json.IndexOf(
                "\"route\"",
                StringComparison.Ordinal);

        if (routeKey < 0)
            return points;

        int pointsKey =
            json.IndexOf(
                "\"points\"",
                routeKey,
                StringComparison.Ordinal);

        if (pointsKey < 0)
            return points;

        int outerStart =
            json.IndexOf('[', pointsKey);

        if (outerStart < 0)
            return points;

        int cursor = outerStart + 1;

        while (cursor < json.Length)
        {
            while (
                cursor < json.Length &&
                (char.IsWhiteSpace(json[cursor]) ||
                 json[cursor] == ','))
            {
                cursor++;
            }

            if (cursor >= json.Length ||
                json[cursor] == ']')
            {
                break;
            }

            if (json[cursor] != '[')
                break;

            int separator =
                json.IndexOf(',', cursor + 1);

            if (separator < 0)
                break;

            int pointEnd =
                json.IndexOf(']', separator + 1);

            if (pointEnd < 0)
                break;

            string xText =
                json.Substring(
                    cursor + 1,
                    separator - cursor - 1);

            string yText =
                json.Substring(
                    separator + 1,
                    pointEnd - separator - 1);

            if (
                float.TryParse(
                    xText,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float x) &&
                float.TryParse(
                    yText,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float y) &&
                !float.IsNaN(x) &&
                !float.IsInfinity(x) &&
                !float.IsNaN(y) &&
                !float.IsInfinity(y))
            {
                points.Add(new Vector2(x, y));
            }

            cursor = pointEnd + 1;
        }

        return points;
    }

    private void HandleCarPreview(
        bool transportSuccess,
        string json,
        string transportError)
    {
        requestBusy = false;

        CarPreviewResponse response =
            ParseResponse<CarPreviewResponse>(json);

        if (!transportSuccess ||
            response == null ||
            !response.success ||
            string.IsNullOrWhiteSpace(response.request_id))
        {
            SetStatus(
                "Vehicle preview failed: " +
                ResponseError(response?.error, transportError)
            );
            LogWarning(statusLabel.text);
            return;
        }

        carPreviewRequestId = response.request_id;
        carPreviewAcknowledged = true;
        carPreviewReady = false;

        SetStatus(
            "Vehicle planner accepted the request. " +
            "Waiting for the route to arrive..."
        );

        Log(
            "vehicle preview accepted request=" +
            carPreviewRequestId
        );
    }

    private void ConfirmGoal()
    {
        if (requestBusy)
            return;

        if (!commandConfirmationEnabled)
        {
            SetStatus(
                "GO is safety-locked. Preview testing cannot move an agent. " +
                "Enable Command Confirmation Enabled only after validation."
            );
            LogWarning("movement blocked by safety lock");
            return;
        }

        if (!PreviewReady())
        {
            SetStatus(
                "A successful preview is required before GO."
            );
            return;
        }

        if (!SelectedAgentOnline())
        {
            SetStatus(
                goalPicker.SelectedAgent +
                " went offline. Recalculate the preview."
            );
            InvalidatePreview();
            return;
        }

        requestBusy = true;

        if (goalPicker.SelectedAgent ==
            G1AgentsGoalPicker.TargetAgent.Robot)
        {
            if (robotPreviewGoal == null ||
                string.IsNullOrWhiteSpace(
                    robotPreviewId))
            {
                requestBusy = false;
                InvalidatePreview();

                SetStatus(
                    "The G1 preview expired locally. " +
                    "Request a new preview."
                );
                return;
            }

            RobotGoalRequest payload =
                new RobotGoalRequest
                {
                    x = robotPreviewGoal.x,
                    y = robotPreviewGoal.y,
                    yaw_deg =
                        robotPreviewGoal.yaw_deg,
                    speed =
                        robotPreviewGoal.speed,
                    preview_id =
                        robotPreviewId
                };

            SetStatus(
                "Confirming preview and starting " +
                "G1 navigation..."
            );

            StartCoroutine(
                PostJson(
                    "/api/robot/goal",
                    JsonUtility.ToJson(payload),
                    HandleRobotGoal
                )
            );
        }
        else
        {
            SetStatus("Sending vehicle goal...");

            StartCoroutine(
                PostJson(
                    "/api/car/goal",
                    JsonUtility.ToJson(CurrentCarGoal()),
                    HandleCarGoal
                )
            );
        }
    }

    private void HandleRobotGoal(
        bool transportSuccess,
        string json,
        string transportError)
    {
        requestBusy = false;

        BasicResponse response =
            ParseResponse<BasicResponse>(json);

        if (!transportSuccess ||
            response == null ||
            !response.success)
        {
            SetStatus(
                "G1 navigation rejected: " +
                ResponseError(response?.error, transportError)
            );
            LogWarning(statusLabel.text);
            robotPreviewId = null;
            robotPreviewGoal = null;
            agentsView?.ClearPlannerPath();
            return;
        }

        SetStatus("G1 navigation started.");
        Log("G1 navigation started");

        robotPreviewId = null;
        robotPreviewGoal = null;
        goalPicker.ClearSelection();
    }

    private void HandleCarGoal(
        bool transportSuccess,
        string json,
        string transportError)
    {
        requestBusy = false;

        BasicResponse response =
            ParseResponse<BasicResponse>(json);

        if (!transportSuccess ||
            response == null ||
            !response.success)
        {
            SetStatus(
                "Vehicle goal rejected: " +
                ResponseError(response?.error, transportError)
            );
            LogWarning(statusLabel.text);
            return;
        }

        SetStatus("Vehicle goal sent.");
        Log("vehicle goal sent");

        InvalidatePreview();
        goalPicker.ClearSelection();
    }

    private void ClearGoal()
    {
        CancelPendingRobotPreview();
        InvalidatePreview();

        goalPicker.SetSelectionEnabled(false);
        agentsView?.SetInteractionEnabled(true);
        goalPicker.ClearSelection();

        SetStatus(
            "Goal cleared. This does not stop an agent already moving."
        );
    }

    private void CancelPendingRobotPreview()
    {
        /*
         * The current robot backend replaces an older pending
         * preview whenever a new one is requested and expires
         * unused previews after 120 seconds. It exposes no
         * preview-cancellation endpoint.
         */
    }

    private void InvalidatePreview()
    {
        robotPreviewId = null;
        robotPreviewGoal = null;
        carPreviewRequestId = null;
        carPreviewReady = false;
        carPreviewAcknowledged = false;
        carPreviewBaselineRevision =
            worldClient != null
                ? worldClient.VehiclePathRevision
                : 0;
        agentsView?.ClearPlannerPath();
    }

    private void UpdateVehiclePreviewReadiness()
    {
        if (!carPreviewAcknowledged ||
            carPreviewReady ||
            worldClient == null)
        {
            return;
        }

        if (worldClient.VehiclePathRevision <=
                carPreviewBaselineRevision ||
            worldClient.VehiclePathPointCount < 2)
        {
            return;
        }

        carPreviewReady = true;

        SetStatus(
            "Vehicle route ready: " +
            worldClient.VehiclePathPointCount +
            " points." +
            (commandConfirmationEnabled
                ? " Hold GO to confirm."
                : " GO remains safety-locked.")
        );

        Log(
            "vehicle route rendered revision=" +
            worldClient.VehiclePathRevision
        );
    }

    private bool PreviewReady()
    {
        if (goalPicker == null)
            return false;

        if (goalPicker.SelectedAgent ==
            G1AgentsGoalPicker.TargetAgent.Robot)
        {
            return !string.IsNullOrWhiteSpace(robotPreviewId);
        }

        return carPreviewReady;
    }

    private bool SelectedAgentOnline()
    {
        if (livePoseClient == null || goalPicker == null)
            return false;

        return goalPicker.SelectedAgent ==
            G1AgentsGoalPicker.TargetAgent.Robot
                ? livePoseClient.RobotOnline
                : livePoseClient.VehicleOnline;
    }

    private bool HasConnection()
    {
        return
            livePoseClient != null &&
            livePoseClient.TryGetConnection(
                out _,
                out _
            );
    }

    private RobotPreviewRequest CurrentRobotGoal()
    {
        return new RobotPreviewRequest
        {
            x = goalPicker.SelectedMapPosition.x,
            y = goalPicker.SelectedMapPosition.y,
            yaw_deg =
                goalPicker.SelectedYawDegrees,
            speed = robotSpeed
        };
    }

    private CarGoalRequest CurrentCarGoal()
    {
        return new CarGoalRequest
        {
            x = goalPicker.SelectedMapPosition.x,
            y = goalPicker.SelectedMapPosition.y,
            yaw_deg = goalPicker.SelectedYawDegrees
        };
    }

    private IEnumerator GetJson(
        string endpoint,
        Action<bool, string, string> completed)
    {
        if (!livePoseClient.TryGetConnection(
                out string baseUrl,
                out string token))
        {
            completed(false, null, "connection unavailable");
            yield break;
        }

        using (
            UnityWebRequest request =
                UnityWebRequest.Get(baseUrl + endpoint)
        )
        {
            request.timeout =
                Mathf.Max(1, requestTimeoutSeconds);

            request.SetRequestHeader(
                "X-G1-Token",
                token
            );

            yield return request.SendWebRequest();

            bool success =
                request.result ==
                UnityWebRequest.Result.Success;

            completed(
                success,
                request.downloadHandler?.text,
                success
                    ? null
                    : request.error +
                      " (HTTP " +
                      request.responseCode + ")"
            );
        }
    }

    private IEnumerator PostJson(
        string endpoint,
        string json,
        Action<bool, string, string> completed)
    {
        if (!livePoseClient.TryGetConnection(
                out string baseUrl,
                out string token))
        {
            completed(false, null, "connection unavailable");
            yield break;
        }

        using (
            UnityWebRequest request =
                new UnityWebRequest(
                    baseUrl + endpoint,
                    UnityWebRequest.kHttpVerbPOST
                )
        )
        {
            request.uploadHandler =
                new UploadHandlerRaw(
                    Encoding.UTF8.GetBytes(json)
                );

            request.downloadHandler =
                new DownloadHandlerBuffer();

            request.timeout =
                Mathf.Max(1, requestTimeoutSeconds);

            request.SetRequestHeader(
                "Content-Type",
                "application/json"
            );

            request.SetRequestHeader(
                "X-G1-Token",
                token
            );

            yield return request.SendWebRequest();

            bool success =
                request.result ==
                UnityWebRequest.Result.Success;

            completed(
                success,
                request.downloadHandler?.text,
                success
                    ? null
                    : request.error +
                      " (HTTP " +
                      request.responseCode + ")"
            );
        }
    }

    private static T ParseResponse<T>(string json)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonUtility.FromJson<T>(json);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string ResponseError(
        string responseError,
        string transportError)
    {
        if (!string.IsNullOrWhiteSpace(responseError))
            return responseError;

        if (!string.IsNullOrWhiteSpace(transportError))
            return transportError;

        return "unexpected response";
    }

    private void BuildUi()
    {
        if (uiRoot != null)
            return;

        RectTransform parent = transform as RectTransform;
        if (parent == null)
        {
            Debug.LogError(
                "[G1 Agents Planner] viewport is not a RectTransform.",
                this
            );
            return;
        }

        GameObject rootObject =
            new GameObject(
                "AgentsPlannerControls",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );

        rootObject.transform.SetParent(parent, false);
        rootObject.transform.SetAsLastSibling();

        uiRoot = rootObject.GetComponent<RectTransform>();
        uiRoot.anchorMin = new Vector2(0.015f, 0.015f);
        uiRoot.anchorMax = new Vector2(0.985f, 0.255f);
        uiRoot.offsetMin = Vector2.zero;
        uiRoot.offsetMax = Vector2.zero;

        Image background = rootObject.GetComponent<Image>();
        background.color =
            HudDashboardTheme.PanelTranslucent;
        background.raycastTarget = false;

        statusLabel = CreateText(
            "Status",
            uiRoot,
            new Vector2(0.02f, 0.67f),
            new Vector2(0.98f, 0.97f),
            32.0f,
            TextAlignmentOptions.Center
        );

        robotButton = CreateButton(
            "RobotButton",
            uiRoot,
            new Vector2(0.02f, 0.35f),
            new Vector2(0.32f, 0.62f),
            out robotLabel,
            out robotImage,
            out _
        );
        robotLabel.text = "G1";
        robotButton.onClick.AddListener(SelectRobot);

        vehicleButton = CreateButton(
            "VehicleButton",
            uiRoot,
            new Vector2(0.35f, 0.35f),
            new Vector2(0.65f, 0.62f),
            out vehicleLabel,
            out vehicleImage,
            out _
        );
        vehicleLabel.text = "VEHICLE";
        vehicleButton.onClick.AddListener(SelectVehicle);

        setGoalButton = CreateButton(
            "SetGoalButton",
            uiRoot,
            new Vector2(0.68f, 0.35f),
            new Vector2(0.98f, 0.62f),
            out setGoalLabel,
            out setGoalImage,
            out _
        );
        setGoalLabel.text = "SET GOAL";
        setGoalButton.onClick.AddListener(BeginGoalSelection);

        previewButton = CreateButton(
            "PreviewButton",
            uiRoot,
            new Vector2(0.02f, 0.04f),
            new Vector2(0.32f, 0.31f),
            out previewLabel,
            out previewImage,
            out _
        );
        previewLabel.text = "PREVIEW";
        previewButton.onClick.AddListener(BeginPreview);

        confirmButton = CreateButton(
            "ConfirmButton",
            uiRoot,
            new Vector2(0.35f, 0.04f),
            new Vector2(0.65f, 0.31f),
            out confirmLabel,
            out confirmImage,
            out confirmHit
        );
        confirmButton.onClick.AddListener(ConfirmGoal);
        confirmHit.ConfigureHoldDuration(
            confirmationHoldSeconds
        );

        clearButton = CreateButton(
            "ClearButton",
            uiRoot,
            new Vector2(0.68f, 0.04f),
            new Vector2(0.98f, 0.31f),
            out clearLabel,
            out clearImage,
            out _
        );
        clearLabel.text = "CLEAR";
        clearButton.onClick.AddListener(ClearGoal);

        BuildViewControls(parent);
        BuildMapControls(parent);
    }

    private void BuildViewControls(RectTransform parent)
    {
        GameObject rootObject = new GameObject(
            "AgentsViewControls",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );

        rootObject.transform.SetParent(parent, false);
        rootObject.transform.SetAsLastSibling();

        viewUiRoot = rootObject.GetComponent<RectTransform>();
        viewUiRoot.anchorMin = new Vector2(0.48f, 0.89f);
        viewUiRoot.anchorMax = new Vector2(0.985f, 0.985f);
        viewUiRoot.offsetMin = Vector2.zero;
        viewUiRoot.offsetMax = Vector2.zero;

        Image background = rootObject.GetComponent<Image>();
        background.color =
            HudDashboardTheme.WithAlpha(
                HudDashboardTheme.Panel,
                0.90f);
        background.raycastTarget = false;

        viewModeButton = CreateButton(
            "ViewModeButton",
            viewUiRoot,
            new Vector2(0.01f, 0.10f),
            new Vector2(0.31f, 0.90f),
            out viewModeLabel,
            out viewModeImage,
            out _
        );
        viewModeButton.onClick.AddListener(
            agentsView.ToggleDragMode
        );

        zoomInButton = CreateButton(
            "ViewZoomInButton",
            viewUiRoot,
            new Vector2(0.33f, 0.10f),
            new Vector2(0.54f, 0.90f),
            out zoomInLabel,
            out zoomInImage,
            out _
        );
        zoomInLabel.text = "+";
        zoomInButton.onClick.AddListener(agentsView.ZoomIn);

        zoomOutButton = CreateButton(
            "ViewZoomOutButton",
            viewUiRoot,
            new Vector2(0.56f, 0.10f),
            new Vector2(0.77f, 0.90f),
            out zoomOutLabel,
            out zoomOutImage,
            out _
        );
        zoomOutLabel.text = "-";
        zoomOutButton.onClick.AddListener(agentsView.ZoomOut);

        resetViewButton = CreateButton(
            "ViewResetButton",
            viewUiRoot,
            new Vector2(0.79f, 0.10f),
            new Vector2(0.99f, 0.90f),
            out resetViewLabel,
            out resetViewImage,
            out _
        );
        resetViewLabel.text = "RESET";
        resetViewButton.onClick.AddListener(
            agentsView.ResetCameraView
        );

        ApplyViewControlTypography(viewModeLabel);
        ApplyViewControlTypography(zoomInLabel);
        ApplyViewControlTypography(zoomOutLabel);
        ApplyViewControlTypography(resetViewLabel);
    }


    private static void ApplyViewControlTypography(
        TMP_Text label)
    {
        if (label == null)
            return;

        label.color = Color.white;
        label.fontStyle = FontStyles.Bold;
    }


    private void BuildMapControls(RectTransform parent)
    {
        GameObject rootObject = new GameObject(
            "AgentsMapControls",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );

        rootObject.transform.SetParent(parent, false);
        rootObject.transform.SetAsLastSibling();

        mapUiRoot = rootObject.GetComponent<RectTransform>();

        // Top-left, opposite the view controls.
        mapUiRoot.anchorMin =
            new Vector2(0.015f, 0.89f);
        mapUiRoot.anchorMax =
            new Vector2(0.465f, 0.985f);

        mapUiRoot.offsetMin = Vector2.zero;
        mapUiRoot.offsetMax = Vector2.zero;

        Image background = rootObject.GetComponent<Image>();
        background.color =
            HudDashboardTheme.WithAlpha(
                HudDashboardTheme.Panel,
                0.90f);
        background.raycastTarget = false;

        pointCloudToggleButton = CreateButton(
            "PointCloudToggleButton",
            mapUiRoot,
            new Vector2(0.01f, 0.10f),
            new Vector2(0.49f, 0.90f),
            out pointCloudToggleLabel,
            out pointCloudToggleImage,
            out _
        );

        pointCloudToggleButton.onClick.AddListener(
            agentsView.ToggleStaticPointCloudVisibility
        );

        worldMapToggleButton = CreateButton(
            "WorldMapToggleButton",
            mapUiRoot,
            new Vector2(0.51f, 0.10f),
            new Vector2(0.99f, 0.90f),
            out worldMapToggleLabel,
            out worldMapToggleImage,
            out _
        );

        worldMapToggleButton.onClick.AddListener(
            agentsView.ToggleWorldMapVisibility
        );
    }


    private static TMP_Text CreateText(
        string name,
        RectTransform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject textObject =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI)
            );

        textObject.transform.SetParent(parent, false);

        RectTransform rect =
            textObject.GetComponent<RectTransform>();

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI text =
            textObject.GetComponent<TextMeshProUGUI>();

        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = HudDashboardTheme.TextPrimary;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;

        return text;
    }

    private static Button CreateButton(
        string name,
        RectTransform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        out TMP_Text label,
        out Image image,
        out HudCurvedButtonHitTarget hitTarget)
    {
        GameObject buttonObject =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(HudCurvedButtonHitTarget)
            );

        buttonObject.transform.SetParent(parent, false);

        RectTransform rect =
            buttonObject.GetComponent<RectTransform>();

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        image = buttonObject.GetComponent<Image>();
        image.color = HudDashboardTheme.Control;
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        button.navigation =
            new Navigation
            {
                mode = Navigation.Mode.None
            };

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor =
            Color.Lerp(
                Color.white,
                HudDashboardTheme.Green,
                0.24f);
        colors.pressedColor =
            Color.Lerp(
                Color.white,
                HudDashboardTheme.Green,
                0.48f);
        colors.selectedColor = Color.white;
        colors.disabledColor =
            HudDashboardTheme.WithAlpha(
                HudDashboardTheme.Control,
                0.72f);
        colors.colorMultiplier = 1.0f;
        button.colors = colors;

        label = CreateText(
            "Label",
            rect,
            Vector2.zero,
            Vector2.one,
            36.8f,
            TextAlignmentOptions.Center
        );

        label.fontStyle = FontStyles.Bold;

        hitTarget =
            buttonObject.GetComponent<
                HudCurvedButtonHitTarget>();

        return button;
    }

    private IEnumerator RefreshCurvatureNextFrame()
    {
        yield return null;
        yield return null;

        if (hudWindow != null)
            hudWindow.RefreshCurvedVisuals();
    }

    private void RefreshUi()
    {
        if (uiRoot == null ||
            goalPicker == null ||
            livePoseClient == null)
        {
            return;
        }

        bool robotSelected =
            goalPicker.SelectedAgent ==
            G1AgentsGoalPicker.TargetAgent.Robot;

        bool viewInteractionAllowed =
            !goalPicker.SelectionEnabled;

        agentsView?.SetInteractionEnabled(
            viewInteractionAllowed
        );

        if (viewModeLabel != null && agentsView != null)
        {
            viewModeLabel.text =
                agentsView.PanMode ? "PAN" : "ROTATE";
        }

        if (agentsView != null)
        {
            if (pointCloudToggleLabel != null)
            {
                pointCloudToggleLabel.text =
                    agentsView.StaticPointCloudVisible
                        ? "PCD ON"
                        : "PCD OFF";
            }

            if (worldMapToggleLabel != null)
            {
                worldMapToggleLabel.text =
                    agentsView.WorldMapVisible
                        ? "2D MAP ON"
                        : "2D MAP OFF";
            }
        }

        Color viewColor =
            viewInteractionAllowed
                ? viewControlColor
                : disabledColor;

        SetButtonColor(viewModeImage, viewColor);
        SetButtonColor(zoomInImage, viewColor);
        SetButtonColor(zoomOutImage, viewColor);
        SetButtonColor(resetViewImage, viewColor);

        if (agentsView != null)
        {
            SetButtonColor(
                pointCloudToggleImage,
                agentsView.StaticPointCloudVisible
                    ? actionColor
                    : disabledColor
            );

            SetButtonColor(
                worldMapToggleImage,
                agentsView.WorldMapVisible
                    ? actionColor
                    : disabledColor
            );
        }

        if (pointCloudToggleButton != null)
            pointCloudToggleButton.interactable = true;

        if (worldMapToggleButton != null)
            worldMapToggleButton.interactable = true;

        SetButtonColor(
            robotImage,
            robotSelected ? robotColor : disabledColor
        );

        SetButtonColor(
            vehicleImage,
            robotSelected ? disabledColor : vehicleColor
        );

        robotButton.interactable = !requestBusy;
        vehicleButton.interactable = !requestBusy;
        setGoalButton.interactable = !requestBusy;

        /*
         * Keep these physical hit targets present while idle.
         * Otherwise the XR ray passes through a disabled button
         * and snaps to another control on the curved surface.
         */
        previewButton.interactable = !requestBusy;
        confirmButton.interactable = !requestBusy;
        clearButton.interactable = !requestBusy;

        SetButtonColor(setGoalImage, actionColor);

        SetButtonColor(
            previewImage,
            goalPicker.HasSelection &&
            SelectedAgentOnline()
                ? actionColor
                : disabledColor
        );

        SetButtonColor(
            confirmImage,
            PreviewReady() &&
            commandConfirmationEnabled
                ? readyColor
                : disabledColor
        );

        SetButtonColor(
            clearImage,
            goalPicker.HasSelection ||
            PreviewReady()
                ? dangerColor
                : disabledColor
        );

        if (requestBusy)
        {
            confirmLabel.text = "WAIT";
        }
        else if (!commandConfirmationEnabled)
        {
            confirmLabel.text = "GO LOCKED";
        }
        else if (
            confirmHit != null &&
            confirmHit.HoldProgress01 > 0.0f
        )
        {
            confirmLabel.text =
                "HOLD " +
                Mathf.RoundToInt(
                    confirmHit.HoldProgress01 * 100.0f
                ) +
                "%";
        }
        else
        {
            confirmLabel.text = "HOLD GO";
        }
    }

    private static void SetButtonColor(
        Image image,
        Color color)
    {
        if (image != null)
            image.color = color;
    }

    private void SetStatus(string message)
    {
        if (statusLabel != null)
            statusLabel.text = message;
    }

    private void Log(string message)
    {
        if (verboseLogging)
        {
            Debug.Log(
                "[G1 Agents Planner] " + message,
                this
            );
        }
    }

    private void LogWarning(string message)
    {
        if (verboseLogging)
        {
            Debug.LogWarning(
                "[G1 Agents Planner] " + message,
                this
            );
        }
    }
}
