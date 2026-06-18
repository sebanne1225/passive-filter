using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Sebanne.PassiveFilter.Editor.Core
{
    /// <summary>1 つの hide 対象（off-state の clip が無効化しているコンポーネント）。</summary>
    internal struct HideTarget
    {
        public string Path;
        public HideKind Kind;

        public HideTarget(string path, HideKind kind)
        {
            Path = path;
            Kind = kind;
        }
    }

    /// <summary>1 レイヤーのトグル検出結果。</summary>
    internal sealed class ToggleHideResult
    {
        public AnimatorStateMachine StateMachine;
        public string LayerName;
        public string DriverParam;
        public bool HiddenValue;
        public AnimatorState OffState;
        public List<HideTarget> Targets = new List<HideTarget>();
    }

    /// <summary>
    /// ベイク済 FX AnimatorController を走査し、bool 1 個駆動の 2 ステートトグルを検出する。
    /// off-clip（無効化 curve を持つ state）から hide 対象と hidden 値を割り出す。
    /// 曖昧なものは警告を出して null（スキップ）にする。
    /// </summary>
    internal static class ToggleScanner
    {
        public static List<ToggleHideResult> Scan(AnimatorController controller, HashSet<string> boolParams)
        {
            var results = new List<ToggleHideResult>();
            if (controller == null) return results;

            foreach (var layer in controller.layers)
            {
                var result = ScanLayer(layer, boolParams);
                if (result != null) results.Add(result);
            }
            return results;
        }

        private static ToggleHideResult ScanLayer(AnimatorControllerLayer layer, HashSet<string> boolParams)
        {
            var sm = layer.stateMachine;
            if (sm == null) return null;

            // ネスト SubStateMachine は MVP 非対応（多くはトグルでないので silent）。
            if (sm.stateMachines != null && sm.stateMachines.Length > 0) return null;

            var childStates = sm.states;
            if (childStates == null || childStates.Length != 2) return null;

            var stateA = childStates[0].state;
            var stateB = childStates[1].state;
            if (stateA == null || stateB == null) return null;

            // driver bool param を特定（全 transition の condition から）。
            var drivers = new HashSet<string>();
            CollectConditionParams(sm.anyStateTransitions, boolParams, drivers);
            CollectConditionParams(sm.entryTransitions, boolParams, drivers);
            CollectConditionParams(stateA.transitions, boolParams, drivers);
            CollectConditionParams(stateB.transitions, boolParams, drivers);
            if (drivers.Count != 1) return null;

            string driver = null;
            foreach (var d in drivers) driver = d;

            var clipA = stateA.motion as AnimationClip;
            var clipB = stateB.motion as AnimationClip;
            if ((stateA.motion != null && clipA == null) || (stateB.motion != null && clipB == null))
            {
                PassiveFilterLog.Warn($"レイヤー '{layer.name}' は BlendTree を含むためスキップしました。");
                return null;
            }

            var mapA = GetStateTargets(clipA);
            var mapB = GetStateTargets(clipB);

            var keys = new HashSet<(string, HideKind)>();
            foreach (var k in mapA.Keys) keys.Add(k);
            foreach (var k in mapB.Keys) keys.Add(k);
            if (keys.Count == 0) return null; // hide 対象なし

            AnimatorState commonHidden = null;
            var targets = new List<HideTarget>();
            foreach (var key in keys)
            {
                bool hasA = mapA.TryGetValue(key, out var va);
                bool hasB = mapB.TryGetValue(key, out var vb);
                bool aOne = hasA && Approximately(va, 1f);
                bool bOne = hasB && Approximately(vb, 1f);
                bool aZero = hasA && Approximately(va, 0f);
                bool bZero = hasB && Approximately(vb, 0f);

                AnimatorState hiddenState;
                if (aOne ^ bOne)
                    hiddenState = aOne ? stateB : stateA;          // 片方だけ有効(1) → もう片方が hidden
                else if (!aOne && !bOne && (aZero ^ bZero))
                    hiddenState = aZero ? stateA : stateB;          // 片方だけ無効(0) → そちらが hidden
                else
                    continue;                                       // 判定不能 → この対象はスキップ

                if (commonHidden == null)
                {
                    commonHidden = hiddenState;
                }
                else if (commonHidden != hiddenState)
                {
                    PassiveFilterLog.Warn($"レイヤー '{layer.name}' は複数対象で hidden 方向が一致しないためスキップしました。");
                    return null;
                }

                targets.Add(new HideTarget(key.Item1, key.Item2));
            }

            if (commonHidden == null || targets.Count == 0) return null;

            if (!TryGetDriverValueForState(sm, commonHidden, driver, stateA, stateB, out bool hiddenValue))
            {
                PassiveFilterLog.Warn($"レイヤー '{layer.name}' は driver パラメータ '{driver}' の hidden 値を特定できずスキップしました。");
                return null;
            }

            return new ToggleHideResult
            {
                StateMachine = sm,
                LayerName = layer.name,
                DriverParam = driver,
                HiddenValue = hiddenValue,
                OffState = commonHidden,
                Targets = targets,
            };
        }

        private static void CollectConditionParams(
            AnimatorTransitionBase[] transitions,
            HashSet<string> boolParams,
            HashSet<string> acc)
        {
            if (transitions == null) return;
            foreach (var t in transitions)
            {
                if (t == null) continue;
                foreach (var c in t.conditions)
                    if (boolParams.Contains(c.parameter)) acc.Add(c.parameter);
            }
        }

        private static Dictionary<(string, HideKind), float> GetStateTargets(AnimationClip clip)
        {
            var dict = new Dictionary<(string, HideKind), float>();
            if (clip == null) return dict;

            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (!HiddenBindingClassifier.TryClassify(binding, out var kind)) continue;
                if (!HiddenBindingClassifier.TryGetConstant(clip, binding, out var value)) continue;
                dict[(binding.path, kind)] = value;
            }
            return dict;
        }

        // off-state（commonHidden）へ遷移させる driver 値を condition から割り出す（逆トグル対応）。
        private static bool TryGetDriverValueForState(
            AnimatorStateMachine sm,
            AnimatorState target,
            string driver,
            AnimatorState a,
            AnimatorState b,
            out bool hiddenValue)
        {
            hiddenValue = false;
            bool? found = null;

            bool Check(AnimatorTransitionBase[] transitions)
            {
                if (transitions == null) return true;
                foreach (var t in transitions)
                {
                    if (t == null || t.destinationState != target) continue;
                    foreach (var c in t.conditions)
                    {
                        if (c.parameter != driver) continue;
                        bool? v =
                            c.mode == AnimatorConditionMode.If ? true :
                            c.mode == AnimatorConditionMode.IfNot ? (bool?)false : null;
                        if (v == null) continue;
                        if (found == null) found = v;
                        else if (found != v) return false; // 矛盾
                    }
                }
                return true;
            }

            if (!Check(sm.anyStateTransitions)) return false;
            if (!Check(sm.entryTransitions)) return false;
            if (!Check(a.transitions)) return false;
            if (!Check(b.transitions)) return false;
            if (found == null) return false;

            hiddenValue = found.Value;
            return true;
        }

        private static bool Approximately(float v, float target) => Mathf.Abs(v - target) < 0.0001f;
    }
}
