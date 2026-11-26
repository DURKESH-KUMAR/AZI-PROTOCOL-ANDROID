using UnityEngine;

/// <summary>
/// Put on the local player's camera after it is detached from a dying body:
/// slowly rises and orbits the body, so the screen never goes black and the
/// player sees what happened while the respawn timer runs.
/// </summary>
public class DeathCamera : MonoBehaviour
{
    public Transform body;
    public float orbitSpeed = 14f;
    public float riseSpeed = 0.6f;
    public float maxHeight = 4.5f;
    public float distance = 4.5f;

    Vector3 focus;
    float angle;
    float height;

    public void Begin(Transform target)
    {
        body = target;
        focus = target != null ? target.position + Vector3.up * 0.6f : transform.position + transform.forward * 3f;
        Vector3 offset = transform.position - focus;
        offset.y = 0f;
        angle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
        height = Mathf.Max(1.5f, transform.position.y - focus.y);
        distance = Mathf.Clamp(offset.magnitude, 3f, 6f);
    }

    void LateUpdate()
    {
        if (body != null)
            focus = Vector3.Lerp(focus, body.position + Vector3.up * 0.6f, 1f - Mathf.Exp(-4f * Time.deltaTime));

        angle += orbitSpeed * Time.deltaTime;
        height = Mathf.Min(maxHeight, height + riseSpeed * Time.deltaTime);

        Quaternion yaw = Quaternion.Euler(0f, angle, 0f);
        Vector3 target = focus + yaw * new Vector3(0f, 0f, distance) + Vector3.up * height;
        transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-3f * Time.deltaTime));
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(focus - transform.position), 1f - Mathf.Exp(-5f * Time.deltaTime));
    }
}
