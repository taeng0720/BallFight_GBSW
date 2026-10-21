using System;
using UnityEngine;

namespace HongBallFight
{
    public sealed class ImpactAudio : MonoBehaviour
    {
        AudioSource source;
        AudioClip hit, pop, ding, swing;
        void Awake()
        {
            source = gameObject.AddComponent<AudioSource>(); source.playOnAwake = false; source.spatialBlend = 0;
            hit = Create("타격", .075f, 150, 1); pop = Create("폭발", .24f, 85, 1); ding = Create("진급", .18f, 740, .08f); swing = Create("휘두르기", .13f, 65, .7f);
        }
        static AudioClip Create(string name, float duration, float frequency, float noise)
        {
            const int rate = 22050; var samples = new float[Mathf.RoundToInt(rate * duration)]; var random = new System.Random(17);
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate, p = t / duration;
                float envelope = Mathf.Sin(Mathf.Min(1, p * 20) * Mathf.PI * .5f) * Mathf.Pow(1 - p, 2.5f);
                samples[i] = ((float)Math.Sin(t * frequency * Math.PI * 2 * (1 - p * .4f)) * .5f + ((float)random.NextDouble() * 2 - 1) * noise * .35f) * envelope;
            }
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false); clip.SetData(samples, 0); return clip;
        }
        public void Play(CombatImpact impact)
        {
            if (impact.Kind == ImpactKind.Dot) return;
            source.pitch = impact.Kind == ImpactKind.Defeat ? .85f : impact.Kind == ImpactKind.Whip ? .9f : 1 + Mathf.Min(impact.Damage, 60) * .003f;
            var clip = impact.Kind == ImpactKind.Defeat ? pop : impact.Kind == ImpactKind.Promotion ? ding : impact.Kind == ImpactKind.Swing ? swing : hit;
            float volume = impact.Kind == ImpactKind.Defeat ? .35f : impact.Kind == ImpactKind.Whip ? .3f : impact.Kind == ImpactKind.Ricochet ? .1f : impact.Kind == ImpactKind.Swing ? .1f : impact.Kind == ImpactKind.Promotion ? .16f : Mathf.Clamp(.06f + impact.Damage * .003f, .06f, .25f);
            source.PlayOneShot(clip, volume);
        }
        void OnDestroy() { if (source) source.Stop(); if (hit) Destroy(hit); if (pop) Destroy(pop); if (ding) Destroy(ding); if (swing) Destroy(swing); }
    }
}
