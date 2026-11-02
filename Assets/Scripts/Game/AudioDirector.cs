using UnityEngine;

namespace RetroRally.Game
{
    /// <summary>Cross-fades between the title and match themes and plays one-shot effects.</summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        const float MusicVolume = 0.32f;

        AudioSource music;
        AudioSource effects;
        AudioClip wanted;
        float fade = 1f;
        bool ducked;

        /// <summary>Master volume from 0 to 1 (the settings menu steps it by tenths).</summary>
        public float Volume { get; private set; } = 0.8f;

        public bool Muted { get; private set; }

        /// <summary>When true (the attract demo behind the title), effects are not played.</summary>
        public bool Silent { get; set; }

        public void Init(float volume, bool muted)
        {
            music = gameObject.AddComponent<AudioSource>();
            music.loop = true;
            music.playOnAwake = false;
            effects = gameObject.AddComponent<AudioSource>();
            effects.playOnAwake = false;
            Volume = Mathf.Clamp01(volume);
            Muted = muted;
            Apply();
        }

        public void SetVolume(float volume)
        {
            Volume = Mathf.Clamp01(volume);
            Apply();
        }

        public void ToggleMute()
        {
            Muted = !Muted;
            Apply();
        }

        void Apply() => AudioListener.volume = Muted ? 0f : Volume;

        public void PlayMusic(AudioClip clip) => wanted = clip;

        public void SetDucked(bool value) => ducked = value;

        public void Play(AudioClip clip, float volume = 1f, float pitch = 1f, float pitchJitter = 0.03f)
        {
            if (clip == null || effects == null || Silent)
            {
                return;
            }
            effects.pitch = pitch * (1f + Random.Range(-pitchJitter, pitchJitter));
            effects.PlayOneShot(clip, volume);
        }

        /// <summary>Menu sounds play even during the attract demo.</summary>
        public void PlayUi(AudioClip clip, float volume = 0.8f)
        {
            if (clip == null || effects == null)
            {
                return;
            }
            effects.pitch = 1f;
            effects.PlayOneShot(clip, volume);
        }

        void Update()
        {
            if (music == null)
            {
                return;
            }
            if (music.clip != wanted)
            {
                fade = Mathf.MoveTowards(fade, 0f, Time.unscaledDeltaTime * 2.5f);
                if (fade <= 0f || !music.isPlaying)
                {
                    music.clip = wanted;
                    if (wanted != null)
                    {
                        music.Play();
                    }
                }
            }
            else
            {
                fade = Mathf.MoveTowards(fade, 1f, Time.unscaledDeltaTime * 1.5f);
            }
            music.volume = MusicVolume * fade * (ducked ? 0.35f : 1f);
        }
    }
}
