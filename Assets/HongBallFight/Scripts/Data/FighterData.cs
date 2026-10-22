using System;
using System.Collections.Generic;

namespace HongBallFight
{
    [Serializable]
    public class FighterData
    {
        public string id;
        public string name = "새로운 파이터";
        public int hp = 120;
        public int attack = 12;
        public float speed = 300;
        public string skill = "burst";
        public int skill_power = 24;
        public float cooldown = 5;
        public string color = "#FF785A";
        [NonSerialized] public string script = "";
        public string blocks_json = "";
        public string assets_json = "{\"items\":[]}";
        public string portrait = "";
        public int revision = 1;
        public string target_id;
        public int base_revision;
        public string status;
        public string review_note;

        public FighterData Copy() => (FighterData)MemberwiseClone();

        public string Validate()
        {
            if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 24) return "이름은 1~24자로 입력하세요.";
            if (hp < 30 || hp > 1000) return "체력 범위: 30~1000";
            if (attack < 1 || attack > 100) return "공격력 범위: 1~100";
            if (!float.IsFinite(speed) || speed < 100 || speed > 500) return "이동 속도 범위: 100~500";
            if (skill != "burst" && skill != "heal" && skill != "shield" && skill != "script" && skill != "army_aura" && skill != "pasta_whip") return "지원하지 않는 스킬입니다.";
            if (skill_power < 1 || skill_power > 150) return "스킬 위력 범위: 1~150";
            if (!float.IsFinite(cooldown) || cooldown < 2 || cooldown > 20) return "쿨타임 범위: 2~20초";
            if (color == null || !System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9a-fA-F]{6}$")) return "색상은 #FF785A 형태로 입력하세요.";
            if ((assets_json ?? "").Length > 700000) return "이미지 데이터는 총 700KB 이하여야 합니다.";
            if ((portrait ?? "").Length > 40) return "이미지 이름은 40자 이하여야 합니다.";
            try
            {
                FighterImages.Validate(assets_json);
                if (skill == "script") script = BlockDocument.Load(blocks_json).Generate();
            }
            catch (Exception error) { return error.Message; }
            return null;
        }

        public static string SkillName(string value) => value == "army_aura" ? "진급 오라" : value == "pasta_whip" ? "파스타 회초리" : value == "script" ? "블록 스킬" : value == "heal" ? "회복" : value == "shield" ? "보호막" : "충격파";
        public string Tagline => id == "gu-bonyun" || name == "구본윤" ? "눈물젖은 그녀와의 파스타를 아십니까?" : id == "jang-heecheol" || name == "장희철" ? "땅개" : SkillName(skill);

        public static List<FighterData> Samples() => new List<FighterData>
        {
            new FighterData { id = "jang-heecheol", name = "장희철", hp = 300, attack = 5, speed = 300, skill = "army_aura", skill_power = 6, cooldown = 3, color = "#728970" },
            new FighterData { id = "gu-bonyun", name = "구본윤", hp = 250, attack = 4, speed = 330, skill = "pasta_whip", skill_power = 22, cooldown = 2, color = "#D6A653" }
        };
    }

    [Serializable]
    public class FighterList { public List<FighterData> items = new List<FighterData>(); }

    [Serializable]
    public class BackendConfig
    {
        public string url = "";
        public string publishableKey = "";
        public bool Ready => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https" && !string.IsNullOrWhiteSpace(publishableKey);
    }
}
