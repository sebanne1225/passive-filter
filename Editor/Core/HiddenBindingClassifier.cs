using UnityEditor;
using UnityEngine;

namespace Sebanne.PassiveFilter.Editor.Core
{
    /// <summary>hide 対象コンポーネントの種別。</summary>
    internal enum HideKind
    {
        GameObjectActive,
        RendererEnabled,
        ParticleEmission,
        LightEnabled,
        AudioEnabled,
    }

    /// <summary>
    /// AnimationClip の curve binding が「hide 可能な対象の有効/無効プロパティ」かを判定し、
    /// その curve が定数 0（無効）/ 1（有効）かを調べるユーティリティ。
    /// </summary>
    internal static class HiddenBindingClassifier
    {
        public static bool TryClassify(EditorCurveBinding binding, out HideKind kind)
        {
            kind = default;
            var type = binding.type;
            var prop = binding.propertyName;
            if (type == null) return false;

            if (type == typeof(GameObject) && prop == "m_IsActive") { kind = HideKind.GameObjectActive; return true; }
            if (typeof(Renderer).IsAssignableFrom(type) && prop == "m_Enabled") { kind = HideKind.RendererEnabled; return true; }
            // ParticleSystem 本体は Behaviour ではないため、実務で animate されるのは EmissionModule.enabled。
            // 念のため m_Enabled も受け、scene 反映時は emission を止める（D3 = 両方）。
            if (type == typeof(ParticleSystem) && (prop == "EmissionModule.enabled" || prop == "m_Enabled")) { kind = HideKind.ParticleEmission; return true; }
            if (type == typeof(Light) && prop == "m_Enabled") { kind = HideKind.LightEnabled; return true; }
            if (type == typeof(AudioSource) && prop == "m_Enabled") { kind = HideKind.AudioEnabled; return true; }
            return false;
        }

        /// <summary>curve が定数値なら true を返し、その値を out する。</summary>
        public static bool TryGetConstant(AnimationClip clip, EditorCurveBinding binding, out float value)
        {
            value = 0f;
            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (curve == null || curve.length == 0) return false;

            var keys = curve.keys;
            value = keys[0].value;
            for (int i = 1; i < keys.Length; i++)
                if (Mathf.Abs(keys[i].value - value) > 0.0001f) return false;
            return true;
        }
    }
}
