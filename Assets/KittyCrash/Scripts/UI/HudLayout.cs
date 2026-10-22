using UnityEngine;

namespace KittyCrash
{
    /// <summary>
    /// What the game screen shows. The clean layout (default) keeps only the essentials: multiplier, cat,
    /// bet controls, balance, season and forecast. Everything else (graph, history, wallet buttons,
    /// crowd leaderboard, mood face, server switch) sits behind the menu and can be opened for the session.
    /// Classic = the old everything-on-screen layout (a saved setting). Also holds the audio settings.
    /// </summary>
    public static class HudLayout
    {
        const string ClassicKey = "kc_layout_classic", VolumeKey = "kc_volume", MutedKey = "kc_muted",
                     TitleKey = "kc_title_screen";

        static bool loaded, classic, muted, titleAtStart = true;
        static float volume = 1f;

        // clean-layout panels the player opened from the menu (this session only)
        public static bool ShowGraph, ShowHistory, ShowLeaderboard, ShowMood;

        public static bool Classic { get { Load(); return classic; } set { Load(); classic = value; Save(); } }
        public static bool Graph => Classic || ShowGraph;
        public static bool History => Classic || ShowHistory;
        public static bool Leaderboard => Classic || ShowLeaderboard;
        public static bool Mood => Classic || ShowMood;
        public static bool WalletButtons => Classic;
        public static bool ServerSwitch => Classic;

        public static bool TitleAtStart { get { Load(); return titleAtStart; } set { Load(); titleAtStart = value; Save(); } }
        public static bool Muted { get { Load(); return muted; } set { Load(); muted = value; Save(); ApplyAudio(); } }
        public static float Volume { get { Load(); return volume; } set { Load(); volume = Mathf.Clamp01(value); Save(); ApplyAudio(); } }

        public static void ApplyAudio()
        {
            Load();
            AudioListener.volume = muted ? 0f : volume;
        }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            classic = PlayerPrefs.GetInt(ClassicKey, 0) == 1;
            muted = PlayerPrefs.GetInt(MutedKey, 0) == 1;
            titleAtStart = PlayerPrefs.GetInt(TitleKey, 1) == 1;
            volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
        }

        static void Save()
        {
            PlayerPrefs.SetInt(ClassicKey, classic ? 1 : 0);
            PlayerPrefs.SetInt(MutedKey, muted ? 1 : 0);
            PlayerPrefs.SetInt(TitleKey, titleAtStart ? 1 : 0);
            PlayerPrefs.SetFloat(VolumeKey, volume);
            PlayerPrefs.Save();
        }
    }
}
