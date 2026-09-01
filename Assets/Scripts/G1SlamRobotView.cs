using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(G1SlamWindowView))]
public sealed class G1SlamRobotView : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private G1SlamWindowView slamView;

    [SerializeField]
    private Transform robotModelSource;

    [Header("Localization")]
    [SerializeField]
    [Min(0.05f)]
    private float posePollIntervalSeconds = 0.10f;

    [SerializeField]
    [Min(0.1f)]
    private float poseSmoothingSpeed = 14.0f;

    [SerializeField]
    [Min(0.1f)]
    private float poseStaleSeconds = 1.0f;

    [Tooltip("Height of the G1 pelvis/root above the map floor.")]
    [SerializeField]
    private float pelvisHeightMeters = 0.79f;

    [Tooltip("Use this only if the mesh faces sideways or backwards.")]
    [SerializeField]
    private float yawOffsetDegrees = 0.0f;

    private GameObject robotReplica;
    private Transform robotTransform;
    private GameObject lightRoot;
    private Coroutine pollingCoroutine;

    private Transform[] sourcePoseTransforms =
        System.Array.Empty<Transform>();

    private Transform[] replicaPoseTransforms =
        System.Array.Empty<Transform>();

    private Vector3 targetPosition;
    private Quaternion targetRotation =
        Quaternion.identity;

    private bool poseValid;
    private float lastPoseRealtime =
        float.NegativeInfinity;

    [Serializable]
    private sealed class SlamStatus
    {
        public bool localized;
        public float pose_age_s;
        public SlamPose pose;
        public NativeMap native_map;
    }

    [Serializable]
    private sealed class SlamPose
    {
        public float x;
        public float y;
        public float yaw;
    }

    [Serializable]
    private sealed class NativeMap
    {
        public bool matches;
    }

    private void Awake()
    {
        if (slamView == null)
            slamView = GetComponent<G1SlamWindowView>();
    }

    private void OnEnable()
    {
        if (pollingCoroutine == null)
            pollingCoroutine = StartCoroutine(PollPose());
    }

    private void OnDisable()
    {
        if (pollingCoroutine != null)
        {
            StopCoroutine(pollingCoroutine);
            pollingCoroutine = null;
        }

        poseValid = false;

        if (robotReplica != null)
            robotReplica.SetActive(false);
    }

    private void LateUpdate()
    {
        if (robotReplica == null)
            return;

        MirrorRobotPose();

        bool fresh =
            poseValid &&
            Time.realtimeSinceStartup -
                lastPoseRealtime <=
                Mathf.Max(0.1f, poseStaleSeconds);

        if (!fresh)
        {
            if (robotReplica.activeSelf)
                robotReplica.SetActive(false);

            return;
        }

        if (!robotReplica.activeSelf)
        {
            robotTransform.localPosition =
                targetPosition;

            robotTransform.localRotation =
                targetRotation;

            robotReplica.SetActive(true);
            return;
        }

        float blend =
            1.0f -
            Mathf.Exp(
                -Mathf.Max(
                    0.1f,
                    poseSmoothingSpeed
                ) *
                Time.unscaledDeltaTime
            );

        robotTransform.localPosition =
            Vector3.Lerp(
                robotTransform.localPosition,
                targetPosition,
                blend
            );

        robotTransform.localRotation =
            Quaternion.Slerp(
                robotTransform.localRotation,
                targetRotation,
                blend
            );
    }

    private IEnumerator PollPose()
    {
        WaitForSecondsRealtime delay =
            new WaitForSecondsRealtime(
                Mathf.Max(
                    0.05f,
                    posePollIntervalSeconds
                )
            );

        while (isActiveAndEnabled)
        {
            if (robotReplica == null)
                TryCreateRobot();

            if (slamView != null &&
                slamView.HasLoadedMap)
            {
                yield return FetchPose();
            }

            yield return delay;
        }
    }

    private IEnumerator FetchPose()
    {
        string url =
            slamView.DashboardBaseUrl
                .TrimEnd('/') +
            "/api/slam/status";

        using UnityWebRequest request =
            UnityWebRequest.Get(url);

        request.timeout = 3;

        yield return request.SendWebRequest();

        if (request.result !=
            UnityWebRequest.Result.Success)
        {
            yield break;
        }

        SlamStatus status;

        try
        {
            status =
                JsonUtility.FromJson<SlamStatus>(
                    request.downloadHandler.text
                );
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "[G1 SLAM] Invalid localization status: " +
                exception.Message,
                this
            );

            yield break;
        }

        bool usable =
            status != null &&
            status.localized &&
            status.pose != null &&
            status.native_map != null &&
            status.native_map.matches;

        if (!usable)
        {
            poseValid = false;
            yield break;
        }

        if (!slamView.TryConvertMapPose(
                status.pose.x,
                status.pose.y,
                status.pose.yaw,
                pelvisHeightMeters,
                out Vector3 position,
                out Quaternion rotation))
        {
            yield break;
        }

        targetPosition =
            position;

        targetRotation =
            rotation *
            Quaternion.Euler(
                0.0f,
                yawOffsetDegrees,
                0.0f
            );

        lastPoseRealtime =
            Time.realtimeSinceStartup;

        poseValid = true;
    }

    private void TryCreateRobot()
    {
        if (slamView == null ||
            slamView.RuntimeContentRoot == null ||
            robotModelSource == null)
        {
            return;
        }

        robotReplica =
            Instantiate(
                robotModelSource.gameObject,
                slamView.RuntimeContentRoot,
                false
            );

        robotReplica.name =
            "LocalizedG1";

        robotReplica.tag =
            "Untagged";

        robotReplica.hideFlags =
            HideFlags.DontSave;

        robotTransform =
            robotReplica.transform;

        robotTransform.localPosition =
            Vector3.zero;

        robotTransform.localRotation =
            Quaternion.identity;

        robotTransform.localScale =
            robotModelSource.localScale;

        SetLayerRecursively(
            robotReplica,
            slamView.RuntimeRenderLayer
        );

        BuildPoseMirror();
        MirrorRobotPose();

        foreach (Collider collider in
                 robotReplica.GetComponentsInChildren
                     <Collider>(true))
        {
            collider.enabled = false;
        }

        foreach (ArticulationBody body in
                 robotReplica.GetComponentsInChildren
                     <ArticulationBody>(true))
        {
            body.useGravity = false;
            body.enabled = false;
        }

        foreach (Renderer renderer in
                 robotReplica.GetComponentsInChildren
                     <Renderer>(true))
        {
            renderer.shadowCastingMode =
                ShadowCastingMode.Off;

            renderer.receiveShadows =
                false;
        }

        CreateLights();

        robotReplica.SetActive(false);

        Debug.Log(
            $"[G1 SLAM] Localized G1 pose mirror ready: " +
            $"{sourcePoseTransforms.Length} articulated transforms.",
            this
        );
    }

    private void BuildPoseMirror()
    {
        ArticulationBody[] sourceBodies =
            robotModelSource.GetComponentsInChildren
                <ArticulationBody>(true);

        ArticulationBody[] replicaBodies =
            robotReplica.GetComponentsInChildren
                <ArticulationBody>(true);

        Dictionary<string, Transform> replicaByName =
            new Dictionary<string, Transform>(
                replicaBodies.Length
            );

        foreach (ArticulationBody body in replicaBodies)
        {
            replicaByName[body.transform.name] =
                body.transform;
        }

        List<Transform> sources =
            new List<Transform>(
                sourceBodies.Length
            );

        List<Transform> replicas =
            new List<Transform>(
                sourceBodies.Length
            );

        foreach (ArticulationBody sourceBody in sourceBodies)
        {
            if (!replicaByName.TryGetValue(
                    sourceBody.transform.name,
                    out Transform replicaBody))
            {
                Debug.LogWarning(
                    "[G1 SLAM] Missing replica link: " +
                    sourceBody.transform.name,
                    this
                );

                continue;
            }

            sources.Add(sourceBody.transform);
            replicas.Add(replicaBody);
        }

        sourcePoseTransforms =
            sources.ToArray();

        replicaPoseTransforms =
            replicas.ToArray();
    }

    private void MirrorRobotPose()
    {
        int count =
            Mathf.Min(
                sourcePoseTransforms.Length,
                replicaPoseTransforms.Length
            );

        for (int index = 0;
             index < count;
             index++)
        {
            Transform sourceTransform =
                sourcePoseTransforms[index];

            Transform replicaTransform =
                replicaPoseTransforms[index];

            if (sourceTransform == null ||
                replicaTransform == null)
            {
                continue;
            }

            replicaTransform.localPosition =
                sourceTransform.localPosition;

            replicaTransform.localRotation =
                sourceTransform.localRotation;

            replicaTransform.localScale =
                sourceTransform.localScale;
        }
    }

    private void CreateLights()
    {
        if (lightRoot != null)
            return;

        lightRoot =
            new GameObject(
                "SlamRobotLights"
            );

        lightRoot.hideFlags =
            HideFlags.DontSave;

        lightRoot.transform.SetParent(
            slamView.RuntimeContentRoot,
            false
        );

        CreateDirectionalLight(
            "Key",
            new Vector3(45.0f, -35.0f, 0.0f),
            1.15f
        );

        CreateDirectionalLight(
            "Fill",
            new Vector3(25.0f, 145.0f, 0.0f),
            0.45f
        );
    }

    private void CreateDirectionalLight(
        string lightName,
        Vector3 eulerAngles,
        float intensity)
    {
        GameObject lightObject =
            new GameObject(lightName);

        lightObject.transform.SetParent(
            lightRoot.transform,
            false
        );

        lightObject.transform.localRotation =
            Quaternion.Euler(eulerAngles);

        Light directionalLight =
            lightObject.AddComponent<Light>();

        directionalLight.type =
            LightType.Directional;

        directionalLight.color =
            Color.white;

        directionalLight.intensity =
            intensity;

        directionalLight.shadows =
            LightShadows.None;

        directionalLight.cullingMask =
            1 << slamView.RuntimeRenderLayer;
    }

    private static void SetLayerRecursively(
        GameObject root,
        int layer)
    {
        root.layer = layer;

        foreach (Transform child in
                 root.transform)
        {
            SetLayerRecursively(
                child.gameObject,
                layer
            );
        }
    }
}
