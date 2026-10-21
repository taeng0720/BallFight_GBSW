using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace HongBallFight
{
    public sealed class FighterImage
    {
        public string Key, Data;
    }
    public static class FighterImages
    {
        public const int MaxImageBytes = 100000;
        public static bool ValidName(string value) => Regex.IsMatch(value ?? "", "^[a-zA-Z][a-zA-Z0-9_]{0,31}$");
        public static List<FighterImage> Read(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<FighterImage>();
            var map = DataJson.Map(DataJson.Parse(json));
            var list = DataJson.List(DataJson.Get(map, "items", new List<object>()));
            if (list.Count > 12) throw new Exception("불러온 이미지는 최대 12개입니다.");
            return list.Select(item => { var entry = DataJson.Map(item); return new FighterImage { Key = DataJson.Text(entry, "key"), Data = DataJson.Text(entry, "data") }; }).ToList();
        }
        public static string Write(List<FighterImage> images) => DataJson.Write(new Dictionary<string, object> { ["items"] = images.Select(i => (object)new Dictionary<string, object> { ["key"] = i.Key, ["data"] = i.Data }).ToList() });
        public static void Validate(string json)
        {
            if ((json ?? "").Length > 700000) throw new Exception("이미지 데이터 총 크기는 700KB 이하여야 합니다.");
            var names = new HashSet<string>();
            foreach (var image in Read(json))
            {
                if (!ValidName(image.Key) || !names.Add(image.Key)) throw new Exception("이미지 이름은 영문으로 시작하는 1~32자이며 중복될 수 없습니다.");
                byte[] bytes;
                try { bytes = Convert.FromBase64String(image.Data); } catch { throw new Exception("손상된 이미지 데이터"); }
                if (bytes.Length > MaxImageBytes || !PngSize(bytes, out var w, out var h) || w > 256 || h > 256) throw new Exception("이미지는 최대 256×256 PNG, 100KB까지 지원합니다.");
            }
        }
        public static bool PngSize(byte[] data, out int width, out int height)
        {
            width = height = 0;
            if (data.Length < 24 || data[0] != 137 || data[1] != 80 || data[2] != 78 || data[3] != 71 || data[4] != 13 || data[5] != 10 || data[6] != 26 || data[7] != 10) return false;
            width = ReadInt(data, 16); height = ReadInt(data, 20); return width > 0 && height > 0;
        }
        static int ReadInt(byte[] data, int offset) => data[offset] << 24 | data[offset + 1] << 16 | data[offset + 2] << 8 | data[offset + 3];
    }
}
