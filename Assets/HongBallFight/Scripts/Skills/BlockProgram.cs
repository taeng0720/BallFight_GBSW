using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace HongBallFight
{
    public static class DataJson
    {
        public static object Parse(string source)
        {
            if (source == null || source.Length > 750000) throw new Exception("데이터 크기 초과");
            var reader = new Reader(source); var value = reader.Read(0); reader.White();
            if (reader.Index != source.Length) throw new Exception("잘못된 JSON 데이터");
            return value;
        }
        public static string Write(object value)
        {
            if (value == null) return "null";
            if (value is string text)
            {
                var result = new StringBuilder("\"");
                foreach (char ch in text)
                {
                    if (ch == '"' || ch == '\\') result.Append('\\').Append(ch);
                    else if (ch < 32) result.Append("\\u").Append(((int)ch).ToString("x4"));
                    else result.Append(ch);
                }
                return result.Append('"').ToString();
            }
            if (value is bool flag) return flag ? "true" : "false";
            if (value is IDictionary<string, object> map) return "{" + string.Join(",", map.Select(p => Write(p.Key) + ":" + Write(p.Value))) + "}";
            if (value is IEnumerable list) { var parts = new List<string>(); foreach (var item in list) parts.Add(Write(item)); return "[" + string.Join(",", parts) + "]"; }
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
        public static Dictionary<string, object> Map(object value) => value as Dictionary<string, object> ?? throw new Exception("객체 데이터가 필요합니다.");
        public static List<object> List(object value) => value as List<object> ?? throw new Exception("목록 데이터가 필요합니다.");
        public static object Get(Dictionary<string, object> map, string key, object fallback = null) => map.TryGetValue(key, out var value) ? value : fallback;
        public static string Text(Dictionary<string, object> map, string key, string fallback = "") => Get(map, key, fallback) as string ?? fallback;
        sealed class Reader
        {
            readonly string source;
            public int Index;
            public Reader(string source) { this.source = source; }
            public void White() { while (Index < source.Length && char.IsWhiteSpace(source[Index])) Index++; }
            char Take() { if (Index >= source.Length) throw new Exception("JSON이 끝나지 않았습니다."); return source[Index++]; }
            public object Read(int depth)
            {
                if (depth > 48) throw new Exception("데이터 중첩 한도 초과");
                White(); char ch = Take();
                if (ch == '"')
                {
                    var text = new StringBuilder();
                    while (true)
                    {
                        char c = Take(); if (c == '"') break;
                        if (c == '\\')
                        {
                            c = Take();
                            if (c == 'u') { var hex = new string(new[] { Take(), Take(), Take(), Take() }); c = (char)Convert.ToInt32(hex, 16); }
                            else if (c == 'n') c = '\n'; else if (c == 'r') c = '\r'; else if (c == 't') c = '\t';
                            else if (c == 'b') c = '\b'; else if (c == 'f') c = '\f'; else if (c != '"' && c != '\\' && c != '/') throw new Exception("잘못된 문자열 이스케이프");
                        }
                        else if (c < 32) throw new Exception("잘못된 문자열");
                        text.Append(c);
                    }
                    return text.ToString();
                }
                if (ch == '{' || ch == '[')
                {
                    var map = new Dictionary<string, object>(); var list = new List<object>();
                    White(); char close = ch == '{' ? '}' : ']';
                    if (Index < source.Length && source[Index] == close) { Index++; return ch == '{' ? (object)map : list; }
                    while (true)
                    {
                        if (ch == '{') { var key = Read(depth + 1) as string ?? throw new Exception("JSON 키 오류"); White(); if (Take() != ':') throw new Exception("JSON 콜론 오류"); map.Add(key, Read(depth + 1)); }
                        else list.Add(Read(depth + 1));
                        White(); char next = Take(); if (next == close) break; if (next != ',') throw new Exception("JSON 구분 오류");
                    }
                    return ch == '{' ? (object)map : list;
                }
                int start = Index - 1;
                while (Index < source.Length && !char.IsWhiteSpace(source[Index]) && source[Index] != ',' && source[Index] != '}' && source[Index] != ']') Index++;
                string token = source.Substring(start, Index - start);
                if (token == "true") return true; if (token == "false") return false; if (token == "null") return null;
                if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number)) return number;
                throw new Exception("JSON 값 오류");
            }
        }
    }

    public sealed class BlockValue
    {
        public string Kind = "number", Text = "0";
        public List<BlockValue> Inputs = new List<BlockValue>();
        public static BlockValue Number(string value = "0") => new BlockValue { Text = value };
        public static BlockValue TextValue(string value) => new BlockValue { Kind = "text", Text = value };
        public static BlockValue Reference(string value) => new BlockValue { Kind = "ref", Text = value };
        public object Json() => new Dictionary<string, object> { ["kind"] = Kind, ["text"] = Text, ["inputs"] = Inputs.Select(v => v.Json()).ToList() };
        public static BlockValue From(object json, int depth = 0)
        {
            if (depth > 8) throw new Exception("값 블록 중첩은 8단계까지 가능합니다.");
            var map = DataJson.Map(json);
            return new BlockValue { Kind = DataJson.Text(map, "kind"), Text = DataJson.Text(map, "text"), Inputs = DataJson.List(DataJson.Get(map, "inputs", new List<object>())).Select(v => From(v, depth + 1)).ToList() };
        }
        public string Emit(int depth = 0)
        {
            if (depth > 8) throw new Exception("값 블록 중첩 초과");
            if (Kind == "text") { if (Text.Length > 256) throw new Exception("텍스트는 256자 이하여야 합니다."); return DataJson.Write(Text); }
            if (Kind == "number")
            {
                if (!double.TryParse(Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number) || Math.Abs(number) > 100000) throw new Exception("값 블록에 올바른 숫자를 입력하세요.");
                return number.ToString("0.########", CultureInfo.InvariantCulture);
            }
            if (Kind == "ref" && (SkillScript.Properties.Contains(Text) || Text == "last" || Enumerable.Range(1, 8).Any(i => Text == "var" + i))) return Text;
            if (Kind == "math" && new[] { "+", "-", "*", "/", "%" }.Contains(Text) && Inputs.Count == 2) return "(" + Inputs[0].Emit(depth + 1) + Text + Inputs[1].Emit(depth + 1) + ")";
            if (Kind == "call")
            {
                var api = SkillApi.Find(Text);
                if (api != null && BlockCatalog.ValueFunctions.Contains(Text) && Inputs.Count == api.Arguments) return Text + "(" + string.Join(",", Inputs.Select(v => v.Emit(depth + 1))) + ")";
            }
            throw new Exception("지원하지 않는 값 블록입니다.");
        }
        public string Display()
        {
            if (Kind == "ref") return BlockCatalog.PropertyTitle(Text);
            if (Kind == "math") return "(" + string.Join(" " + Text + " ", Inputs.Select(v => v.Display())) + ")";
            if (Kind == "call") return BlockCatalog.Title(Text) + "(" + string.Join(", ", Inputs.Select(v => v.Display())) + ")";
            return Text;
        }
    }
    public sealed class SkillBlock
    {
        public string Id = Guid.NewGuid().ToString("N"), Op, Option = ">", Variable = "var1";
        public List<BlockValue> Values = new List<BlockValue>();
        public List<SkillBlock> Children = new List<SkillBlock>(), Else = new List<SkillBlock>();
        public object Json() => new Dictionary<string, object> { ["id"] = Id, ["op"] = Op, ["option"] = Option, ["variable"] = Variable, ["values"] = Values.Select(v => v.Json()).ToList(), ["children"] = Children.Select(b => b.Json()).ToList(), ["else"] = Else.Select(b => b.Json()).ToList() };
        public static SkillBlock From(object json, int depth = 0)
        {
            if (depth > 10) throw new Exception("블록 중첩은 10단계까지 가능합니다.");
            var map = DataJson.Map(json);
            return new SkillBlock
            {
                Id = DataJson.Text(map, "id"), Op = DataJson.Text(map, "op"), Option = DataJson.Text(map, "option"), Variable = DataJson.Text(map, "variable", "var1"),
                Values = DataJson.List(DataJson.Get(map, "values", new List<object>())).Select(v => BlockValue.From(v)).ToList(),
                Children = DataJson.List(DataJson.Get(map, "children", new List<object>())).Select(v => From(v, depth + 1)).ToList(),
                Else = DataJson.List(DataJson.Get(map, "else", new List<object>())).Select(v => From(v, depth + 1)).ToList()
            };
        }
        public bool Contains(SkillBlock block) => this == block || Children.Any(b => b.Contains(block)) || Else.Any(b => b.Contains(block));
        public int Depth => 1 + Math.Max(Children.Count == 0 ? 0 : Children.Max(b => b.Depth), Else.Count == 0 ? 0 : Else.Max(b => b.Depth));
        public int Count => 1 + Children.Sum(b => b.Count) + Else.Sum(b => b.Count);
    }
    public sealed class BlockDocument
    {
        public readonly Dictionary<string, List<SkillBlock>> Events = new Dictionary<string, List<SkillBlock>>();
        public BlockDocument() { foreach (var name in SkillScript.Events) Events[name] = new List<SkillBlock>(); }
        public string ToJson() => DataJson.Write(new Dictionary<string, object> { ["version"] = 1, ["events"] = Events.ToDictionary(p => p.Key, p => (object)p.Value.Select(b => b.Json()).ToList()) });
        public static BlockDocument Load(string json)
        {
            var document = new BlockDocument();
            if (string.IsNullOrWhiteSpace(json)) return document;
            if (json.Length > 100000) throw new Exception("블록 데이터는 100KB 이하여야 합니다.");
            var map = DataJson.Map(DataJson.Parse(json));
            var events = DataJson.Map(DataJson.Get(map, "events"));
            foreach (var item in events)
            {
                if (!document.Events.ContainsKey(item.Key)) throw new Exception("지원하지 않는 이벤트");
                document.Events[item.Key] = DataJson.List(item.Value).Select(b => SkillBlock.From(b)).ToList();
            }
            return document;
        }
        public string Generate()
        {
            var code = new StringBuilder("float last = 0;\n");
            for (int i = 1; i <= 8; i++) code.Append("float var").Append(i).Append(" = 0;\n");
            int count = 0; var ids = new HashSet<string>();
            foreach (var item in Events)
            {
                if (!SkillScript.Events.Contains(item.Key)) throw new Exception("이벤트 이름 오류");
                code.Append("void ").Append(item.Key).Append("() {\n"); Emit(item.Value, code, ref count, ids, 0); code.Append("}\n");
            }
            string source = code.ToString();
            var error = SkillScript.Validate(source); if (error != null) throw new Exception(error);
            return source;
        }
        static void Emit(List<SkillBlock> blocks, StringBuilder code, ref int count, HashSet<string> ids, int depth)
        {
            if (depth > 10) throw new Exception("블록 중첩 초과");
            foreach (var block in blocks)
            {
                if (++count > 200 || !ids.Add(block.Id) || block.Id.Length != 32 || !block.Id.All(char.IsLetterOrDigit)) throw new Exception("블록 수 또는 블록 ID 오류");
                if (block.Op == "repeat" || block.Op == "if" || block.Op == "every")
                {
                    int needed = block.Op == "if" ? 2 : 1; if (block.Values.Count != needed) throw new Exception("제어 블록 값 오류");
                    if (block.Op == "if")
                    {
                        if (!new[] { ">", "<", ">=", "<=", "==", "!=" }.Contains(block.Option)) throw new Exception("조건 연산 오류");
                        code.Append("if (").Append(block.Values[0].Emit()).Append(block.Option).Append(block.Values[1].Emit()).Append(") {\n");
                    }
                    else if (block.Op == "repeat") code.Append("for (int loop").Append(count).Append(" = 0; loop").Append(count).Append(" < Mathf.Clamp(").Append(block.Values[0].Emit()).Append(",0,32); loop").Append(count).Append("++) {\n");
                    else code.Append("if (Every(\"").Append(block.Id).Append("\",").Append(block.Values[0].Emit()).Append(")) {\n");
                    Emit(block.Children, code, ref count, ids, depth + 1); code.Append("}\n");
                    if (block.Op == "if" && block.Else.Count > 0) { code.Append("else {\n"); Emit(block.Else, code, ref count, ids, depth + 1); code.Append("}\n"); }
                }
                else if (block.Op == "set" || block.Op == "add")
                {
                    if (block.Values.Count != 1 || !Enumerable.Range(1, 8).Any(i => block.Variable == "var" + i)) throw new Exception("변수 블록 오류");
                    code.Append(block.Variable).Append(block.Op == "set" ? "=" : "+=").Append(block.Values[0].Emit()).Append(";\n");
                }
                else
                {
                    var api = SkillApi.Find(block.Op);
                    if (api == null || block.Values.Count != api.Arguments || BlockCatalog.ValueFunctions.Contains(block.Op)) throw new Exception("동작 블록 오류");
                    for (int i = 0; i < block.Values.Count; i++)
                        if (BlockCatalog.IsText(block.Op, i) != (block.Values[i].Kind == "text")) throw new Exception("블록 값의 종류가 맞지 않습니다: " + BlockCatalog.Title(block.Op));
                    if (BlockCatalog.ReturnsObject.Contains(block.Op)) code.Append("last = ");
                    code.Append(block.Op).Append('(').Append(string.Join(",", block.Values.Select(v => v.Emit()))).Append(");\n");
                }
            }
        }
        public bool Move(SkillBlock block, List<SkillBlock> target, int index, SkillBlock parent)
        {
            if (block == null || parent != null && block.Contains(parent)) return false;
            var previous = FindList(block);
            if (previous == null && Events.Values.Sum(list => list.Sum(b => b.Count)) + block.Count > 200) return false;
            if (parent != null && ParentDepth(parent) + block.Depth > 11) return false;
            if (previous == target && previous.IndexOf(block) < index) index--;
            previous?.Remove(block); target.Insert(Math.Clamp(index, 0, target.Count), block); return true;
        }
        int ParentDepth(SkillBlock block)
        {
            int Find(List<SkillBlock> list, int depth)
            {
                foreach (var item in list) { if (item == block) return depth; int found = Find(item.Children, depth + 1); if (found < 0) found = Find(item.Else, depth + 1); if (found >= 0) return found; } return -1;
            }
            foreach (var list in Events.Values) { int found = Find(list, 1); if (found >= 0) return found; } return 0;
        }
        public List<SkillBlock> FindList(SkillBlock target)
        {
            foreach (var list in Events.Values) { var result = Search(list, target); if (result != null) return result; } return null;
        }
        static List<SkillBlock> Search(List<SkillBlock> list, SkillBlock target)
        {
            if (list.Contains(target)) return list;
            foreach (var item in list) { var found = Search(item.Children, target) ?? Search(item.Else, target); if (found != null) return found; } return null;
        }
    }

    public static class BlockCatalog
    {
        public static readonly string[] ReturnsObject = { "Fire", "FireAngle", "FireSpread", "FireRing", "Spawn", "Orbit" };
        public static readonly string[] ValueFunctions = { "GetX", "GetY", "GetCount", "AimAngle", "EnemyDistance", "Exists", "Every", "Random.Range", "Mathf.Sin", "Mathf.Cos", "Mathf.Abs", "Mathf.Sqrt", "Mathf.Min", "Mathf.Max", "Mathf.Clamp", "Mathf.Lerp", "Mathf.Floor", "Mathf.Ceil", "Mathf.Pow", "Distance" };
        static readonly Dictionary<string, string> Titles = new Dictionary<string, string>
        {
            ["Fire"] = "이미지를 적에게 발사", ["FireAngle"] = "지정 각도로 발사", ["FireSpread"] = "부채꼴로 발사", ["FireRing"] = "원형으로 발사", ["Spawn"] = "이미지 오브젝트 만들기", ["Orbit"] = "주변을 도는 구 만들기", ["LaunchOrbitals"] = "주변 구를 적에게 발사", ["Destroy"] = "오브젝트 지우기", ["DestroyAll"] = "내 오브젝트 모두 지우기", ["Exists"] = "오브젝트 존재 여부", ["SetVelocity"] = "오브젝트 이동 속도", ["SetPosition"] = "오브젝트 위치", ["SetSize"] = "오브젝트 크기", ["SetDamage"] = "오브젝트 피해량", ["SetLifetime"] = "오브젝트 수명", ["SetHoming"] = "유도 강도", ["SetBounce"] = "벽 반사 횟수", ["SetPiercing"] = "관통 횟수", ["SetRotation"] = "이미지 방향", ["SetSpin"] = "이미지 회전 속도", ["SetTint"] = "이미지 색상", ["SetImage"] = "오브젝트 이미지 바꾸기", ["GetX"] = "오브젝트 X 위치", ["GetY"] = "오브젝트 Y 위치", ["GetCount"] = "내 오브젝트 수", ["AimAngle"] = "적을 향한 각도", ["EnemyDistance"] = "적과의 거리", ["Explode"] = "범위 폭발", ["Damage"] = "적에게 피해 주기", ["Heal"] = "체력 회복", ["Shield"] = "보호막 만들기", ["Dash"] = "적 방향 돌진", ["SetSpeed"] = "이동 속도 바꾸기", ["SetDirection"] = "이동 방향 바꾸기", ["Teleport"] = "순간이동", ["SetSprite"] = "캐릭터 이미지 바꾸기", ["CreateImage"] = "단색 이미지 만들기", ["SetPixel"] = "픽셀 칠하기", ["DrawCircle"] = "이미지에 원 그리기", ["DrawRect"] = "이미지에 사각형 그리기", ["Every"] = "타이머", ["Random.Range"] = "무작위 수", ["Mathf.Sin"] = "사인", ["Mathf.Cos"] = "코사인", ["Mathf.Abs"] = "절댓값", ["Mathf.Sqrt"] = "제곱근", ["Mathf.Min"] = "작은 값", ["Mathf.Max"] = "큰 값", ["Mathf.Clamp"] = "범위 제한", ["Mathf.Lerp"] = "비율 섞기", ["Mathf.Floor"] = "내림", ["Mathf.Ceil"] = "올림", ["Mathf.Pow"] = "거듭제곱", ["Distance"] = "두 위치의 거리", ["Log"] = "메시지 표시", ["repeat"] = "횟수만큼 반복", ["if"] = "만약 ~ 라면", ["every"] = "몇 초마다 실행", ["set"] = "변수 정하기", ["add"] = "변수에 더하기"
        };
        public static string Title(string key) => Titles.TryGetValue(key, out var title) ? title : key;
        public static readonly Dictionary<string, string> EventTitles = new Dictionary<string, string> { ["Start"] = "시작했을 때", ["Update"] = "계속 실행", ["FixedUpdate"] = "물리 갱신할 때", ["OnSkill"] = "동작 주기가 됐을 때", ["OnCollision"] = "공과 부딪혔을 때", ["OnWallHit"] = "벽에 부딪혔을 때", ["OnDamage"] = "피해를 받았을 때", ["OnDeath"] = "체력이 0이 됐을 때", ["OnProjectileHit"] = "내 발사체가 적에게 맞았을 때" };
        public static string PropertyTitle(string key)
        {
            var labels = new Dictionary<string, string> { ["Time.deltaTime"] = "지난 프레임 시간", ["Time.time"] = "경과 시간", ["Self.HP"] = "내 체력", ["Self.MaxHP"] = "내 최대 체력", ["Self.X"] = "내 X", ["Self.Y"] = "내 Y", ["Self.Speed"] = "내 속도", ["Self.Attack"] = "내 공격력", ["Self.SkillPower"] = "스킬 위력", ["Self.Shield"] = "내 보호막", ["Self.Cooldown"] = "남은 쿨타임", ["Enemy.HP"] = "적 체력", ["Enemy.MaxHP"] = "적 최대 체력", ["Enemy.X"] = "적 X", ["Enemy.Y"] = "적 Y", ["Arena.Width"] = "경기장 너비", ["Arena.Height"] = "경기장 높이", ["Hit.Damage"] = "받은 피해", ["Hit.Projectile"] = "맞힌 발사체", ["Mathf.PI"] = "원주율", ["last"] = "마지막 생성 오브젝트" };
            return labels.TryGetValue(key, out var title) ? title : key.StartsWith("var", StringComparison.Ordinal) ? "변수 " + key.Substring(3) : key;
        }
        public static bool IsText(string op, int index)
        {
            if (op == "Log") return index == 0;
            if (index == 0 && new[] { "Fire", "FireAngle", "FireSpread", "FireRing", "Spawn", "Orbit", "SetSprite", "CreateImage", "SetPixel", "DrawCircle", "DrawRect", "Every" }.Contains(op)) return true;
            return op == "SetImage" && index == 1;
        }
        public static SkillBlock Create(string op)
        {
            var block = new SkillBlock { Op = op };
            if (op == "if") block.Values.AddRange(new[] { BlockValue.Reference("Self.HP"), BlockValue.Number("60") });
            else if (op == "repeat") block.Values.Add(BlockValue.Number("3"));
            else if (op == "every") block.Values.Add(BlockValue.Number("1"));
            else if (op == "set" || op == "add") block.Values.Add(BlockValue.Number("1"));
            else
            {
                var api = SkillApi.Find(op) ?? throw new Exception("블록 종류 오류");
                var parameters = api.Signature.Substring(op.Length + 1).TrimEnd(')').Split(',').Select(s => s.Trim()).ToArray();
                for (int i = 0; i < api.Arguments; i++)
                {
                    string p = parameters[i];
                    if (IsText(op, i)) block.Values.Add(BlockValue.TextValue(p == "message" ? "안녕!" : p == "name" ? "custom" : "orb"));
                    else if (p == "id") block.Values.Add(BlockValue.Reference("last"));
                    else if (p == "x" || p == "x1") block.Values.Add(op == "Spawn" || op == "Explode" ? BlockValue.Reference("Self.X") : BlockValue.Number("16"));
                    else if (p == "y" || p == "y1") block.Values.Add(op == "Spawn" || op == "Explode" ? BlockValue.Reference("Self.Y") : BlockValue.Number("16"));
                    else block.Values.Add(BlockValue.Number(p == "speed" ? "320" : p == "count" ? "6" : p == "radius" ? "80" : p == "degreesPerSecond" ? "120" : p == "damage" ? "8" : p == "lifetime" || p == "seconds" ? "5" : p == "width" || p == "height" || p == "size" ? "32" : p == "spread" ? "60" : p == "strength" ? "3" : p == "amount" ? "20" : "1"));
                }
            }
            return block;
        }
        public static BlockDocument Template(int index)
        {
            var doc = new BlockDocument();
            if (index == 0) { doc.Events["Start"].Add(Create("Orbit")); doc.Events["OnSkill"].Add(Create("LaunchOrbitals")); doc.Events["OnSkill"].Add(Create("Orbit")); }
            else if (index == 1)
            {
                var timer = Create("every"); timer.Children.Add(Create("Fire")); timer.Children.Add(Create("SetHoming")); doc.Events["Update"].Add(timer);
                doc.Events["OnSkill"].Add(Create("FireSpread"));
            }
            else if (index == 2) { doc.Events["Start"].Add(Create("CreateImage")); var shot = Create("FireRing"); shot.Values[0] = BlockValue.TextValue("custom"); doc.Events["OnSkill"].Add(shot); }
            else if (index == 3) { doc.Events["OnSkill"].Add(Create("Heal")); doc.Events["OnCollision"].Add(Create("Shield")); }
            else doc.Events["OnSkill"].Add(Create("FireRing"));
            return doc;
        }
    }
}
