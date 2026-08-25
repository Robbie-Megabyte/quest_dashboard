using UnityEngine;

public class G1FullBodyDriver : MonoBehaviour
{
    [Header("References")]
    public G1UdpReceiver receiver;
    public Transform robotModelRoot;

    [Header("Motion")]
    public bool useSmoothing = true;

    [Tooltip(
        "Higher = more responsive. Lower = smoother."
    )]
    public float smoothingSpeed = 25.0f;

    [Header("Debug")]
    [Tooltip(
        "Print the imported joint axis and limits " +
        "for all 29 joints when the app starts."
    )]
    public bool logJointConfiguration = true;

    private const int JointCount = 29;

    private readonly float[] q =
        new float[JointCount];

    private readonly float[] currentDegrees =
        new float[JointCount];

    private readonly Transform[] joints =
        new Transform[JointCount];

    private readonly ArticulationBody[] jointBodies =
        new ArticulationBody[JointCount];

    private readonly Quaternion[] zeroRotations =
        new Quaternion[JointCount];

    /*
     * Actual revolute axis expressed in the
     * Transform's local coordinate system.
     *
     * This is NOT necessarily Vector3.right.
     *
     * Unity ArticulationBody revolute joints rotate
     * around the X axis of the articulation anchor.
     *
     * anchorRotation converts that anchor-frame X
     * axis into the body's local coordinate system.
     */
    private readonly Vector3[] localJointAxes =
        new Vector3[JointCount];

    private bool initialized = false;

    /*
     * Per-joint direction correction.
     *
     * Start with every joint at +1.
     *
     * If, after using the proper anchor axes, a
     * particular joint still moves exactly backwards,
     * we can change ONLY that entry to -1.
     *
     * Index corresponds directly to Unitree motor index.
     */
    private static readonly float[] JointDirections =
    {
        // LEFT LEG
        +1.0f, // 0  left hip pitch
        +1.0f, // 1  left hip roll
        +1.0f, // 2  left hip yaw
        +1.0f, // 3  left knee
        +1.0f, // 4  left ankle pitch
        +1.0f, // 5  left ankle roll

        // RIGHT LEG
        +1.0f, // 6  right hip pitch
        +1.0f, // 7  right hip roll
        +1.0f, // 8  right hip yaw
        +1.0f, // 9  right knee
        +1.0f, // 10 right ankle pitch
        +1.0f, // 11 right ankle roll

        // WAIST
        +1.0f, // 12 waist yaw
        +1.0f, // 13 waist roll
        +1.0f, // 14 waist pitch

        // LEFT ARM
        +1.0f, // 15 left shoulder pitch
        +1.0f, // 16 left shoulder roll
        +1.0f, // 17 left shoulder yaw
        +1.0f, // 18 left elbow
        +1.0f, // 19 left wrist roll
        +1.0f, // 20 left wrist pitch
        +1.0f, // 21 left wrist yaw

        // RIGHT ARM
        +1.0f, // 22 right shoulder pitch
        +1.0f, // 23 right shoulder roll
        +1.0f, // 24 right shoulder yaw
        +1.0f, // 25 right elbow
        +1.0f, // 26 right wrist roll
        +1.0f, // 27 right wrist pitch
        +1.0f  // 28 right wrist yaw
    };

    /*
     * Child link corresponding to each Unitree
     * 29-DOF motor index.
     */
    private static readonly string[] LinkNames =
    {
        // LEFT LEG
        "left_hip_pitch_link",       // 0
        "left_hip_roll_link",        // 1
        "left_hip_yaw_link",         // 2
        "left_knee_link",            // 3
        "left_ankle_pitch_link",     // 4
        "left_ankle_roll_link",      // 5

        // RIGHT LEG
        "right_hip_pitch_link",      // 6
        "right_hip_roll_link",       // 7
        "right_hip_yaw_link",        // 8
        "right_knee_link",           // 9
        "right_ankle_pitch_link",    // 10
        "right_ankle_roll_link",     // 11

        // WAIST
        "waist_yaw_link",            // 12
        "waist_roll_link",           // 13
        "torso_link",                // 14

        // LEFT ARM
        "left_shoulder_pitch_link",  // 15
        "left_shoulder_roll_link",   // 16
        "left_shoulder_yaw_link",    // 17
        "left_elbow_link",           // 18
        "left_wrist_roll_link",      // 19
        "left_wrist_pitch_link",     // 20
        "left_wrist_yaw_link",       // 21

        // RIGHT ARM
        "right_shoulder_pitch_link", // 22
        "right_shoulder_roll_link",  // 23
        "right_shoulder_yaw_link",   // 24
        "right_elbow_link",          // 25
        "right_wrist_roll_link",     // 26
        "right_wrist_pitch_link",    // 27
        "right_wrist_yaw_link"       // 28
    };

