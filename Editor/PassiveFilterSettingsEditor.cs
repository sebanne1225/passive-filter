using UnityEditor;
using UnityEngine;

namespace Sebanne.PassiveFilter.Editor
{
    [CustomEditor(typeof(Sebanne.PassiveFilter.PassiveFilterSettings))]
    public sealed class PassiveFilterSettingsEditor : UnityEditor.Editor
    {
        private SerializedProperty _enable;
        private SerializedProperty _scope;
        private SerializedProperty _exclusions;

        private void OnEnable()
        {
            _enable = serializedObject.FindProperty("enable");
            _scope = serializedObject.FindProperty("scope");
            _exclusions = serializedObject.FindProperty("exclusions");
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
            EditorGUILayout.PropertyField(
                _exclusions,
                new GUIContent("除外リスト（指定オブジェクト以下は対象外）"),
                true);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
