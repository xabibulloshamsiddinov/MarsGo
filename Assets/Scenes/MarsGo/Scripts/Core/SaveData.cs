using UnityEngine;

namespace MarsGo
{
    /// <summary>Progress stored in PlayerPrefs: unlocked levels, best stars and best times.</summary>
    public static class SaveData
    {
        const string KeyUnlocked = "mg_unlocked";
        const string KeyLastLevel = "mg_last_level";
        const string KeyStars = "mg_stars_";
        const string KeyTime = "mg_time_";

        /// <summary>Automated tests set this so they never touch the player's progress.</summary>
        public static bool ReadOnly;

        public static int UnlockedCount
        {
            get => Mathf.Max(1, PlayerPrefs.GetInt(KeyUnlocked, 1));
            set { if (ReadOnly) return; PlayerPrefs.SetInt(KeyUnlocked, value); PlayerPrefs.Save(); }
        }

        public static int LastLevel
        {
            get => PlayerPrefs.GetInt(KeyLastLevel, 0);
            set { if (ReadOnly) return; PlayerPrefs.SetInt(KeyLastLevel, value); PlayerPrefs.Save(); }
        }

        public static int BestStars(int level) => PlayerPrefs.GetInt(KeyStars + level, 0);

        /// <summary>0 = never finished.</summary>
        public static float BestTime(int level) => PlayerPrefs.GetFloat(KeyTime + level, 0f);

        public static bool IsUnlocked(int level) => level < UnlockedCount;

        /// <summary>Stores the result if it beats the record. Returns true for a new record.</summary>
        public static bool RecordResult(int level, float time, int stars)
        {
            int oldStars = BestStars(level);
            float oldTime = BestTime(level);
            bool record = stars > oldStars || (stars == oldStars && (oldTime <= 0f || time < oldTime));
            if (ReadOnly) return record;
            if (record)
            {
                PlayerPrefs.SetInt(KeyStars + level, stars);
                PlayerPrefs.SetFloat(KeyTime + level, time);
            }
            if (level + 1 >= UnlockedCount) PlayerPrefs.SetInt(KeyUnlocked, level + 2);
            PlayerPrefs.Save();
            return record;
        }

        public static int TotalStars()
        {
            int total = 0;
            for (int i = 0; i < LevelLibrary.Count; i++) total += BestStars(i);
            return total;
        }
    }
}
