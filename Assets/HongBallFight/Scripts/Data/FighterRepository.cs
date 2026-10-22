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
        public bool Online => config.Ready;
        public bool Busy { get; private set; }
        public string Message { get; private set; } = "";
        public IReadOnlyList<IdeaRecord> Ideas => Online ? onlineIdeas : GameRecords.Ideas;
        public event Action Changed;
        BackendConfig config = new BackendConfig();
        readonly List<IdeaRecord> onlineIdeas = new List<IdeaRecord>();

        [Serializable] class ApiError { public string code = null; public string message = null; public string msg = null; public string error_description = null; }
        readonly List<IdeaRecord> recentlySubmitted = new List<IdeaRecord>();
        bool submitting;
        [Serializable] class IdeaList { public List<IdeaRecord> items = new List<IdeaRecord>(); }
        [Serializable] class IdeaPayload { public string name, author, description; }

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            var asset = Resources.Load<TextAsset>("backend");
            config = asset ? JsonUtility.FromJson<BackendConfig>(asset.text) : new BackendConfig();
            Fighters = FighterData.Samples();
        }

        public void Refresh() { if (!Busy) StartCoroutine(RefreshRoutine()); }
        IEnumerator RefreshRoutine()
        {
            if (!Online) { Changed?.Invoke(); yield break; }
            Busy = true;
            yield return Send("GET", "/rest/v1/fighters?select=*&order=name.asc", null, json =>
            {
                var all = JsonUtility.FromJson<FighterList>("{\"items\":" + json + "}").items;
                var valid = all.FindAll(f => !string.IsNullOrEmpty(f.id) && f.Validate() == null);
                if (valid.Count > 0) Fighters = valid;
            });
            yield return Send("GET", "/rest/v1/ideas?select=*&order=created_at.desc&limit=200", null, json =>
            {
                onlineIdeas.Clear();
                onlineIdeas.AddRange(JsonUtility.FromJson<IdeaList>("{\"items\":" + json + "}").items);
                for (int i = recentlySubmitted.Count - 1; i >= 0; i--)
                {
                    var idea = recentlySubmitted[i];
                    if (onlineIdeas.Exists(item => item.id == idea.id)) recentlySubmitted.RemoveAt(i);
                    else onlineIdeas.Insert(0, idea);
                }
            });
            Busy = false;
            Changed?.Invoke();
        }

        public void SubmitIdea(string name, string author, string description, Action<bool> done)
        {
            var error = GameRecords.ValidateIdea(ref name, ref author, ref description);
            if (error != null) { SetMessage(error); done(false); return; }
            if (!Online)
            {
                GameRecords.AddIdea(name, author, description);
                SetMessage("아이디어를 이 기기에 저장했습니다.");
                done(true); Changed?.Invoke(); return;
            }
            if (submitting) { done(false); return; }
            StartCoroutine(SubmitRoutine(new IdeaPayload { name = name, author = author, description = description }, done));
        }
        IEnumerator SubmitRoutine(IdeaPayload payload, Action<bool> done)
        {
            submitting = true;
            bool success = false;
            yield return Send("POST", "/rest/v1/ideas", JsonUtility.ToJson(payload), json =>
            {
                var created = JsonUtility.FromJson<IdeaList>("{\"items\":" + json + "}").items;
                onlineIdeas.InsertRange(0, created);
                recentlySubmitted.AddRange(created);
                Message = "아이디어를 보냈습니다. 고마워요!";
                success = true;
            }, "return=representation");
            submitting = false;
            done(success);
            Changed?.Invoke();
        }

        IEnumerator Send(string method, string path, string body, Action<string> success, string prefer = null)
        {
            using var request = new UnityWebRequest(config.url.TrimEnd('/') + path, method);
            request.downloadHandler = new DownloadHandlerBuffer();
            if (body != null) request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            request.SetRequestHeader("apikey", config.publishableKey);
            if (config.publishableKey.StartsWith("eyJ")) request.SetRequestHeader("Authorization", "Bearer " + config.publishableKey);
            request.SetRequestHeader("Content-Type", "application/json");
            if (prefer != null) request.SetRequestHeader("Prefer", prefer);
            request.timeout = 15;
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                try { success(request.downloadHandler.text); }
                catch (Exception error) { Message = "서버 응답을 읽을 수 없습니다: " + error.Message; }
                yield break;
            }
            if (request.result == UnityWebRequest.Result.ConnectionError) { Message = "서버에 연결할 수 없어요. 인터넷 연결을 확인해 주세요."; yield break; }
            var detail = request.error; string code = null;
            try
            {
                var error = JsonUtility.FromJson<ApiError>(request.downloadHandler.text);
                code = error.code;
                detail = error.message ?? error.error_description ?? error.msg ?? detail;
            }
            catch { }
            if (code == "PGRST205" || code == "42P01") Message = "아이디어 서버가 아직 준비되지 않았어요. 잠시 후 다시 시도해 주세요.";
            else if (code == "P0001") Message = detail;
            else if (code == "23514") Message = "입력한 내용이 조건에 맞지 않아요. 이름 1~24자, 설명 5~300자로 적어 주세요.";
            else Message = "서버 오류가 발생했어요. (" + detail + ")";
        }
        public void SetMessage(string value) { Message = value; }
    }
}
