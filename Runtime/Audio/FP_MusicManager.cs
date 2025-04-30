namespace FuzzPhyte.Utility.Audio
{
    using UnityEngine;
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine.Events;
    using FuzzPhyte.Utility;
    /// <summary>
    /// Easily allow A & B mixing
    /// </summary>
    public class FP_MusicManager : MonoBehaviour
    {
        public static FP_MusicManager Instance;
        public bool DontDestroy;

        [Header("Music Tracks")]
        public List<FP_MusicTrack> MusicTracks;

        [Header("Audio Sources")]
        public AudioSource SourceA;
        public AudioSource SourceB;

        [Header("Settings")]
        public float DefaultVolume = 0.5f;
        public float FadeDuration = 2f;

        [Header("Events")]
        public UnityEvent<string> OnTrackChanged;
        protected AudioSource _activeSource;
        protected AudioSource _inactiveSource;
        protected int _lastIndex = -1;
        public string CurrentTrackName => _activeSource?.clip?.name;
        public AudioClip CurrentClip => _activeSource?.clip;

        protected void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (DontDestroy)
            {
                DontDestroyOnLoad(gameObject);
            }
            

            _activeSource = SourceA;
            _inactiveSource = SourceB;
        }
#if UNITY_EDITOR
        [ContextMenu("Play Random Track (Editor)")]
        protected void EditorPlayRandom() => PlayRandomTrack();
#endif
        #region Public Accessor Methods
        /// <summary>
        /// Main PlayTrack w/Fade option
        /// </summary>
        /// <param name="track"></param>
        public void PlayTrackFade(FP_MusicTrack track)
        {
            PlayTrack(track.name, true);
        }
        /// <summary>
        /// Main PlayTrack w/out Fade Option
        /// </summary>
        /// <param name="track"></param>
        public void PlayTrackNoFade(FP_MusicTrack track)
        {
            PlayTrack(track.name, false);
        }
        public virtual void PlayTrack(FP_MusicTrack track, bool fade, float overrideVolume)
        {
            DefaultVolume = overrideVolume;
            PlayTrack(track.name, fade);
        }
        public virtual void PlayTrack(string name, bool fade = true)
        {
            int index = MusicTracks.FindIndex(t => t.Name == name);
            if (index != -1)
            {
                _lastIndex = index;
                if (MusicTracks[index].Clip != null)
                {
                    PlayClip(MusicTracks[index].Clip, fade);
                }
            }
            else
            {
                Debug.LogWarning($"Track '{name}' not found.");
            }
               
        }
        public virtual void PlayRandomTrack(bool fade = true)
        {
            if (MusicTracks.Count == 0) return;

            int rand;
            do
            {
                rand = Random.Range(0, MusicTracks.Count);
            } while (MusicTracks.Count > 1 && rand == _lastIndex);

            _lastIndex = rand;
            PlayClip(MusicTracks[rand].Clip, fade);
        }
        public virtual void PlayRandomTrackByEmotion(EmotionalState emoState, bool fade = true)
        {
            if (MusicTracks.Count == 0) return;
            var emoTracks = MusicTracks.FindAll(t => t.MusicEmotionalState == emoState);
            if (emoTracks.Count > 0)
            {
                int rand=0;
                int tempLastIndex = 0;
                FP_MusicTrack track;
                do
                {
                    rand = Random.Range(0, emoTracks.Count);
                    track = emoTracks[rand];
                    tempLastIndex = MusicTracks.IndexOf(track);
                } while(emoTracks.Count>1&& tempLastIndex == _lastIndex);
                _lastIndex = MusicTracks.IndexOf(track);
                PlayClip(track.Clip, fade);
            }
        }
        public void AddTrack(FP_MusicTrack track)
        {
            if (!MusicTracks.Contains(track))
            {
                MusicTracks.Add(track);
            }
        }
        public void RemoveTrack(FP_MusicTrack track)
        {
            if (MusicTracks.Contains(track))
            {
                int index = MusicTracks.FindIndex(t => t.name == track.name);
                //account for lastindex as we change the list size
                if(_lastIndex == index)
                {
                    _lastIndex--;
                    if(_lastIndex < 0)
                    {
                        _lastIndex = 0;
                    }
                }
                else
                {
                    if (_lastIndex > index)
                    {
                        _lastIndex--;
                    }
                }
                MusicTracks.Remove(track);
            }
        }
        public void FadeOutMusic(float duration = -1f)
        {
            StartCoroutine(FadeVolume(_activeSource, DefaultVolume, 0f, duration > 0 ? duration : FadeDuration));
        }
        public void FadeInMusic(float duration = -1f)
        {
            StartCoroutine(FadeVolume(_activeSource, 0f, DefaultVolume, duration > 0 ? duration : FadeDuration));
        }
        #endregion
        protected void PlayClip(AudioClip clip, bool fade)
        {
            if (_activeSource.clip == clip) return;

            _inactiveSource.clip = clip;
            _inactiveSource.Play();
            OnTrackChanged?.Invoke(clip.name);
            if (fade)
                StartCoroutine(Crossfade());
            else
                ImmediateSwitch();
        }
        protected void ImmediateSwitch()
        {
            _activeSource.Stop();
            SwapSources();
            _activeSource.volume = DefaultVolume;
        }
        protected IEnumerator Crossfade()
        {
            float time = 0f;

            while (time < FadeDuration)
            {
                float t = time / FadeDuration;
                _activeSource.volume = Mathf.Lerp(DefaultVolume, 0f, t);
                _inactiveSource.volume = Mathf.Lerp(0f, DefaultVolume, t);
                time += Time.deltaTime;
                yield return null;
            }

            _activeSource.Stop();
            _inactiveSource.volume = DefaultVolume;
            SwapSources();
        }
        protected void SwapSources()
        {
            var temp = _activeSource;
            _activeSource = _inactiveSource;
            _inactiveSource = temp;
        }
        protected IEnumerator FadeVolume(AudioSource source, float from, float to, float duration)
        {
            float time = 0f;
            source.volume = from;

            while (time < duration)
            {
                source.volume = Mathf.Lerp(from, to, time / duration);
                time += Time.deltaTime;
                yield return null;
            }

            source.volume = to;
        }
    }
}
