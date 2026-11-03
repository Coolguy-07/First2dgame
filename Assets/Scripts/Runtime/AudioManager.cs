using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    public float musicVolume = 0.5f;
    public float sfxVolume = 0.8f;

    static AudioClip cachedMusic;
    static readonly Dictionary<string, AudioClip> cachedSfx = new Dictionary<string, AudioClip>();

    AudioSource musicSource;
    AudioSource[] sfxSources;
    int sfxCursor;
    Coroutine musicFade;

    void Awake()
    {
        Instance = this;

        musicSource = CreateSource("Music", true);
        musicSource.volume = 0f;

        sfxSources = new AudioSource[8];
        for (int i = 0; i < sfxSources.Length; i++)
            sfxSources[i] = CreateSource("SFX_" + i, false);
    }

    void Start()
    {
        PlayMusic();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    AudioSource CreateSource(string name, bool loop)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = loop;
        src.spatialBlend = 0f;
        src.volume = loop ? 0f : sfxVolume;
        return src;
    }

    AudioClip Clip(string key, System.Func<AudioClip> make)
    {
        AudioClip c;
        if (cachedSfx.TryGetValue(key, out c) && c != null) return c;
        c = make();
        cachedSfx[key] = c;
        return c;
    }

    void Shot(AudioClip clip, float volume, float pitch)
    {
        if (clip == null || sfxSources == null) return;
        var src = sfxSources[sfxCursor];
        sfxCursor = (sfxCursor + 1) % sfxSources.Length;
        src.pitch = pitch;
        src.PlayOneShot(clip, Mathf.Clamp01(volume * sfxVolume));
    }

    public void PlayMusic()
    {
        if (musicSource == null) return;
        if (cachedMusic == null) cachedMusic = AudioSynth.Music();
        musicSource.clip = cachedMusic;
        musicSource.loop = true;
        musicSource.pitch = 1f;
        musicSource.Play();
        if (musicFade != null) StopCoroutine(musicFade);
        musicFade = StartCoroutine(FadeMusic(musicVolume, 1.4f));
    }

    public void PlayBossMusic()
    {
        if (musicSource == null) return;
        if (cachedMusic == null) cachedMusic = AudioSynth.Music();
        if (musicSource.clip != cachedMusic) musicSource.clip = cachedMusic;
        musicSource.loop = true;
        musicSource.pitch = 0.86f;
        if (!musicSource.isPlaying) musicSource.Play();
        if (musicFade != null) StopCoroutine(musicFade);
        musicFade = StartCoroutine(FadeMusic(musicVolume * 1.25f, 0.9f));
    }

    IEnumerator FadeMusic(float target, float duration)
    {
        float start = musicSource.volume;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            musicSource.volume = Mathf.Lerp(start, target, Mathf.Clamp01(t / duration));
            yield return null;
        }
        musicSource.volume = target;
    }

    public void PlaySwing() { Shot(Clip("swing", AudioSynth.Swing), 0.5f, Random.Range(0.94f, 1.07f)); }
    public void PlayClash() { Shot(Clip("clash", AudioSynth.Clash), 0.55f, Random.Range(0.96f, 1.05f)); }

    public void PlayHit(bool heavy)
    {
        float pitch = heavy ? 0.72f : Random.Range(0.95f, 1.12f);
        Shot(Clip(heavy ? "hitHeavy" : "hit", () => AudioSynth.Hit(heavy)), 0.6f, pitch);
    }

    public void PlayMonsterDeath(bool heavy = false)
    {
        float pitch = heavy ? 0.66f : Random.Range(0.9f, 1.08f);
        Shot(Clip("death", AudioSynth.MonsterDeath), heavy ? 0.8f : 0.6f, pitch);
    }
    public void PlayJump() { Shot(Clip("jump", AudioSynth.Jump), 0.45f, Random.Range(0.97f, 1.05f)); }
    public void PlayLand() { Shot(Clip("land", AudioSynth.Land), 0.4f, Random.Range(0.95f, 1.05f)); }
    public void PlayHurt() { Shot(Clip("hurt", AudioSynth.Hurt), 0.7f, 1f); }
    public void PlayCheckpoint() { Shot(Clip("checkpoint", AudioSynth.Checkpoint), 0.45f, 1f); }
    public void PlayPortalOpen() { Shot(Clip("portal", AudioSynth.PortalOpen), 0.7f, 1f); }
    public void PlayVictory() { Shot(Clip("victory", AudioSynth.Victory), 0.8f, 1f); }

    public void PlaySpit() { Shot(Clip("spit", AudioSynth.Spit), 0.45f, Random.Range(0.92f, 1.08f)); }
    public void PlaySpitSplat() { Shot(Clip("spitSplat", AudioSynth.SpitSplat), 0.5f, Random.Range(0.94f, 1.06f)); }
    public void PlayLunge() { Shot(Clip("lunge", AudioSynth.Lunge), 0.5f, Random.Range(0.9f, 1.05f)); }
    public void PlaySlam() { Shot(Clip("slam", AudioSynth.Slam), 0.85f, Random.Range(0.94f, 1.02f)); }
    public void PlayRoar() { Shot(Clip("roar", AudioSynth.Roar), 0.6f, Random.Range(0.9f, 0.98f)); }

    public void PlayBlip(string speaker)
    {
        float pitch = speaker == "Ash" ? 1.28f : speaker == "Thorne" ? 0.68f : 0.94f;
        Shot(Clip("blip", AudioSynth.Blip), 0.22f, pitch);
    }

    public void StopMusic()
    {
        if (musicFade != null) StopCoroutine(musicFade);
        if (musicSource != null) musicSource.Stop();
    }
}
