using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reuses particle effects (blood hits) instead of Instantiate/Destroy on every shot,
/// which caused GC spikes and frame hitches on low-end Android devices.
/// </summary>
public static class EffectPool
{
    static readonly Dictionary<ParticleSystem, Stack<PooledEffect>> pools = new Dictionary<ParticleSystem, Stack<PooledEffect>>();

    public static void Play(ParticleSystem prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
            return;

        if (!pools.TryGetValue(prefab, out Stack<PooledEffect> stack))
        {
            stack = new Stack<PooledEffect>();
            pools.Add(prefab, stack);
        }

        PooledEffect effect = null;
        while (stack.Count > 0 && effect == null)
            effect = stack.Pop(); // instances die with their scene; skip destroyed ones

        if (effect == null)
        {
            ParticleSystem ps = Object.Instantiate(prefab);
            effect = ps.gameObject.AddComponent<PooledEffect>();
            effect.system = ps;
            ParticleSystem.MainModule main = ps.main;
            effect.lifetime = main.duration + main.startLifetime.constantMax;
        }

        effect.prefab = prefab;
        effect.transform.SetPositionAndRotation(position, rotation);
        effect.gameObject.SetActive(true);
        effect.system.Clear(true);
        effect.system.Play(true);
        effect.returnAt = Time.time + effect.lifetime;
    }

    internal static void Return(PooledEffect effect)
    {
        effect.gameObject.SetActive(false);
        if (effect.prefab != null && pools.TryGetValue(effect.prefab, out Stack<PooledEffect> stack))
            stack.Push(effect);
    }
}

public class PooledEffect : MonoBehaviour
{
    internal ParticleSystem system;
    internal ParticleSystem prefab;
    internal float lifetime;
    internal float returnAt;

    void Update()
    {
        if (Time.time >= returnAt)
            EffectPool.Return(this);
    }
}
