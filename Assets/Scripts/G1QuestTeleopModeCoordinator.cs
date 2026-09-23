using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

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
        PoseHeld,
        Realigning,
        Transition,
        Fault
    }

    [Header("References")]

    [SerializeField]
    private G1DashboardTelemetryClient telemetryClient;

    [SerializeField]
    private G1QuestLocomotionSender locomotionSender;

    [SerializeField]
    private XRInputModalityManager inputModalityManager;

    [Header("Entry Sequence")]

    [SerializeField, Min(0f)]
    private float readinessStableSeconds = 0.5f;

    [SerializeField, Min(1f)]
    private float alignmentCountdownSeconds = 3f;

    [Header("Action Transport")]

    [SerializeField, Min(0.5f)]
    private float actionTransitionTimeoutSeconds = 2f;

    [SerializeField, Min(0f)]
    private float controllerReadyStableSeconds = 0.25f;

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

    public bool CanReleaseFaultHold { get; private set; }

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
    private XRInputModalityManager boundInputModalityManager;
    private bool controllerModeActive;
    private bool trackedHandModeActive;
    private float controllersBecameReadyAt = -1f;
    private bool poseHoldLocomotionArmed;
    private bool faultPanelOpenedForCurrentFault;

    private void Awake()
    {
        ResolveReferences();
        RefreshState(true);
    }

    private void OnEnable()
    {
        ResolveReferences();
        BindInputModalityEvents();
        RefreshState(true);
    }

    private void OnDisable()
    {
        UnbindInputModalityEvents();
        SetLocomotionAllowed(false);
    }

    private void Update()
    {
        BindInputModalityEvents();
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
            if (
                IsTeleopActive &&
                pendingActionOperation !=
                    "HAND_BACK_ARMS")
            {
                ClearPendingAction();
                ExitTeleopImmediately();
            }

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

        if (State == ModeState.Fault)
        {
            ReleaseFaultHold();
            return;
        }

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

    private void ReleaseFaultHold()
    {
        string readinessReason;

        if (!TryEvaluateFaultReleaseReadiness(
                out readinessReason))
        {
            EntryPanelOpen = true;

            SetState(
                ModeState.Fault,
                "SAFETY FAULT · ARMS HELD",
                GetSafetyFaultDescription(
                    readinessReason),
                true);

            return;
        }

        if (dryRun)
        {
            EntryPanelOpen = true;

            SetState(
                ModeState.Fault,
                "FAULT RELEASE BLOCKED",
                "Dry run cannot release real arm ownership.",
                true);

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
            EntryPanelOpen = true;

            SetState(
                ModeState.Fault,
                "RELEASE SEND FAILED",
                SafeText(
                    error,
                    "Quest action transport is unavailable."),
                true);

            return;
        }

        EntryPanelOpen = false;
        BeginPendingAction(
            "HAND_BACK_ARMS");

        SetLocomotionAllowed(false);

        SetState(
            ModeState.Transition,
            "RELEASING ARMS",
            "The frozen target is being held while ownership " +
                "fades to Regular mode.",
            true);
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

        if (inputModalityManager == null)
        {
            inputModalityManager =
                FindAnyObjectByType<
                    XRInputModalityManager>();
        }
    }

    private void BindInputModalityEvents()
    {
        if (inputModalityManager == null)
            ResolveReferences();

        if (
            inputModalityManager == null ||
            boundInputModalityManager ==
                inputModalityManager)
        {
            return;
        }

        UnbindInputModalityEvents();

        boundInputModalityManager =
            inputModalityManager;

        boundInputModalityManager
            .motionControllerModeStarted
            .AddListener(
                HandleMotionControllerModeStarted);

        boundInputModalityManager
            .motionControllerModeEnded
            .AddListener(
                HandleMotionControllerModeEnded);

        boundInputModalityManager
            .trackedHandModeStarted
            .AddListener(
                HandleTrackedHandModeStarted);

        boundInputModalityManager
            .trackedHandModeEnded
            .AddListener(
                HandleTrackedHandModeEnded);

        controllerModeActive =
            AnyControllerTracked();
        trackedHandModeActive =
            AnyTrackedHandActive();
    }

    private void UnbindInputModalityEvents()
    {
        if (boundInputModalityManager == null)
            return;

        boundInputModalityManager
            .motionControllerModeStarted
            .RemoveListener(
                HandleMotionControllerModeStarted);

        boundInputModalityManager
            .motionControllerModeEnded
            .RemoveListener(
                HandleMotionControllerModeEnded);

        boundInputModalityManager
            .trackedHandModeStarted
            .RemoveListener(
                HandleTrackedHandModeStarted);

        boundInputModalityManager
            .trackedHandModeEnded
            .RemoveListener(
                HandleTrackedHandModeEnded);

        boundInputModalityManager = null;
    }

    private void HandleMotionControllerModeStarted()
    {
        controllerModeActive = true;
        controllersBecameReadyAt = -1f;

        if (verboseLogging)
        {
            Debug.Log(
                "[G1 Teleop Mode] motion controller " +
                "mode started; requesting safe pose hold.",
                this);
        }
    }

    private void HandleMotionControllerModeEnded()
    {
        controllerModeActive = false;
        controllersBecameReadyAt = -1f;
    }

    private void HandleTrackedHandModeStarted()
    {
        trackedHandModeActive = true;
    }

    private void HandleTrackedHandModeEnded()
    {
        trackedHandModeActive = false;
    }

    private void RefreshState(
        bool forceLog)
    {
        ResolveReferences();
        CanReleaseFaultHold = false;

        string serverState =
            GetServerState();

        bool safetyFaultActive =
            string.Equals(
                serverState,
                "SAFETY_FAULT_HOLD",
                StringComparison.Ordinal);

        if (!safetyFaultActive)
            faultPanelOpenedForCurrentFault = false;

        if (!string.Equals(
                serverState,
                "XR_OPERATOR_HOLD",
                StringComparison.Ordinal))
        {
            poseHoldLocomotionArmed = false;
            controllersBecameReadyAt = -1f;
        }

        UpdateXrStatus();

        ServiceInputModalityRequests(
            serverState);

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
                        (
                            string.Equals(
                                serverState,
                                "ARM_RAMP_DOWN",
                                StringComparison.Ordinal) ||
                            string.Equals(
                                serverState,
                                "LOCOMOTION_READY",
                                StringComparison.Ordinal)
                        )
                    )
                    : pendingActionOperation ==
                        "HOLD_XR_POSE"
                        ? string.Equals(
                            serverState,
                            "XR_OPERATOR_HOLD",
                            StringComparison.Ordinal)
                        : pendingActionOperation ==
                            "RESUME_XR_POSE"
                            ? (
                                serverStateAvailable &&
                                !string.Equals(
                                    serverState,
                                    "XR_OPERATOR_HOLD",
                                    StringComparison.Ordinal)
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
                        : pendingActionOperation ==
                            "HOLD_XR_POSE"
                            ? "FREEZING POSE"
                            : pendingActionOperation ==
                                "RESUME_XR_POSE"
                                ? "PREPARING HANDS"
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
                safetyFaultActive
            )
            {
                string releaseReason;
                CanReleaseFaultHold =
                    TryEvaluateFaultReleaseReadiness(
                        out releaseReason);
                EntryPanelOpen = true;

                SetLocomotionAllowed(false);

                SetState(
                    ModeState.Fault,
                    "RELEASE NOT CONFIRMED",
                    GetSafetyFaultDescription(
                        releaseReason),
                    true);

                return;
            }

            if (IsServerTeleopActive(serverState))
            {
                SetLocomotionAllowed(false);

                SetState(
                    string.Equals(
                        serverState,
                        "XR_OPERATOR_HOLD",
                        StringComparison.Ordinal)
                        ? ModeState.PoseHeld
                        : ModeState.TeleopActive,
                    timedOutOperation ==
                        "HAND_BACK_ARMS"
                        ? "EXIT REQUEST TIMEOUT"
                        : "MODE REQUEST TIMEOUT",
                    "The robot remained in " +
                        SafeText(
                            serverState,
                            "teleoperation") +
                        ". Locomotion remains blocked until " +
                        "the state is confirmed.",
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

        if (string.Equals(
                serverState,
                "XR_OPERATOR_HOLD",
                StringComparison.Ordinal))
        {
            simulatedTeleopActive = false;
            countdownEndsAt = -1f;
            EntryPanelOpen = false;
            CanConfirmEntry = false;
            readinessBecameTrueAt = -1f;

            bool controllersReady =
                ControllersReadyForLocomotion();

            SetLocomotionAllowed(
                controllersReady);

            SetState(
                ModeState.PoseHeld,
                controllersReady
                    ? "POSE HELD · DRIVE READY"
                    : "POSE HELD",
                controllersReady
                    ? "Arms and fingers are frozen. " +
                        "Hold the deadman to move the base."
                    : "Waiting for both tracked controllers " +
                        "with centered controls.",
                forceLog);

            return;
        }

        if (string.Equals(
                serverState,
                "XR_TRACKING_HOLD",
                StringComparison.Ordinal))
        {
            simulatedTeleopActive = false;
            countdownEndsAt = -1f;
            EntryPanelOpen = false;
            CanConfirmEntry = false;
            readinessBecameTrueAt = -1f;

            SetLocomotionAllowed(false);

            SetState(
                ModeState.Realigning,
                "ALIGN HANDS",
                "Base motion is stopped. Match both wrists " +
                    "to the frozen robot pose to resume teleoperation.",
                forceLog);

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

        if (safetyFaultActive)
        {
            countdownEndsAt = -1f;
            CanConfirmEntry = false;
            readinessBecameTrueAt = -1f;

            string releaseReason;
            CanReleaseFaultHold =
                TryEvaluateFaultReleaseReadiness(
                    out releaseReason);

            if (!faultPanelOpenedForCurrentFault)
            {
                EntryPanelOpen = true;
                faultPanelOpenedForCurrentFault = true;
            }

            SetLocomotionAllowed(false);

            SetState(
                ModeState.Fault,
                "SAFETY FAULT · ARMS HELD",
                GetSafetyFaultDescription(
                    releaseReason),
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

    private void ServiceInputModalityRequests(
        string serverState)
    {
        if (
            dryRun ||
            locomotionSender == null ||
            !string.IsNullOrEmpty(
                pendingActionOperation))
        {
            return;
        }

        controllerModeActive =
            controllerModeActive ||
            AnyControllerTracked();

        if (
            controllerModeActive &&
            string.Equals(
                serverState,
                "XR_ACTIVE",
                StringComparison.Ordinal))
        {
            TrySendAutomaticModeAction(
                "HOLD_XR_POSE");
            return;
        }

        bool handsReady =
            trackedHandModeActive &&
            BothTrackedHandsActive() &&
            !AnyControllerTracked();

        if (
            handsReady &&
            string.Equals(
                serverState,
                "XR_OPERATOR_HOLD",
                StringComparison.Ordinal))
        {
            TrySendAutomaticModeAction(
                "RESUME_XR_POSE");
        }
    }

    private void TrySendAutomaticModeAction(
        string operation)
    {
        string error = null;

        SetLocomotionAllowed(false);

        if (
            locomotionSender.TrySendTeleopAction(
                operation,
                out error))
        {
            BeginPendingAction(operation);
            return;
        }

        if (verboseLogging)
        {
            Debug.LogWarning(
                "[G1 Teleop Mode] automatic action " +
                $"{operation} failed: " +
                SafeText(
                    error,
                    "transport unavailable"),
                this);
        }
    }

    private bool ControllersReadyForLocomotion()
    {
        bool prerequisitesReady =
            telemetryClient != null &&
            telemetryClient.HasData &&
            telemetryClient.TransportFresh &&
            BothControllersTracked() &&
            locomotionSender != null;

        if (!prerequisitesReady)
        {
            poseHoldLocomotionArmed = false;
            controllersBecameReadyAt = -1f;
            return false;
        }

        if (poseHoldLocomotionArmed)
            return true;

        if (!locomotionSender.ControlsNeutral)
        {
            controllersBecameReadyAt = -1f;
            return false;
        }

        if (controllersBecameReadyAt < 0f)
        {
            controllersBecameReadyAt =
                Time.unscaledTime;
        }

        bool stable =
            Time.unscaledTime -
                controllersBecameReadyAt >=
            Mathf.Max(
                0f,
                controllerReadyStableSeconds);

        if (!stable)
            return false;

        poseHoldLocomotionArmed = true;
        return true;
    }

    private bool AnyControllerTracked()
    {
        return
            IsActive(
                inputModalityManager != null
                    ? inputModalityManager.leftController
                    : null) ||
            IsActive(
                inputModalityManager != null
                    ? inputModalityManager.rightController
                    : null);
    }

    private bool BothControllersTracked()
    {
        return
            IsActive(
                inputModalityManager != null
                    ? inputModalityManager.leftController
                    : null) &&
            IsActive(
                inputModalityManager != null
                    ? inputModalityManager.rightController
                    : null);
    }

    private bool AnyTrackedHandActive()
    {
        return
            IsActive(
                inputModalityManager != null
                    ? inputModalityManager.leftHand
                    : null) ||
            IsActive(
                inputModalityManager != null
                    ? inputModalityManager.rightHand
                    : null);
    }

    private bool BothTrackedHandsActive()
    {
        return
            IsActive(
                inputModalityManager != null
                    ? inputModalityManager.leftHand
                    : null) &&
            IsActive(
                inputModalityManager != null
                    ? inputModalityManager.rightHand
                    : null);
    }

    private static bool IsActive(
        GameObject target)
    {
        return
            target != null &&
            target.activeInHierarchy;
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

    private bool TryEvaluateFaultReleaseReadiness(
        out string reason)
    {
        reason = "Waiting for fresh controller telemetry.";

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

        if (envelope.control == null ||
            !string.Equals(
                envelope.control.state,
                "SAFETY_FAULT_HOLD",
                StringComparison.Ordinal))
        {
            reason = "The controller is not reporting fault hold.";
            return false;
        }

        if (envelope.actions == null ||
            !envelope.actions.request_channel_enabled)
        {
            reason = "Controller action channel is unavailable.";
            return false;
        }

        G1DashboardTelemetryClient.EngagementConditions conditions =
            envelope.actions.engagement_conditions;

        if (conditions == null ||
            !conditions.stop_gate_ready)
        {
            reason = "Arms remain frozen. Wait for the full-stop gate.";
            return false;
        }

        G1DashboardTelemetryClient.XrHandover handover =
            envelope.actions.xr_handover;

        if (handover == null ||
            !string.Equals(
                handover.operation,
                "HAND_BACK_ARMS",
                StringComparison.Ordinal))
        {
            reason = "Controlled arm release is unavailable.";
            return false;
        }

        if (!handover.available)
        {
            reason = SafeText(
                handover.reason,
                "Controlled arm release is locked.");
            return false;
        }

        if (locomotionSender == null)
        {
            reason = "Quest action transport is unavailable.";
            return false;
        }

        reason =
            "Full stop confirmed. Release requires an explicit hold.";
        return true;
    }

    private string GetSafetyFaultDescription(
        string releaseReason)
    {
        if (telemetryClient != null &&
            telemetryClient.Latest != null &&
            telemetryClient.Latest.control != null)
        {
            G1DashboardTelemetryClient.Envelope envelope =
                telemetryClient.Latest;

            string fault = SafeText(
                envelope.control.safety_fault,
                "The robot controller is holding a safety fault.");

            int ownershipPercent =
                Mathf.RoundToInt(
                    Mathf.Clamp01(
                        envelope.control.arm_ownership) *
                    100f);

            string fingerMode =
                envelope.hands != null
                    ? SafeText(
                        envelope.hands.mode,
                        "HELD")
                    : "HELD";

            return
                fault +
                "\nARMS FROZEN · OWNERSHIP " +
                ownershipPercent +
                "% · FINGERS " +
                fingerMode.ToUpperInvariant() +
                "\n" +
                SafeText(
                    releaseReason,
                    "Release remains locked.");
        }

        return
            "The robot controller is holding a safety fault.\n" +
            SafeText(
                releaseReason,
                "Release remains locked.");
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
                "XR_OPERATOR_HOLD",
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
