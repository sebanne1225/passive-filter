using System.Collections.Generic;
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

        /// <summary>
        /// clip の curve binding のうち hide 対象（GameObject/Renderer/Particle/Light/Audio）で
        /// 定数値を持つものを (path, kind) → 値 の辞書にまとめる。古典・BlendTree 両スキャナ共通。
        /// </summary>
        public static Dictionary<(string, HideKind), float> GetHideTargets(AnimationClip clip)
        {
            var dict = new Dictionary<(string, HideKind), float>();
            if (clip == null) return dict;

            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (!TryClassify(binding, out var kind)) continue;
                if (!TryGetConstant(clip, binding, out var value)) continue;
                dict[(binding.path, kind)] = value;
            }
            return dict;
        }

        /// <summary>
        /// 2 つの clip（候補 first / second）の hide 対象マップから、どちらが hidden（無効化=0）側かを判定する。
        /// 全 hide 対象で hidden 方向が一致すれば true（hiddenIsFirst = first が hidden 側か）と対象一覧を返す。
        /// hide 対象が無い場合は false（conflictReason=null）。方向が矛盾する場合は false（conflictReason 有り）。
        /// 古典スキャナ（stateA/stateB）と BlendTree スキャナ（child0/child1）で共通利用する。
        /// </summary>
        public static bool TryResolveHidden(
            Dictionary<(string, HideKind), float> mapFirst,
            Dictionary<(string, HideKind), float> mapSecond,
            out bool hiddenIsFirst,
            out List<HideTarget> targets,
            out string conflictReason)
        {
            hiddenIsFirst = false;
            targets = new List<HideTarget>();
            conflictReason = null;

            var keys = new HashSet<(string, HideKind)>();
            foreach (var k in mapFirst.Keys) keys.Add(k);
            foreach (var k in mapSecond.Keys) keys.Add(k);
            if (keys.Count == 0) return false; // hide 対象なし

            bool? hidden = null;
            foreach (var key in keys)
            {
                bool hasA = mapFirst.TryGetValue(key, out var va);
                bool hasB = mapSecond.TryGetValue(key, out var vb);
                bool aOne = hasA && Approximately(va, 1f);
                bool bOne = hasB && Approximately(vb, 1f);
                bool aZero = hasA && Approximately(va, 0f);
                bool bZero = hasB && Approximately(vb, 0f);

                bool thisHiddenFirst;
                if (aOne ^ bOne)
                    thisHiddenFirst = !aOne;             // 片方だけ有効(1) → もう片方が hidden
                else if (!aOne && !bOne && (aZero ^ bZero))
                    thisHiddenFirst = aZero;             // 片方だけ無効(0) → そちらが hidden
                else
                    continue;                            // この対象は判定不能 → 寄与しない

                if (hidden == null) hidden = thisHiddenFirst;
                else if (hidden != thisHiddenFirst)
                {
                    conflictReason = "複数対象で hidden 方向が一致しない";
                    return false;
                }
                targets.Add(new HideTarget(key.Item1, key.Item2));
            }

            if (hidden == null || targets.Count == 0) return false;
            hiddenIsFirst = hidden.Value;
            return true;
        }

        /// <summary>
        /// N 個の clip（threshold + hide 対象マップ）から、clean な単一トグルかを判定する
        /// （全 clip が off 群 / on 群へ一貫分類でき、off 群が単一 threshold）。
        /// BlendTreeToggleScanner の N&gt;2 子（多バリアント）経路用。2 子は <see cref="TryResolveHidden"/> を使う。
        /// 成立時、off 群の共通 threshold と対象一覧を返す。conflictReason 有り = 補正不能（warn 対象）。
        /// </summary>
        public static bool TryResolveHiddenMulti(
            List<(float threshold, Dictionary<(string, HideKind), float> map)> clips,
            out float offThreshold,
            out List<HideTarget> targets,
            out string conflictReason)
        {
            offThreshold = 0f;
            targets = new List<HideTarget>();
            conflictReason = null;

            var union = new HashSet<(string, HideKind)>();
            foreach (var c in clips)
                foreach (var k in c.map.Keys) union.Add(k);
            if (union.Count == 0) return false; // hide 対象なし（silent）

            // 各 clip を off-clip（opinion を持つ全対象が 0）/ on-clip（全 1）/ mixed / neutral に分類。
            var offThresholds = new HashSet<float>();
            bool hasOff = false, hasOn = false;
            foreach (var c in clips)
            {
                bool anyZero = false, anyOne = false;
                foreach (var kv in c.map)
                {
                    if (Approximately(kv.Value, 0f)) anyZero = true;
                    else if (Approximately(kv.Value, 1f)) anyOne = true;
                }
                if (anyZero && anyOne)
                {
                    conflictReason = "1 つの clip 内で hidden/shown が混在する";
                    return false;
                }
                if (anyZero) { hasOff = true; offThresholds.Add(c.threshold); }
                else if (anyOne) { hasOn = true; }
                // neutral（対象なし / 0,1 以外）は寄与しない
            }

            if (!hasOff || !hasOn) return false; // トグルになっていない（常時 off / 常時 on）

            if (offThresholds.Count != 1)
            {
                conflictReason = "off 側 threshold が単一に定まらない";
                return false;
            }

            // 全対象が「ある off-clip で 0」かつ「ある on-clip で 1」になっているか。
            foreach (var key in union)
            {
                bool hiddenSomewhere = false, shownSomewhere = false;
                foreach (var c in clips)
                {
                    if (!c.map.TryGetValue(key, out var v)) continue;
                    if (Approximately(v, 0f)) hiddenSomewhere = true;
                    else if (Approximately(v, 1f)) shownSomewhere = true;
                }
                if (!hiddenSomewhere || !shownSomewhere)
                {
                    conflictReason = "対象が off/on 両方のトグルになっていない";
                    return false;
                }
                targets.Add(new HideTarget(key.Item1, key.Item2));
            }

            foreach (var t in offThresholds) offThreshold = t; // 単一値
            return true;
        }

        private static bool Approximately(float v, float target) => Mathf.Abs(v - target) < 0.0001f;
    }
}
