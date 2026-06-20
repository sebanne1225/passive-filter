using System.Collections.Generic;
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

    /// <summary>1 トグルの検出結果（古典 2 ステート / BlendTree 共通）。</summary>
    internal sealed class ToggleHideResult
    {
        public string LayerName;
        public string DriverParam;

        /// <summary>true=Float driver（BlendTree blendParameter）/ false=Bool driver（古典 2 ステート）。</summary>
        public bool IsFloat;

        /// <summary>hidden へ倒す driver の既定値。Bool は 0/1、Float は off 子の threshold。</summary>
        public float HiddenValue;

        /// <summary>古典トグルのレイヤー初期 state 変更用。BlendTree では null（初期 state 変更は不要）。</summary>
        public AnimatorStateMachine StateMachine;
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

            var mapA = HiddenBindingClassifier.GetHideTargets(clipA);
            var mapB = HiddenBindingClassifier.GetHideTargets(clipB);
            if (!HiddenBindingClassifier.TryResolveHidden(mapA, mapB, out bool hiddenIsA, out var targets, out var conflict))
            {
                if (conflict != null)
                    PassiveFilterLog.Warn($"レイヤー '{layer.name}' は{conflict}ためスキップしました。");
                return null; // hide 対象なし or 方向矛盾
            }

            var commonHidden = hiddenIsA ? stateA : stateB;

            if (!TryGetDriverValueForState(sm, commonHidden, driver, stateA, stateB, out bool hiddenValue))
            {
                PassiveFilterLog.Warn($"レイヤー '{layer.name}' は driver パラメータ '{driver}' の hidden 値を特定できずスキップしました。");
                return null;
            }

            return new ToggleHideResult
            {
                LayerName = layer.name,
                DriverParam = driver,
                IsFloat = false,
                HiddenValue = hiddenValue ? 1f : 0f,
                StateMachine = sm,
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
    }
}