    void Start()
    {
        if (robotModelRoot == null)
        {
            Debug.LogError(
                "G1FullBodyDriver: Robot Model Root is missing."
            );

            enabled = false;
            return;
        }

        FindJoints();
    }

    private void FindJoints()
    {
        ArticulationBody[] bodies =
            robotModelRoot.GetComponentsInChildren
            <ArticulationBody>(true);

        int found = 0;

        for (int i = 0; i < JointCount; i++)
        {
            joints[i] = null;
            jointBodies[i] = null;

            foreach (ArticulationBody body in bodies)
            {
                if (body.transform.name != LinkNames[i])
                    continue;

                joints[i] = body.transform;
                jointBodies[i] = body;

                /*
                 * Save the imported q=0 Transform rotation.
                 */
                zeroRotations[i] =
                    body.transform.localRotation;

                /*
                 * A revolute ArticulationBody rotates around
                 * the X axis of its JOINT ANCHOR.
                 *
                 * anchorRotation is defined relative to this
                 * body, so:
                 *
                 *     anchorRotation * Vector3.right
                 *
                 * gives us the articulation X axis expressed
                 * in the body's local Transform coordinates.
                 */
                Vector3 axis =
                    body.anchorRotation *
                    Vector3.right;

                if (axis.sqrMagnitude > 0.000001f)
                {
                    axis.Normalize();
                }
                else
                {
                    axis = Vector3.right;
                }

                localJointAxes[i] = axis;

                found++;

                if (logJointConfiguration)
                {
                    ArticulationDrive drive =
                        body.xDrive;

                    Vector3 anchorEuler =
                        body.anchorRotation.eulerAngles;

                    Debug.Log(
                        $"G1 JOINT {i:00} | " +
                        $"{LinkNames[i]} | " +
                        $"anchorRot=(" +
                        $"{anchorEuler.x:F2}, " +
                        $"{anchorEuler.y:F2}, " +
                        $"{anchorEuler.z:F2}) | " +
                        $"axis=(" +
                        $"{axis.x:F3}, " +
                        $"{axis.y:F3}, " +
                        $"{axis.z:F3}) | " +
                        $"limits=[" +
                        $"{drive.lowerLimit:F2}, " +
                        $"{drive.upperLimit:F2}]"
                    );
                }

                break;
            }

            if (joints[i] == null)
            {
                Debug.LogError(
                    $"G1FullBodyDriver: " +
                    $"could not find joint {i}: " +
                    $"{LinkNames[i]}"
                );
            }
        }

        Debug.Log(
            $"G1FullBodyDriver: found " +
            $"{found}/{JointCount} joints."
        );
    }

    void Update()
    {
        if (receiver == null)
            return;

        if (!receiver.TryGetAllJoints(
                q,
                out int robotMode))
        {
            return;
        }

        float alpha = 1.0f;

        if (useSmoothing)
        {
            alpha =
                1.0f -
                Mathf.Exp(
                    -smoothingSpeed *
                    Time.unscaledDeltaTime
                );
        }

        for (int i = 0; i < JointCount; i++)
        {
            if (joints[i] == null)
                continue;

            float targetDegrees =
                q[i] *
                Mathf.Rad2Deg *
                JointDirections[i];

            if (!initialized)
            {
                currentDegrees[i] =
                    targetDegrees;
            }
            else if (useSmoothing)
            {
                currentDegrees[i] =
                    Mathf.LerpAngle(
                        currentDegrees[i],
                        targetDegrees,
                        alpha
                    );
            }
            else
            {
                currentDegrees[i] =
                    targetDegrees;
            }

            /*
             * IMPORTANT:
             *
             * Previously we used:
             *
             * Quaternion.AngleAxis(
             *     angle,
             *     Vector3.right
             * )
             *
             * for EVERY joint.
             *
             * That is only correct when the imported
             * articulation anchor happens to have an
             * identity rotation.
             *
             * Now we rotate around the actual imported
             * revolute-joint axis.
             */
            Quaternion jointDelta =
                Quaternion.AngleAxis(
                    currentDegrees[i],
                    localJointAxes[i]
                );

            joints[i].localRotation =
                zeroRotations[i] *
                jointDelta;
        }

        initialized = true;
    }
}