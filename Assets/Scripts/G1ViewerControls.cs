using UnityEngine;

public class G1ViewerControls : MonoBehaviour
{
    [Header("References")]
    public Transform robotRoot;
    public Transform xrCamera;

    [Header("Scale Presets")]
    public float tabletopScale = 0.25f;
    public float fullScale = 1.0f;

    [Header("Bring To Me")]
    public float distanceFromUser = 1.2f;
    public float verticalOffset = -0.25f;

    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Vector3 initialScale;

    void Start()
    {
        initialPosition = robotRoot.position;
        initialRotation = robotRoot.rotation;
        initialScale = robotRoot.localScale;
    }

    public void BringToMe()
    {
        Vector3 forward = xrCamera.forward;
        forward.y = 0f;
        forward.Normalize();

        robotRoot.position =
            xrCamera.position +
            forward * distanceFromUser +
            Vector3.up * verticalOffset;

        // Face the robot toward the user.
        robotRoot.rotation =
            Quaternion.LookRotation(-forward, Vector3.up);
    }

    public void TabletopScale()
    {
        robotRoot.localScale =
            Vector3.one * tabletopScale;
    }

    public void FullScale()
    {
        robotRoot.localScale =
            Vector3.one * fullScale;
    }

    public void ResetRobot()
    {
        robotRoot.position = initialPosition;
        robotRoot.rotation = initialRotation;
        robotRoot.localScale = initialScale;
    }
}
