using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

public sealed class G1CameraUdpReceiver : MonoBehaviour
{
    private const int HeaderSize = 20;
    private const byte ProtocolVersion = 1;

    // Bits 0..6 are camera views. Bit 15 independently asks the
    // robot to enable its single shared YOLO inference pipeline.
    private const ushort YoloControlBit = 0x8000;

    private const int AssembliesPerView = 4;
    private const int MaximumAssemblies =
        G1CameraViewInfo.Count * AssembliesPerView;

    [Header("Robot")]
    [SerializeField]
    private string robotIp = "192.168.0.116";

    [SerializeField]
    private int controlPort = 5057;

    [Header("UDP Receiver")]
    [SerializeField]
    private int listenPort = 5056;

    [SerializeField]
    private string expectedSenderIp = "192.168.0.116";

    [Tooltip("1200-byte packet minus the 20-byte header.")]
    [SerializeField]
    private int payloadStride = 1180;

    [SerializeField]
    private int maximumJpegBytes = 2097152;

    [Header("Subscription")]
    [SerializeField]
    private float heartbeatIntervalSeconds = 0.5f;

    [Header("Diagnostics")]
    [SerializeField]
    private bool verboseLogging = true;

    public static G1CameraUdpReceiver Instance
    {
        get;
        private set;
    }

    public event Action<G1CameraView, Texture2D>
        ViewTextureUpdated;

    public event Action<bool>
        YoloRequestChanged;

    public bool YoloRequested
    {
        get
        {
            return yoloRequested;
        }
    }

    public ushort ActiveSubscriptionMask
    {
        get
        {
            return aggregateSubscriptionMask;
        }
    }

    private readonly object frameLock =
        new object();

    private readonly byte[][] latestJpegs =
        new byte[G1CameraViewInfo.Count][];

    private readonly Texture2D[] textures =
        new Texture2D[G1CameraViewInfo.Count];

    private readonly long[] completedFrames =
        new long[G1CameraViewInfo.Count];

    private readonly long[] decodedFrames =
        new long[G1CameraViewInfo.Count];

    private readonly Dictionary<UnityEngine.EntityId, ushort>
        consumerMasks =
            new Dictionary<UnityEngine.EntityId, ushort>();

    private Socket receiveSocket;
    private Socket controlSocket;
    private Thread receiveThread;

    private IPEndPoint robotControlEndpoint;

    private volatile bool stopping;

    private string threadError;

    private ushort aggregateSubscriptionMask;
    private bool yoloRequested;

    private long receivedPackets;
    private long discardedPackets;
    private long incompleteFrames;
    private long replacedCompleteFrames;

    private float nextHeartbeatTime;
    private float nextStatisticsTime;
    private float nextControlWarningTime;

    private sealed class FrameAssembly
    {
        public readonly byte[] Buffer;
        public readonly bool[] ReceivedChunks;

        public int ReceivedCount;
        public long LastTouched;

        public FrameAssembly(
            int jpegSize,
            int chunkCount)
        {
            Buffer = new byte[jpegSize];
            ReceivedChunks = new bool[chunkCount];
            LastTouched = Stopwatch.GetTimestamp();
        }
    }

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Debug.LogError(
                "[G1 Camera UDP] A second receiver exists. " +
                "Only one global receiver may bind UDP port 5056.");

