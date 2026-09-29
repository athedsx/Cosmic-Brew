using UnityEngine;

// Configurações do jogador (volume, vídeo). Ficam no PlayerPrefs, separadas do save do jogo,
// então "Novo Jogo" não apaga as opções.
public static class GameSettings
{
    const string P = "cosmicbrew.";

    public static float Master = 1f;
    public static float Music = 0.8f;
    public static float Sfx = 0.9f;
    public static float Characters = 0.9f;
    public static float Ambience = 0.8f;
    public static bool Fullscreen = true;
    public static int ResolutionWidth, ResolutionHeight;
    public static int Quality = 2;     // 0 baixa, 1 média, 2 alta
    public static bool VSync = true;

    static bool loaded;

    public static void Load()
    {
        if (loaded) return;
        loaded = true;
        Master = PlayerPrefs.GetFloat(P + "master", Master);
        Music = PlayerPrefs.GetFloat(P + "music", Music);
        Sfx = PlayerPrefs.GetFloat(P + "sfx", Sfx);
        Characters = PlayerPrefs.GetFloat(P + "characters", Characters);
        Ambience = PlayerPrefs.GetFloat(P + "ambience", Ambience);
        Fullscreen = PlayerPrefs.GetInt(P + "fullscreen", Screen.fullScreen ? 1 : 0) == 1;
        ResolutionWidth = PlayerPrefs.GetInt(P + "resW", Screen.currentResolution.width);
        ResolutionHeight = PlayerPrefs.GetInt(P + "resH", Screen.currentResolution.height);
        Quality = PlayerPrefs.GetInt(P + "quality", Quality);
        VSync = PlayerPrefs.GetInt(P + "vsync", 1) == 1;
    }

    public static void Save()
    {
        PlayerPrefs.SetFloat(P + "master", Master);
        PlayerPrefs.SetFloat(P + "music", Music);
        PlayerPrefs.SetFloat(P + "sfx", Sfx);
        PlayerPrefs.SetFloat(P + "characters", Characters);
        PlayerPrefs.SetFloat(P + "ambience", Ambience);
        PlayerPrefs.SetInt(P + "fullscreen", Fullscreen ? 1 : 0);
        PlayerPrefs.SetInt(P + "resW", ResolutionWidth);
        PlayerPrefs.SetInt(P + "resH", ResolutionHeight);
        PlayerPrefs.SetInt(P + "quality", Quality);
        PlayerPrefs.SetInt(P + "vsync", VSync ? 1 : 0);
        PlayerPrefs.Save();
    }

    // Aplica vídeo (o áudio é lido direto pelo AudioManager)
    public static void ApplyVideo()
    {
        AudioListener.volume = Master;
        int levels = QualitySettings.names.Length;
        if (levels > 0)
        {
            int idx = Quality <= 0 ? 0 : Quality == 1 ? levels / 2 : levels - 1;
            QualitySettings.SetQualityLevel(idx, true);
        }
        QualitySettings.vSyncCount = VSync ? 1 : 0;
#if !UNITY_EDITOR
        var mode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        if (ResolutionWidth > 0 && ResolutionHeight > 0) Screen.SetResolution(ResolutionWidth, ResolutionHeight, mode);
        else Screen.fullScreenMode = mode;
#endif
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => loaded = false;
}
