using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

[DisallowMultipleComponent]
[RequireComponent(typeof(G1AgentsWindowView))]
public sealed class G1AgentsLivePoseClient : MonoBehaviour
{
    [Serializable]
    private sealed class ConnectionConfig
    {
        public string base_url;
        public string token;
    }

    [Serializable]
    private sealed class PoseEnvelope
    {
        public bool success;
        public string schema;
        public AgentState robot;
        public AgentState vehicle;
    }

    [Serializable]
    private sealed class AgentState
    {
        public bool online;
        public AgentPose pose;
        public string source;
    }

    [Serializable]
    private sealed class AgentPose
    {
        public float x;
        public float y;
        public float yaw;
    }

    [Header("References")]
    [SerializeField] private G1AgentsWindowView agentsView;

    [Header("Connection")]
    [SerializeField] private string defaultBaseUrl =
        "http://192.168.0.116:3003";
    [SerializeField] private string configFileName =
        "g1_agents_connection.json";
    [SerializeField, Min(0.05f)] private float pollingIntervalSeconds = 0.05f;
    [SerializeField, Min(1)] private int requestTimeoutSeconds = 2;

    [Header("Display")]
    [SerializeField, Min(0.1f)] private float poseSmoothing = 14.0f;

    [Header("Diagnostics")]
    [SerializeField] private bool verboseLogging = true;

    public bool TransportOnline { get; private set; }
    public bool RobotOnline { get; private set; }
    public bool VehicleOnline { get; private set; }
    public string LastError { get; private set; }

    public string ConfigurationPath => Path.Combine(
        Application.persistentDataPath,
        configFileName
    );

    public bool TryGetConnection(
        out string baseUrl,
        out string token)
    {
        baseUrl = activeBaseUrl;
        token = dashboardToken;

        return
            !string.IsNullOrWhiteSpace(baseUrl) &&
            !string.IsNullOrWhiteSpace(token);
    }

    private string activeBaseUrl;
    private string dashboardToken;
    private Coroutine pollingCoroutine;
    private bool livePoseActivated;
    private bool announcedConnection;

    private bool robotTargetValid;
    private bool vehicleTargetValid;
    private bool robotDisplayInitialized;
    private bool vehicleDisplayInitialized;

    private Vector2 robotTargetPosition;
    private Vector2 vehicleTargetPosition;
    private Vector2 robotDisplayPosition;
    private Vector2 vehicleDisplayPosition;

    private float robotTargetYawDegrees;
    private float vehicleTargetYawDegrees;
    private float robotDisplayYawDegrees;
    private float vehicleDisplayYawDegrees;
    private float nextWarningTime;

    private void Awake()
    {
        ResolveReferences();
        ReloadConfiguration();
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (string.IsNullOrWhiteSpace(activeBaseUrl))
            ReloadConfiguration();

        if (pollingCoroutine == null)
            pollingCoroutine = StartCoroutine(PollingLoop());
    }

    private void OnDisable()
    {
        if (pollingCoroutine != null)
        {
            StopCoroutine(pollingCoroutine);
            pollingCoroutine = null;
        }
        TransportOnline = false;
        RobotOnline = false;
        VehicleOnline = false;

        if (agentsView != null)
        {
            agentsView.SetAgentOnlineStates(
                false,
                false
            );
        }
    }

    private void Update()
    {
        if (agentsView == null)
            return;

        float blend = 1.0f - Mathf.Exp(
            -Mathf.Max(0.1f, poseSmoothing) * Time.unscaledDeltaTime
        );

        if (robotTargetValid)
        {
            SmoothPose(
                robotTargetPosition,
                robotTargetYawDegrees,
                blend,
                ref robotDisplayInitialized,
                ref robotDisplayPosition,
                ref robotDisplayYawDegrees
            );
            agentsView.SetRobotMapPose(
                robotDisplayPosition.x,
                robotDisplayPosition.y,
                robotDisplayYawDegrees
            );
        }

        if (vehicleTargetValid)
        {
            SmoothPose(
                vehicleTargetPosition,
                vehicleTargetYawDegrees,
                blend,
                ref vehicleDisplayInitialized,
                ref vehicleDisplayPosition,
                ref vehicleDisplayYawDegrees
            );
            agentsView.SetVehicleMapPose(
                vehicleDisplayPosition.x,
                vehicleDisplayPosition.y,
                vehicleDisplayYawDegrees
            );
        }
    }

