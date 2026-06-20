using UnityEditor;
using UnityEngine;

namespace Sebanne.PassiveFilter.Editor
{
    [CustomEditor(typeof(Sebanne.PassiveFilter.PassiveFilterSettings))]
    public sealed class PassiveFilterSettingsEditor : UnityEditor.Editor
    {
        private SerializedProperty _enable;
        private SerializedProperty _scope;
        private SerializedProperty _baseHandling;
        private SerializedProperty _exclusions;
        private SerializedProperty _verbose;

        private void OnEnable()
        {
            _enable = serializedObject.FindProperty("enable");
            _scope = serializedObject.FindProperty("scope");
            _baseHandling = serializedObject.FindProperty("baseToggleHandling");
            _exclusions = serializedObject.FindProperty("exclusions");
            _verbose = serializedObject.FindProperty("verboseLogging");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "ビルド時に、アバターのトグルギミックの初期状態を「非表示」へ自動補正します。\n" +
                "他人視点で勝手に出る出っぱなしを防ぎます。Saved（保存表示）は尊重します。",
                MessageType.Info);

            EditorGUILayout.PropertyField(_enable, new GUIContent("有効"));

            EditorGUILayout.PropertyField(_scope, new GUIContent("対象範囲"));
            EditorGUILayout.PropertyField(_baseHandling, new GUIContent("補正する対象"));

            var hint = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            EditorGUILayout.LabelField(
                "「対象範囲」はどのトグルを見るか、「補正する対象」は元アバターのトグルまで触るか。別の設定です。" +
                "『全部』は元アバターに最初から入っているトグル（衣装表示など）も初期 OFF にします。基本は『あとから足したギミックだけ』を推奨。",
                hint);

            // 危険組合せ（すべてのトグル × 全部）のときだけ裸化注意を出す。
            var scope = (Sebanne.PassiveFilter.PassiveFilterSettings.TargetScope)_scope.enumValueIndex;
            var handling = (Sebanne.PassiveFilter.PassiveFilterSettings.BaseToggleHandling)_baseHandling.enumValueIndex;
            if (scope == Sebanne.PassiveFilter.PassiveFilterSettings.TargetScope.AllToggles
                && handling == Sebanne.PassiveFilter.PassiveFilterSettings.BaseToggleHandling.IncludeBase)
            {
                EditorGUILayout.HelpBox(
                    "元アバターのトグルも対象になります。裸化（衣装が消える）に注意してください。",
                    MessageType.Warning);
            }

            EditorGUILayout.PropertyField(
                _exclusions,
                new GUIContent("除外リスト（指定オブジェクト以下は対象外）"),
                true);

            EditorGUILayout.PropertyField(
                _verbose,
                new GUIContent(
                    "詳細ログを Unity コンソールに出力",
                    "ベイク時の検出・補正の詳細を Unity コンソールに出します。補正できなかった通知は常に表示されます。"));

            serializedObject.ApplyModifiedProperties();
        }
    }
}
