using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class G1QuestLocomotionSender :
    MonoBehaviour
{
    private const int BodyBytes = 52;
    private const int ActionBodyBytes = 32;
    private const int HmacBytes = 32;

    [Header("Robot")]

    [SerializeField]
    private string robotIp = "192.168.0.116";

    [SerializeField]
    private int robotPort = 5059;

    [SerializeField]
    private string keyResourcePath =
        "LocalSecrets/g1_unity_televuer_key";

    [Header("XRI Input Actions")]

    [SerializeField]
    private InputActionReference leftMoveAction;

    [SerializeField]
    private InputActionReference rightTurnAction;

    [SerializeField]
    private InputActionReference leftDeadmanAction;

    [Header("Mode Gate")]

    [SerializeField, Range(0.05f, 0.5f)]
    private float neutralInputThreshold = 0.15f;

    [Header("Transmission")]

    [SerializeField, Range(10f, 60f)]
    private float sendRateHz = 30f;

    [SerializeField]
    private bool verboseLogging = true;

    private UdpClient udp;
    private byte[] key;
    private ulong sessionId;
    private ulong sequence;
    private ulong actionSequence;

    private float nextSendTime;
    private float nextLogTime;
    private long transmittedPackets;
    private bool commandEnabled = true;

    public Vector2 CurrentLeftInput { get; private set; }

    public Vector2 CurrentRightInput { get; private set; }

    public float CurrentDeadman { get; private set; }

    public bool CommandEnabled
    {
        get { return commandEnabled; }
    }

    public bool ControlsNeutral
    {
        get
        {
            float threshold =
                Mathf.Clamp(
                    neutralInputThreshold,
                    0.05f,
                    0.5f);

            return
                CurrentLeftInput.magnitude <= threshold &&
                CurrentRightInput.magnitude <= threshold &&
                CurrentDeadman < 0.1f;
        }
    }

    private void Awake()
    {
        try
        {
            TextAsset keyAsset =
                Resources.Load<TextAsset>(
                    keyResourcePath);

            if (keyAsset == null)
            {
                throw new InvalidOperationException(
                    "Missing locomotion key resource: " +
                    keyResourcePath);
            }

            key = ParseHexKey(keyAsset.text);
            sessionId = CreateSessionId();
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[G1 Locomotion TX] setup failed: " +
                exception.Message,
                this);

            enabled = false;
        }
    }

    private void OnEnable()
    {
        OpenTransport();
    }

    private void OnDisable()
    {
        SendNeutralBurst();
        CloseTransport();
    }

    private void OnApplicationQuit()
    {
        SendNeutralBurst();
        CloseTransport();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            SendNeutralBurst();
            CloseTransport();
        }
        else if (isActiveAndEnabled)
        {
            OpenTransport();
        }
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused)
            SendNeutralBurst();
    }

    private void Update()
    {
        CurrentLeftInput =
            ReadVector2(leftMoveAction);

        CurrentRightInput =
            ReadVector2(rightTurnAction);

        CurrentDeadman =
            Mathf.Clamp01(
                ReadFloat(leftDeadmanAction));

        if (udp == null ||
            Time.unscaledTime < nextSendTime)
        {
            return;
        }

        nextSendTime =
            Time.unscaledTime +
            1f / Mathf.Max(10f, sendRateHz);

        Vector2 left =
            commandEnabled
                ? CurrentLeftInput
                : Vector2.zero;

        Vector2 right =
            commandEnabled
                ? CurrentRightInput
                : Vector2.zero;

        float deadman =
            commandEnabled
                ? CurrentDeadman
                : 0f;

        SendPacket(
            left,
            right,
            deadman);

        if (
            verboseLogging &&
            Time.unscaledTime >= nextLogTime)
        {
            nextLogTime =
                Time.unscaledTime + 1f;

            Debug.Log(
                "[G1 Locomotion TX] " +
                $"packets={transmittedPackets} " +
                $"gate={(commandEnabled ? "OPEN" : "BLOCKED")} " +
                $"left=({left.x:+0.000;-0.000;0.000}," +
                $"{left.y:+0.000;-0.000;0.000}) " +
                $"right=({right.x:+0.000;-0.000;0.000}," +
                $"{right.y:+0.000;-0.000;0.000}) " +
                $"deadman={deadman:0.000}",
                this);
        }
    }

    public void SetCommandEnabled(
        bool value)
    {
        if (commandEnabled == value)
            return;

        commandEnabled = value;

        if (!commandEnabled)
            SendNeutralBurst();

        if (verboseLogging)
        {
            Debug.Log(
                "[G1 Locomotion TX] command gate " +
                (commandEnabled
                    ? "OPEN"
                    : "BLOCKED"),
                this);
        }
    }

    public bool TrySendTeleopAction(
        string operation,
        out string error)
    {
        error = null;

        if (udp == null || key == null)
        {
            error = "Quest action transport is unavailable.";
            return false;
        }

        ushort operationCode;

        switch (operation)
        {
            case "REQUEST_XR":
                operationCode = 1;
                break;

            case "CANCEL_XR_REQUEST":
                operationCode = 2;
                break;

            case "HAND_BACK_ARMS":
                operationCode = 3;
                break;

            case "HOLD_XR_POSE":
                operationCode = 4;
                break;

            case "RESUME_XR_POSE":
                operationCode = 5;
                break;

            default:
                error =
                    "Unsupported teleop action: " +
                    operation;
                return false;
        }

        try
        {
            actionSequence++;

            byte[] body =
                new byte[ActionBodyBytes];

            body[0] = (byte)'G';
            body[1] = (byte)'1';
            body[2] = (byte)'A';
            body[3] = (byte)'1';

            WriteUInt16(body, 4, 1);
            WriteUInt16(
                body,
                6,
                operationCode);

            WriteUInt64(body, 8, sessionId);
            WriteUInt64(
                body,
                16,
                actionSequence);
            WriteUInt64(
                body,
                24,
                GetMonotonicNanoseconds());

            byte[] digest;

            using (
                HMACSHA256 hmac =
                    new HMACSHA256(key))
            {
                digest =
                    hmac.ComputeHash(body);
            }

            byte[] packet =
                new byte[
                    ActionBodyBytes +
                    HmacBytes];

            Buffer.BlockCopy(
                body,
                0,
                packet,
                0,
                ActionBodyBytes);

            Buffer.BlockCopy(
                digest,
                0,
                packet,
                ActionBodyBytes,
                HmacBytes);

            /*
             * Repeat one idempotent sequence to tolerate an
             * occasional Wi-Fi UDP loss. The robot accepts the
             * first copy and silently ignores the duplicates.
             */
            for (int index = 0;
                 index < 3;
                 index++)
            {
                udp.Send(
                    packet,
                    packet.Length);
            }

            if (verboseLogging)
            {
                Debug.Log(
                    "[G1 Teleop Action TX] " +
                    $"operation={operation} " +
                    $"sequence={actionSequence}",
                    this);
            }

            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;

            Debug.LogWarning(
                "[G1 Teleop Action TX] send failed: " +
                exception.Message,
                this);

            return false;
        }
    }

    private void OpenTransport()
    {
        if (udp != null || key == null)
            return;

        try
        {
            if (!IPAddress.TryParse(
                    robotIp,
                    out IPAddress address))
            {
                throw new InvalidOperationException(
                    "Invalid robot IP: " + robotIp);
            }

            udp = new UdpClient(
                AddressFamily.InterNetwork);

            udp.Connect(
                new IPEndPoint(
                    address,
                    robotPort));

            nextSendTime = 0f;

            Debug.Log(
                $"[G1 Locomotion TX] target=" +
                $"{robotIp}:{robotPort} rate={sendRateHz:0}Hz",
                this);
        }
        catch (Exception exception)
        {
            CloseTransport();

            Debug.LogError(
                "[G1 Locomotion TX] open failed: " +
                exception.Message,
                this);
        }
    }

    private void CloseTransport()
    {
        if (udp == null)
            return;

        try
        {
            udp.Close();
        }
        catch
        {
            // Transport is already closing.
        }

        udp = null;
    }

    private void SendNeutralBurst()
    {
        if (udp == null || key == null)
            return;

        for (int index = 0; index < 3; index++)
        {
            SendPacket(
                Vector2.zero,
                Vector2.zero,
                0f);
        }
    }

    private void SendPacket(
        Vector2 left,
        Vector2 right,
        float deadman)
    {
        if (udp == null)
            return;

        try
        {
            sequence++;

            byte[] body =
                new byte[BodyBytes];

            body[0] = (byte)'G';
            body[1] = (byte)'1';
            body[2] = (byte)'L';
            body[3] = (byte)'1';

            WriteUInt16(body, 4, 1);
            WriteUInt16(body, 6, 1);

            WriteUInt64(body, 8, sessionId);
            WriteUInt64(body, 16, sequence);

            WriteUInt64(
                body,
                24,
                GetMonotonicNanoseconds());

            WriteFloat(body, 32, left.x);
            WriteFloat(body, 36, left.y);
            WriteFloat(body, 40, right.x);
            WriteFloat(body, 44, right.y);
            WriteFloat(body, 48, deadman);

            byte[] digest;

            using (
                HMACSHA256 hmac =
                    new HMACSHA256(key))
            {
                digest =
                    hmac.ComputeHash(body);
            }

            byte[] packet =
                new byte[
                    BodyBytes +
                    HmacBytes];

            Buffer.BlockCopy(
                body,
                0,
                packet,
                0,
                BodyBytes);

            Buffer.BlockCopy(
                digest,
                0,
                packet,
                BodyBytes,
                HmacBytes);

            udp.Send(
                packet,
                packet.Length);

            transmittedPackets++;
        }
        catch (Exception exception)
        {
            if (verboseLogging)
            {
                Debug.LogWarning(
                    "[G1 Locomotion TX] send failed: " +
                    exception.Message,
                    this);
            }
        }
    }

    private static Vector2 ReadVector2(
        InputActionReference reference)
    {
        if (
            reference == null ||
            reference.action == null ||
            !reference.action.enabled)
        {
            return Vector2.zero;
        }

        return Vector2.ClampMagnitude(
            reference.action.ReadValue<Vector2>(),
            1f);
    }

    private static float ReadFloat(
        InputActionReference reference)
    {
        if (
            reference == null ||
            reference.action == null ||
            !reference.action.enabled)
        {
            return 0f;
        }

        return reference.action.ReadValue<float>();
    }

    private static ulong CreateSessionId()
    {
        byte[] bytes = new byte[8];

        using (
            RandomNumberGenerator random =
                RandomNumberGenerator.Create())
        {
            random.GetBytes(bytes);
        }

        ulong value =
            BitConverter.ToUInt64(bytes, 0);

        return value == 0 ? 1UL : value;
    }

    private static ulong GetMonotonicNanoseconds()
    {
        double seconds =
            (double)System.Diagnostics.Stopwatch
                .GetTimestamp() /
            System.Diagnostics.Stopwatch.Frequency;

        return (ulong)(
            seconds * 1_000_000_000.0);
    }

    private static byte[] ParseHexKey(
        string text)
    {
        string normalized =
            (text ?? string.Empty).Trim();

        if (
            normalized.Length < 64 ||
            normalized.Length % 2 != 0)
        {
            throw new InvalidOperationException(
                "Locomotion key must contain at least " +
                "32 bytes encoded as hexadecimal.");
        }

        byte[] result =
            new byte[normalized.Length / 2];

        for (int index = 0;
             index < result.Length;
             index++)
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

    private static void WriteUInt16(
        byte[] destination,
        int offset,
        ushort value)
    {
        destination[offset] =
            (byte)value;

        destination[offset + 1] =
            (byte)(value >> 8);
    }

    private static void WriteUInt64(
        byte[] destination,
        int offset,
        ulong value)
    {
        for (int index = 0;
             index < 8;
             index++)
        {
            destination[offset + index] =
                (byte)(value >> (index * 8));
        }
    }

    private static void WriteFloat(
        byte[] destination,
        int offset,
        float value)
    {
        byte[] bytes =
            BitConverter.GetBytes(value);

        if (!BitConverter.IsLittleEndian)
            Array.Reverse(bytes);

        Buffer.BlockCopy(
            bytes,
            0,
            destination,
            offset,
            4);
    }
}
