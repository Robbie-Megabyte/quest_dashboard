using UnityEngine;

public class G1ViewerSetup : MonoBehaviour
{
    void Awake()
    {
        // Disable all URDF collision geometry.
        Collider[] colliders =
            GetComponentsInChildren<Collider>(true);

        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }

        // Disable the URDF physics articulation.
        ArticulationBody[] bodies =
            GetComponentsInChildren<ArticulationBody>(true);

        foreach (ArticulationBody body in bodies)
        {
            body.useGravity = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.Sleep();
            body.enabled = false;
        }

        Debug.Log(
            $"G1 display mode: disabled {bodies.Length} articulation bodies " +
            $"and {colliders.Length} URDF colliders."
        );
    }
}
