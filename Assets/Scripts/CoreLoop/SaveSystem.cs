using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Autosave: Star Coins + decorations (position, rotation and color).
// Saves shortly after changes (coins, place/move/store), every 30 s and on quit.
public class SaveSystem : MonoBehaviour
{
    public static SaveSystem Instance { get; private set; }

    public const string FileName = "cosmic_brew_save.json";
    public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

    public BuildMode build;
    public CustomerSpawner spawner;
    [Tooltip("Saves periodically, even without changes")]
    public float autosaveInterval = 30f;
    [Tooltip("Waits a moment after a change before saving (batches several changes)")]
    public float debounce = 0.6f;

    [System.Serializable]
    class SaveData
    {
        public int version = 1;
        public int coins;
        public List<DecorationSave> decorations = new List<DecorationSave>();
        public List<string> colors = new List<string>();
        public int musicTrack;
        public bool musicOn = true;
    }

    [System.Serializable]
    class DecorationSave
    {
        public string id;
        public float x, z, yaw;
        public string color;
    }

    private float saveAt = -1f;
    private float nextAutosave;
    private bool loaded;

    void Awake() => Instance = this;

    void Start()
    {
        Load();
        CoinWallet.Changed += OnCoins;
        nextAutosave = Time.unscaledTime + autosaveInterval;
    }

    void OnDestroy()
    {
        CoinWallet.Changed -= OnCoins;
        if (Instance == this) Instance = null;
    }

    void OnCoins(int total, int delta) => MarkDirty();

    // Requests a save soon
    public void MarkDirty()
    {
        if (loaded) saveAt = Time.unscaledTime + debounce;
    }

    void Update()
    {
        if (saveAt > 0f && Time.unscaledTime >= saveAt) Save(true);
        else if (Time.unscaledTime >= nextAutosave) Save(false);
    }

    void OnApplicationPause(bool pausing) { if (pausing) Save(false); }
    void OnApplicationQuit() => Save(false);

    public void Save(bool showToast)
    {
        if (!loaded) return;
        // while a decoration is being moved it is hidden; save later
        if (build != null && build.IsMovingSomething) { saveAt = Time.unscaledTime + 1f; return; }
        saveAt = -1f;
        nextAutosave = Time.unscaledTime + autosaveInterval;

        var data = new SaveData { coins = CoinWallet.Coins };
        if (IslandColors.Instance != null) data.colors.AddRange(IslandColors.Instance.CurrentNames());
        if (AudioManager.Instance != null)
        {
            data.musicTrack = Mathf.Max(0, AudioManager.Instance.CurrentTrack);
            data.musicOn = AudioManager.Instance.MusicOn;
        }
        foreach (var d in FindObjectsByType<Decoration>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID))
        {
            if (!d.enabled) continue; // Build Mode ghost
            Vector3 p = d.transform.position;
            data.decorations.Add(new DecorationSave { id = d.displayName, x = p.x, z = p.z, yaw = d.transform.eulerAngles.y, color = d.ColorName });
        }

        try
        {
            File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
            if (showToast && GameUI.Instance != null) GameUI.Instance.ShowToast(Loc.Get("toast.saved"));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Save] Could not save: " + e.Message);
        }
    }

    void Load()
    {
        loaded = true;
        if (!File.Exists(SavePath)) return; // first run: keep the scene's default island

        SaveData data;
        try { data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath)); }
        catch (System.Exception e) { Debug.LogWarning("[Save] Invalid save, starting fresh: " + e.Message); return; }
        if (data == null) return;

        CoinWallet.Set(data.coins);
        if (IslandColors.Instance != null) IslandColors.Instance.ApplyNames(data.colors);
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayTrack(data.musicTrack, false);
            if (!data.musicOn) AudioManager.Instance.SetMusicOn(false);
        }

        // remove the scene's decorations and recreate the saved ones
        foreach (var d in FindObjectsByType<Decoration>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (d.isSeat && spawner != null) spawner.seats.Remove(d.transform);
            d.gameObject.SetActive(false);
            Destroy(d.gameObject);
        }
        if (build == null || data.decorations == null) return;
        foreach (var s in data.decorations)
        {
            var template = build.FindTemplate(s.id);
            if (template == null) { Debug.LogWarning("[Save] Unknown item in save: " + s.id); continue; }
            var go = Instantiate(template.gameObject, new Vector3(s.x, 0f, s.z), Quaternion.Euler(0f, s.yaw, 0f), build.decorationsParent);
            go.name = template.displayName;
            go.SetActive(true);
            if (!string.IsNullOrEmpty(s.color)) go.GetComponent<Decoration>().ApplyColor(Decoration.FindColor(s.color));
            if (template.isSeat && spawner != null) spawner.AddSeat(go.transform);
        }
        Physics.SyncTransforms();
    }
}
