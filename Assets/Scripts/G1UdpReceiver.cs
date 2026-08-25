using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using TMPro;
using UnityEngine;

public class G1UdpReceiver : MonoBehaviour
{
    [Header("UDP")]
    public int listenPort = 5055;

    [Header("Optional UI")]
    public TMP_Text statusText;

    private UdpClient udp;
    private Thread receiveThread;
    private volatile bool running;

    private readonly object dataLock = new object();

    // General packet diagnostics
    private string lastMessage = "Waiting...";
    private string lastSender = "-";
    private int packetCount = 0;
    private DateTime lastPacketTime;

    // Old single-joint telemetry support
    private float latestJointRad = 0.0f;
    private bool hasJointData = false;

    // Full 29-DOF telemetry
    private readonly float[] latestJoints = new float[29];
    private bool hasFullJointData = false;
    private int latestRobotMode = -1;

    void Start()
    {
        try
        {
            udp = new UdpClient(listenPort);
            udp.Client.ReceiveTimeout = 500;

            running = true;

            receiveThread = new Thread(ReceiveLoop);
            receiveThread.IsBackground = true;
            receiveThread.Start();

            Debug.Log(
                $"G1UdpReceiver listening on UDP port {listenPort}"
            );
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"G1UdpReceiver failed to start: {e}"
            );
        }
    }

    private void ReceiveLoop()
    {
        IPEndPoint remote =
            new IPEndPoint(IPAddress.Any, 0);

        while (running)
        {
            try
            {
                byte[] bytes =
                    udp.Receive(ref remote);

                string message =
                    Encoding.UTF8.GetString(bytes);

                ParseTelemetry(message);

                lock (dataLock)
                {
                    lastMessage = message;
                    lastSender = remote.Address.ToString();
                    packetCount++;
                    lastPacketTime = DateTime.UtcNow;
                }
            }
            catch (SocketException)
            {
                // Receive timeout.
                // This is intentional so the thread periodically
                // gets a chance to check "running".
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"UDP receive error: {e}"
                );
            }
        }
    }

    private void ParseTelemetry(string message)
    {
        // -----------------------------------------------------
        // Legacy single-joint packet:
        //
        // G1J1|seq|tick|q_rad|age
        // -----------------------------------------------------

        if (message.StartsWith("G1J1|"))
        {
            string[] parts =
                message.Split('|');

            if (parts.Length >= 5 &&
                float.TryParse(
                    parts[3],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float q))
            {
                lock (dataLock)
                {
                    latestJointRad = q;
                    hasJointData = true;
                }
            }

            return;
        }

        // -----------------------------------------------------
        // Full-body packet:
        //
        // G1Q
        // | seq
        // | tick
        // | mode
        // | q0
        // | q1
        // ...
        // | q28
        // | age
        //
        // Total fields = 34
        // -----------------------------------------------------

        if (message.StartsWith("G1Q|"))
        {
            string[] parts =
                message.Split('|');

            if (parts.Length < 34)
                return;

            if (!int.TryParse(
                    parts[3],
                    out int robotMode))
            {
                return;
            }

            float[] parsed =
                new float[29];

            for (int i = 0; i < 29; i++)
            {
                bool ok =
                    float.TryParse(
                        parts[4 + i],
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out parsed[i]);

                if (!ok)
                    return;
            }

            lock (dataLock)
            {
                Array.Copy(
                    parsed,
                    latestJoints,
                    29
                );

                latestRobotMode =
                    robotMode;

                hasFullJointData =
                    true;

                // Keep old single-joint accessor working.
                latestJointRad =
                    parsed[15];

                hasJointData =
                    true;
            }
        }
    }

    public bool TryGetJointRad(
        out float qRad)
    {
        lock (dataLock)
        {
            qRad = latestJointRad;
            return hasJointData;
        }
    }

    public bool TryGetAllJoints(
        float[] destination,
        out int robotMode)
    {
        lock (dataLock)
        {
            robotMode =
                latestRobotMode;

            if (!hasFullJointData)
                return false;

            if (destination == null)
                return false;

            if (destination.Length < 29)
                return false;

            Array.Copy(
                latestJoints,
                destination,
                29
            );

            return true;
        }
    }

    void Update()
    {
        if (statusText == null)
            return;

        string sender;
        int count;
        DateTime timestamp;

        lock (dataLock)
        {
            sender = lastSender;
            count = packetCount;
            timestamp = lastPacketTime;
        }

        if (count == 0)
        {
            statusText.text =
                "Robot: waiting...";

            return;
        }

        double age =
            (DateTime.UtcNow - timestamp)
            .TotalSeconds;

        bool hasFull;
        int mode;
        float leftShoulder;

        lock (dataLock)
        {
            hasFull =
                hasFullJointData;

            mode =
                latestRobotMode;

            leftShoulder =
                latestJoints[15];
        }

        if (hasFull)
        {
            float shoulderDeg =
                leftShoulder *
                Mathf.Rad2Deg;

            statusText.text =
                $"Robot: CONNECTED | " +
                $"{sender} | " +
                $"#{count} | " +
                $"{age:F2}s\n" +
                $"Mode {mode} | " +
                $"L shoulder {shoulderDeg:F1}°";
        }
        else
        {
            statusText.text =
                $"Robot: CONNECTED | " +
                $"{sender} | " +
                $"#{count} | " +
                $"{age:F2}s";
        }
    }

    private void StopReceiver()
    {
        running = false;

        try
        {
            udp?.Close();
        }
        catch
        {
        }

        if (receiveThread != null &&
            receiveThread.IsAlive &&
            Thread.CurrentThread != receiveThread)
        {
            receiveThread.Join(1000);
        }
    }

    void OnDestroy()
    {
        StopReceiver();
    }

    void OnApplicationQuit()
    {
        StopReceiver();
    }
}
