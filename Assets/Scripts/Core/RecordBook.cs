using System;
using System.Collections.Generic;

namespace RetroRally.Core
{
    /// <summary>Where records and settings are kept (PlayerPrefs in the game, memory in tests).</summary>
    public interface IRecordStore
    {
        int GetInt(string key, int fallback);
        void SetInt(string key, int value);
        void Save();
    }

    public sealed class MemoryRecordStore : IRecordStore
    {
        readonly Dictionary<string, int> values = new Dictionary<string, int>();

        public int GetInt(string key, int fallback) => values.TryGetValue(key, out int value) ? value : fallback;
        public void SetInt(string key, int value) => values[key] = value;
        public void Save() => SaveCount++;

        /// <summary>How many times the records were flushed.</summary>
        public int SaveCount { get; private set; }
    }

    /// <summary>What a finished match changed in the records.</summary>
    public readonly struct RecordUpdate
    {
        public readonly bool NewBestStreak;
        public readonly bool NewLongestRally;

        public RecordUpdate(bool newBestStreak, bool newLongestRally)
        {
            NewBestStreak = newBestStreak;
            NewLongestRally = newLongestRally;
        }
    }

    /// <summary>
    /// Wins and losses against each CPU difficulty, the current and best winning streak against
    /// the CPU, and the longest rally ever played (any mode).
    /// </summary>
    public sealed class RecordBook
    {
        const string Prefix = "retrorally.records.";

        readonly IRecordStore store;

        public RecordBook(IRecordStore store)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public int Wins(Difficulty difficulty) => store.GetInt(Key("wins", difficulty), 0);
        public int Losses(Difficulty difficulty) => store.GetInt(Key("losses", difficulty), 0);
        public int Streak => store.GetInt(Prefix + "streak", 0);
        public int BestStreak => store.GetInt(Prefix + "best-streak", 0);
        public int LongestRally => store.GetInt(Prefix + "longest-rally", 0);

        /// <summary>Records a finished match against the CPU.</summary>
        public RecordUpdate RecordCpuMatch(Difficulty difficulty, bool won, int longestRally)
        {
            bool newStreak = false;
            if (won)
            {
                store.SetInt(Key("wins", difficulty), Wins(difficulty) + 1);
                int streak = Streak + 1;
                store.SetInt(Prefix + "streak", streak);
                if (streak > BestStreak)
                {
                    store.SetInt(Prefix + "best-streak", streak);
                    newStreak = true;
                }
            }
            else
            {
                store.SetInt(Key("losses", difficulty), Losses(difficulty) + 1);
                store.SetInt(Prefix + "streak", 0);
            }
            bool newRally = KeepRally(longestRally);
            store.Save();
            return new RecordUpdate(newStreak, newRally);
        }

        /// <summary>Records a finished two-player match: only the rally length counts.</summary>
        public RecordUpdate RecordVersusMatch(int longestRally)
        {
            bool newRally = KeepRally(longestRally);
            store.Save();
            return new RecordUpdate(false, newRally);
        }

        bool KeepRally(int hits)
        {
            if (hits <= LongestRally)
            {
                return false;
            }
            store.SetInt(Prefix + "longest-rally", hits);
            return true;
        }

        static string Key(string what, Difficulty difficulty) => $"{Prefix}{what}.{difficulty.ToString().ToLowerInvariant()}";
    }
}
