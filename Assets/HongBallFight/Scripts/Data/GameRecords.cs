using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HongBallFight
{
    [Serializable]
    public sealed class MatchRecord
    {
        public string leftId, leftName, leftColor, rightId, rightName, rightColor;
        public int winner;
        public float seconds;
        public long time;
        public string LeftKey => string.IsNullOrEmpty(leftId) ? leftName : leftId;
        public string RightKey => string.IsNullOrEmpty(rightId) ? rightName : rightId;
        public bool Mirror => LeftKey == RightKey;
    }

    [Serializable]
    public sealed class IdeaRecord
    {
        public string id, name, author, description, created_at;
        public long time;
        public string When => time > 0 ? GameRecords.FormatTime(time) : DateTimeOffset.TryParse(created_at, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date) ? date.ToLocalTime().ToString("MM/dd HH:mm") : "";
    }

    public static class GameRecords
    {
        [Serializable]
        sealed class Store
        {
            public List<MatchRecord> matches = new List<MatchRecord>();
            public List<IdeaRecord> ideas = new List<IdeaRecord>();
        }

        public sealed class Standing
        {
            public string Key, Name, Color;
            public int Wins, Losses, Draws;
            public int Games => Wins + Losses + Draws;
            public float Rate => Games == 0 ? 0 : Wins / (float)Games;
        }

        public sealed class Rivalry
        {
            public string AName, BName;
            public int AWins, BWins, Draws;
            public int Games => AWins + BWins + Draws;
        }

        const string Key = "HongBallFight.Records.v1";
        const int MatchLimit = 500;
        static Store store;

        static Store Data
        {
            get
            {
                if (store != null) return store;
                try { store = JsonUtility.FromJson<Store>(PlayerPrefs.GetString(Key, "{}")); } catch { store = null; }
                return store ??= new Store();
            }
        }
        static void Save() { PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data)); PlayerPrefs.Save(); }
        static long Now => DateTimeOffset.Now.ToUnixTimeSeconds();
        public static string FormatTime(long time) => DateTimeOffset.FromUnixTimeSeconds(time).ToLocalTime().ToString("MM/dd HH:mm");

        public static IReadOnlyList<MatchRecord> Matches => Data.matches;
        public static IReadOnlyList<IdeaRecord> Ideas => Data.ideas;

        public static void AddMatch(FighterData left, FighterData right, int winner, float seconds)
        {
            Data.matches.Insert(0, new MatchRecord
            {
                leftId = left.id, leftName = left.name, leftColor = left.color,
                rightId = right.id, rightName = right.name, rightColor = right.color,
                winner = winner, seconds = seconds, time = Now
            });
            if (Data.matches.Count > MatchLimit) Data.matches.RemoveRange(MatchLimit, Data.matches.Count - MatchLimit);
            Save();
        }
        public static void ClearMatches() { Data.matches.Clear(); Save(); }

        public static string ValidateIdea(ref string name, ref string author, ref string description)
        {
            name = (name ?? "").Trim(); author = (author ?? "").Trim(); description = (description ?? "").Trim();
            if (name.Length == 0 || name.Length > 24) return "캐릭터 이름은 1~24자로 입력하세요.";
            if (description.Length < 5) return "아이디어 설명을 5자 이상 적어 주세요.";
            if (description.Length > 300) return "아이디어 설명은 300자 이하로 적어 주세요.";
            if (author.Length > 16) return "작성자는 16자 이하로 입력하세요.";
            return null;
        }
        public static string AddIdea(string name, string author, string description)
        {
            var error = ValidateIdea(ref name, ref author, ref description);
            if (error != null) return error;
            Data.ideas.Insert(0, new IdeaRecord { id = Guid.NewGuid().ToString("N"), name = name, author = author, description = description, time = Now });
            Save();
            return null;
        }

        public static List<Standing> Standings()
        {
            var table = new Dictionary<string, Standing>();
            Standing Get(string key, string name, string color)
            {
                if (!table.TryGetValue(key, out var standing)) table[key] = standing = new Standing { Key = key };
                standing.Name = name; standing.Color = color;
                return standing;
            }
            foreach (var match in Data.matches)
            {
                if (match.Mirror) continue;
                var left = Get(match.LeftKey, match.leftName, match.leftColor);
                var right = Get(match.RightKey, match.rightName, match.rightColor);
                if (match.winner == 1) { left.Wins++; right.Losses++; }
                else if (match.winner == 2) { right.Wins++; left.Losses++; }
                else { left.Draws++; right.Draws++; }
            }
            var result = new List<Standing>(table.Values);
            result.Sort((a, b) => b.Rate != a.Rate ? b.Rate.CompareTo(a.Rate) : b.Games.CompareTo(a.Games));
            return result;
        }

        public static List<Rivalry> Rivalries()
        {
            var table = new Dictionary<string, Rivalry>();
            foreach (var match in Data.matches)
            {
                if (match.Mirror) continue;
                bool swap = string.CompareOrdinal(match.LeftKey, match.RightKey) > 0;
                string key = swap ? match.RightKey + "|" + match.LeftKey : match.LeftKey + "|" + match.RightKey;
                if (!table.TryGetValue(key, out var rivalry))
                    table[key] = rivalry = new Rivalry { AName = swap ? match.rightName : match.leftName, BName = swap ? match.leftName : match.rightName };
                int winner = swap && match.winner != 0 ? 3 - match.winner : match.winner;
                if (winner == 1) rivalry.AWins++; else if (winner == 2) rivalry.BWins++; else rivalry.Draws++;
            }
            var result = new List<Rivalry>(table.Values);
            result.Sort((a, b) => b.Games.CompareTo(a.Games));
            return result;
        }
    }
}
