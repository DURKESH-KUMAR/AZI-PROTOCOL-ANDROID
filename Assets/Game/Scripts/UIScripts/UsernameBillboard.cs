using UnityEngine;

/// <summary>Keeps a name tag facing the local player's camera.</summary>
public class UsernameBillboard : MonoBehaviour
{
    Transform camTransform;

    void LateUpdate()
    {
        if (camTransform == null)
        {
            // Remote players' cameras are destroyed, so the only MainCamera left is the local one.
            // Camera.main is cached by Unity; FindObjectOfType every frame was expensive.
            Camera cam = Camera.main;
            if (cam == null)
                return;
            camTransform = cam.transform;
        }

        // Same result as LookAt(camera) + 180° turn, without two transform writes.
        Vector3 away = transform.position - camTransform.position;
        if (away.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(away);
    }
}
