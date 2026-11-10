using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace WayfarerMech
{
    /// <summary>
    /// Loads native-size layered PNGs declared by Assets/parts.json. Pivots are in
    /// source pixel coordinates (x right, y down). No image is stretched to replace
    /// missing native frames; a missing key is logged and omitted.
    /// </summary>
    public static class MechArt
    {
        public const float Ppu = 11f;

        public sealed class SpriteInfo
        {
            public string Key;
            public int Width;
            public int Height;
            public int PivotX;
            public int PivotY;
            public Sprite Sprite;
        }

        private sealed class PivotInfo
        {
            public int W;
            public int H;
            public int X;
            public int Y;
            public float Ppu;
        }

        private static readonly Dictionary<string, PivotInfo> Pivots = new Dictionary<string, PivotInfo>();
        private static readonly Dictionary<string, SpriteInfo> Cache = new Dictionary<string, SpriteInfo>();
        private static bool manifestLoaded;

        public static SpriteInfo Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            EnsureManifest();
            if (Cache.TryGetValue(key, out var cached) && cached.Sprite) return cached;
            var info = Load(key);
            if (info != null) Cache[key] = info;
            return info;
        }

        public static Sprite Sprite(string key) => Get(key)?.Sprite;

        public static Sprite Beacon()
        {
            var info = Get("beacon");
            if (info != null && info.Sprite) return info.Sprite;
            var tex = new Texture2D(16, 20, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var pixels = new Color[320];
            for (int y = 0; y < 20; y++)
            for (int x = 0; x < 16; x++)
            {
                pixels[y * 16 + x] = x >= 3 && x <= 12 && y >= 2 && y <= 16
                    ? (x == 3 || x == 12 || y == 2 || y == 16
                        ? new Color32(24, 29, 35, 255)
                        : new Color32(81, 98, 109, 255))
                    : Color.clear;
            }
            tex.SetPixels(pixels); tex.Apply();
            return UnityEngine.Sprite.Create(tex, new Rect(0, 0, 16, 20), Vector2.one * 0.5f, 8f);
        }

        private static void EnsureManifest()
        {
            if (manifestLoaded) return;
            manifestLoaded = true;
            var asm = typeof(MechArt).Assembly;
            string resource = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("parts.json", StringComparison.OrdinalIgnoreCase));
            if (resource == null)
            {
                Plugin.LogError("Mech art manifest missing: parts.json");
                return;
            }
            try
            {
                string text;
                using (var stream = asm.GetManifestResourceStream(resource))
                using (var reader = new StreamReader(stream))
                    text = reader.ReadToEnd();
                var obj = JObject.Parse(text);
                foreach (var prop in obj.Properties())
                {
                    var v = prop.Value;
                    var pivot = v["pivot"] as JArray;
                    Pivots[prop.Name] = new PivotInfo
                    {
                        W = v.Value<int?>("w") ?? 0,
                        H = v.Value<int?>("h") ?? 0,
                        X = pivot != null && pivot.Count > 0 ? pivot[0].Value<int>() : 0,
                        Y = pivot != null && pivot.Count > 1 ? pivot[1].Value<int>() : 0,
                        Ppu = v.Value<float?>("ppu") ?? Ppu,
                    };
                }
                Plugin.LogInfo("Mech art manifest loaded: " + Pivots.Count + " entries");
            }
            catch (Exception ex)
            {
                Plugin.LogError("Mech art manifest parse failed: " + ex);
            }
        }

        private static SpriteInfo Load(string key)
        {
            if (!Pivots.TryGetValue(key, out var pivot))
            {
                Plugin.LogError("No art manifest entry for: " + key);
                return null;
            }
            var asm = typeof(MechArt).Assembly;
            string suffix = key + ".png";
            string resource = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
            if (resource == null)
            {
                Plugin.LogError("Embedded art missing: " + suffix);
                return null;
            }
            byte[] bytes;
            using (var stream = asm.GetManifestResourceStream(resource))
            {
                if (stream == null) return null;
                using (var memory = new MemoryStream())
                {
                    stream.CopyTo(memory);
                    bytes = memory.ToArray();
                }
            }
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(bytes);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.name = key;
            texture.Apply(false, true);
            if (texture.width <= 0 || texture.height <= 0 ||
                pivot.X < 0 || pivot.Y < 0 || pivot.X > texture.width || pivot.Y > texture.height)
            {
                Plugin.LogError("Invalid art texture/pivot: " + key + " " + texture.width + "x" + texture.height +
                                " pivot=" + pivot.X + "," + pivot.Y);
                return null;
            }
            var info = new SpriteInfo
            {
                Key = key,
                Width = texture.width,
                Height = texture.height,
                PivotX = pivot.X,
                PivotY = pivot.Y,
                Sprite = UnityEngine.Sprite.Create(
                    texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(pivot.X / (float)texture.width, 1f - pivot.Y / (float)texture.height),
                    pivot.Ppu > 0f ? pivot.Ppu : Ppu,
                    0,
                    SpriteMeshType.FullRect)
            };
            info.Sprite.name = key;
            return info;
        }

        public static void ReleaseAll()
        {
            foreach (var info in Cache.Values)
            {
                if (info?.Sprite)
                {
                    var tex = info.Sprite.texture;
                    UnityEngine.Object.Destroy(info.Sprite);
                    if (tex) UnityEngine.Object.Destroy(tex);
                }
            }
            Cache.Clear();
        }
    }
}
