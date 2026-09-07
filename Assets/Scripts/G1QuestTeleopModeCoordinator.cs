using System;
using UnityEngine;

[DefaultExecutionOrder(50)]
[DisallowMultipleComponent]
public sealed class G1QuestTeleopModeCoordinator :
    MonoBehaviour
{
    public enum ModeState
    {
        Unavailable,
        Locked,
        Ready,
        ConfirmEntry,
        Aligning,
        TeleopActive,
        Transition,
        Fault
    }

    [Header("References")]

    [SerializeField]
    private G1DashboardTelemetryClient telemetryClient;

    [SerializeField]
    private G1QuestLocomotionSender locomotionSender;

    [Header("Entry Sequence")]

    [SerializeField, Min(0f)]
    private float readinessStableSeconds = 0.5f;

    [SerializeField, Min(1f)]
    private float alignmentCountdownSeconds = 3f;

    [Header("Action Transport")]

    [SerializeField, Min(0.5f)]
    private float actionTransitionTimeoutSeconds = 2f;

    [Header("Development")]

    [Tooltip(
        "Simulates enter/exit locally. No robot teleop action " +
        "request is transmitted while this is enabled."
    )]
    [SerializeField]
    private bool dryRun = true;

    [SerializeField]
    private bool verboseLogging = true;

    public ModeState State { get; private set; }

    public string StatusTitle { get; private set; } =
        "UNAVAILABLE";

    public string StatusDetail { get; private set; } =
        "Waiting for controller telemetry.";

    public string XrStatusLabel { get; private set; } =
        "XR WAIT";

    public bool EntryPanelOpen { get; private set; }

    public bool CanConfirmEntry { get; private set; }

    public float AlignmentSecondsRemaining
    {
        get
        {
            if (countdownEndsAt < 0f)
                return 0f;

            return Mathf.Max(
                0f,
                countdownEndsAt -
                Time.unscaledTime);
        }
    }

    public bool IsTeleopActive
    {
        get
        {
            return
                simulatedTeleopActive ||
                IsServerTeleopActive(
                    GetServerState());
        }
    }

    public bool BlocksHudEditing
    {
        get
        {
            return
                IsTeleopActive ||
                State == ModeState.Aligning ||
                State == ModeState.Transition ||
                State == ModeState.Fault;
        }
    }

    private float readinessBecameTrueAt = -1f;
    private float countdownEndsAt = -1f;
    private bool simulatedTeleopActive;
    private string pendingActionOperation = string.Empty;
    private float pendingActionSentAt = -1f;
    private ModeState lastLoggedState;
    private bool hasLoggedState;

    private void Awake()
    {
        ResolveReferences();
        RefreshState(true);
    }

    private void OnEnable()
    {
        ResolveReferences();
        RefreshState(true);
    }

    private void Update()
    {
        RefreshState(false);
    }

    /*
     * Wire the permanent top button to this method.
     *
     * In teleop, it exits immediately. In every other state,
     * it opens the information/confirmation panel.
     */
    public void HandleTopButtonPressed()
    {
        RefreshState(false);

        if (!string.IsNullOrEmpty(
                pendingActionOperation))
        {
            return;
        }

        if (IsTeleopActive)
        {
            ExitTeleopImmediately();
            return;
        }

        if (countdownEndsAt >= 0f)
        {
            CancelEntry();
            return;
        }

        EntryPanelOpen = true;
        RefreshState(true);
    }

    /*
     * The future one-second hold button calls this only after
     * its hold has completed.
     */
    public void ConfirmEntryHoldCompleted()
    {
        RefreshState(false);

        if (!CanConfirmEntry)
        {
            EntryPanelOpen = true;
            RefreshState(true);
            return;
        }

        EntryPanelOpen = false;
        countdownEndsAt =
            Time.unscaledTime +
            Mathf.Max(
                1f,
                alignmentCountdownSeconds);

        SetLocomotionAllowed(false);

        SetState(
            ModeState.Aligning,
            "ALIGN HANDS",
            "Keep your hands aligned with the robot. " +
            "Teleoperation remains blocked during countdown.",
            true);
    }

    public void CancelEntry()
    {
        countdownEndsAt = -1f;
        EntryPanelOpen = false;
        simulatedTeleopActive = false;

        RefreshState(true);
    }

    /*
     * The top red button uses this directly—there is no exit
     * confirmation panel.
     */
    public void ExitTeleopImmediately()
    {
        countdownEndsAt = -1f;
        EntryPanelOpen = false;

        if (dryRun)
        {
            simulatedTeleopActive = false;
            RefreshState(true);
            return;
        }

        string error = null;

        if (
            locomotionSender == null ||
            !locomotionSender.TrySendTeleopAction(
                "HAND_BACK_ARMS",
                out error)
        )
        {
            SetState(
                ModeState.TeleopActive,
                "EXIT SEND FAILED",
                SafeText(
                    error,
                    "Quest action transport is unavailable."),
                true);

            return;
        }

        BeginPendingAction(
            "HAND_BACK_ARMS");

        SetLocomotionAllowed(false);

        SetState(
            ModeState.Transition,
            "EXIT REQUESTED",
            "Waiting for controlled arm handback.",
            true);
    }

    private void BeginPendingAction(
        string operation)
    {
        pendingActionOperation =
            operation ?? string.Empty;
        pendingActionSentAt =
            Time.unscaledTime;
        CanConfirmEntry = false;
    }

    private void ClearPendingAction()
    {
        pendingActionOperation = string.Empty;
        pendingActionSentAt = -1f;
    }

    private void ResolveReferences()
    {
        if (telemetryClient == null)
        {
            telemetryClient =
                FindAnyObjectByType<
                    G1DashboardTelemetryClient>();
        }

        if (locomotionSender == null)
        {
            locomotionSender =
                FindAnyObjectByType<
                    G1QuestLocomotionSender>();
        }
    }

    private void RefreshState(
        bool forceLog)
    {
        ResolveReferences();

        string serverState =
            GetServerState();

        UpdateXrStatus();

        bool serverStateAvailable =
            !string.IsNullOrEmpty(serverState);

        bool pendingAcknowledged =
            pendingActionOperation == "REQUEST_XR"
                ? (
                    serverStateAvailable &&
                    !string.Equals(
                        serverState,
                        "LOCOMOTION_READY",
                        StringComparison.Ordinal)
                )
                : pendingActionOperation ==
                    "HAND_BACK_ARMS"
                    ? (
                        serverStateAvailable &&
                        !IsServerTeleopActive(
                            serverState)
                    )
                    : false;

        if (pendingAcknowledged)
            ClearPendingAction();

        if (!string.IsNullOrEmpty(
                pendingActionOperation))
        {
            float pendingAge =
                Time.unscaledTime -
                pendingActionSentAt;

            if (
                pendingAge <= Mathf.Max(
                    0.5f,
                    actionTransitionTimeoutSeconds)
            )
            {
                CanConfirmEntry = false;
                SetLocomotionAllowed(false);

                SetState(
                    ModeState.Transition,
                    pendingActionOperation ==
                        "HAND_BACK_ARMS"
                        ? "EXIT REQUESTED"
                        : "ENTRY REQUESTED",
                    "Waiting for the robot controller " +
                    "to acknowledge the request.",
                    forceLog);

                return;
            }

            string timedOutOperation =
                pendingActionOperation;

            ClearPendingAction();

            if (
                timedOutOperation ==
                    "HAND_BACK_ARMS" &&
                IsServerTeleopActive(serverState)
            )
            {
                SetLocomotionAllowed(false);

                SetState(
                    ModeState.TeleopActive,
                    "EXIT REQUEST TIMEOUT",
                    "The controller remained in teleoperation. " +
                    "Press Stop Teleop to retry.",
                    true);
            }
            else
            {
                SetLocomotionAllowed(true);

                SetState(
                    ModeState.Locked,
                    "ENTRY REQUEST TIMEOUT",
                    "The controller did not enter a transition. " +
                    "Check the listener log and retry.",
                    true);
            }

            return;
        }

        if (IsServerTeleopActive(serverState))
        {
            simulatedTeleopActive = false;
            countdownEndsAt = -1f;
            EntryPanelOpen = false;
            CanConfirmEntry = false;
            readinessBecameTrueAt = -1f;

            SetLocomotionAllowed(false);

            SetState(
                ModeState.TeleopActive,
                "TELEOP ACTIVE",
                XrStatusLabel +
                " · Press the red mode button to stop.",
                forceLog);

            return;
        }

        if (IsServerTransition(serverState))
        {
            countdownEndsAt = -1f;
            EntryPanelOpen = false;
            CanConfirmEntry = false;
            readinessBecameTrueAt = -1f;

            SetLocomotionAllowed(false);

            SetState(
                ModeState.Transition,
                "TRANSITION",
                DescribeServerTransition(serverState),
                forceLog);

            return;
        }

        if (string.Equals(
                serverState,
                "SAFETY_FAULT_HOLD",
                StringComparison.Ordinal))
        {
            countdownEndsAt = -1f;
            CanConfirmEntry = false;
            readinessBecameTrueAt = -1f;

            SetLocomotionAllowed(false);

            SetState(
                ModeState.Fault,
                "SAFETY HOLD",
                GetSafetyFaultDescription(),
                forceLog);

            return;
        }

        if (simulatedTeleopActive)
        {
            CanConfirmEntry = false;
            SetLocomotionAllowed(false);

            SetState(
                ModeState.TeleopActive,
                "TELEOP ACTIVE · DRY RUN",
                XrStatusLabel +
                " · No robot mode action was transmitted.",
                forceLog);

            return;
        }

        string readinessReason;
        bool rawReady =
            TryEvaluateEntryReadiness(
                out readinessReason);

        if (countdownEndsAt >= 0f)
        {
            if (!rawReady)
            {
                countdownEndsAt = -1f;
                readinessBecameTrueAt = -1f;

                SetLocomotionAllowed(true);

                SetState(
                    ModeState.Locked,
                    "ENTRY CANCELLED",
                    readinessReason,
                    true);

                return;
            }

            SetLocomotionAllowed(false);

            float remaining =
                AlignmentSecondsRemaining;

            if (remaining > 0f)
            {
                SetState(
                    ModeState.Aligning,
                    "ALIGN HANDS · " +
                    Mathf.CeilToInt(remaining),
                    XrStatusLabel +
                    " · Match the robot pose and hold still.",
                    forceLog);

                return;
            }

            countdownEndsAt = -1f;

            if (dryRun)
            {
                simulatedTeleopActive = true;

                SetState(
                    ModeState.TeleopActive,
                    "TELEOP ACTIVE · DRY RUN",
                    XrStatusLabel +
                    " · No robot mode action was transmitted.",
                    true);
            }
            else
            {
                string error = null;

                if (
                    locomotionSender != null &&
                    locomotionSender.TrySendTeleopAction(
                        "REQUEST_XR",
                        out error)
                )
                {
                    BeginPendingAction(
                        "REQUEST_XR");

                    SetLocomotionAllowed(false);

                    SetState(
                        ModeState.Transition,
                        "ENTRY REQUESTED",
                        "Waiting for the robot controller " +
                        "to begin the handover.",
                        true);
                }
                else
                {
                    SetLocomotionAllowed(true);

                    SetState(
                        ModeState.Locked,
                        "ACTION SEND FAILED",
                        SafeText(
                            error,
                            "Quest action transport is unavailable."),
                        true);
                }
            }

            return;
        }

        SetLocomotionAllowed(true);

        if (rawReady)
        {
            if (readinessBecameTrueAt < 0f)
            {
                readinessBecameTrueAt =
                    Time.unscaledTime;
            }

            CanConfirmEntry =
                Time.unscaledTime -
                readinessBecameTrueAt >=
                Mathf.Max(
                    0f,
                    readinessStableSeconds);

            if (CanConfirmEntry)
            {
                SetState(
                    EntryPanelOpen
                        ? ModeState.ConfirmEntry
                        : ModeState.Ready,
                    EntryPanelOpen
                        ? "ENTER TELEOP?"
                        : "TELEOP READY",
                    XrStatusLabel +
                    " · Hold Enter for 1 second.",
                    forceLog);
            }
            else
            {
                SetState(
                    ModeState.Locked,
                    "CHECKING",
                    "Safety conditions are stabilizing.",
                    forceLog);
            }

            return;
        }

        readinessBecameTrueAt = -1f;
        CanConfirmEntry = false;

        SetState(
            telemetryClient == null ||
            !telemetryClient.HasData ||
            !telemetryClient.TransportFresh
                ? ModeState.Unavailable
                : ModeState.Locked,
            "TELEOP LOCKED",
            readinessReason,
            forceLog);
    }

    private bool TryEvaluateEntryReadiness(
        out string reason)
    {
        reason = "Waiting for controller telemetry.";

        if (telemetryClient == null ||
            !telemetryClient.HasData ||
            !telemetryClient.TransportFresh)
        {
            return false;
        }

        G1DashboardTelemetryClient.Envelope envelope =
            telemetryClient.Latest;

        if (envelope == null ||
            envelope.connection == null ||
            !envelope.connection.online)
        {
            reason = "Controller telemetry is offline.";
            return false;
        }

        if (envelope.control == null)
        {
            reason = "Controller state is unavailable.";
            return false;
        }

        if (!string.Equals(
                envelope.control.state,
                "LOCOMOTION_READY",
                StringComparison.Ordinal))
        {
            reason =
                "Controller state: " +
                SafeText(
                    envelope.control.state,
                    "unknown");

            return false;
        }

        if (envelope.actions == null ||
            !envelope.actions.request_channel_enabled)
        {
            reason =
                "Controller action channel is unavailable.";

            return false;
        }

        G1DashboardTelemetryClient
            .EngagementConditions conditions =
                envelope.actions
                    .engagement_conditions;

        if (conditions == null)
        {
            reason =
                "Safety conditions are unavailable.";

            return false;
        }

        if (!conditions.lowstate_ok)
        {
            reason =
                "Robot low-state telemetry is not ready.";

            return false;
        }

        if (!conditions.safety_fault_clear)
        {
            reason =
                "A controller safety fault is active.";

            return false;
        }

        if (conditions.tracking_hold_active)
        {
            reason =
                "Controller tracking hold is active.";

            return false;
        }

        if (!conditions.stop_gate_ready)
        {
            reason =
                "Wait for the robot stop gate.";

            return false;
        }

        if (!conditions.xr_ok)
        {
            reason =
                "XR BAD · " +
                SafeText(
                    conditions.xr_reason,
                    "hand tracking is not ready");

            return false;
        }

        if (envelope.hands == null ||
            !envelope.hands.tracking_valid)
        {
            reason =
                "XR BAD · Hand tracking is not valid.";

            return false;
        }

        G1DashboardTelemetryClient.XrHandover handover =
            envelope.actions.xr_handover;

        if (handover == null)
        {
            reason =
                "Teleop action readiness is unavailable.";

            return false;
        }

        if (!string.Equals(
                handover.operation,
                "REQUEST_XR",
                StringComparison.Ordinal))
        {
            reason =
                SafeText(
                    handover.reason,
                    "Teleop entry is not currently available.");

            return false;
        }

        if (!handover.available)
        {
            reason =
                SafeText(
                    handover.reason,
                    "Teleop entry is locked.");

            return false;
        }

        if (locomotionSender != null &&
            !locomotionSender.ControlsNeutral)
        {
            reason =
                "Release the deadman and center both sticks.";

            return false;
        }

        reason = "Ready.";
        return true;
    }

    private void UpdateXrStatus()
    {
        if (telemetryClient == null ||
            !telemetryClient.HasData ||
            !telemetryClient.TransportFresh ||
            telemetryClient.Latest == null ||
            telemetryClient.Latest.control == null)
        {
            XrStatusLabel = "XR WAIT";
            return;
        }

        XrStatusLabel =
            telemetryClient.Latest.control.xr_ok
                ? "XR OK"
                : "XR BAD";
    }

    private string GetServerState()
    {
        if (telemetryClient == null ||
            telemetryClient.Latest == null ||
            telemetryClient.Latest.control == null)
        {
            return string.Empty;
        }

        return
            telemetryClient.Latest.control.state ??
            string.Empty;
    }

    private string GetSafetyFaultDescription()
    {
        if (telemetryClient != null &&
            telemetryClient.Latest != null &&
            telemetryClient.Latest.control != null)
        {
            return SafeText(
                telemetryClient
                    .Latest
                    .control
                    .safety_fault,
                "The robot controller is holding a safety fault.");
        }

        return "The robot controller is holding a safety fault.";
    }

    private void SetLocomotionAllowed(
        bool allowed)
    {
        if (locomotionSender != null)
        {
            locomotionSender.SetCommandEnabled(
                allowed);
        }
    }

    private void SetState(
        ModeState state,
        string title,
        string detail,
        bool forceLog)
    {
        State = state;
        StatusTitle = title ?? string.Empty;
        StatusDetail = detail ?? string.Empty;

        if (
            verboseLogging &&
            (
                forceLog ||
                !hasLoggedState ||
                lastLoggedState != state
            ))
        {
            Debug.Log(
                "[G1 Teleop Mode] " +
                $"state={state} " +
                $"title=\"{StatusTitle}\" " +
                $"detail=\"{StatusDetail}\"",
                this);

            lastLoggedState = state;
            hasLoggedState = true;
        }
    }

    private static bool IsServerTeleopActive(
        string state)
    {
        return
            string.Equals(
                state,
                "XR_ACTIVE",
                StringComparison.Ordinal) ||
            string.Equals(
                state,
                "XR_TRACKING_HOLD",
                StringComparison.Ordinal);
    }

    private static bool IsServerTransition(
        string state)
    {
        return
            string.Equals(
                state,
                "WAITING_FOR_STOP",
                StringComparison.Ordinal) ||
            string.Equals(
                state,
                "XR_ALIGNMENT",
                StringComparison.Ordinal) ||
            string.Equals(
                state,
                "ARM_RAMP_UP",
                StringComparison.Ordinal) ||
            string.Equals(
                state,
                "RETURN_ARMS_HOME",
                StringComparison.Ordinal) ||
            string.Equals(
                state,
                "ARM_RAMP_DOWN",
                StringComparison.Ordinal);
    }

    private static string DescribeServerTransition(
        string state)
    {
        switch (state)
        {
            case "WAITING_FOR_STOP":
                return "Waiting for the robot to stop.";

            case "XR_ALIGNMENT":
                return "Align your hands with the robot.";

            case "ARM_RAMP_UP":
                return "Teleoperation is engaging.";

            case "RETURN_ARMS_HOME":
                return "Returning the arms home.";

            case "ARM_RAMP_DOWN":
                return "Teleoperation is disengaging.";

            default:
                return "Controller transition in progress.";
        }
    }

    private static string SafeText(
        string value,
        string fallback)
    {
        return
            string.IsNullOrWhiteSpace(value)
                ? fallback
                : value;
    }
}
