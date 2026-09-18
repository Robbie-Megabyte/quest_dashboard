using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

[DisallowMultipleComponent]
[RequireComponent(typeof(G1AgentsWindowView))]
[RequireComponent(typeof(G1AgentsLivePoseClient))]
public sealed class G1AgentsWorldClient : MonoBehaviour
{
    [Serializable]
    private sealed class WorldEnvelope
    {
        public bool success;
        public string schema;
        public StaticPointCloudState point_cloud;
        public VehicleWorldState vehicle;
    }

    [Serializable]
    private sealed class StaticPointCloudState
    {
        public int revision;
        public bool changed;
        public int point_count;
        public WorldPoint[] points;
    }

    [Serializable]
    private sealed class VehicleWorldState
    {
        public float map_resolution;
        public int map_revision;
        public bool map_changed;
        public WorldPoint[] map_points;
        public int scan_revision;
        public bool scan_changed;
        public bool scan_fresh;
        public float scan_age_s;
        public int scan_point_count;
        public WorldPoint[] scan_points;
        public int path_revision;
        public bool path_changed;
        public int path_point_count;
        public WorldPoint[] path;
    }

    [Serializable]
    private sealed class WorldPoint
    {
        public float x;
        public float y;
        public float z;
    }

    [SerializeField]
    [Min(0.05f)]
    private float pollingIntervalSeconds = 0.1f;

    [SerializeField]
    [Min(1)]
    private int requestTimeoutSeconds = 5;

    [SerializeField]
    private bool verboseLogging = true;

    public int StaticPointCloudRevision { get; private set; }
    public int StaticPointCloudPointCount { get; private set; }
    public int VehicleMapRevision { get; private set; }
    public int VehicleScanRevision { get; private set; }
    public int VehicleScanPointCount { get; private set; }
    public bool VehicleScanFresh { get; private set; }
    public int VehiclePathRevision { get; private set; }
    public int VehiclePathPointCount { get; private set; }
    public string LastError { get; private set; }

