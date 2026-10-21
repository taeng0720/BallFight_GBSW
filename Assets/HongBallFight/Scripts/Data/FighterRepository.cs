using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace HongBallFight
{
    public class FighterRepository : MonoBehaviour
    {
        public List<FighterData> Fighters = new List<FighterData>();
        public List<FighterData> Requests = new List<FighterData>();
        public bool Online => config.Ready;
        public bool LoggedIn => !string.IsNullOrEmpty(token);
        public bool IsAdmin { get; private set; }
        public bool Busy { get; private set; }
        public string Message { get; private set; } = "";
        public event Action Changed;
        BackendConfig config = new BackendConfig();
        string token;
        const string LocalKey = "HongBallFight.Local.v1";

        [Serializable] class Credentials { public string email; public string password; }
        [Serializable] class Session { public string access_token = null; }
        [Serializable] class ApiError { public string message = null; public string msg = null; public string error_description = null; }
        [Serializable] class Proposal
        {
            public string name, skill, color, blocks_json, assets_json, portrait;
            public int hp, attack, skill_power;
            public float speed, cooldown;
            public Proposal(FighterData f)
            {
                name = f.name.Trim(); hp = f.hp; attack = f.attack; speed = f.speed;
                skill = f.skill; skill_power = f.skill_power; cooldown = f.cooldown; color = f.color;
                blocks_json = f.blocks_json ?? ""; assets_json = f.assets_json ?? "{\"items\":[]}"; portrait = f.portrait ?? "";
            }
        }
        [Serializable] class BalanceProposal : Proposal
        {
            public string target_id;
            public int base_revision;
            public BalanceProposal(FighterData f) : base(f) { target_id = f.target_id; base_revision = f.base_revision; }
        }
        [Serializable] class Review
        {
            public string request_id, decision, note;
            public Proposal payload;
        }

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            var asset = Resources.Load<TextAsset>("backend");
            config = asset ? JsonUtility.FromJson<BackendConfig>(asset.text) : new BackendConfig();
            if (!Online)
            {
                Fighters = FighterData.Samples();
                try { Requests = JsonUtility.FromJson<FighterList>(PlayerPrefs.GetString(LocalKey, "{\"items\":[]}")).items; }
                catch { Requests = new List<FighterData>(); }
                Message = "로컬 초안 · 서버 연결 전에는 등록 요청만 기기에 저장됩니다.";
            }
        }

        public void Refresh() { if (!Busy) StartCoroutine(RefreshRoutine()); }
        IEnumerator RefreshRoutine()
        {
            if (!Online) { Changed?.Invoke(); yield break; }
            Busy = true;
            yield return Fetch();
            Busy = false;
            Changed?.Invoke();
        }
        IEnumerator Fetch()
        {
            yield return Send("GET", "/rest/v1/fighters?select=*&order=name.asc", null,
                json =>
                {
                    var all = JsonUtility.FromJson<FighterList>("{\"items\":" + json + "}").items;
                    Fighters = all.FindAll(f => !string.IsNullOrEmpty(f.id) && f.Validate() == null);
                    if (Fighters.Count != all.Count) Message = "유효하지 않은 공개 캐릭터 데이터가 있어 목록에서 제외했습니다.";
                });
            if (LoggedIn)
                yield return Send("GET", "/rest/v1/fighter_requests?select=*&order=created_at.desc&limit=100", null,
                    json => Requests = JsonUtility.FromJson<FighterList>("{\"items\":" + json + "}").items);
        }

        public void Authenticate(string email, string password, bool register)
        {
            if (!Busy) StartCoroutine(AuthRoutine(email.Trim(), password, register));
        }
        IEnumerator AuthRoutine(string email, string password, bool register)
        {
            if (!Online) { SetMessage("로그인하려면 Resources/backend.json에 Supabase 연결 정보를 입력하세요."); yield break; }
            if (string.IsNullOrEmpty(email) || password.Length < 8) { SetMessage("이메일과 8자 이상의 비밀번호를 입력하세요."); yield break; }
            Busy = true;
            yield return Send("POST", register ? "/auth/v1/signup" : "/auth/v1/token?grant_type=password",
                JsonUtility.ToJson(new Credentials { email = email, password = password }), json =>
                {
                    token = JsonUtility.FromJson<Session>(json).access_token;
                    Message = string.IsNullOrEmpty(token) ? "가입 요청 완료. 이메일 인증 후 로그인하세요." : "로그인되었습니다.";
                });
            if (LoggedIn)
            {
                yield return Send("POST", "/rest/v1/rpc/is_admin", "{}", json => IsAdmin = json.Trim() == "true");
                yield return Fetch();
            }
            Busy = false;
            Changed?.Invoke();
        }

        public void Logout()
        {
            if (Busy) return;
            token = null; IsAdmin = false; Requests.Clear();
            SetMessage("로그아웃되었습니다."); Changed?.Invoke();
        }

        public void Submit(FighterData fighter)
        {
            if (Busy) return;
            var error = fighter.Validate();
            if (error != null) { SetMessage(error); return; }
            if (!Online)
            {
                var request = fighter.Copy(); request.id = Guid.NewGuid().ToString("N"); request.status = "pending";
                Requests.Insert(0, request);
                PlayerPrefs.SetString(LocalKey, JsonUtility.ToJson(new FighterList { items = Requests })); PlayerPrefs.Save();
                SetMessage("로컬 등록 요청이 저장되었습니다. 승인 전에는 대전 목록에 추가되지 않습니다.");
                return;
            }
            if (!LoggedIn) { SetMessage("계정 탭에서 로그인한 뒤 등록을 요청하세요."); return; }
            StartCoroutine(SubmitRoutine(fighter.Copy()));
        }
        IEnumerator SubmitRoutine(FighterData fighter)
        {
            Busy = true;
            var body = string.IsNullOrEmpty(fighter.target_id) ? JsonUtility.ToJson(new Proposal(fighter)) : JsonUtility.ToJson(new BalanceProposal(fighter));
            yield return Send("POST", "/rest/v1/fighter_requests", body,
                _ => Message = "등록 요청 완료. 관리자 승인을 기다려 주세요.");
            yield return Fetch();
            Busy = false; Changed?.Invoke();
        }

        public void ReviewRequest(FighterData request, FighterData edited, bool approve, string note)
        {
            if (Busy || !Online || !IsAdmin) return;
            var error = edited.Validate();
            if (approve && error != null) { SetMessage(error); return; }
            StartCoroutine(ReviewRoutine(new Review
            {
                request_id = request.id, decision = approve ? "approved" : "rejected",
                note = note, payload = new Proposal(edited)
            }));
        }
        IEnumerator ReviewRoutine(Review review)
        {
            Busy = true;
            yield return Send("POST", "/rest/v1/rpc/review_fighter", JsonUtility.ToJson(review), _ => Message = "검토 결과를 저장했습니다.");
            yield return Fetch();
            Busy = false; Changed?.Invoke();
        }

        IEnumerator Send(string method, string path, string body, Action<string> success)
        {
            using var request = new UnityWebRequest(config.url.TrimEnd('/') + path, method);
            request.downloadHandler = new DownloadHandlerBuffer();
            if (body != null) request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            request.SetRequestHeader("apikey", config.publishableKey);
            if (LoggedIn) request.SetRequestHeader("Authorization", "Bearer " + token);
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 20;
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                try { success(request.downloadHandler.text); }
                catch (Exception error) { Message = "서버 응답을 읽을 수 없습니다: " + error.Message; }
            }
            else
            {
                var detail = request.error;
                try
                {
                    var error = JsonUtility.FromJson<ApiError>(request.downloadHandler.text);
                    detail = error.error_description ?? error.message ?? error.msg ?? detail;
                }
                catch { }
                if (request.responseCode == 401) { token = null; IsAdmin = false; Requests.Clear(); }
                Message = "요청 실패: " + detail;
            }
        }
        public void SetMessage(string value) { Message = value; }
    }
}
