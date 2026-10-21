using UnityEditor;
using UnityEngine;

namespace HongBallFight.Editor
{
    [CustomEditor(typeof(SceneUiBindings))]
    public sealed class SceneUiBindingsInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var view = (SceneUiBindings)target;
            EditorGUILayout.Space(); EditorGUILayout.LabelField("화면 미리보기", EditorStyles.boldLabel);
            foreach (var page in view.Pages)
            {
                if (!page || !GUILayout.Button(page.name)) continue;
                foreach (var sibling in view.Pages)
                {
                    Undo.RecordObject(sibling.gameObject, "Preview screen"); sibling.gameObject.SetActive(sibling == page);
                    EditorUtility.SetDirty(sibling.gameObject);
                }
                Selection.activeGameObject = page.gameObject;
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
            }
            EditorGUILayout.HelpBox("RectTransform, Image, Text, Button을 직접 편집하세요. SceneUiSlot의 Key는 기능 연결에 사용합니다. RuntimeText가 켜진 글자는 실행 중 데이터로 갱신됩니다. 직접 추가한 장식과 UI도 유지됩니다.", MessageType.Info);
        }
    }
}
