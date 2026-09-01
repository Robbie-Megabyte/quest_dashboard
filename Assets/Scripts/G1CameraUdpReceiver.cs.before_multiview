using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Stopwatch = System.Diagnostics.Stopwatch;

[RequireComponent(typeof(RawImage))]
public sealed class G1CameraUdpReceiver : MonoBehaviour
{
    private const int HeaderSize = 20;
    private const byte ProtocolVersion = 1;
    private const int MaximumAssemblies = 4;

    [Header("UDP")]
    [SerializeField] private int listenPort = 5056;
    [SerializeField] private string expectedSenderIp = "192.168.0.116";

    [Tooltip("1200-byte packet minus the 20-byte protocol header.")]
    [SerializeField] private int payloadStride = 1180;

    [SerializeField] private int maximumJpegBytes = 2097152;

    [Header("Display")]
    [SerializeField] private RawImage targetImage;
    [SerializeField] private AspectRatioFitter aspectRatioFitter;
    [SerializeField] private bool flipVertically;
    [SerializeField] private bool verboseLogging = true;

    private readonly object frameLock = new object();

    private Socket receiveSocket;
    private Thread receiveThread;
    private volatile bool stopping;

    private byte[] latestJpeg;
    private string threadError;
    private Texture2D cameraTexture;

    private long receivedPackets;
    private long discardedPackets;
    private long completedFrames;
    private long decodedFrames;

    private float nextStatisticsTime;

    private sealed class FrameAssembly
    {
        public readonly byte[] Buffer;
        public readonly bool[] ReceivedChunks;
        public int ReceivedCount;
        public long LastTouched;

        public FrameAssembly(int jpegSize, int chunkCount)
        {
            Buffer = new byte[jpegSize];
            ReceivedChunks = new bool[chunkCount];
            LastTouched = Stopwatch.GetTimestamp();
        }
    }

    private void Awake()
    {
        if (targetImage == null)
            targetImage = GetComponent<RawImage>();

        if (aspectRatioFitter == null)
            aspectRatioFitter = GetComponent<AspectRatioFitter>();

        targetImage.uvRect = flipVertically
            ? new Rect(0f, 1f, 1f, -1f)
            : new Rect(0f, 0f, 1f, 1f);
    }

    private void OnEnable()
    {
        StartReceiver();
    }

    private void OnDisable()
    {
        StopReceiver();
    }

    private void OnDestroy()
    {
        StopReceiver();

        if (cameraTexture != null)
        {
            Destroy(cameraTexture);
            cameraTexture = null;
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            StopReceiver();
        }
        else if (isActiveAndEnabled)
        {
            StartReceiver();
        }
    }

    private void Update()
    {
        byte[] jpegToDecode = null;
        string errorToReport = null;

        lock (frameLock)
        {
            if (latestJpeg != null)
            {
                jpegToDecode = latestJpeg;
                latestJpeg = null;
            }

            if (threadError != null)
            {
                errorToReport = threadError;
                threadError = null;
            }
        }

        if (errorToReport != null)
            Debug.LogError("[G1 Camera UDP] " + errorToReport);

        if (jpegToDecode != null)
            DecodeLatestJpeg(jpegToDecode);

        if (verboseLogging && Time.unscaledTime >= nextStatisticsTime)
        {
            nextStatisticsTime = Time.unscaledTime + 5f;

            Debug.Log(
                $"[G1 Camera UDP] packets={Interlocked.Read(ref receivedPackets)} " +
                $"discarded={Interlocked.Read(ref discardedPackets)} " +
                $"complete={Interlocked.Read(ref completedFrames)} " +
                $"displayed={Interlocked.Read(ref decodedFrames)}");
        }
    }

