using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Hands;

[DisallowMultipleComponent]
public sealed class G1TeleVuerConnectionProbe : MonoBehaviour
{
    private const int JointCount = 25;
    private const int MatrixCount = 51;
    private const int StateFloatCount = 4;
    private const int ObservationFloatCount =
        MatrixCount * 16 + StateFloatCount;
    private const int HeaderBytes = 24;
    private const int HmacBytes = 32;
    private const uint TrackingFlags = 0x07;

    private static readonly XRHandJointID[] JointOrder =
    {
        XRHandJointID.Wrist,

        XRHandJointID.ThumbMetacarpal,
        XRHandJointID.ThumbProximal,
        XRHandJointID.ThumbDistal,
        XRHandJointID.ThumbTip,

        XRHandJointID.IndexMetacarpal,
        XRHandJointID.IndexProximal,
        XRHandJointID.IndexIntermediate,
        XRHandJointID.IndexDistal,
        XRHandJointID.IndexTip,

        XRHandJointID.MiddleMetacarpal,
        XRHandJointID.MiddleProximal,
        XRHandJointID.MiddleIntermediate,
        XRHandJointID.MiddleDistal,
        XRHandJointID.MiddleTip,

        XRHandJointID.RingMetacarpal,
        XRHandJointID.RingProximal,
        XRHandJointID.RingIntermediate,
        XRHandJointID.RingDistal,
        XRHandJointID.RingTip,

        XRHandJointID.LittleMetacarpal,
        XRHandJointID.LittleProximal,
        XRHandJointID.LittleIntermediate,
        XRHandJointID.LittleDistal,
        XRHandJointID.LittleTip
    };

    [Header("Unity TeleVuer ingress")]
    [SerializeField]
    private string serverUrl =
        "ws://192.168.0.116:8013/";

    [SerializeField]
    private string keyResourcePath =
        "LocalSecrets/g1_unity_televuer_key";

    [SerializeField]
    private bool connectOnEnable = true;

    [Tooltip(
        "Explicitly permit a server configured to feed tracking into " +
        "TeleVuer. Robot ownership is still controlled independently " +
        "by the listener safety state machine.")]
    [SerializeField]
    private bool acceptMotionEnabledServer = false;

    [SerializeField, Min(0.5f)]
    private float reconnectDelaySeconds = 2f;

    [SerializeField, Range(10f, 60f)]
    private float observationRateHz = 30f;

    [Header("Optional UI")]
    [SerializeField]
    private TMP_Text statusText;

    private readonly object frameLock = new object();
    private readonly List<XRHandSubsystem> subsystemReuse =
        new List<XRHandSubsystem>();

    private CancellationTokenSource lifetime;
    private ClientWebSocket socket;
    private Task runner;
    private byte[] preSharedKey;

    private XRHandSubsystem handSubsystem;
    private XROrigin xrOrigin;
    private Camera xrCamera;

    private float[] latestObservation;
    private long latestObservationVersion;
    private ulong latestClientNanoseconds;
    private float nextCaptureTime;

    private volatile int observationAuthenticated;
    private volatile string connectionStatus = "STOPPED";
    private volatile string trackingStatus = "WAITING";
    private string lastLoggedStatus;
    private long transmittedFrames;

    [Serializable]
    private sealed class ChallengeMessage
    {
        public string type;
        public int version;
        public string nonce;
        public long server_unix_ms;
    }

    [Serializable]
    private sealed class AuthenticationMessage
    {
        public string type = "authenticate";
        public int version = 1;
        public string nonce;
        public string mac;
    }

    [Serializable]
    private sealed class HelloAck
    {
        public string type;
        public int version;
        public long server_unix_ms;
        public string session;
        public bool motion_enabled;
        public bool observation_enabled;
    }

    private void Awake()
    {
        if (!BitConverter.IsLittleEndian)
        {
            throw new PlatformNotSupportedException(
                "The observation protocol requires little-endian data");
        }

        TextAsset keyAsset =
            Resources.Load<TextAsset>(keyResourcePath);

        if (keyAsset == null)
        {
            throw new InvalidOperationException(
                "Missing TeleVuer key resource: " +
                keyResourcePath);
        }

        preSharedKey = ParseHexKey(keyAsset.text);
    }

    private void OnEnable()
    {
        if (connectOnEnable)
            StartProbe();
    }

