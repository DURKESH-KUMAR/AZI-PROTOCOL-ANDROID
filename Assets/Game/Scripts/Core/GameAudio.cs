using UnityEngine;

/// <summary>
/// Small feedback sounds synthesised at runtime (no audio files to import): hit-marker tick,
/// kill confirm, damage thud, footsteps, countdown beeps, victory/defeat stingers.
/// Clips are generated once on first use. Swap any of them for a real AudioClip later.
/// </summary>
public static class GameAudio
{
    const int Rate = 44100;

    static AudioClip hit, kill, hurt, beep, go, click, victory, defeat;
    static AudioClip[] steps;

    public static AudioClip Hit => hit != null ? hit : hit = Tone("hit", 0.06f, 1900f, 1900f, 60f, 0f);
    public static AudioClip Kill => kill != null ? kill : kill = Arpeggio("kill", new[] { 880f, 1320f }, 0.09f);
    public static AudioClip Hurt => hurt != null ? hurt : hurt = Tone("hurt", 0.18f, 160f, 70f, 18f, 0.35f);
    public static AudioClip Beep => beep != null ? beep : beep = Tone("beep", 0.12f, 880f, 880f, 14f, 0f);
    public static AudioClip Go => go != null ? go : go = Tone("go", 0.4f, 1320f, 1320f, 6f, 0f);
    public static AudioClip Click => click != null ? click : click = Tone("click", 0.03f, 2400f, 1200f, 120f, 0.2f);
    public static AudioClip Victory => victory != null ? victory : victory = Arpeggio("victory", new[] { 523f, 659f, 784f, 1047f }, 0.14f);
    public static AudioClip Defeat => defeat != null ? defeat : defeat = Arpeggio("defeat", new[] { 523f, 466f, 392f, 311f }, 0.18f);

    public static AudioClip RandomStep()
    {
        if (steps == null)
        {
            steps = new AudioClip[4];
            for (int i = 0; i < steps.Length; i++)
                steps[i] = Noise("step" + i, 0.09f, 0.18f + i * 0.04f, 40f, 1234 + i * 77);
        }
        return steps[Random.Range(0, steps.Length)];
    }

    /// <summary>2D sound on the persistent runtime source (UI / local feedback).</summary>
    public static void PlayUI(AudioClip clip, float volume = 1f)
    {
        AudioSource source = GameRuntime.UISource;
        if (source != null && clip != null)
            source.PlayOneShot(clip, volume);
    }

    // ---------------------------------------------------------------- synthesis

    static AudioClip Tone(string name, float seconds, float startHz, float endHz, float decay, float noise)
    {
        int n = Mathf.CeilToInt(seconds * Rate);
        float[] data = new float[n];
        System.Random rng = new System.Random(name.GetHashCode());
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float k = i / (float)n;
            float hz = Mathf.Lerp(startHz, endHz, k);
            phase += 2.0 * Mathf.PI * hz / Rate;
            float env = Mathf.Exp(-decay * t) * Mathf.Clamp01(i / 60f);
            float s = Mathf.Sin((float)phase);
            if (noise > 0f)
                s = Mathf.Lerp(s, (float)(rng.NextDouble() * 2.0 - 1.0), noise);
            data[i] = s * env * 0.6f;
        }
        return Make(name, data);
    }

    static AudioClip Arpeggio(string name, float[] notes, float noteSeconds)
    {
        int per = Mathf.CeilToInt(noteSeconds * Rate);
        int tail = Rate / 4;
        float[] data = new float[per * notes.Length + tail];
        for (int nIdx = 0; nIdx < notes.Length; nIdx++)
        {
            double phase = 0;
            int start = nIdx * per;
            int len = per + (nIdx == notes.Length - 1 ? tail : per / 2);
            for (int i = 0; i < len && start + i < data.Length; i++)
            {
                phase += 2.0 * Mathf.PI * notes[nIdx] / Rate;
                float env = Mathf.Exp(-5f * i / (float)Rate) * Mathf.Clamp01(i / 80f);
                // Square-ish (sine + 3rd harmonic) for a "game" timbre.
                float s = Mathf.Sin((float)phase) + 0.3f * Mathf.Sin((float)(phase * 3.0));
                data[start + i] += s * env * 0.35f;
            }
        }
        return Make(name, data);
    }

    static AudioClip Noise(string name, float seconds, float lowpass, float decay, int seed)
    {
        int n = Mathf.CeilToInt(seconds * Rate);
        float[] data = new float[n];
        System.Random rng = new System.Random(seed);
        float y = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float x = (float)(rng.NextDouble() * 2.0 - 1.0);
            y += lowpass * (x - y); // one-pole low-pass: dull "thud" instead of hiss
            data[i] = y * Mathf.Exp(-decay * t) * Mathf.Clamp01(i / 40f) * 1.4f;
        }
        return Make(name, data);
    }

    static AudioClip Make(string name, float[] data)
    {
        AudioClip clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
