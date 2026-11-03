using System;
using UnityEngine;

// Procedural audio: a driving "dusk" theme plus every sword/impact sound,
// synthesized at runtime so the project ships with no external audio files.
public static class AudioSynth
{
    public const int Rate = 44100;
    const double TwoPi = Math.PI * 2.0;

    static System.Random rng = new System.Random(20261103);
    static float Noise() { return (float)(rng.NextDouble() * 2.0 - 1.0); }

    // ---------------------------------------------------------------- shaping

    // 0 sine, 1 saw, 2 square, 3 triangle, 4 noise
    static float Wave(int shape, double phase)
    {
        switch (shape)
        {
            case 0: return (float)Math.Sin(phase);
            case 1: return (float)((phase / Math.PI) % 2.0) - 1f;
            case 2: return Math.Sin(phase) >= 0 ? 1f : -1f;
            case 3: return (float)(Math.Asin(Math.Sin(phase)) * (2.0 / Math.PI));
            default: return Noise();
        }
    }

    static float Env(int i, int len, float attack, float decay, float release)
    {
        float t = (float)i / Rate;
        float total = (float)len / Rate;
        float a = attack <= 0.0001f ? 1f : Mathf.Clamp01(t / attack);
        float r = release <= 0.0001f ? 1f : Mathf.Clamp01((total - t) / release);
        float d = decay <= 0.0001f ? 1f : Mathf.Exp(-decay * t);
        return a * r * d;
    }

    static void AddTone(float[] buf, double startSec, double durSec, double f0, double f1,
                        float amp, int shape, float attack, float decay, float release, float vibrato = 0f)
    {
        int start = (int)(startSec * Rate);
        int len = Math.Max(1, (int)(durSec * Rate));
        double phase = 0.0;
        for (int i = 0; i < len; i++)
        {
            double t = (double)i / Rate;
            double k = (double)i / len;
            double freq = f0 + (f1 - f0) * k;
            if (vibrato > 0f) freq *= 1.0 + vibrato * Math.Sin(TwoPi * 5.2 * t);
            phase += TwoPi * freq / Rate;
            int idx = start + i;
            if (idx < 0 || idx >= buf.Length) continue;
            buf[idx] += Wave(shape, phase) * Env(i, len, attack, decay, release) * amp;
        }
    }

    static double Hz(int midi) { return 440.0 * Math.Pow(2.0, (midi - 69) / 12.0); }

    static AudioClip Finish(string name, float[] buf, float peak)
    {
        float max = 0f;
        for (int i = 0; i < buf.Length; i++) max = Mathf.Max(max, Mathf.Abs(buf[i]));
        if (max > 0.0001f)
        {
            float g = peak / max;
            for (int i = 0; i < buf.Length; i++) buf[i] *= g;
        }
        var clip = AudioClip.Create(name, buf.Length, 1, Rate, false);
        clip.SetData(buf, 0);
        return clip;
    }

    static void EdgeFade(float[] buf, float seconds)
    {
        int n = Mathf.Min(buf.Length / 2, (int)(seconds * Rate));
        for (int i = 0; i < n; i++)
        {
            float k = (float)i / n;
            buf[i] *= k;
            buf[buf.Length - 1 - i] *= k;
        }
    }

    // ---------------------------------------------------------------- drums

    static void AddKick(float[] buf, double startSec, float amp)
    {
        int start = (int)(startSec * Rate);
        int len = (int)(0.22f * Rate);
        double phase = 0.0;
        for (int i = 0; i < len; i++)
        {
            int idx = start + i;
            if (idx < 0 || idx >= buf.Length) continue;
            float t = (float)i / Rate;
            float f = Mathf.Lerp(155f, 42f, Mathf.Clamp01(t / 0.17f));
            phase += TwoPi * f / Rate;
            float env = Mathf.Exp(-9.5f * t);
            float click = i < 90 ? Noise() * 0.45f * (1f - i / 90f) : 0f;
            buf[idx] += ((float)Math.Sin(phase) * 0.9f + click) * env * amp;
        }
    }