    private void OnDisable()
    {
        StopProbe();
    }

    private void OnApplicationQuit()
    {
        StopProbe();
    }

    private void Update()
    {
        if (
            Volatile.Read(
                ref observationAuthenticated) == 1 &&
            Time.unscaledTime >= nextCaptureTime)
        {
            nextCaptureTime =
                Time.unscaledTime +
                1f / Mathf.Max(10f, observationRateHz);

            CaptureObservation();
        }

        string display =
            connectionStatus +
            "\n" +
            trackingStatus +
            "\nTX frames: " +
            Interlocked.Read(ref transmittedFrames);

        if (statusText != null)
            statusText.text = display;

        string logStatus =
            connectionStatus +
            " • " +
            trackingStatus;

        if (logStatus != lastLoggedStatus)
        {
            lastLoggedStatus = logStatus;
            Debug.Log(
                "[G1 TeleVuer Probe] " + logStatus,
                this);
        }
    }

    public void StartProbe()
    {
        if (runner != null && !runner.IsCompleted)
            return;

        lifetime = new CancellationTokenSource();
        runner = RunConnectionLoop(lifetime.Token);
    }

    public void StopProbe()
    {
        Volatile.Write(ref observationAuthenticated, 0);
        connectionStatus = "STOPPED";

        if (
            lifetime != null &&
            !lifetime.IsCancellationRequested)
        {
            lifetime.Cancel();
        }

        ClientWebSocket active = socket;

        if (active != null)
        {
            try
            {
                active.Abort();
            }
            catch
            {
                // It may already be closed.
            }
        }

        socket = null;
    }

