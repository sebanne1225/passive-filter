using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;

namespace Sebanne.PassiveFilter.Editor.Core
{
    /// <summary>
    /// ベイク済 FX を走査し、Simple1D BlendTree に集約されたトグル（2 子 = off clip / on clip の
    /// 葉サブツリー）を検出する。近年の最適化アバター（paryi 系など）はトグルを古典 2 ステート層でなく
    /// ネスト Simple1D BlendTree に畳むため、古典スキャナでは拾えない分をここで補う。
    /// driver = blendParameter、hidden 値 = off 子の threshold。
    /// </summary>
    internal static class BlendTreeToggleScanner
    {
        private const int MaxDepth = 32;

        /// <param name="paramNames">controller の全パラメータ名（blendParameter の妥当性確認用）。</param>
        public static List<ToggleHideResult> Scan(AnimatorController controller, HashSet<string> paramNames)
        {
            var results = new List<ToggleHideResult>();
            if (controller == null) return results;

            var visited = new HashSet<BlendTree>();
            foreach (var layer in controller.layers)
            {
                if (layer.stateMachine == null) continue;
                ScanStateMachine(layer.stateMachine, layer.name, paramNames, results, visited);
            }
            return results;
        }

        // SubStateMachine を含め全 state を辿り、motion が BlendTree の物を walk する。
        private static void ScanStateMachine(
            AnimatorStateMachine sm,
            string layerName,
            HashSet<string> paramNames,
            List<ToggleHideResult> results,
            HashSet<BlendTree> visited)
        {
            if (sm.states != null)
            {
                foreach (var cs in sm.states)
                {
                    if (cs.state != null && cs.state.motion is BlendTree bt)
                        WalkBlendTree(bt, layerName, paramNames, results, visited, 0);
                }
            }

            if (sm.stateMachines != null)
            {
                foreach (var child in sm.stateMachines)
                    if (child.stateMachine != null)
                        ScanStateMachine(child.stateMachine, layerName, paramNames, results, visited);
            }
        }

        private static void WalkBlendTree(
            BlendTree tree,
            string layerName,
            HashSet<string> paramNames,
            List<ToggleHideResult> results,
            HashSet<BlendTree> visited,
            int depth)
        {
            if (tree == null || depth > MaxDepth || !visited.Add(tree)) return;

            var children = tree.children;
            if (children == null) return;

            // 葉トグル判定: Simple1D で 2 子とも AnimationClip。
            if (tree.blendType == BlendTreeType.Simple1D && children.Length == 2
                && children[0].motion is AnimationClip clip0
                && children[1].motion is AnimationClip clip1)
            {
                var toggle = TryBuildToggle(tree, layerName, children[0], children[1], clip0, clip1, paramNames);
                if (toggle != null) results.Add(toggle);
                return; // 2 子ともクリップ＝葉。BlendTree の子は無いので再帰しない。
            }

            // コンテナ: 子の BlendTree を再帰する。
            foreach (var c in children)
                if (c.motion is BlendTree childTree)
                    WalkBlendTree(childTree, layerName, paramNames, results, visited, depth + 1);
        }

        private static ToggleHideResult TryBuildToggle(
            BlendTree tree,
            string layerName,
            ChildMotion childA,
            ChildMotion childB,
            AnimationClip clipA,
            AnimationClip clipB,
            HashSet<string> paramNames)
        {
            var driver = tree.blendParameter;
            if (string.IsNullOrEmpty(driver) || !paramNames.Contains(driver)) return null;

            var mapA = HiddenBindingClassifier.GetHideTargets(clipA);
            var mapB = HiddenBindingClassifier.GetHideTargets(clipB);
            if (!HiddenBindingClassifier.TryResolveHidden(mapA, mapB, out bool hiddenIsA, out var targets, out var conflict))
            {
                if (conflict != null)
                    PassiveFilterLog.Warn($"レイヤー '{layerName}' の BlendTree (param '{driver}') は{conflict}ためスキップしました。");
                return null; // hide 対象なし or 方向矛盾
            }

            float offThreshold = hiddenIsA ? childA.threshold : childB.threshold;
            float onThreshold = hiddenIsA ? childB.threshold : childA.threshold;
            if (Mathf.Abs(offThreshold - onThreshold) < 0.0001f)
            {
                PassiveFilterLog.Warn(
                    $"レイヤー '{layerName}' の BlendTree (param '{driver}') は off/on の threshold が同一のためスキップしました。");
                return null;
            }

            return new ToggleHideResult
            {
                LayerName = layerName,
                DriverParam = driver,
                IsFloat = true,
                HiddenValue = offThreshold, // off 子の threshold へ倒す
                StateMachine = null,        // BlendTree はレイヤー初期 state を変更しない
                OffState = null,
                Targets = targets,
            };
        }
    }
}
