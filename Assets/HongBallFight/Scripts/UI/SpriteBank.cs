using System;
using System.Collections.Generic;
using UnityEngine;

namespace HongBallFight
{
    public sealed class SpriteBank : IDisposable
    {
        sealed class Entry { public Sprite Sprite; public Texture2D Texture; public int Revision; }
        readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
        public SpriteBank(FighterData data)
        {
            foreach (var image in FighterImages.Read(data.assets_json))
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    byte[] bytes = Convert.FromBase64String(image.Data);
                    if (!FighterImages.PngSize(bytes, out int w, out int h) || w > 256 || h > 256 || !texture.LoadImage(bytes)) { UnityEngine.Object.Destroy(texture); continue; }
                    Add(image.Key, texture, 0);
                }
                catch { UnityEngine.Object.Destroy(texture); }
            }
        }
        void Add(string key, Texture2D texture, int revision)
        {
            texture.filterMode = FilterMode.Bilinear; texture.wrapMode = TextureWrapMode.Clamp;
            entries[key] = new Entry { Texture = texture, Revision = revision, Sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * .5f) };
        }
        public Sprite Get(string key, SkillWorld.ScriptState state = null)
        {
            key = string.IsNullOrEmpty(key) ? "orb" : key;
            entries.TryGetValue(key, out var entry);
            if (state != null && state.Images.TryGetValue(key, out var image))
            {
                if (entry == null || entry.Texture.width != image.Width || entry.Texture.height != image.Height)
                {
                    if (entry != null) { UnityEngine.Object.Destroy(entry.Sprite); UnityEngine.Object.Destroy(entry.Texture); }
                    Add(key, new Texture2D(image.Width, image.Height, TextureFormat.RGBA32, false), -1); entry = entries[key];
                }
                if (entry.Revision != image.Revision) { entry.Texture.LoadRawTextureData(image.Pixels); entry.Texture.Apply(); entry.Revision = image.Revision; }
            }
            if (entry != null) return entry.Sprite;
            if (key != "star" && key != "missile") return BallFightApp.Circle;
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var colors = new Color[4096];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float dx = x - 31.5f, dy = y - 31.5f;
                double angle = Math.Atan2(dy, dx); double radius = key == "star" ? 22 + 8 * Math.Cos(angle * 5) : 30 - Math.Abs(dy) * 1.5;
                colors[y * 64 + x] = new Color(1, 1, 1, Math.Sqrt(dx * dx + dy * dy) < radius ? 1 : 0);
            }
            texture.SetPixels(colors); texture.Apply(); Add(key, texture, 0); return entries[key].Sprite;
        }
        public void Dispose()
        {
            foreach (var entry in entries.Values) { UnityEngine.Object.Destroy(entry.Sprite); UnityEngine.Object.Destroy(entry.Texture); }
            entries.Clear();
        }
    }
}