            enabled = false;
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        if (Instance == this)
            StartReceiver();
    }

    private void OnDisable()
    {
        StopReceiver();
    }

    private void OnDestroy()
    {
        StopReceiver();

        if (Instance == this)
            Instance = null;

        for (int i = 0;
             i < textures.Length;
             i++)
        {
            if (textures[i] != null)
            {
                Destroy(textures[i]);
                textures[i] = null;
            }
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            StopReceiver();
        }
        else if (isActiveAndEnabled &&
                 Instance == this)
        {
            StartReceiver();
        }
    }

    private void Update()
    {
        for (int viewId = 0;
             viewId < G1CameraViewInfo.Count;
             viewId++)
        {
            byte[] jpeg = null;

            lock (frameLock)
            {
                if (latestJpegs[viewId] != null)
                {
                    jpeg = latestJpegs[viewId];
                    latestJpegs[viewId] = null;
                }
            }

            if (jpeg != null)
            {
                DecodeLatestJpeg(
                    (G1CameraView)viewId,
                    jpeg);
            }
        }

        string error = null;

        lock (frameLock)
        {
            if (threadError != null)
            {
                error = threadError;
                threadError = null;
            }
        }

        if (error != null)
        {
            Debug.LogError(
                "[G1 Camera UDP] " + error);
        }

        if (Time.unscaledTime >=
            nextHeartbeatTime)
        {
            nextHeartbeatTime =
                Time.unscaledTime +
                Mathf.Max(
                    0.1f,
                    heartbeatIntervalSeconds);

            SendSubscription(
                aggregateSubscriptionMask);
        }

        if (verboseLogging &&
            Time.unscaledTime >=
            nextStatisticsTime)
        {
            nextStatisticsTime =
                Time.unscaledTime + 5f;

            Debug.Log(BuildStatistics());
        }
    }

    public Texture2D GetTexture(
        G1CameraView view)
    {
        int index = (int)view;

        if (!G1CameraViewInfo.IsValid(index))
            return null;

        return textures[index];
    }

    public void SetConsumerMask(
        UnityEngine.Object owner,
        ushort mask)
    {
        if (owner == null)
            return;

        consumerMasks[owner.GetEntityId()] =
            (ushort)(
                mask &
                G1CameraViewInfo.ValidMask);

        RecalculateSubscription();
    }

    public void RemoveConsumer(
        UnityEngine.Object owner)
    {
        if (owner == null)
            return;

        consumerMasks.Remove(
            owner.GetEntityId());

        RecalculateSubscription();
    }

    public void SetYoloRequested(
        bool requested)
    {
        if (yoloRequested == requested)
            return;

        yoloRequested = requested;
        nextHeartbeatTime = 0f;

        YoloRequestChanged?.Invoke(
            yoloRequested);

        if (verboseLogging)
        {
            Debug.Log(
                "[G1 Camera UDP] YOLO request changed to " +
                (yoloRequested ? "ON" : "OFF"));
        }
    }

    private void RecalculateSubscription()
    {
        ushort combined = 0;

        foreach (ushort mask
                 in consumerMasks.Values)
        {
            combined |= mask;
        }

        combined &=
            G1CameraViewInfo.ValidMask;

        if (combined ==
            aggregateSubscriptionMask)
        {
            return;
        }

        aggregateSubscriptionMask =
            combined;

        nextHeartbeatTime = 0f;

        if (verboseLogging)
        {
            Debug.Log(
                "[G1 Camera UDP] Subscription mask changed to 0x" +
                combined.ToString("X4"));
        }
    }

    private void StartReceiver()
    {
        if (receiveThread != null &&
            receiveThread.IsAlive)
        {
            return;
        }

        if (listenPort < 1 ||
            listenPort > 65535 ||
            controlPort < 1 ||
            controlPort > 65535)
        {
            Debug.LogError(
                "[G1 Camera UDP] Invalid UDP port.");
            return;
        }

        if (payloadStride < 1 ||
            payloadStride > 65515)
        {
            Debug.LogError(
                "[G1 Camera UDP] Invalid payload stride.");
            return;
        }

        if (!IPAddress.TryParse(
                robotIp,
                out IPAddress robotAddress))
        {
            Debug.LogError(
                "[G1 Camera UDP] Invalid robot IP: " +
                robotIp);
            return;
        }

        IPAddress expectedAddress = null;

        if (!string.IsNullOrWhiteSpace(
                expectedSenderIp) &&
            !IPAddress.TryParse(
                expectedSenderIp,
                out expectedAddress))
        {
            Debug.LogError(
                "[G1 Camera UDP] Invalid expected sender IP: " +
                expectedSenderIp);
            return;
        }

        Socket receiver = null;
        Socket controller = null;

        try
        {
            receiver = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Dgram,
                ProtocolType.Udp);

            receiver.ReceiveTimeout = 500;
            receiver.ReceiveBufferSize =
                4 * 1024 * 1024;

            receiver.Bind(
                new IPEndPoint(
                    IPAddress.Any,
                    listenPort));

            controller = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Dgram,
                ProtocolType.Udp);

            robotControlEndpoint =
                new IPEndPoint(
                    robotAddress,
                    controlPort);

            stopping = false;

            receiveSocket = receiver;
            controlSocket = controller;

            receiveThread = new Thread(
                () => ReceiveLoop(
                    receiver,
                    expectedAddress))
            {
                IsBackground = true,
                Name = "G1 Camera UDP Receiver"
            };

            receiveThread.Start();

            nextHeartbeatTime = 0f;
            nextStatisticsTime = 0f;

            Debug.Log(
                $"[G1 Camera UDP] Listening on " +
                $"0.0.0.0:{listenPort}; robot control=" +
                $"{robotIp}:{controlPort}");
        }
        catch (Exception exception)
        {
            try
            {
                receiver?.Close();
                controller?.Close();
            }
            catch
            {
                // Ignore cleanup errors.
            }

            receiveSocket = null;
            controlSocket = null;
            receiveThread = null;
            robotControlEndpoint = null;

            Debug.LogError(
                "[G1 Camera UDP] Failed to start: " +
                exception);
        }
    }

    private void StopReceiver()
    {
        // Explicitly clear both views and YOLO at the robot while
        // retaining the user's local toggle preference for resume.
        SendSubscription(0, false);

        stopping = true;

        Socket receiver = receiveSocket;
        Socket controller = controlSocket;

        receiveSocket = null;
        controlSocket = null;
        robotControlEndpoint = null;

        try
        {
            receiver?.Close();
            controller?.Close();
        }
        catch
        {
            // Ignore cleanup errors.
        }

        Thread thread = receiveThread;
        receiveThread = null;

        if (thread != null &&
            thread.IsAlive &&
            thread != Thread.CurrentThread)
        {
            thread.Join(750);
        }
    }

    private void SendSubscription(
        ushort mask,
        bool includeYoloRequest = true)
    {
        Socket socket = controlSocket;
        IPEndPoint endpoint =
            robotControlEndpoint;

        if (socket == null ||
            endpoint == null)
        {
            return;
        }

        mask &=
            G1CameraViewInfo.ValidMask;

        ushort controlMask = mask;

        // Do not spend inference time when no camera window is
        // requesting any view, even if the preference remains ON.
        if (includeYoloRequest &&
            yoloRequested &&
            mask != 0)
        {
            controlMask |=
                YoloControlBit;
        }

        byte[] packet =
        {
            (byte)'G',
            (byte)'1',
            (byte)'Q',
            (byte)'S',
            1,
            (byte)(controlMask & 0xFF),
            (byte)((controlMask >> 8) & 0xFF)
        };

        try
        {
            socket.SendTo(
                packet,
                endpoint);
        }
        catch (Exception exception)
        {
            if (Time.unscaledTime >=
                nextControlWarningTime)
            {
                nextControlWarningTime =
                    Time.unscaledTime + 2f;

                Debug.LogWarning(
                    "[G1 Camera UDP] Subscription heartbeat failed: " +
                    exception.Message);
            }
        }
    }

    private void ReceiveLoop(
        Socket socket,
        IPAddress expectedAddress)
    {
        var assemblies =
            new Dictionary<ulong, FrameAssembly>();

        var packet =
            new byte[HeaderSize + payloadStride];

        EndPoint remoteEndpoint =
            new IPEndPoint(
                IPAddress.Any,
                0);

        while (!stopping)
        {
            try
            {
                int packetLength =
                    socket.ReceiveFrom(
                        packet,
                        0,
                        packet.Length,
                        SocketFlags.None,
                        ref remoteEndpoint);

                Interlocked.Increment(
                    ref receivedPackets);

                var remote =
                    remoteEndpoint as IPEndPoint;

                if (expectedAddress != null &&
                    (remote == null ||
                     !expectedAddress.Equals(
                         remote.Address)))
                {
                    Interlocked.Increment(
                        ref discardedPackets);
                    continue;
                }

                ProcessPacket(
                    packet,
                    packetLength,
                    assemblies);
            }
            catch (SocketException exception)
                when (
                    exception.SocketErrorCode ==
                        SocketError.TimedOut ||
                    exception.SocketErrorCode ==
                        SocketError.WouldBlock ||
                    exception.SocketErrorCode ==
                        SocketError.Interrupted ||
                    exception.SocketErrorCode ==
                        SocketError.OperationAborted)
            {
                // Timeout allows shutdown polling.
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception exception)
            {
                if (!stopping)
                {
                    lock (frameLock)
                    {
                        threadError =
                            "Receive loop failed: " +
                            exception;
                    }
                }

                return;
            }
        }
    }

    private void ProcessPacket(
        byte[] packet,
        int packetLength,
        Dictionary<ulong, FrameAssembly>
            assemblies)
    {
        if (packetLength < HeaderSize ||
            packet[0] != (byte)'G' ||
            packet[1] != (byte)'1' ||
            packet[2] != (byte)'J' ||
            packet[3] != (byte)'P' ||
            packet[4] != ProtocolVersion)
        {
            DiscardPacket();
            return;
        }

        byte flags = packet[5];

        int viewId = flags >> 1;

        if (!G1CameraViewInfo.IsValid(viewId))
        {
            DiscardPacket();
            return;
        }

        uint frameId =
            ReadUInt32LittleEndian(
                packet,
                6);

        int chunkIndex =
            ReadUInt16LittleEndian(
                packet,
                10);

        int chunkCount =
            ReadUInt16LittleEndian(
                packet,
                12);

        int payloadSize =
            ReadUInt16LittleEndian(
                packet,
                14);

        uint jpegSizeValue =
            ReadUInt32LittleEndian(
                packet,
                16);

        if (chunkCount < 1 ||
            chunkIndex < 0 ||
            chunkIndex >= chunkCount ||
            jpegSizeValue < 1 ||
            jpegSizeValue > maximumJpegBytes ||
            payloadSize < 1 ||
            packetLength !=
                HeaderSize + payloadSize)
        {
            DiscardPacket();
            return;
        }

        int jpegSize =
            (int)jpegSizeValue;

        int expectedChunkCount =
            (
                jpegSize +
                payloadStride -
                1
            ) /
            payloadStride;

        if (chunkCount !=
            expectedChunkCount)
        {
            DiscardPacket();
            return;
        }

        int destinationOffset =
            chunkIndex *
            payloadStride;

        int expectedPayloadSize =
            Math.Min(
                payloadStride,
                jpegSize -
                destinationOffset);

        bool markedAsFinal =
            (flags & 1) != 0;

        bool actuallyFinal =
            chunkIndex ==
            chunkCount - 1;

        if (destinationOffset < 0 ||
            destinationOffset >= jpegSize ||
            payloadSize != expectedPayloadSize ||
            markedAsFinal != actuallyFinal)
        {
            DiscardPacket();
            return;
        }

        ulong key =
            ((ulong)(byte)viewId << 32) |
            frameId;

        if (!assemblies.TryGetValue(
                key,
                out FrameAssembly assembly))
        {
            if (assemblies.Count >=
                MaximumAssemblies)
            {
                RemoveOldestAssembly(
                    assemblies);
            }

            assembly = new FrameAssembly(
                jpegSize,
                chunkCount);

            assemblies.Add(
                key,
                assembly);
        }
        else if (
            assembly.Buffer.Length != jpegSize ||
            assembly.ReceivedChunks.Length !=
                chunkCount)
        {
            assemblies.Remove(key);
            DiscardPacket();
            return;
        }

        assembly.LastTouched =
            Stopwatch.GetTimestamp();

        if (assembly.ReceivedChunks[
                chunkIndex])
        {
            return;
        }

        Buffer.BlockCopy(
            packet,
            HeaderSize,
            assembly.Buffer,
            destinationOffset,
            payloadSize);

        assembly.ReceivedChunks[
            chunkIndex] = true;

        assembly.ReceivedCount++;

        if (assembly.ReceivedCount !=
            chunkCount)
        {
            return;
        }

        assemblies.Remove(key);

        lock (frameLock)
        {
            if (latestJpegs[viewId] != null)
            {
                Interlocked.Increment(
                    ref replacedCompleteFrames);
            }

            latestJpegs[viewId] =
                assembly.Buffer;
        }

        Interlocked.Increment(
            ref completedFrames[viewId]);
    }

    private void RemoveOldestAssembly(
        Dictionary<ulong, FrameAssembly>
            assemblies)
    {
        bool found = false;
        ulong oldestKey = 0;
        long oldestTime = long.MaxValue;

        foreach (
            KeyValuePair<ulong, FrameAssembly>
                pair
            in assemblies)
        {
            if (pair.Value.LastTouched <
                oldestTime)
            {
                found = true;
                oldestKey = pair.Key;
                oldestTime =
                    pair.Value.LastTouched;
            }
        }

        if (found)
        {
            assemblies.Remove(oldestKey);

            Interlocked.Increment(
                ref incompleteFrames);
        }
    }

    private void DecodeLatestJpeg(
        G1CameraView view,
        byte[] jpeg)
    {
        int viewId = (int)view;

        try
        {
            Texture2D texture =
                textures[viewId];

            if (texture == null)
            {
                texture = new Texture2D(
                    2,
                    2,
                    TextureFormat.RGB24,
                    false)
                {
                    name =
                        "G1 Camera " +
                        G1CameraViewInfo.Label(view),
                    filterMode =
                        FilterMode.Bilinear,
                    wrapMode =
                        TextureWrapMode.Clamp
                };

                textures[viewId] =
                    texture;
            }

            if (!ImageConversion.LoadImage(
                    texture,
                    jpeg,
                    false))
            {
                Debug.LogWarning(
                    "[G1 Camera UDP] JPEG decode failed for " +
                    G1CameraViewInfo.Label(view));
                return;
            }

            Interlocked.Increment(
                ref decodedFrames[viewId]);

            ViewTextureUpdated?.Invoke(
                view,
                texture);
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[G1 Camera UDP] Display failed for " +
                G1CameraViewInfo.Label(view) +
                ": " +
                exception);
        }
    }

    private string BuildStatistics()
    {
        var complete =
            new StringBuilder();

        var displayed =
            new StringBuilder();

        for (int i = 0;
             i < G1CameraViewInfo.Count;
             i++)
        {
            long completeCount =
                Interlocked.Read(
                    ref completedFrames[i]);

            long displayedCount =
                Interlocked.Read(
                    ref decodedFrames[i]);

            if (completeCount > 0)
            {
                if (complete.Length > 0)
                    complete.Append(',');

                complete.Append(
                    G1CameraViewInfo.Label(
                        (G1CameraView)i));

                complete.Append(':');
                complete.Append(completeCount);
            }

            if (displayedCount > 0)
            {
                if (displayed.Length > 0)
                    displayed.Append(',');

                displayed.Append(
                    G1CameraViewInfo.Label(
                        (G1CameraView)i));

                displayed.Append(':');
                displayed.Append(displayedCount);
            }
        }

        return
            "[G1 Camera UDP] mask=0x" +
            aggregateSubscriptionMask.ToString("X4") +
            " yolo=" +
            (
                yoloRequested &&
                aggregateSubscriptionMask != 0
                    ? "ON"
                    : "OFF"
            ) +
            " packets=" +
            Interlocked.Read(ref receivedPackets) +
            " malformed=" +
            Interlocked.Read(ref discardedPackets) +
            " incomplete=" +
            Interlocked.Read(ref incompleteFrames) +
            " replacedComplete=" +
            Interlocked.Read(ref replacedCompleteFrames) +
            " complete={" +
            complete +
            "} displayed={" +
            displayed +
            "}";
    }

    private void DiscardPacket()
    {
        Interlocked.Increment(
            ref discardedPackets);
    }

    private static int
        ReadUInt16LittleEndian(
            byte[] data,
            int offset)
    {
        return
            data[offset] |
            (data[offset + 1] << 8);
    }

    private static uint
        ReadUInt32LittleEndian(
            byte[] data,
            int offset)
    {
        return
            (uint)data[offset] |
            ((uint)data[offset + 1] << 8) |
            ((uint)data[offset + 2] << 16) |
            ((uint)data[offset + 3] << 24);
    }
}