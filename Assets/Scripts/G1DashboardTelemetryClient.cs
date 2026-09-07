using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

[DisallowMultipleComponent]
public sealed class G1DashboardTelemetryClient :
    MonoBehaviour
{
    [Serializable]
    public sealed class Envelope
    {
        public string schema;
        public double generated_unix_time_s;
        public Connection connection;
        public Control control;
        public Motion motion;
        public Arms arms;
        public Motors motors;
        public Hands hands;
        public Actions actions;
        public Computer computer;
    }

    [Serializable]
    public sealed class Connection
    {
        public bool online;
        public float packet_age_s;
        public long packet_count;
        public long invalid_count;
    }

    [Serializable]
    public sealed class Control
    {
        public string state;
        public float main_loop_hz;
        public float arm_ownership;
        public bool lowstate_ok;
        public float lowstate_age_s;
        public bool xr_ok;
        public string xr_reason;
        public string safety_fault;
        public string tracking_hold;
        public bool tracking_guard;
    }

    [Serializable]
    public sealed class Actions
    {
        public string schema;
        public bool request_channel_enabled;
        public XrHandover xr_handover;
        public EngagementConditions engagement_conditions;
    }

    [Serializable]
    public sealed class XrHandover
    {
        public bool available;
        public string operation;
        public string label;
        public string reason;
        public string would_enter_state;
        public string source;
    }

    [Serializable]
    public sealed class EngagementConditions
    {
        public bool lowstate_ok;
        public bool xr_ok;
        public string xr_reason;
        public bool stop_gate_instant;
        public bool stop_gate_ready;
        public float stop_gate_elapsed_s;
        public bool safety_fault_clear;
        public bool tracking_hold_active;
    }

    [Serializable]
    public sealed class Motion
    {
        public float base_speed_mps;
        public float yaw_rate_rps;
        public float max_joint_speed_rps;
    }

    [Serializable]
    public sealed class Arms
    {
        public float max_tracking_error_deg;
        public float max_estimated_torque_nm;
        public bool publisher_ok;
        public string publisher_error;
    }

    [Serializable]
    public sealed class Motors
    {
        public string hottest_joint;
        public float hottest_temperature_c;
        public int fault_count;
    }

    [Serializable]
    public sealed class Hands
    {
        public string mode;
        public bool tracking_valid;
        public string tracking_reason;
        public bool feedback_fault;
        public float feedback_age_s;
    }

    [Serializable]
    public sealed class Computer
    {
        public bool online;
        public float cpu_used_pct;
        public float ram_used_pct;
        public float disk_used_pct;
        public float maximum_temperature_c;
        public float imu_temperature_c;
        public string network_state;
        public float uptime_s;
    }

    [Header("Dashboard")]
    [SerializeField]
    private string dashboardBaseUrl =
        "http://192.168.0.116:8080";

    [SerializeField]
    [Min(0.15f)]
    private float pollingIntervalSeconds =
        0.25f;

    [SerializeField]
    [Min(1)]
    private int requestTimeoutSeconds = 3;

    [Header("Diagnostics")]
    [SerializeField]
    private bool verboseLogging = true;

    public Envelope Latest { get; private set; }

    public int Revision { get; private set; }

    public string LastError { get; private set; }

    public bool HasData
    {
        get { return Latest != null; }
    }

    public bool TransportFresh
    {
        get
        {
            if (lastSuccessfulResponseTime < 0.0f)
                return false;

            float maximumAge =
                Mathf.Max(
                    2.0f,
                    pollingIntervalSeconds * 6.0f
                );

            return
                Time.realtimeSinceStartup -
                lastSuccessfulResponseTime
                <= maximumAge;
        }
    }

    private Coroutine pollingCoroutine;
    private float lastSuccessfulResponseTime = -1.0f;
    private float nextWarningTime;
    private bool announcedConnection;

    private void OnEnable()
    {
        if (pollingCoroutine == null)
        {
            pollingCoroutine =
                StartCoroutine(PollingLoop());
        }
    }

    private void OnDisable()
    {
        if (pollingCoroutine != null)
        {
            StopCoroutine(pollingCoroutine);
            pollingCoroutine = null;
        }
    }

    private IEnumerator PollingLoop()
    {
        WaitForSecondsRealtime delay =
            new WaitForSecondsRealtime(
                Mathf.Max(
                    0.15f,
                    pollingIntervalSeconds
                )
            );

        while (isActiveAndEnabled)
        {
            yield return DownloadTelemetry();
            yield return delay;
        }

        pollingCoroutine = null;
    }

    private IEnumerator DownloadTelemetry()
    {
        string url =
            dashboardBaseUrl.TrimEnd('/') +
            "/api/unity/telemetry";

        using UnityWebRequest request =
            UnityWebRequest.Get(url);

        request.timeout =
            Mathf.Max(1, requestTimeoutSeconds);

        yield return request.SendWebRequest();

        if (request.result !=
            UnityWebRequest.Result.Success)
        {
            RecordFailure(
                $"{request.error} " +
                $"(HTTP {request.responseCode})"
            );

            yield break;
        }

        Envelope parsed;

        try
        {
            parsed =
                JsonUtility.FromJson<Envelope>(
                    request.downloadHandler.text
                );
        }
        catch (Exception exception)
        {
            RecordFailure(
                $"Invalid telemetry JSON: " +
                exception.Message
            );

            yield break;
        }

        if (
            parsed == null ||
            parsed.schema !=
            "g1_dashboard.unity_telemetry.v1"
        )
        {
            RecordFailure(
                "Unexpected telemetry schema."
            );

            yield break;
        }

        Latest = parsed;
        Revision++;

        LastError = null;

        lastSuccessfulResponseTime =
            Time.realtimeSinceStartup;

        if (
            verboseLogging &&
            !announcedConnection
        )
        {
            announcedConnection = true;

            Debug.Log(
                "[G1 Telemetry] Dashboard telemetry connected.",
                this
            );
        }
    }

    private void RecordFailure(
        string message)
    {
        LastError = message;

        if (
            verboseLogging &&
            Time.realtimeSinceStartup >=
            nextWarningTime
        )
        {
            nextWarningTime =
                Time.realtimeSinceStartup +
                10.0f;

            Debug.LogWarning(
                "[G1 Telemetry] " + message,
                this
            );
        }
    }
}