    static void AddSnare(float[] buf, double startSec, float amp)
    {
        int start = (int)(startSec * Rate);
        int len = (int)(0.2f * Rate);
        for (int i = 0; i < len; i++)
        {
            int idx = start + i;
            if (idx < 0 || idx >= buf.Length) continue;
            float t = (float)i / Rate;
            float env = Mathf.Exp(-17f * t);
            float tone = (float)Math.Sin(TwoPi * 185.0 * t) * Mathf.Exp(-24f * t);
            buf[idx] += (Noise() * 0.55f * env + tone * 0.35f) * amp;
        }
    }

    static void AddHat(float[] buf, double startSec, float amp)
    {
        int start = (int)(startSec * Rate);
        int len = (int)(0.06f * Rate);
        float prev = 0f;
        for (int i = 0; i < len; i++)
        {
            int idx = start + i;
            if (idx < 0 || idx >= buf.Length) continue;
            float t = (float)i / Rate;
            float n = Noise();
            float hp = n - prev;
            prev = n;
            buf[idx] += hp * Mathf.Exp(-70f * t) * amp;
        }
    }

    // ---------------------------------------------------------------- music

    public static AudioClip Music()
    {
        const float bpm = 140f;
        float beat = 60f / bpm;
        float bar = beat * 4f;
        const int bars = 16;
        float length = bar * bars;
        var buf = new float[Mathf.CeilToInt(length * Rate)];

        int[][] chords =
        {
            new[] { 57, 60, 64 }, // Am
            new[] { 53, 57, 60 }, // F
            new[] { 48, 52, 55 }, // C
            new[] { 55, 59, 62 }, // G
            new[] { 57, 60, 64 }, // Am
            new[] { 53, 57, 60 }, // F
            new[] { 50, 53, 57 }, // Dm
            new[] { 52, 56, 59 }, // E
        };

        // midi, beat offset, beats  (two entries per bar, bars 8..15)
        int[,] melody =
        {
            { 69, 0, 2, 72, 2, 1 },
            { 69, 0, 3, 67, 3, 1 },
            { 72, 0, 2, 76, 2, 2 },
            { 74, 0, 3, 0, 0, 0 },
            { 76, 0, 2, 72, 2, 1 },
            { 72, 0, 2, 69, 2, 2 },
            { 74, 0, 2, 77, 2, 2 },
            { 76, 0, 3, 71, 3, 1 },
        };

        int[] arp = { 0, 1, 2, 1, 0, 2, 1, 2 };
        int[] bassPattern = { 0, 0, 12, 0, 0, 0, 12, 7 };

        for (int b = 0; b < bars; b++)
        {
            int[] chord = chords[b % 8];
            double barStart = b * bar;

            // sustained dusk pad
            for (int n = 0; n < 3; n++)
                AddTone(buf, barStart, bar * 1.02, Hz(chord[n] + 12), Hz(chord[n] + 12), 0.028f, 1, 0.35f, 0f, 0.55f, 0.003f);

            // driving octave bass
            for (int e = 0; e < 8; e++)
            {
                double t = barStart + e * (beat / 2.0);
                int root = chord[0] - 12;
                int note = root + bassPattern[e];
                AddTone(buf, t, beat * 0.42, Hz(note), Hz(note), 0.17f, 1, 0.005f, 5.5f, 0.03f);
                AddTone(buf, t, beat * 0.42, Hz(note - 12), Hz(note - 12), 0.09f, 0, 0.005f, 0f, 0.08f);
            }

            // arpeggio sparkle
            for (int e = 0; e < 8; e++)
            {
                double t = barStart + e * (beat / 2.0);
                int note = chord[arp[e]] + 12;
                AddTone(buf, t, beat * 0.4, Hz(note), Hz(note), 0.06f, 3, 0.004f, 9f, 0.02f);
                AddTone(buf, t, beat * 0.4, Hz(note + 12), Hz(note + 12), 0.018f, 0, 0.004f, 12f, 0.02f);
            }

            // hero melody in the second half
            if (b >= 8)
            {
                int mi = b - 8;
                for (int n = 0; n < 2; n++)
                {
                    int midi = melody[mi, n * 3];
                    if (midi <= 0) continue;
                    double off = melody[mi, n * 3 + 1];
                    double beats = melody[mi, n * 3 + 2];
                    double t = barStart + off * beat;
                    AddTone(buf, t, beats * beat * 0.95, Hz(midi), Hz(midi), 0.085f, 3, 0.02f, 2.2f, 0.2f, 0.004f);
                    AddTone(buf, t, beats * beat * 0.95, Hz(midi + 12), Hz(midi + 12), 0.02f, 0, 0.02f, 4f, 0.2f);
                }
            }

            // percussion
            for (int beatIdx = 0; beatIdx < 4; beatIdx++)
            {
                double t = barStart + beatIdx * beat;
                AddKick(buf, t, beatIdx == 0 || beatIdx == 2 ? 0.85f : 0.55f);
                if (beatIdx == 1 || beatIdx == 3) AddSnare(buf, t, 0.5f);
                AddHat(buf, t, 0.2f);
                AddHat(buf, t + beat * 0.5, 0.28f);
            }
        }

        EdgeFade(buf, 0.03f);
        return Finish("HollowGroveTheme", buf, 0.62f);
    }

