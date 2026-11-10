using UnityEngine;

namespace WayfarerMech
{
    /// <summary>Two looping game clips driven by real state, with no per-cell sound spam.</summary>
    public sealed class MechAudio : MonoBehaviour
    {
        private AudioSource jet;
        private AudioSource drill;
        private float jetTarget;
        private float drillTarget;
        private bool clipsLoaded;

        public void Build()
        {
            var jetClip = Resources.Load<AudioClip>("Sounds/jetpackloop");
            var drillClip = Resources.Load<AudioClip>("Sounds/drillLoop");
            clipsLoaded = jetClip != null || drillClip != null;
            if (!clipsLoaded) Plugin.LogInfo("AUDIO_CLIPS_MISSING (jetpackloop/drillLoop)");
            jet = Create("jet-loop", jetClip);
            drill = Create("drill-loop", drillClip);
        }

        private AudioSource Create(string name, AudioClip clip)
        {
            if (!clip) return null;
            var source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0.25f;
            source.dopplerLevel = 0f;
            source.volume = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 3f;
            source.maxDistance = 40f;
            source.name = name;
            return source;
        }

        public void SetThrust(bool on, float intensity = 1f)
        {
            jetTarget = on ? 0.42f * Mathf.Clamp01(intensity) : 0f;
        }

        public void SetDrill(bool on)
        {
            drillTarget = on ? 0.48f : 0f;
        }

        private void Update()
        {
            Fade(jet, jetTarget, 0.9f);
            Fade(drill, drillTarget, 1.0f);
        }

        private static void Fade(AudioSource source, float target, float pitch)
        {
            if (!source) return;
            float dt = Time.unscaledDeltaTime;
            source.volume = Mathf.MoveTowards(source.volume, target, dt * 2.5f);
            source.pitch = Mathf.MoveTowards(source.pitch, pitch, dt * 1.5f);
            if (source.volume > 0.005f)
            {
                if (!source.isPlaying) source.Play();
            }
            else if (source.isPlaying)
            {
                source.Stop();
            }
        }
    }
}
