using UnityEngine.SceneManagement;

namespace HongBallFight
{
    public partial class BallFightApp
    {
        void Navigate(string destination)
        {
            CaptureDraft(); ClosePopup();
            if (tab == "제작" && !reviewMode) SaveDraft();
            if (destination == "제작")
            {
                if (!IsCreator) { SceneManager.LoadScene("CharacterCreator"); return; }
                tab = "제작"; Repaint(); return;
            }
            if (IsCreator || SceneName == "Arena") { pendingReview = null; nextTab = destination; SceneManager.LoadScene("MainMenu"); return; }
            tab = destination; Repaint();
        }
        void ApplySandbox(FighterData fighter)
        {
            string error = fighter.Validate();
            if (error == null && sandbox) error = sandbox.Load(fighter);
            studioStatus = error ?? "테스트장에 적용했습니다. 위쪽 스킬 버튼으로 시험하세요.";
            repository.SetMessage(studioStatus);
        }
    }
}