    private void StartReceiver()
    {
        if (receiveThread != null && receiveThread.IsAlive)
            return;

        if (listenPort < 1 || listenPort > 65535)
        {
            Debug.LogError("[G1 Camera UDP] Invalid listen port.");
            return;
        }

        if (payloadStride < 1 || payloadStride > 65515)
        {
            Debug.LogError("[G1 Camera UDP] Invalid payload stride.");
            return;
        }

        IPAddress expectedAddress = null;

        if (!string.IsNullOrWhiteSpace(expectedSenderIp) &&
            !IPAddress.TryParse(expectedSenderIp, out expectedAddress))
        {
            Debug.LogError(
                "[G1 Camera UDP] Invalid expected sender IP: " +
                expectedSenderIp);
            return;
        }

        Socket socket = null;

        try
        {
            socket = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Dgram,
                ProtocolType.Udp);

            socket.ReceiveTimeout = 500;
            socket.ReceiveBufferSize = 4 * 1024 * 1024;
            socket.Bind(new IPEndPoint(IPAddress.Any, listenPort));

            stopping = false;
            receiveSocket = socket;

            receiveThread = new Thread(
                () => ReceiveLoop(socket, expectedAddress))
            {
                IsBackground = true,
                Name = "G1 Camera UDP Receiver"
            };

            receiveThread.Start();

            Debug.Log(
                $"[G1 Camera UDP] Listening on 0.0.0.0:{listenPort}; " +
                $"expected sender={expectedSenderIp}");
        }
        catch (Exception exception)
        {
            try
            {
                socket?.Close();
            }
            catch
            {
                // Ignore cleanup errors.
            }

            receiveSocket = null;
            receiveThread = null;

            Debug.LogError(
                "[G1 Camera UDP] Failed to start: " +
                exception);
        }
    }

    private void StopReceiver()
    {
        stopping = true;

        Socket socket = receiveSocket;
        receiveSocket = null;

        try
        {
            socket?.Close();
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

    private void ReceiveLoop(
        Socket socket,
        IPAddress expectedAddress)
    {
        var assemblies =
            new Dictionary<uint, FrameAssembly>();

        var packet =
            new byte[HeaderSize + payloadStride];

        EndPoint remoteEndpoint =
            new IPEndPoint(IPAddress.Any, 0);

        while (!stopping)
        {
            try
            {
                int packetLength = socket.ReceiveFrom(
                    packet,
                    0,
                    packet.Length,
                    SocketFlags.None,
                    ref remoteEndpoint);

                Interlocked.Increment(ref receivedPackets);

                var remoteIp =
                    remoteEndpoint as IPEndPoint;

                if (expectedAddress != null &&
                    (remoteIp == null ||
                     !expectedAddress.Equals(remoteIp.Address)))
                {
                    Interlocked.Increment(ref discardedPackets);
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
                // Timeout permits the thread to notice shutdown.
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
        Dictionary<uint, FrameAssembly> assemblies)
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
        uint frameId = ReadUInt32LittleEndian(packet, 6);
        int chunkIndex = ReadUInt16LittleEndian(packet, 10);
        int chunkCount = ReadUInt16LittleEndian(packet, 12);
        int payloadSize = ReadUInt16LittleEndian(packet, 14);
        uint jpegSizeValue = ReadUInt32LittleEndian(packet, 16);

        if (chunkCount < 1 ||
            chunkIndex < 0 ||
            chunkIndex >= chunkCount ||
            jpegSizeValue < 1 ||
            jpegSizeValue > maximumJpegBytes ||
            payloadSize < 1 ||
            packetLength != HeaderSize + payloadSize)
        {
            DiscardPacket();
            return;
        }

        int jpegSize = (int)jpegSizeValue;
        int expectedChunkCount =
            (jpegSize + payloadStride - 1) /
            payloadStride;

        if (chunkCount != expectedChunkCount)
        {
            DiscardPacket();
            return;
        }

        int destinationOffset =
            chunkIndex * payloadStride;

        int expectedPayloadSize =
            Math.Min(
                payloadStride,
                jpegSize - destinationOffset);

        bool markedAsFinal =
            (flags & 1) != 0;

        bool actuallyFinal =
            chunkIndex == chunkCount - 1;

        if (destinationOffset < 0 ||
            destinationOffset >= jpegSize ||
            payloadSize != expectedPayloadSize ||
            markedAsFinal != actuallyFinal)
        {
            DiscardPacket();
            return;
        }

        FrameAssembly assembly;

        if (!assemblies.TryGetValue(
                frameId,
                out assembly))
        {
            if (assemblies.Count >= MaximumAssemblies)
                RemoveOldestAssembly(assemblies);

            assembly =
                new FrameAssembly(
                    jpegSize,
                    chunkCount);

            assemblies.Add(frameId, assembly);
        }
        else if (
            assembly.Buffer.Length != jpegSize ||
            assembly.ReceivedChunks.Length != chunkCount)
        {
            assemblies.Remove(frameId);
            DiscardPacket();
            return;
        }

        assembly.LastTouched =
            Stopwatch.GetTimestamp();

        if (assembly.ReceivedChunks[chunkIndex])
            return;

        Buffer.BlockCopy(
            packet,
            HeaderSize,
            assembly.Buffer,
            destinationOffset,
            payloadSize);

        assembly.ReceivedChunks[chunkIndex] = true;
        assembly.ReceivedCount++;

        if (assembly.ReceivedCount != chunkCount)
            return;

        assemblies.Remove(frameId);

        lock (frameLock)
        {
            // Latest complete frame replaces any frame not yet decoded.
            latestJpeg = assembly.Buffer;
        }

        Interlocked.Increment(ref completedFrames);
    }

    private static void RemoveOldestAssembly(
        Dictionary<uint, FrameAssembly> assemblies)
    {
        bool found = false;
        uint oldestKey = 0;
        long oldestTime = long.MaxValue;

        foreach (KeyValuePair<uint, FrameAssembly> pair
                 in assemblies)
        {
            if (pair.Value.LastTouched < oldestTime)
            {
                found = true;
                oldestKey = pair.Key;
                oldestTime = pair.Value.LastTouched;
            }
        }

        if (found)
            assemblies.Remove(oldestKey);
    }

    private void DecodeLatestJpeg(byte[] jpeg)
    {
        try
        {
            if (cameraTexture == null)
            {
                cameraTexture = new Texture2D(
                    2,
                    2,
                    TextureFormat.RGB24,
                    false)
                {
                    name = "G1 Camera UDP Texture",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
            }

            if (!ImageConversion.LoadImage(
                    cameraTexture,
                    jpeg,
                    false))
            {
                Debug.LogWarning(
                    "[G1 Camera UDP] JPEG decoding failed.");
                return;
            }

            targetImage.texture = cameraTexture;

            if (aspectRatioFitter != null &&
                cameraTexture.height > 0)
            {
                aspectRatioFitter.aspectRatio =
                    (float)cameraTexture.width /
                    cameraTexture.height;
            }

            Interlocked.Increment(ref decodedFrames);
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[G1 Camera UDP] JPEG display failed: " +
                exception);
        }
    }

    private void DiscardPacket()
    {
        Interlocked.Increment(ref discardedPackets);
    }

    private static int ReadUInt16LittleEndian(
        byte[] data,
        int offset)
    {
        return data[offset] |
               (data[offset + 1] << 8);
    }

    private static uint ReadUInt32LittleEndian(
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