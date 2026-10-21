using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

namespace HongBallFight
{
    public partial class BallFightApp
    {
        bool studioAdmin;
        BlockDocument studioDocument;
        string studioEvent = "OnSkill", blockCategory = "전투", studioStatus = "블록을 왼쪽에서 오른쪽으로 끌어오세요.";
        float workspaceScroll = 1;
        ScrollRect workspace;
        RectTransform popup;
        Text blockStatus;
        SpriteBank imageBank;
        string imageKey = "myImage", imageColor = "#FF785A";
        string pendingImageKey;
        FighterData StudioFighter => studioAdmin && reviewing != null ? reviewing : editing;
        readonly string[] blockCategories = { "전투", "생성", "꾸미기", "제어", "값", "이미지" };
        static readonly Dictionary<string, string> ParamTitles = new Dictionary<string, string>
        {
            ["image"] = "이미지", ["name"] = "이름", ["speed"] = "속도", ["damage"] = "피해량", ["angle"] = "각도", ["count"] = "개수", ["spread"] = "퍼짐 각도", ["x"] = "X", ["y"] = "Y", ["size"] = "크기", ["lifetime"] = "수명(초)", ["radius"] = "반지름", ["degreesPerSecond"] = "회전 속도", ["id"] = "대상 오브젝트", ["seconds"] = "초", ["strength"] = "강도", ["degrees"] = "각도", ["r"] = "빨강(0~1)", ["g"] = "초록(0~1)", ["b"] = "파랑(0~1)", ["a"] = "불투명도(0~1)", ["amount"] = "양", ["width"] = "너비", ["height"] = "높이", ["message"] = "메시지"
        };
        void UpdateStudio() { if (blockStatus) blockStatus.text = studioStatus; }
        void Studio()
        {
            if (studioAdmin && reviewing == null) studioAdmin = false;
            if (studioDocument == null)
            {
                try { studioDocument = string.IsNullOrWhiteSpace(StudioFighter.blocks_json) ? BlockCatalog.Template(0) : BlockDocument.Load(StudioFighter.blocks_json); }
                catch (Exception error) { studioStatus = error.Message; studioDocument = new BlockDocument(); }
            }
            Label(body, "스킬 블록 공방", new Rect(16, 5, 480, 38), 25);
            Button(body, "설정으로", new Rect(1060, 8, 138, 34), () => { SaveStudio(); tab = studioAdmin ? "관리자" : "제작"; Repaint(); });
            Button(body, "블록 검사", new Rect(282, 8, 145, 34), () => { SaveStudio(); var error = StudioFighter.Validate(); studioStatus = error ?? "블록 검사 완료. 테스트장에서 적용해 보세요."; });
            Button(body, "테스트 적용", new Rect(438, 8, 160, 34), () => { SaveStudio(); ApplySandbox(StudioFighter); }, true);
            Button(body, "예제 선택", new Rect(610, 8, 138, 34), () => Choose("예제로 블록 교체", new[] { "주변 구 회전과 발사", "이미지 유도탄", "이미지 직접 만들기", "회복과 보호막", "원형 탄막" }, i => { studioDocument = BlockCatalog.Template(i); ChangedBlocks("예제를 불러왔습니다."); }));
            Button(body, BlockCatalog.EventTitles[studioEvent] + "  ▾", new Rect(762, 8, 286, 34), () => Choose("언제 실행할까요?", SkillScript.Events.Select(e => BlockCatalog.EventTitles[e]).ToArray(), i => { RememberScroll(); studioEvent = SkillScript.Events[i]; workspaceScroll = 1; Repaint(); }));
            for (int i = 0; i < blockCategories.Length; i++)
            {
                string category = blockCategories[i];
                Button(body, category, new Rect(16 + i % 3 * 82, 53 + i / 3 * 38, 76, 32), () => { RememberScroll(); blockCategory = category; Repaint(); }, category == blockCategory);
            }
            TransientUi = true;
            if (blockCategory == "이미지") ImagePalette(); else BlockPalette();
            TransientUi = false;
            var list = studioDocument.Events[studioEvent];
            float height = StackHeight(list, 862) + 62;
            var content = Scroll(body, new Rect(282, 55, 916, 445), Mathf.Max(445, height));
            workspace = content.parent.GetComponent<ScrollRect>();
            TransientUi = true;
            var hat = Box(content, "Event", new Rect(0, 0, 874, 42), new Color32(244, 235, 213, 255));
            Label(hat, "▶  " + BlockCatalog.EventTitles[studioEvent], new Rect(14, 0, 850, 42), 19);
            RenderStack(content, list, null, 12, 52, 862, 0);
            TransientUi = false;
            workspace.verticalNormalizedPosition = workspaceScroll;
            blockStatus = Label(body, studioStatus, new Rect(16, 506, 1182, 48), 15, Muted);
            var trash = Box(body, "Trash", new Rect(16, 464, 242, 36), new Color32(244, 231, 234, 255));
            Label(trash, "블록을 여기에 놓아 삭제", new Rect(8, 0, 226, 36), 14);
            (trash.GetComponent<BlockDropZone>() ?? trash.gameObject.AddComponent<BlockDropZone>()).Drop = data =>
            {
                if (data.Block != null) { studioDocument.FindList(data.Block)?.Remove(data.Block); ChangedBlocks("블록을 삭제했습니다."); }
            };
        }
        void RememberScroll() { if (workspace) workspaceScroll = workspace.verticalNormalizedPosition; }
        void SaveStudio()
        {
            if (studioDocument == null) return;
            StudioFighter.blocks_json = studioDocument.ToJson(); StudioFighter.skill = "script";
            if (!studioAdmin) { PlayerPrefs.SetString("HongBallFight.CreatorDraft.v2", JsonUtility.ToJson(editing)); PlayerPrefs.Save(); }
        }
        void ChangedBlocks(string status = "변경사항을 저장했습니다.")
        {
            RememberScroll(); SaveStudio(); studioStatus = status; Repaint();
        }
        static Color BlockColor(string op)
        {
            if (op == "if" || op == "repeat" || op == "every") return new Color32(244, 229, 201, 255);
            if (op == "set" || op == "add") return new Color32(242, 222, 230, 255);
            var category = SkillApi.Find(op)?.Category;
            return category == "이미지" ? new Color32(234, 227, 245, 255) : category == "오브젝트" || category == "이동" ? new Color32(222, 233, 247, 255) : new Color32(246, 230, 216, 255);
        }
        void BlockPalette()
        {
            var content = Scroll(body, new Rect(16, 135, 242, 318), 4500);
            float y = 0;
            if (blockCategory == "값")
            {
                AddValuePalette(content, ref y, "숫자", () => BlockValue.Number("1"));
                foreach (var key in SkillScript.Properties.Concat(new[] { "last", "var1", "var2", "var3", "var4", "var5", "var6", "var7", "var8" }))
                { string captured = key; AddValuePalette(content, ref y, BlockCatalog.PropertyTitle(key), () => BlockValue.Reference(captured)); }
                foreach (string op in new[] { "+", "-", "*", "/", "%" })
                { string operation = op; AddValuePalette(content, ref y, "계산 " + op, () => new BlockValue { Kind = "math", Text = operation, Inputs = new List<BlockValue> { BlockValue.Number("1"), BlockValue.Number("2") } }); }
                foreach (string key in BlockCatalog.ValueFunctions.Where(k => k != "Every" && k != "Exists"))
                {
                    string captured = key;
                    AddValuePalette(content, ref y, BlockCatalog.Title(key), () => new BlockValue { Kind = "call", Text = captured, Inputs = Enumerable.Range(0, SkillApi.Find(captured).Arguments).Select(i => captured == "GetX" || captured == "GetY" ? BlockValue.Reference("last") : BlockValue.Number(i == 0 ? "0" : "1")).ToList() });
                }
            }
            else
            {
                IEnumerable<string> ops;
                if (blockCategory == "제어") ops = new[] { "if", "repeat", "every", "set", "add" };
                else ops = SkillApi.All.Where(a => !BlockCatalog.ValueFunctions.Contains(a.Name) &&
                    (blockCategory == "전투" ? a.Category == "전투" || a.Category == "발사" || a.Category == "이동" : blockCategory == "생성" ? a.Category == "생성" || a.Category == "오브젝트" : a.Category == "이미지" || a.Category == "디버그")).Select(a => a.Name);
                foreach (string key in ops)
                {
                    string captured = key;
                    var card = Box(content, key, new Rect(0, y, 226, 50), BlockColor(key));
                    Label(card, BlockCatalog.Title(key), new Rect(10, 0, 206, 50), 15);
                    AddDrag(card, () => new BlockDragData { NewBlock = () => BlockCatalog.Create(captured), Title = BlockCatalog.Title(captured) }); y += 60;
                }
            }
            content.sizeDelta = new Vector2(content.sizeDelta.x, Mathf.Max(318, y));
        }
        void AddValuePalette(RectTransform content, ref float y, string title, Func<BlockValue> value)
        {
            var card = Box(content, title, new Rect(0, y, 226, 38), new Color32(221, 238, 228, 255));
            Label(card, title, new Rect(10, 0, 206, 38), 15);
            AddDrag(card, () => new BlockDragData { NewValue = value, Title = title }); y += 46;
        }
        void AddDrag(RectTransform target, Func<BlockDragData> data)
        {
            target.GetComponent<Image>().raycastTarget = true;
            var drag = target.gameObject.AddComponent<BlockDrag>(); drag.App = this; drag.Overlay = root; drag.Data = data;
        }
        float CardHeight(SkillBlock block, float width)
        {
            int columns = width > 650 ? 3 : width > 450 ? 2 : 1;
            float height = 42 + Mathf.Ceil(block.Values.Count / (float)columns) * 58 + 10;
            if (block.Op == "if" || block.Op == "repeat" || block.Op == "every")
            {
                height += StackHeight(block.Children, width - 28) + 12;
                if (block.Op == "if") height += 27 + StackHeight(block.Else, width - 28);
            }
            return height;
        }
        float StackHeight(List<SkillBlock> list, float width) => 24 + list.Sum(b => CardHeight(b, width) + 24);
        float RenderStack(RectTransform parent, List<SkillBlock> list, SkillBlock owner, float x, float y, float width, int depth)
        {
            for (int i = 0; i <= list.Count; i++)
            {
                int index = i;
                var slot = Box(parent, "Drop block here", new Rect(x, y, width, 20), new Color32(237, 241, 238, 255));
                Label(slot, list.Count == 0 ? "+ 블록을 여기에 놓으세요" : "＋", new Rect(8, 0, width - 16, 20), 12, Accent);
                slot.gameObject.AddComponent<BlockDropZone>().Drop = data =>
                {
                    if (data.NewValue != null) { studioStatus = "초록색 값 블록은 숫자 칸에 놓으세요."; return; }
                    var block = data.Block ?? data.NewBlock?.Invoke();
                    if (block == null) return;
                    if (depth >= 10 && (block.Op == "if" || block.Op == "repeat" || block.Op == "every")) { studioStatus = "중첩 한도에 도달했습니다."; return; }
                    if (studioDocument.Move(block, list, index, owner)) ChangedBlocks();
                    else studioStatus = "자기 안으로 이동하거나 블록·중첩 한도를 넘길 수 없습니다.";
                };
                y += 24;
                if (i == list.Count) break;
                var block = list[i]; float height = CardHeight(block, width);
                var card = Box(parent, block.Op, new Rect(x, y, width, height), Color.Lerp(Color.white, BlockColor(block.Op), .6f));
                var handle = Box(card, "Block handle", new Rect(0, 0, width, 36), BlockColor(block.Op));
                Label(handle, "≡  " + BlockCatalog.Title(block.Op), new Rect(10, 0, width - 110, 36), 16);
                AddDrag(handle, () => new BlockDragData { Block = block, Title = BlockCatalog.Title(block.Op) });
                Button(card, "×", new Rect(width - 36, 3, 30, 30), () => { list.Remove(block); ChangedBlocks(); });
                if (block.Op == "if") Button(card, block.Option, new Rect(width - 96, 3, 52, 30), () => Choose("조건", new[] { ">", "<", ">=", "<=", "==", "!=" }, choice => { block.Option = new[] { ">", "<", ">=", "<=", "==", "!=" }[choice]; ChangedBlocks(); }));
                if (block.Op == "set" || block.Op == "add") Button(card, BlockCatalog.PropertyTitle(block.Variable), new Rect(width - 150, 3, 108, 30), () => Choose("변수", Enumerable.Range(1, 8).Select(n => "변수 " + n).ToArray(), n => { block.Variable = "var" + (n + 1); ChangedBlocks(); }));
                int columns = width > 650 ? 3 : width > 450 ? 2 : 1; float slotWidth = (width - 20) / columns;
                string[] parameters = SkillApi.Find(block.Op)?.Signature.Substring(block.Op.Length + 1).TrimEnd(')').Split(',').Select(s => s.Trim()).ToArray();
                for (int p = 0; p < block.Values.Count; p++)
                {
                    int parameter = p;
                    string key = parameters != null && p < parameters.Length ? parameters[p] : block.Op == "if" ? (p == 0 ? "비교할 값" : "기준 값") : block.Op == "repeat" ? "반복 횟수" : block.Op == "every" ? "주기(초)" : "값";
                    string label = ParamTitles.TryGetValue(key, out var translated) ? translated : key;
                    var rect = new Rect(10 + p % columns * slotWidth, 40 + p / columns * 58, slotWidth - 8, 52);
                    ValueSlot(card, rect, label, block.Values[p], value => { block.Values[parameter] = value; ChangedBlocks(); }, block.Values[p].Kind == "text");
                }
                float childY = 42 + Mathf.Ceil(block.Values.Count / (float)columns) * 58 + 8;
                if (block.Op == "if" || block.Op == "repeat" || block.Op == "every")
                {
                    childY = RenderStack(card, block.Children, block, 18, childY, width - 28, depth + 1);
                    if (block.Op == "if") { Label(card, "아니면", new Rect(18, childY, width - 36, 27), 15); RenderStack(card, block.Else, block, 18, childY + 27, width - 28, depth + 1); }
                }
                y += height;
            }
            return y;
        }
        void ValueSlot(RectTransform parent, Rect rect, string title, BlockValue value, Action<BlockValue> replace, bool text)
        {
            Label(parent, title, new Rect(rect.x, rect.y, rect.width, 18), 12, Muted);
            var box = Box(parent, "Value socket", new Rect(rect.x, rect.y + 18, rect.width, 32), Ink);
            if (value.Kind == "number" || value.Kind == "text")
            {
                box.GetComponent<Image>().raycastTarget = true;
                var field = box.gameObject.AddComponent<InputField>(); field.targetGraphic = box.GetComponent<Image>();
                var label = Label(box, "", new Rect(8, 0, rect.width - (text ? 16 : 42), 32), 14); field.textComponent = label; field.characterLimit = text ? 64 : 16; field.text = value.Text;
                field.onEndEdit.AddListener(updated => { value.Text = updated; SaveStudio(); });
            }
            else Button(box, value.Display(), new Rect(0, 0, rect.width - 32, 32), () => EditValue(value, replace));
            if (!text)
            {
                Button(box, "⋯", new Rect(rect.width - 32, 0, 32, 32), () => EditValue(value, replace));
                box.gameObject.AddComponent<BlockDropZone>().Drop = data => { if (data.NewValue != null) replace(data.NewValue()); };
            }
            else if (title == "이미지") Button(box, "▾", new Rect(rect.width - 30, 0, 30, 32), () => Choose("이미지 선택", ImageNames(), index => replace(BlockValue.TextValue(ImageNames()[index]))));
        }
        void EditValue(BlockValue value, Action<BlockValue> replace)
        {
            var panel = Modal("값 블록 설정", 700, 520);
            var input = Input(panel, "숫자", value.Kind == "number" ? value.Text : "1", new Rect(24, 55, 320, 70));
            Button(panel, "숫자로 설정", new Rect(368, 83, 300, 40), () => { ClosePopup(); replace(BlockValue.Number(input.text)); });
            Button(panel, "상태 / 변수 선택", new Rect(24, 148, 644, 40), () =>
            {
                var refs = SkillScript.Properties.Concat(new[] { "last", "var1", "var2", "var3", "var4", "var5", "var6", "var7", "var8" }).ToArray();
                Choose("어떤 값을 사용할까요?", refs.Select(BlockCatalog.PropertyTitle).ToArray(), i => replace(BlockValue.Reference(refs[i])));
            });
            foreach (var item in new[] { "+", "-", "*", "/", "%" }.Select((op, index) => (op, index)))
                Button(panel, item.op, new Rect(24 + item.index * 130, 210, 122, 38), () =>
                {
                    var next = new BlockValue { Kind = "math", Text = item.op, Inputs = new List<BlockValue> { value, BlockValue.Number("1") } };
                    ClosePopup(); replace(next);
                });
            if (value.Inputs.Count > 0)
            {
                Label(panel, "계산 블록의 안쪽 값", new Rect(24, 268, 644, 30), 18);
                for (int i = 0; i < value.Inputs.Count; i++)
                {
                    int captured = i; var inner = value.Inputs[i];
                    Button(panel, "값 " + (i + 1) + ": " + inner.Display(), new Rect(24, 310 + i * 44, 644, 38), () => EditValue(inner, next => { value.Inputs[captured] = next; ClosePopup(); replace(value); }));
                }
            }
        }
        RectTransform Modal(string title, float width, float height)
        {
            ClosePopup();
            popup = Box(root, "Dialog overlay", new Rect(0, 0, 1280, root.sizeDelta.y), new Color(0, 0, 0, .78f)); popup.GetComponent<Image>().raycastTarget = true;
            var panel = Box(popup, title, new Rect((1280 - width) / 2, (root.sizeDelta.y - height) / 2, width, height), Panel);
            Label(panel, title, new Rect(24, 12, width - 80, 34), 23);
            Button(panel, "×", new Rect(width - 50, 12, 34, 34), ClosePopup); return panel;
        }
        void ClosePopup() { if (popup) { popup.gameObject.SetActive(false); Destroy(popup.gameObject); } popup = null; }
        void Choose(string title, string[] values, Action<int> select)
        {
            var panel = Modal(title, 680, 520);
            var content = Scroll(panel, new Rect(24, 64, 632, 434), Math.Max(434, values.Length * 48));
            for (int i = 0; i < values.Length; i++)
            {
                int index = i; Button(content, values[i], new Rect(0, i * 48, 608, 40), () => { ClosePopup(); select(index); });
            }
        }
        string[] ImageNames() => new[] { "orb", "star", "missile", "hero" }.Concat(FighterImages.Read(StudioFighter.assets_json).Select(i => i.Key)).Distinct().ToArray();
        void ImagePalette()
        {
            var key = Input(body, "이미지 이름(영문)", imageKey, new Rect(16, 135, 242, 70)); key.onEndEdit.AddListener(value => imageKey = value);
            Button(body, "이미지 불러오기", new Rect(16, 212, 242, 34), SelectImage, true);
            Button(body, "도형 이미지 만들기", new Rect(16, 255, 242, 34), CreateImageDialog);
            Button(body, "캐릭터 이미지 선택", new Rect(16, 298, 242, 34), () => Choose("캐릭터 이미지", ImageNames(), index => { StudioFighter.portrait = ImageNames()[index]; ChangedBlocks("캐릭터 이미지를 지정했습니다. 테스트 적용으로 확인하세요."); }));
            Button(body, "등록 이미지 관리", new Rect(16, 341, 242, 34), ManageImages);
            Label(body, "PNG · JPG · WEBP\n가져올 때 최대 256px로 축소\n기본 이미지: orb / star / missile\n이미지 이름을 발사 블록에서 선택", new Rect(16, 382, 242, 74), 12, Muted);
        }
        void CreateImageDialog()
        {
            var panel = Modal("도형으로 이미지 만들기", 600, 360);
            var key = Input(panel, "이미지 이름", imageKey, new Rect(24, 65, 260, 70));
            var color = Input(panel, "색상 HEX", imageColor, new Rect(310, 65, 260, 70));
            foreach (var shape in new[] { "원", "별", "사각형" }.Select((title, index) => (title, index)))
                Button(panel, shape.title + " 만들기", new Rect(24 + shape.index * 188, 174, 176, 46), () =>
                {
                    if (!ColorUtility.TryParseHtmlString(color.text, out var tint)) { studioStatus = "올바른 HEX 색상을 입력하세요."; return; }
                    var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false); var colors = new Color[4096];
                    for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                    {
                        float dx = x - 31.5f, dy = y - 31.5f, length = Mathf.Sqrt(dx * dx + dy * dy);
                        bool visible = shape.index == 2 || length < (shape.index == 0 ? 30 : 22 + 8 * Mathf.Cos(Mathf.Atan2(dy, dx) * 5));
                        colors[y * 64 + x] = visible ? tint : Color.clear;
                    }
                    texture.SetPixels(colors); texture.Apply(); string data = Convert.ToBase64String(texture.EncodeToPNG()); Destroy(texture);
                    if (StoreImage(key.text, data)) { imageKey = key.text; imageColor = color.text; ClosePopup(); ChangedBlocks("이미지를 만들었습니다."); }
                }, true);
            Label(panel, "만든 이미지는 블록의 ‘이미지’ 칸에서 골라 사용할 수 있습니다.", new Rect(24, 250, 552, 72), 17, Muted);
        }
        void ManageImages()
        {
            imageBank?.Dispose(); imageBank = new SpriteBank(StudioFighter);
            var panel = Modal("이미지 보관함", 700, 520);
            var names = ImageNames(); var content = Scroll(panel, new Rect(24, 65, 652, 430), Math.Max(430, names.Length * 76));
            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i]; var icon = Box(content, name, new Rect(0, i * 76, 60, 60), Color.white); icon.GetComponent<Image>().sprite = imageBank.Get(name); icon.GetComponent<Image>().preserveAspect = true;
                Label(content, name, new Rect(80, i * 76, 330, 60), 20);
                if (FighterImages.Read(StudioFighter.assets_json).Any(image => image.Key == name)) Button(content, "삭제", new Rect(488, i * 76 + 10, 130, 38), () =>
                {
                    var images = FighterImages.Read(StudioFighter.assets_json); images.RemoveAll(image => image.Key == name);
                    StudioFighter.assets_json = FighterImages.Write(images); SaveStudio(); ManageImages();
                });
            }
        }
        bool StoreImage(string key, string data)
        {
            try
            {
                var images = FighterImages.Read(StudioFighter.assets_json); images.RemoveAll(image => image.Key == key); images.Add(new FighterImage { Key = key, Data = data });
                string json = FighterImages.Write(images); FighterImages.Validate(json); StudioFighter.assets_json = json; SaveStudio(); return true;
            }
            catch (Exception error) { studioStatus = error.Message; return false; }
        }
        [DllImport("__Internal")] static extern void HBF_SelectImage(string receiver);
        void SelectImage()
        {
            if (!FighterImages.ValidName(imageKey)) { studioStatus = "이미지 이름은 영문으로 시작하는 1~32자여야 합니다."; return; }
            pendingImageKey = imageKey;
#if UNITY_WEBGL && !UNITY_EDITOR
            HBF_SelectImage(gameObject.name);
#elif UNITY_EDITOR
            string path = UnityEditor.EditorUtility.OpenFilePanel("이미지 선택", "", "png");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                if (bytes.Length > 4194304 || !FighterImages.PngSize(bytes, out int width, out int height) || width > 1024 || height > 1024) throw new Exception("에디터에서는 최대 1024px, 4MB PNG를 선택하세요.");
                var original = new Texture2D(2, 2); if (!original.LoadImage(bytes)) { Destroy(original); throw new Exception("이미지를 읽을 수 없습니다."); }
                float scale = Mathf.Min(1, 256f / Mathf.Max(original.width, original.height));
                var small = new Texture2D(Mathf.Max(1, Mathf.RoundToInt(original.width * scale)), Mathf.Max(1, Mathf.RoundToInt(original.height * scale)), TextureFormat.RGBA32, false);
                for (int y = 0; y < small.height; y++) for (int x = 0; x < small.width; x++) small.SetPixel(x, y, original.GetPixelBilinear((x + .5f) / small.width, (y + .5f) / small.height));
                small.Apply(); string encoded = Convert.ToBase64String(small.EncodeToPNG()); Destroy(original); Destroy(small);
                ReceiveImage(DataJson.Write(new Dictionary<string, object> { ["data"] = encoded }));
            }
            catch (Exception error) { studioStatus = error.Message; }
#else
            studioStatus = "이미지 불러오기는 WebGL과 Unity 에디터에서 지원합니다.";
#endif
        }
        public void ReceiveImage(string json)
        {
            try
            {
                var message = DataJson.Map(DataJson.Parse(json)); string error = DataJson.Text(message, "error");
                if (error.Length > 0) { studioStatus = error; return; }
                if (tab != "코드" || string.IsNullOrEmpty(pendingImageKey)) return;
                if (StoreImage(pendingImageKey, DataJson.Text(message, "data"))) ChangedBlocks("이미지를 가져왔습니다. 발사 블록에서 선택하세요.");
            }
            catch (Exception error) { studioStatus = error.Message; }
            pendingImageKey = null;
        }
    }
}
