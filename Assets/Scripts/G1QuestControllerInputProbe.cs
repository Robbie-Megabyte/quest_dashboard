using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class G1QuestControllerInputProbe :
    MonoBehaviour
{
    [Header("XRI Input Actions")]

    [SerializeField]
    private InputActionReference leftMoveAction;

    [SerializeField]
    private InputActionReference rightTurnAction;

    [SerializeField]
    private InputActionReference leftDeadmanAction;

    [Header("Diagnostics")]

    [SerializeField, Min(0.1f)]
    private float reportIntervalSeconds = 0.5f;

    private float nextReportTime;

    private void Update()
    {
        if (Time.unscaledTime < nextReportTime)
            return;

        nextReportTime =
            Time.unscaledTime +
            reportIntervalSeconds;

        Vector2 leftStick =
            ReadVector2(leftMoveAction);

        Vector2 rightStick =
            ReadVector2(rightTurnAction);

        float deadmanValue =
            ReadFloat(leftDeadmanAction);

        bool deadmanPressed =
            leftDeadmanAction != null &&
            leftDeadmanAction.action != null &&
            leftDeadmanAction.action.IsPressed();

        Debug.Log(
            "[G1 Controller Probe] " +
            $"left=({leftStick.x:+0.000;-0.000;0.000}," +
            $"{leftStick.y:+0.000;-0.000;0.000}) " +
            $"right=({rightStick.x:+0.000;-0.000;0.000}," +
            $"{rightStick.y:+0.000;-0.000;0.000}) " +
            $"deadman={deadmanValue:0.000} " +
            $"pressed={deadmanPressed}"
        );
    }

    private static Vector2 ReadVector2(
        InputActionReference reference)
    {
        if (reference == null ||
            reference.action == null ||
            !reference.action.enabled)
        {
            return Vector2.zero;
        }

        return reference.action.ReadValue<Vector2>();
    }

    private static float ReadFloat(
        InputActionReference reference)
    {
        if (reference == null ||
            reference.action == null ||
            !reference.action.enabled)
        {
            return 0.0f;
        }

        return reference.action.ReadValue<float>();
    }
}