    // ---------------------------------------------------------------- sfx

    public static AudioClip Swing()
    {
        int len = (int)(0.26f * Rate);
        var buf = new float[len];
        double phase = 0.0;
        float lp = 0f;
        for (int i = 0; i < len; i++)
        {
            float k = (float)i / len;
            float env = Mathf.Sin(Mathf.PI * k);
            float cutoff = Mathf.Lerp(500f, 4200f, Mathf.Sin(Mathf.PI * Mathf.Pow(k, 0.7f)));
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate);
            lp += a * (Noise() - lp);
            phase += TwoPi * (1600.0 + 900.0 * k) / Rate;
            float shimmer = (float)Math.Sin(phase) * 0.18f;
            buf[i] = (lp * 0.9f + shimmer) * env;
        }
        return Finish("Swing", buf, 0.55f);
    }

    public static AudioClip Clash()
    {
        int len = (int)(0.42f * Rate);
        var buf = new float[len];
        double[] freqs = { 1318.5, 1975.5, 2870.0, 3910.0 };
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / Rate;
            float s = Noise() * Mathf.Exp(-30f * t) * 0.55f;
            for (int p = 0; p < freqs.Length; p++)
            {
                float decay = 8f + p * 2.5f;
                s += (float)Math.Sin(TwoPi * freqs[p] * t) * Mathf.Exp(-decay * t) * 0.16f;
            }
            buf[i] = s;
        }
        return Finish("Clash", buf, 0.6f);
    }

    public static AudioClip Hit(bool heavy)
    {
        var buf = new float[(int)(0.2f * Rate)];
        float lp = 0f;
        for (int i = 0; i < buf.Length; i++)
        {
            float t = (float)i / Rate;
            float cutoff = heavy ? 900f : 1600f;
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate);
            lp += a * (Noise() - lp);
            float thump = (float)Math.Sin(TwoPi * Mathf.Lerp(170f, 55f, Mathf.Clamp01(t / 0.16f)) * t);
            buf[i] = (lp * 0.55f + thump * 0.7f) * Mathf.Exp(-13f * t);
        }
        return Finish(heavy ? "HitHeavy" : "Hit", buf, 0.65f);
    }

    public static AudioClip MonsterDeath()
    {
        var buf = new float[(int)(0.5f * Rate)];
        AddTone(buf, 0.0, 0.45, 480, 85, 0.35f, 2, 0.005f, 4f, 0.06f, 0.02f);
        AddTone(buf, 0.0, 0.3, 240, 60, 0.25f, 1, 0.005f, 6f, 0.05f);
        for (int i = 0; i < buf.Length; i++)
        {
            float t = (float)i / Rate;
            buf[i] += Noise() * Mathf.Exp(-9f * t) * 0.18f;
        }
        return Finish("MonsterDeath", buf, 0.55f);
    }

    public static AudioClip Jump()
    {
        var buf = new float[(int)(0.17f * Rate)];
        AddTone(buf, 0.0, 0.16, 360, 880, 0.4f, 0, 0.005f, 9f, 0.03f);
        AddTone(buf, 0.0, 0.1, 720, 1500, 0.12f, 3, 0.005f, 16f, 0.02f);
        return Finish("Jump", buf, 0.4f);
    }

    public static AudioClip Land()
    {
        var buf = new float[(int)(0.16f * Rate)];
        for (int i = 0; i < buf.Length; i++)
        {
            float t = (float)i / Rate;
            float f = Mathf.Lerp(120f, 48f, Mathf.Clamp01(t / 0.12f));
            float thump = (float)Math.Sin(TwoPi * f * t);
            buf[i] = (thump * 0.8f + Noise() * 0.2f) * Mathf.Exp(-20f * t);
        }
        return Finish("Land", buf, 0.45f);
    }

    public static AudioClip Hurt()
    {
        var buf = new float[(int)(0.38f * Rate)];
        double phase = 0.0;
        for (int i = 0; i < buf.Length; i++)
        {
            float t = (float)i / Rate;
            float f = Mathf.Lerp(520f, 110f, Mathf.Clamp01(t / 0.34f));
            phase += TwoPi * f / Rate;
            float saw = (float)((phase / Math.PI) % 2.0) - 1f;
            float s = Mathf.Clamp(saw * 1.7f, -1f, 1f);
            buf[i] = (s * 0.7f + Noise() * 0.25f) * Mathf.Exp(-7f * t);
        }
        return Finish("Hurt", buf, 0.6f);
    }

    public static AudioClip Checkpoint()
    {
        var buf = new float[(int)(1.0f * Rate)];
        double[] notes = { 880.0, 1318.5 };
        for (int n = 0; n < notes.Length; n++)
        {
            AddTone(buf, n * 0.11, 0.85, notes[n], notes[n], 0.22f, 0, 0.005f, 3.2f, 0.15f);
            AddTone(buf, n * 0.11, 0.85, notes[n] * 2.0, notes[n] * 2.0, 0.07f, 0, 0.005f, 5f, 0.15f);
        }
        return Finish("Checkpoint", buf, 0.5f);
    }

    public static AudioClip PortalOpen()
    {
        var buf = new float[(int)(1.8f * Rate)];
        double[] notes = { 440.0, 523.25, 659.25, 880.0, 1046.5, 1318.5 };
        for (int n = 0; n < notes.Length; n++)
        {
            AddTone(buf, n * 0.13, 1.6 - n * 0.1, notes[n], notes[n], 0.2f, 0, 0.005f, 2.4f, 0.4f);
            AddTone(buf, n * 0.13, 1.6 - n * 0.1, notes[n] * 2.0, notes[n] * 2.0, 0.06f, 3, 0.005f, 3.5f, 0.4f);
        }
        float lp = 0f;
        for (int i = 0; i < buf.Length; i++)
        {
            float t = (float)i / Rate;
            float k = t / 1.8f;
            float cutoff = Mathf.Lerp(300f, 3500f, k);
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate);
            lp += a * (Noise() - lp);
            buf[i] += lp * 0.12f * Mathf.Sin(Mathf.PI * k);
        }
        return Finish("PortalOpen", buf, 0.55f);
    }

    public static AudioClip Victory()
    {
        var buf = new float[(int)(3.0f * Rate)];
        int[] notes = { 57, 60, 64, 69 };
        for (int n = 0; n < notes.Length; n++)
        {
            double t = n * 0.16;
            AddTone(buf, t, 0.55, Hz(notes[n]), Hz(notes[n]), 0.2f, 1, 0.01f, 3f, 0.1f);
            AddTone(buf, t, 0.55, Hz(notes[n] + 12), Hz(notes[n] + 12), 0.06f, 3, 0.01f, 4f, 0.1f);
        }
        int[] finalChord = { 69, 72, 76 };
        for (int n = 0; n < finalChord.Length; n++)
        {
            AddTone(buf, 0.7, 2.2, Hz(finalChord[n]), Hz(finalChord[n]), 0.14f, 1, 0.02f, 0.8f, 0.8f, 0.003f);
            AddTone(buf, 0.7, 2.2, Hz(finalChord[n] + 12), Hz(finalChord[n] + 12), 0.04f, 0, 0.02f, 1.2f, 0.8f);
        }
        AddKick(buf, 0.0, 0.6f);
        AddKick(buf, 0.7, 0.7f);
        return Finish("Victory", buf, 0.6f);
    }

    public static AudioClip Spit()
    {
        var buf = new float[(int)(0.28f * Rate)];
        float lp = 0f;
        for (int i = 0; i < buf.Length; i++)
        {
            float t = (float)i / Rate;
            float k = t / 0.28f;
            float cutoff = Mathf.Lerp(2200f, 420f, k);
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate);
            lp += a * (Noise() - lp);
            buf[i] = lp * Mathf.Exp(-14f * t) * 0.9f;
        }
        AddTone(buf, 0.0, 0.12, 340, 180, 0.16f, 2, 0.005f, 14f, 0.02f);
        return Finish("Spit", buf, 0.5f);
    }

    public static AudioClip SpitSplat()
    {
        var buf = new float[(int)(0.34f * Rate)];
        float lp = 0f;
        for (int i = 0; i < buf.Length; i++)
        {
            float t = (float)i / Rate;
            float cutoff = Mathf.Lerp(1500f, 260f, Mathf.Clamp01(t / 0.3f));
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate);
            lp += a * (Noise() - lp);
            buf[i] = lp * Mathf.Exp(-11f * t) * 0.85f;
        }
        AddTone(buf, 0.0, 0.2, 140, 55, 0.3f, 1, 0.005f, 12f, 0.04f);
        return Finish("SpitSplat", buf, 0.5f);
    }

    public static AudioClip Lunge()
    {
        var buf = new float[(int)(0.32f * Rate)];
        AddTone(buf, 0.0, 0.3, 720, 180, 0.22f, 1, 0.01f, 9f, 0.05f);
        AddTone(buf, 0.0, 0.26, 160, 92, 0.3f, 2, 0.005f, 8f, 0.05f, 0.02f);
        for (int i = 0; i < buf.Length; i++)
        {
            float t = (float)i / Rate;
            buf[i] += Noise() * Mathf.Exp(-24f * t) * 0.16f;
        }
        return Finish("Lunge", buf, 0.55f);
    }

    public static AudioClip Slam()
    {
        var buf = new float[(int)(0.8f * Rate)];
        AddTone(buf, 0.0, 0.55, 120, 34, 0.75f, 0, 0.004f, 4.5f, 0.15f);
        AddTone(buf, 0.0, 0.4, 220, 60, 0.26f, 2, 0.004f, 7f, 0.1f);
        for (int i = 0; i < buf.Length; i++)
        {
            float t = (float)i / Rate;
            float lp = Mathf.Exp(-5f * t) * 0.4f;
            buf[i] += Noise() * lp * Mathf.Exp(-3f * t);
        }
        return Finish("Slam", buf, 0.75f);
    }

    public static AudioClip Roar()
    {
        var buf = new float[(int)(0.62f * Rate)];
        AddTone(buf, 0.0, 0.58, 128, 74, 0.5f, 1, 0.03f, 3.2f, 0.12f, 0.05f);
        AddTone(buf, 0.0, 0.5, 96, 62, 0.35f, 2, 0.03f, 4f, 0.12f, 0.04f);
        float lp = 0f;
        for (int i = 0; i < buf.Length; i++)
        {
            float t = (float)i / Rate;
            float cutoff = Mathf.Lerp(900f, 280f, Mathf.Clamp01(t / 0.55f));
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate);
            lp += a * (Noise() - lp);
            buf[i] += lp * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.55f)) * 0.22f;
        }
        return Finish("Roar", buf, 0.65f);
    }

    public static AudioClip Blip()
    {
        var buf = new float[(int)(0.075f * Rate)];
        AddTone(buf, 0.0, 0.07, 720, 820, 0.28f, 3, 0.002f, 26f, 0.01f);
        AddTone(buf, 0.0, 0.05, 1440, 1640, 0.08f, 0, 0.002f, 30f, 0.01f);
        return Finish("Blip", buf, 0.3f);
    }
}
