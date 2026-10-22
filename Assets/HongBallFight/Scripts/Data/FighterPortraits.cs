using System;
using System.Collections.Generic;
using UnityEngine;

namespace HongBallFight
{
    [CreateAssetMenu(menuName = "Hong Ball Fight/Fighter Portraits", fileName = "FighterPortraits")]
    public sealed class FighterPortraits : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string fighterName;
            public Sprite photo;
            [Tooltip("사진 안에서 얼굴이 있는 영역 (0~1, 왼쪽 아래 기준)")]
            public Rect faceCrop = new Rect(0, 0, 1, 1);
        }
        public List<Entry> entries = new List<Entry>();

        static FighterPortraits instance;
        static readonly Dictionary<string, Sprite> faces = new Dictionary<string, Sprite>();

        public static Sprite Face(string fighterName)
        {
            if (string.IsNullOrEmpty(fighterName)) return null;
            if (faces.TryGetValue(fighterName, out var cached) && cached) return cached;
            if (!instance) instance = Resources.Load<FighterPortraits>("FighterPortraits");
            var entry = instance ? instance.entries.Find(e => e.fighterName == fighterName.Trim()) : null;
            if (entry == null || !entry.photo) return null;
            var source = entry.photo.rect; var crop = entry.faceCrop;
            var rect = new Rect(source.x + crop.x * source.width, source.y + crop.y * source.height, crop.width * source.width, crop.height * source.height);
            var face = Sprite.Create(entry.photo.texture, rect, Vector2.one * .5f, 100);
            face.name = fighterName + " 얼굴";
            faces[fighterName] = face;
            return face;
        }
    }
}
