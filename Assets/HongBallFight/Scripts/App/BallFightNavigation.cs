using UnityEngine;
using UnityEngine.SceneManagement;

namespace HongBallFight
{
    public partial class BallFightApp
    {
        void Navigate(string destination)
        {
            CaptureDraft(); ClosePopup();
            if (tab == "제작") { PlayerPrefs.SetString("HongBallFight.CreatorDraft.v2", JsonUtility.ToJson(editing)); PlayerPrefs.Save(); }
            if (destination == "제작" && !IsCreator) { nextTab = "제작"; SceneManager.LoadScene("CharacterCreator"); return; }
            if (destination != "제작" && (IsCreator || SceneManager.GetActiveScene().name == "Arena")) { nextTab = destination; SceneManager.LoadScene("MainMenu"); return; }
            tab = destination; reviewing = null; Repaint();
        }
        void ApplySandbox(FighterData fighter)
        {
            string error = fighter.Validate();
            if (error == null && sandbox) error = sandbox.Load(fighter);
            studioStatus = error ?? "테스트장에 적용했습니다. 위쪽 스킬 버튼으로 시험하세요.";
            repository.SetMessage(studioStatus);
        }
        void MainMenu()
        {
            var title = Label(body, "볼 파이트", new Rect(258, 95, 700, 70), 44); title.alignment = UnityEngine.TextAnchor.MiddleCenter;
            var caption = Label(body, "1번과 2번의 대결", new Rect(258, 176, 700, 36), 18, Muted); caption.alignment = UnityEngine.TextAnchor.MiddleCenter;
            Button(body, "시작", new Rect(408, 268, 400, 56), () => Navigate("대전"), true);
            Button(body, "캐릭터 만들기", new Rect(408, 342, 400, 50), () => Navigate("제작"));
        }
    }
}
