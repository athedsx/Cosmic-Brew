using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Game audio: lo-fi radio (several tapes with crossfade), space ambience,
// ships passing in the background and sound effects (2D, panned by screen position).
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [System.Serializable]
    public class Track
    {
        public string title;
        [Tooltip("Localization key for the title, e.g. music.1")]
        public string titleKey;
        public AudioClip clip;
    }

    [System.Serializable]
    public class Sfx
    {
        public string name;
        [Tooltip("sfx or characters (footsteps and voices)")]
        public string category = "sfx";
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 0.7f;
        public float pitchVariance = 0.05f;
    }

    [Header("Radio (music)")]
    public Track[] tracks;
    [Range(0f, 1f)] public float musicVolume = 0.5f;
    public float crossfadeTime = 1.6f;

    [Header("Ambience")]
    public AudioClip ambience;
    [Range(0f, 1f)] public float ambienceVolume = 0.22f;
    public AudioClip shipPass;
    [Range(0f, 1f)] public float shipVolume = 0.5f;
    public Vector2 shipInterval = new Vector2(35f, 75f);

    [Header("Sound effects")]
    public Sfx[] sfx;
    [Range(0f, 1f)] public float sfxVolume = 0.9f;

    public int CurrentTrack { get; private set; } = -1;
    public bool MusicOn { get; private set; } = true;
    public string CurrentTitle => CurrentTrack >= 0 && CurrentTrack < tracks.Length ? Loc.KeyOr(tracks[CurrentTrack].titleKey, tracks[CurrentTrack].title) : "";

    private AudioSource musicA, musicB, active, ambienceSource;
    private readonly List<AudioSource> pool = new List<AudioSource>();
    private readonly Dictionary<string, Sfx> byName = new Dictionary<string, Sfx>();
    private float nextShip, musicLevel = 1f, levelA, levelB;

    void Awake()
    {
        Instance = this;
        GameSettings.Load();
        musicA = NewSource("Musica_A", true);
        musicB = NewSource("Musica_B", true);
        ambienceSource = NewSource("Ambiente", true);
        foreach (var s in sfx) if (s != null && !string.IsNullOrEmpty(s.name)) byName[s.name] = s;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        if (CurrentTrack < 0 && tracks.Length > 0) PlayTrack(0, false);
        if (ambience != null)
        {
            ambienceSource.clip = ambience;
            ambienceSource.volume = ambienceVolume;
            ambienceSource.Play();
        }
        nextShip = Time.time + Random.Range(shipInterval.x * 0.4f, shipInterval.y * 0.5f);
    }

    AudioSource NewSource(string name, bool loop)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = loop;
        src.spatialBlend = 0f;
        return src;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.mKey.wasPressedThisFrame) SetMusicOn(!MusicOn);

        // music volume (smooth on/off) + crossfade between tapes
        musicLevel = Mathf.MoveTowards(musicLevel, MusicOn ? 1f : 0f, Time.unscaledDeltaTime / 0.6f);
        float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, crossfadeTime);
        levelA = Mathf.MoveTowards(levelA, active == musicA ? 1f : 0f, step);
        levelB = Mathf.MoveTowards(levelB, active == musicB ? 1f : 0f, step);
        float music = musicVolume * musicLevel * GameSettings.Music;
        musicA.volume = levelA * music;
        musicB.volume = levelB * music;
        ambienceSource.volume = ambienceVolume * GameSettings.Ambience;
        if (active != musicA && levelA <= 0f && musicA.isPlaying) musicA.Stop();
        if (active != musicB && levelB <= 0f && musicB.isPlaying) musicB.Stop();

        // every now and then a ship passes in the background
        if (shipPass != null && Time.time >= nextShip)
        {
            Play(shipPass, shipVolume * GameSettings.Ambience / Mathf.Max(0.01f, GameSettings.Sfx), Random.Range(0.85f, 1.1f), Random.Range(-0.6f, 0.6f));
            nextShip = Time.time + Random.Range(shipInterval.x, shipInterval.y);
        }
    }

    // ------------------------------------------------------------------ music
    public void PlayTrack(int index, bool announce = true)
    {
        if (tracks == null || tracks.Length == 0) return;
        index = ((index % tracks.Length) + tracks.Length) % tracks.Length;
        var next = active == musicA ? musicB : musicA;
        bool first = active == null;
        next.clip = tracks[index].clip;
        next.Play();
        if (next == musicA) levelA = first ? 1f : 0f; else levelB = first ? 1f : 0f; // first track: no fade
        active = next;
        CurrentTrack = index;
        if (announce && GameUI.Instance != null) GameUI.Instance.ShowToast(Loc.Get("toast.track", CurrentTitle));
    }

    public string NextTrack()
    {
        PlayTrack(CurrentTrack + 1);
        return CurrentTitle;
    }

    public void SetMusicOn(bool on)
    {
        MusicOn = on;
        if (GameUI.Instance != null) GameUI.Instance.ShowToast(Loc.Get(on ? "toast.music_on" : "toast.music_off"));
        if (SaveSystem.Instance != null) SaveSystem.Instance.MarkDirty();
    }

    // ------------------------------------------------------------------ sound effects
    public void Play(string name, Vector3? worldPos = null, float volumeScale = 1f, float delay = 0f)
    {
        if (!byName.TryGetValue(name, out var s) || s.clips == null || s.clips.Length == 0) return;
        var clip = s.clips[Random.Range(0, s.clips.Length)];
        float pitch = 1f + Random.Range(-s.pitchVariance, s.pitchVariance);
        float category = s.category == "characters" ? GameSettings.Characters / Mathf.Max(0.01f, GameSettings.Sfx) : 1f;
        Play(clip, s.volume * volumeScale * category, pitch, worldPos.HasValue ? PanFor(worldPos.Value) : 0f, delay);
    }

    // customer ship arriving/leaving (reuses the background ship sound)
    public void PlayShip(Vector3 worldPos, float volumeScale = 1f)
    {
        Play(shipPass, shipVolume * volumeScale * GameSettings.Ambience / Mathf.Max(0.01f, GameSettings.Sfx), Random.Range(1.05f, 1.25f), PanFor(worldPos));
    }

    void Play(AudioClip clip, float volume, float pitch, float pan, float delay = 0f)
    {
        if (clip == null) return;
        AudioSource src = null;
        foreach (var p in pool) if (!p.isPlaying) { src = p; break; }
        if (src == null)
        {
            if (pool.Count >= 24) return;
            src = NewSource("Efeito_" + pool.Count, false);
            pool.Add(src);
        }
        src.clip = clip;
        src.volume = volume * sfxVolume * GameSettings.Sfx;
        src.pitch = pitch;
        src.panStereo = pan;
        if (delay > 0f) src.PlayDelayed(delay); else src.Play();
    }

    static float PanFor(Vector3 world)
    {
        var cam = Camera.main;
        if (cam == null) return 0f;
        float x = cam.WorldToViewportPoint(world).x;
        return Mathf.Clamp((x - 0.5f) * 1.1f, -0.7f, 0.7f);
    }
}
