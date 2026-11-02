using UnityEngine;

namespace RetroRally.Game
{
    /// <summary>The generated materials and the synthesized audio, wired up by ProjectBuilder.</summary>
    public sealed class GameArt : ScriptableObject
    {
        [Header("Materials")]
        public Material Glow;
        public Material Trail;

        [Header("Music")]
        public AudioClip TitleMusic;
        public AudioClip MatchMusic;

        [Header("Effects")]
        public AudioClip Paddle;
        public AudioClip Wall;
        public AudioClip Serve;
        public AudioClip Goal;
        public AudioClip MatchPoint;
        public AudioClip PickupSpawn;
        public AudioClip Grow;
        public AudioClip Shrink;
        public AudioClip MultiBall;
        public AudioClip Win;
        public AudioClip Lose;
        public AudioClip Move;
        public AudioClip Select;
        public AudioClip Pause;
    }
}