    private async Task RunConnectionLoop(
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                connectionStatus = "CONNECTING";
                trackingStatus = "WAITING";

                using (var client = new ClientWebSocket())
                {
                    socket = client;

                    client.Options.KeepAliveInterval =
                        TimeSpan.FromSeconds(5);

                    await client.ConnectAsync(
                        new Uri(serverUrl),
                        cancellationToken);

                    connectionStatus = "SOCKET CONNECTED";

                    string challengeJson =
                        await ReceiveTextMessage(
                            client,
                            cancellationToken);

                    if (string.IsNullOrEmpty(challengeJson))
                    {
                        throw new InvalidOperationException(
                            "Server closed before sending challenge");
                    }

                    ChallengeMessage challenge =
                        JsonUtility.FromJson<ChallengeMessage>(
                            challengeJson);

                    if (
                        challenge == null ||
                        challenge.type != "challenge" ||
                        challenge.version != 1 ||
                        string.IsNullOrEmpty(challenge.nonce))
                    {
                        throw new InvalidOperationException(
                            "Invalid server challenge: " +
                            challengeJson);
                    }

                    string authenticationJson =
                        CreateAuthenticationMessage(
                            challenge.nonce);

                    byte[] authenticationBytes =
                        Encoding.UTF8.GetBytes(
                            authenticationJson);

                    await client.SendAsync(
                        new ArraySegment<byte>(
                            authenticationBytes),
                        WebSocketMessageType.Text,
                        true,
                        cancellationToken);

                    string response =
                        await ReceiveTextMessage(
                            client,
                            cancellationToken);

                    if (string.IsNullOrEmpty(response))
                    {
                        throw new InvalidOperationException(
                            "Server closed before authentication");
                    }

                    HelloAck ack =
                        JsonUtility.FromJson<HelloAck>(response);

                    if (
                        ack == null ||
                        ack.type != "hello_ack" ||
                        ack.version != 1 ||
                        string.IsNullOrEmpty(ack.session))
                    {
                        throw new InvalidOperationException(
                            "Invalid authentication response: " +
                            response);
                    }

                    if (
                        ack.motion_enabled &&
                        !acceptMotionEnabledServer)
                    {
                        throw new InvalidOperationException(
                            "Server offers motion input but this Unity " +
                            "build has not explicitly opted in");
                    }

                    if (!ack.observation_enabled)
                    {
                        throw new InvalidOperationException(
                            "Server does not support observation frames");
                    }

                    connectionStatus =
                        ack.motion_enabled
                            ? "AUTHENTICATED • MOTION INPUT"
                            : "AUTHENTICATED • OBSERVATION ONLY";

                    Interlocked.Exchange(
                        ref transmittedFrames,
                        0);

                    lock (frameLock)
                    {
                        latestObservation = null;
                        latestObservationVersion = 0;
                    }

                    Volatile.Write(
                        ref observationAuthenticated,
                        1);

                    try
                    {
                        using (
                            var connectionLifetime =
                                CancellationTokenSource
                                    .CreateLinkedTokenSource(
                                        cancellationToken))
                        {
                            Task sendTask =
                                SendObservationFrames(
                                    client,
                                    ack.session,
                                    connectionLifetime.Token);

                            Task receiveTask =
                                ReceiveConnection(
                                    client,
                                    connectionLifetime.Token);

                            Task completedTask =
                                await Task.WhenAny(
                                    sendTask,
                                    receiveTask);

                            connectionLifetime.Cancel();

                            try
                            {
                                await completedTask;
                            }
                            finally
                            {
                                client.Abort();
                            }
                        }
                    }
                    finally
                    {
                        Volatile.Write(
                            ref observationAuthenticated,
                            0);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                connectionStatus =
                    "ERROR: " +
                    DescribeException(exception);
            }
            finally
            {
                Volatile.Write(
                    ref observationAuthenticated,
                    0);

                socket = null;
            }

            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(
                        reconnectDelaySeconds),
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void CaptureObservation()
    {
        if (!ResolveTrackingObjects())
        {
            trackingStatus =
                "WAITING FOR XR HAND SUBSYSTEM";
            return;
        }

        XRHand leftHand = handSubsystem.leftHand;
        XRHand rightHand = handSubsystem.rightHand;

        if (!leftHand.isTracked || !rightHand.isTracked)
        {
            trackingStatus =
                "WAITING FOR BOTH TRACKED HANDS";
            return;
        }

        float[] values =
            new float[ObservationFloatCount];

        int offset = 0;

        Transform trackingSpace =
    xrOrigin.CameraFloorOffsetObject != null
        ? xrOrigin.CameraFloorOffsetObject.transform
        : xrOrigin.transform;

Matrix4x4 headMatrix =
    trackingSpace.worldToLocalMatrix *
    xrCamera.transform.localToWorldMatrix;

        WriteOpenXrMatrix(
            headMatrix,
            values,
            ref offset);

        if (
            !TryWriteHand(
                leftHand,
                values,
                ref offset,
                out float leftPinch,
                out float leftSqueeze))
        {
            trackingStatus =
                "LEFT HAND JOINTS INCOMPLETE";
            return;
        }

        if (
            !TryWriteHand(
                rightHand,
                values,
                ref offset,
                out float rightPinch,
                out float rightSqueeze))
        {
            trackingStatus =
                "RIGHT HAND JOINTS INCOMPLETE";
            return;
        }

        if (offset != MatrixCount * 16)
        {
            trackingStatus =
                "INTERNAL MATRIX COUNT ERROR";
            return;
        }

        values[offset++] = leftPinch;
        values[offset++] = leftSqueeze;
        values[offset++] = rightPinch;
        values[offset++] = rightSqueeze;

        if (offset != ObservationFloatCount)
        {
            trackingStatus =
                "INTERNAL FRAME SIZE ERROR";
            return;
        }

        ulong clientNanoseconds =
            GetMonotonicNanoseconds();

        lock (frameLock)
        {
            latestObservation = values;
            latestClientNanoseconds =
                clientNanoseconds;
            latestObservationVersion++;
        }

        trackingStatus = "TRACKING VALID";
    }

    private bool ResolveTrackingObjects()
    {
        if (xrCamera == null)
            xrCamera = Camera.main;

        if (xrOrigin == null)
        {
            xrOrigin =
                UnityEngine.Object
                    .FindAnyObjectByType<XROrigin>();
        }

        if (
            handSubsystem == null ||
            !handSubsystem.running)
        {
            subsystemReuse.Clear();

            SubsystemManager.GetSubsystems(
                subsystemReuse);

            handSubsystem = null;

            foreach (
                XRHandSubsystem candidate
                in subsystemReuse)
            {
                if (candidate != null &&
                    candidate.running)
                {
                    handSubsystem = candidate;
                    break;
                }
            }
        }

        return
            xrCamera != null &&
            xrOrigin != null &&
            handSubsystem != null &&
            handSubsystem.running;
    }

    private static bool TryWriteHand(
        XRHand hand,
        float[] destination,
        ref int offset,
        out float pinchDistance,
        out float squeeze)
    {
        Pose[] poses = new Pose[JointCount];

        for (
            int index = 0;
            index < JointOrder.Length;
            ++index)
        {
            XRHandJoint joint =
                hand.GetJoint(JointOrder[index]);

            if (!joint.TryGetPose(out Pose pose))
            {
                pinchDistance = 0f;
                squeeze = 0f;
                return false;
            }

            poses[index] = pose;

            Matrix4x4 matrix =
                Matrix4x4.TRS(
                    pose.position,
                    pose.rotation,
                    Vector3.one);

            WriteOpenXrMatrix(
                matrix,
                destination,
                ref offset);
        }

        pinchDistance =
            Vector3.Distance(
                poses[4].position,
                poses[9].position);

        float averageTipDistance =
            (
                Vector3.Distance(
                    poses[9].position,
                    poses[0].position)
                + Vector3.Distance(
                    poses[14].position,
                    poses[0].position)
                + Vector3.Distance(
                    poses[19].position,
                    poses[0].position)
                + Vector3.Distance(
                    poses[24].position,
                    poses[0].position)
            ) / 4f;

        squeeze =
            1f -
            Mathf.InverseLerp(
                0.07f,
                0.18f,
                averageTipDistance);

        pinchDistance =
            Mathf.Clamp(
                pinchDistance,
                0f,
                0.5f);

        squeeze = Mathf.Clamp01(squeeze);

        return true;
    }

    private static void WriteOpenXrMatrix(
        Matrix4x4 unityMatrix,
        float[] destination,
        ref int offset)
    {
        for (int column = 0; column < 4; ++column)
        {
            float columnSign =
                column == 2 ? -1f : 1f;

            for (int row = 0; row < 4; ++row)
            {
                float rowSign =
                    row == 2 ? -1f : 1f;

                destination[offset++] =
                    unityMatrix[row, column] *
                    rowSign *
                    columnSign;
            }
        }
    }

    private static async Task ReceiveConnection(
        ClientWebSocket client,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[1024];

        while (
            client.State == WebSocketState.Open &&
            !cancellationToken.IsCancellationRequested)
        {
            WebSocketReceiveResult result =
                await client.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    cancellationToken);

            if (
                result.MessageType ==
                WebSocketMessageType.Close)
            {
                return;
            }
        }
    }

    private async Task SendObservationFrames(
        ClientWebSocket client,
        string session,
        CancellationToken cancellationToken)
    {
        long sentVersion = 0;
        ulong sequence = 0;

        while (
            client.State == WebSocketState.Open &&
            !cancellationToken.IsCancellationRequested)
        {
            float[] values = null;
            ulong clientNanoseconds = 0;
            long version;

            lock (frameLock)
            {
                version = latestObservationVersion;

                if (
                    latestObservation != null &&
                    version != sentVersion)
                {
                    values =
                        (float[])latestObservation.Clone();

                    clientNanoseconds =
                        latestClientNanoseconds;
                }
            }

            if (values == null)
            {
                await Task.Delay(
                    5,
                    cancellationToken);

                continue;
            }

            sequence++;

            byte[] packet =
                BuildObservationPacket(
                    values,
                    sequence,
                    clientNanoseconds,
                    session);

            await client.SendAsync(
                new ArraySegment<byte>(packet),
                WebSocketMessageType.Binary,
                true,
                cancellationToken);

            sentVersion = version;

            Interlocked.Increment(
                ref transmittedFrames);
        }
    }

    private byte[] BuildObservationPacket(
        float[] values,
        ulong sequence,
        ulong clientNanoseconds,
        string session)
    {
        if (
            values == null ||
            values.Length != ObservationFloatCount)
        {
            throw new InvalidOperationException(
                "Invalid observation payload");
        }

        byte[] body =
            new byte[
                HeaderBytes +
                ObservationFloatCount * 4
            ];

        body[0] = (byte)'G';
        body[1] = (byte)'1';
        body[2] = (byte)'F';
        body[3] = (byte)'1';

        WriteUInt64LittleEndian(
            body,
            4,
            sequence);

        WriteUInt64LittleEndian(
            body,
            12,
            clientNanoseconds);

        WriteUInt32LittleEndian(
            body,
            20,
            TrackingFlags);

        Buffer.BlockCopy(
            values,
            0,
            body,
            HeaderBytes,
            values.Length * 4);

        byte[] sessionBytes =
            Encoding.ASCII.GetBytes(session);

        byte[] signed =
            new byte[
                sessionBytes.Length +
                body.Length
            ];

        Buffer.BlockCopy(
            sessionBytes,
            0,
            signed,
            0,
            sessionBytes.Length);

        Buffer.BlockCopy(
            body,
            0,
            signed,
            sessionBytes.Length,
            body.Length);

        byte[] digest;

        using (HMACSHA256 hmac =
               new HMACSHA256(preSharedKey))
        {
            digest = hmac.ComputeHash(signed);
        }

        byte[] packet =
            new byte[
                body.Length +
                HmacBytes
            ];

        Buffer.BlockCopy(
            body,
            0,
            packet,
            0,
            body.Length);

        Buffer.BlockCopy(
            digest,
            0,
            packet,
            body.Length,
            HmacBytes);

        return packet;
    }

    private string CreateAuthenticationMessage(
        string nonce)
    {
        byte[] signed = Encoding.ASCII.GetBytes(
            "G1TV1|AUTH|" +
            nonce);

        byte[] digest;

        using (HMACSHA256 hmac =
               new HMACSHA256(preSharedKey))
        {
            digest = hmac.ComputeHash(signed);
        }

        var authentication =
            new AuthenticationMessage
            {
                nonce = nonce,
                mac = ToLowerHex(digest)
            };

        return JsonUtility.ToJson(authentication);
    }

    private static async Task<string> ReceiveTextMessage(
        ClientWebSocket client,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[4096];
        var text = new StringBuilder();
        WebSocketReceiveResult result;

        do
        {
            result = await client.ReceiveAsync(
                new ArraySegment<byte>(buffer),
                cancellationToken);

            if (
                result.MessageType ==
                WebSocketMessageType.Close)
            {
                return null;
            }

            if (
                result.MessageType !=
                WebSocketMessageType.Text)
            {
                throw new InvalidOperationException(
                    "Expected a text response");
            }

            text.Append(
                Encoding.UTF8.GetString(
                    buffer,
                    0,
                    result.Count));

            if (text.Length > 4096)
            {
                throw new InvalidOperationException(
                    "Authentication response is too large");
            }
        }
        while (!result.EndOfMessage);

        return text.ToString();
    }

    private static byte[] ParseHexKey(
        string value)
    {
        string normalized =
            (value ?? string.Empty).Trim();

        if (
            normalized.Length < 64 ||
            normalized.Length % 2 != 0)
        {
            throw new InvalidOperationException(
                "The Unity TeleVuer key is invalid");
        }

        byte[] result =
            new byte[normalized.Length / 2];

        for (
            int index = 0;
            index < result.Length;
            ++index)
        {
            result[index] =
                Convert.ToByte(
                    normalized.Substring(
                        index * 2,
                        2),
                    16);
        }

        return result;
    }

    private static string ToLowerHex(
        byte[] bytes)
    {
        var output =
            new StringBuilder(bytes.Length * 2);

        foreach (byte value in bytes)
            output.Append(value.ToString("x2"));

        return output.ToString();
    }

    private static ulong GetMonotonicNanoseconds()
    {
        double ticks =
            System.Diagnostics.Stopwatch
                .GetTimestamp();

        double frequency =
            System.Diagnostics.Stopwatch
                .Frequency;

        return (ulong)(
            ticks *
            (1_000_000_000.0 / frequency));
    }

    private static void WriteUInt64LittleEndian(
        byte[] destination,
        int offset,
        ulong value)
    {
        for (int index = 0; index < 8; ++index)
        {
            destination[offset + index] =
                (byte)(value >> (index * 8));
        }
    }

    private static void WriteUInt32LittleEndian(
        byte[] destination,
        int offset,
        uint value)
    {
        for (int index = 0; index < 4; ++index)
        {
            destination[offset + index] =
                (byte)(value >> (index * 8));
        }
    }

    private static string DescribeException(
        Exception exception)
    {
        var output = new StringBuilder();
        Exception current = exception;
        int depth = 0;

        while (current != null && depth < 4)
        {
            if (depth > 0)
                output.Append(" -> ");

            output.Append(
                current.GetType().Name);
            output.Append(": ");
            output.Append(current.Message);

            current = current.InnerException;
            depth++;
        }

        return output.ToString();
    }
}