    private G1AgentsWindowView agentsView;
    private G1AgentsLivePoseClient poseClient;
    private Coroutine pollingCoroutine;
    private int renderedCloudRevision = -1;
    private int renderedMapRevision = -1;
    private int renderedScanRevision = -1;
    private int renderedPathRevision = -1;
    private float nextWarningTime;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
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
    }

    private void ResolveReferences()
    {
        if (agentsView == null)
            agentsView = GetComponent<G1AgentsWindowView>();

        if (poseClient == null)
            poseClient = GetComponent<G1AgentsLivePoseClient>();
    }

    private IEnumerator PollingLoop()
    {
        while (isActiveAndEnabled)
        {
            if (poseClient == null ||
                !poseClient.TryGetConnection(
                    out string baseUrl,
                    out string token))
            {
                yield return new WaitForSecondsRealtime(1.0f);
                continue;
            }

            yield return DownloadWorld(baseUrl, token);
            yield return new WaitForSecondsRealtime(
                Mathf.Max(0.05f, pollingIntervalSeconds)
            );
        }

        pollingCoroutine = null;
    }

    private IEnumerator DownloadWorld(
        string baseUrl,
        string token)
    {
        string url =
            baseUrl.TrimEnd('/') +
            "/api/agents/world?map_revision=" +
            renderedMapRevision +
            "&path_revision=" +
            renderedPathRevision +
            "&scan_revision=" +
            renderedScanRevision +
            "&cloud_revision=" +
            renderedCloudRevision;

        using UnityWebRequest request =
            UnityWebRequest.Get(url);

        request.timeout = Mathf.Max(1, requestTimeoutSeconds);
        request.SetRequestHeader("X-G1-Token", token);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            RecordFailure(
                request.error +
                " (HTTP " + request.responseCode + ")"
            );
            yield break;
        }

        WorldEnvelope envelope;
        try
        {
            envelope = JsonUtility.FromJson<WorldEnvelope>(
                request.downloadHandler.text
            );
        }
        catch (Exception exception)
        {
            RecordFailure("Invalid JSON: " + exception.Message);
            yield break;
        }

        if (envelope == null ||
            !envelope.success ||
            envelope.schema != "g1_multi_agent.world.v1" ||
            envelope.vehicle == null)
        {
            RecordFailure("Unexpected world response.");
            yield break;
        }

        VehicleWorldState vehicle = envelope.vehicle;
        StaticPointCloudState cloud = envelope.point_cloud;
        LastError = null;

        VehicleMapRevision = vehicle.map_revision;
        VehicleScanRevision = vehicle.scan_revision;
        VehicleScanPointCount = vehicle.scan_point_count;
        VehicleScanFresh = vehicle.scan_fresh;
        VehiclePathRevision = vehicle.path_revision;
        VehiclePathPointCount = vehicle.path_point_count;

        if (cloud != null)
        {
            StaticPointCloudRevision = cloud.revision;
            StaticPointCloudPointCount = cloud.point_count;

            if (cloud.changed)
            {
                List<Vector3> points = ToVector3Points(
                    cloud.points
                );

                agentsView?.SetStaticPointCloud(points);
                renderedCloudRevision = cloud.revision;

                if (verboseLogging)
                {
                    Debug.Log(
                        "[G1 Agents World] PCD revision=" +
                        renderedCloudRevision +
                        " points=" + points.Count,
                        this
                    );
                }
            }
        }

        if (vehicle.scan_fresh)
        {
            if (vehicle.scan_changed)
            {
                List<Vector2> points = ToVectorPoints(
                    vehicle.scan_points
                );

                agentsView?.SetVehicleLidarScan(points);
                renderedScanRevision = vehicle.scan_revision;
            }
        }
        else
        {
            agentsView?.ClearVehicleLidarScan();
            renderedScanRevision = vehicle.scan_revision;
        }

        if (vehicle.map_changed)
        {
            List<Vector2> points = ToVectorPoints(
                vehicle.map_points
            );
            agentsView?.SetWorldMap(
                points,
                vehicle.map_resolution
            );
            renderedMapRevision = vehicle.map_revision;

            if (verboseLogging)
            {
                Debug.Log(
                    "[G1 Agents World] map revision=" +
                    renderedMapRevision +
                    " points=" + points.Count,
                    this
                );
            }
        }

        if (vehicle.path_changed)
        {
            List<Vector2> points = ToVectorPoints(
                vehicle.path
            );
            agentsView?.SetPlannerPath(points);
            renderedPathRevision = vehicle.path_revision;

            if (verboseLogging)
            {
                Debug.Log(
                    "[G1 Agents World] path revision=" +
                    renderedPathRevision +
                    " points=" + points.Count,
                    this
                );
            }
        }
    }

    private static List<Vector2> ToVectorPoints(
        WorldPoint[] source)
    {
        List<Vector2> result = new List<Vector2>();
        if (source == null)
            return result;

        foreach (WorldPoint point in source)
        {
            if (point == null ||
                !IsFinite(point.x) ||
                !IsFinite(point.y))
            {
                continue;
            }

            result.Add(new Vector2(point.x, point.y));
        }

        return result;
    }

    private static List<Vector3> ToVector3Points(
        WorldPoint[] source)
    {
        List<Vector3> result = new List<Vector3>();

        if (source == null)
            return result;

        foreach (WorldPoint point in source)
        {
            if (point == null ||
                !IsFinite(point.x) ||
                !IsFinite(point.y) ||
                !IsFinite(point.z))
            {
                continue;
            }

            result.Add(new Vector3(
                point.x,
                point.y,
                point.z
            ));
        }

        return result;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) &&
            !float.IsInfinity(value);
    }

    private void RecordFailure(string message)
    {
        LastError = message;

        if (!verboseLogging ||
            Time.realtimeSinceStartup < nextWarningTime)
        {
            return;
        }

        nextWarningTime = Time.realtimeSinceStartup + 5.0f;
        Debug.LogWarning(
            "[G1 Agents World] " + message,
            this
        );
    }
}
