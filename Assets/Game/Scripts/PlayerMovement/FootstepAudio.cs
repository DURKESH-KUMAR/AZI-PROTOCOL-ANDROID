using UnityEngine;

/// <summary>
/// 3D footsteps for every player body, driven by how fast the body actually moves
/// (works for remote players too, which have no Rigidbody). Hearing enemies
/// approach is a big part of making the map feel alive.
/// </summary>
public class FootstepAudio : MonoBehaviour
{
    public float walkInterval = 0.52f;
    public float sprintInterval = 0.3f;
    public float sprintSpeed = 7f;
    public float volume = 0.55f;

    AudioSource source;
    PlayerMovement movement;
    Vector3 lastPosition;
    float timer;
    float smoothedSpeed;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 2f;
        source.maxDistance = 28f;
        source.dopplerLevel = 0f;
        lastPosition = transform.position;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        Vector3 position = transform.position;
        Vector3 delta = position - lastPosition;
        lastPosition = position;

        float vertical = Mathf.Abs(delta.y) / dt;
        delta.y = 0f;
        float speed = delta.magnitude / dt;
        smoothedSpeed = Mathf.Lerp(smoothedSpeed, speed, 1f - Mathf.Exp(-10f * dt));

        bool airborne = vertical > 2.5f || (movement != null && (movement.isJumping || (movement.IsMine && !movement.isGrounded)));
        if ((movement != null && movement.IsDead) || airborne || smoothedSpeed < 0.6f || smoothedSpeed > 25f)
        {
            timer = 0f;
            return;
        }

        float interval = Mathf.Lerp(walkInterval, sprintInterval, Mathf.InverseLerp(2f, sprintSpeed, smoothedSpeed));
        timer += dt;
        if (timer >= interval)
        {
            timer = 0f;
            float v = volume * (movement != null && movement.IsMine ? 0.45f : 1f);
            source.pitch = Random.Range(0.9f, 1.1f);
            source.PlayOneShot(GameAudio.RandomStep(), v);
        }
    }
}
