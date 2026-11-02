using RetroRally.Core;
using UnityEngine;

namespace RetroRally.Game
{
    /// <summary>Keeps records and settings on the device with PlayerPrefs.</summary>
    public sealed class PlayerPrefsStore : IRecordStore
    {
        public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
        public void Save() => PlayerPrefs.Save();
    }
}