    public void ReloadConfiguration()
    {
        activeBaseUrl = defaultBaseUrl.TrimEnd('/');
        dashboardToken = string.Empty;

        if (!File.Exists(ConfigurationPath))
        {
            LastError = "Configuration file is missing: " + ConfigurationPath;
            if (verboseLogging)
                Debug.LogWarning("[G1 Agents Live] " + LastError, this);
            return;
        }

        try
        {
            ConnectionConfig config = JsonUtility.FromJson<ConnectionConfig>(
                File.ReadAllText(ConfigurationPath)
            );
            if (config == null)
                throw new InvalidDataException("Configuration JSON is empty.");

            if (!string.IsNullOrWhiteSpace(config.base_url))
                activeBaseUrl = config.base_url.Trim().TrimEnd('/');

            dashboardToken = (config.token ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(dashboardToken))
                throw new InvalidDataException("The dashboard token is empty.");

            LastError = null;
            if (verboseLogging)
            {
                Debug.Log(
                    "[G1 Agents Live] configuration loaded url=" + activeBaseUrl,
                    this
                );
            }
        }
        catch (Exception exception)
        {
            dashboardToken = string.Empty;
            LastError = "Cannot load configuration: " + exception.Message;
            Debug.LogError("[G1 Agents Live] " + LastError, this);
        }
    }

    private IEnumerator PollingLoop()
    {
        while (isActiveAndEnabled)
        {
            if (string.IsNullOrWhiteSpace(dashboardToken))
            {
                RecordFailure("Dashboard token is unavailable.");
                yield return new WaitForSecondsRealtime(1.0f);
                continue;
            }

            yield return DownloadStatus();
            yield return new WaitForSecondsRealtime(
                Mathf.Max(0.05f, pollingIntervalSeconds)
            );
        }
        pollingCoroutine = null;
    }

    private IEnumerator DownloadStatus()
    {
        using UnityWebRequest request = UnityWebRequest.Get(
            activeBaseUrl + "/api/agents/status"
        );
        request.timeout = Mathf.Max(1, requestTimeoutSeconds);
        request.SetRequestHeader("X-G1-Token", dashboardToken);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            RecordFailure(
                request.error + " (HTTP " + request.responseCode + ")"
            );
            yield break;
        }

        PoseEnvelope envelope;
        try
        {
            envelope = JsonUtility.FromJson<PoseEnvelope>(
                request.downloadHandler.text
            );
        }
        catch (Exception exception)
        {
            RecordFailure("Invalid status JSON: " + exception.Message);
            yield break;
        }

        if (envelope == null || !envelope.success ||
            envelope.schema != "g1_multi_agent.pose.v1")
        {
            RecordFailure("Unexpected Agents status response.");
            yield break;
        }

        TransportOnline = true;
        LastError = null;
        RobotOnline = envelope.robot != null && envelope.robot.online;
        VehicleOnline = envelope.vehicle != null && envelope.vehicle.online;

        if (agentsView != null)
        {
            agentsView.SetAgentOnlineStates(
                RobotOnline,
                VehicleOnline
            );
        }

        bool acceptedPose = false;
        if (RobotOnline && IsValidPose(envelope.robot.pose))
        {
            robotTargetPosition = new Vector2(
                envelope.robot.pose.x,
                envelope.robot.pose.y
            );
            robotTargetYawDegrees = envelope.robot.pose.yaw * Mathf.Rad2Deg;
            robotTargetValid = true;
            acceptedPose = true;
        }

        if (VehicleOnline && IsValidPose(envelope.vehicle.pose))
        {
            vehicleTargetPosition = new Vector2(
                envelope.vehicle.pose.x,
                envelope.vehicle.pose.y
            );
            vehicleTargetYawDegrees = envelope.vehicle.pose.yaw * Mathf.Rad2Deg;
            vehicleTargetValid = true;
            acceptedPose = true;
        }

        if (acceptedPose && !livePoseActivated && agentsView != null)
        {
            agentsView.SetPreviewAnimationEnabled(false);
            livePoseActivated = true;
        }

        if (!announcedConnection && verboseLogging)
        {
            announcedConnection = true;
            Debug.Log(
                "[G1 Agents Live] connected robot=" + RobotOnline +
                " vehicle=" + VehicleOnline,
                this
            );
        }
    }

    private static void SmoothPose(
        Vector2 targetPosition,
        float targetYaw,
        float blend,
        ref bool initialized,
        ref Vector2 displayPosition,
        ref float displayYaw)
    {
        if (!initialized)
        {
            displayPosition = targetPosition;
            displayYaw = targetYaw;
            initialized = true;
            return;
        }

        displayPosition = Vector2.Lerp(displayPosition, targetPosition, blend);
        displayYaw = Mathf.LerpAngle(displayYaw, targetYaw, blend);
    }

    private static bool IsValidPose(AgentPose pose)
    {
        return pose != null &&
            IsFinite(pose.x) &&
            IsFinite(pose.y) &&
            IsFinite(pose.yaw);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private void ResolveReferences()
    {
        if (agentsView == null)
            agentsView = GetComponent<G1AgentsWindowView>();
    }

    private void RecordFailure(string message)
    {
        TransportOnline = false;
        RobotOnline = false;
        VehicleOnline = false;
        LastError = message;
        announcedConnection = false;

        if (agentsView != null)
        {
            agentsView.SetAgentOnlineStates(
                false,
                false
            );
        }

        if (!verboseLogging || Time.realtimeSinceStartup < nextWarningTime)
            return;

        nextWarningTime = Time.realtimeSinceStartup + 5.0f;
        Debug.LogWarning("[G1 Agents Live] " + message, this);
    }
}
