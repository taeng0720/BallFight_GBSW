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

        [Serializable] class ApiError { public string message = null; public string msg = null; public string error_description = null; }
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
            if (Busy) { SetMessage("서버와 통신 중입니다. 잠시 후 다시 시도해 주세요."); done(false); return; }
            StartCoroutine(SubmitRoutine(new IdeaPayload { name = name, author = author, description = description }, done));
        }
        IEnumerator SubmitRoutine(IdeaPayload payload, Action<bool> done)
        {
            Busy = true;
            bool success = false;
            yield return Send("POST", "/rest/v1/ideas", JsonUtility.ToJson(payload), json =>
            {
                var created = JsonUtility.FromJson<IdeaList>("{\"items\":" + json + "}").items;
                onlineIdeas.InsertRange(0, created);
                Message = "아이디어를 보냈습니다. 고마워요!";
                success = true;
            }, "return=representation");
            Busy = false;
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
            var detail = request.error;
            try
            {
                var error = JsonUtility.FromJson<ApiError>(request.downloadHandler.text);
                detail = error.message ?? error.error_description ?? error.msg ?? detail;
            }
            catch { }
            Message = "서버 오류: " + detail;
        }
        public void SetMessage(string value) { Message = value; }
    }
}
