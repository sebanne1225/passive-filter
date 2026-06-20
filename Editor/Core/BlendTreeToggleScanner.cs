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

            // 葉トグル判定: Simple1D で 2 子とも AnimationClip（既存経路・無改修）。
            if (tree.blendType == BlendTreeType.Simple1D && children.Length == 2
                && children[0].motion is AnimationClip clip0
                && children[1].motion is AnimationClip clip1)
            {
                var toggle = TryBuildToggle(tree, layerName, children[0], children[1], clip0, clip1, paramNames);
                if (toggle != null) results.Add(toggle);
                return; // 2 子ともクリップ＝葉。BlendTree の子は無いので再帰しない。
            }

            // 多バリアント（>2 子）/ clip+subtree 混在の Simple1D: clip 子を評価する。
            if (tree.blendType == BlendTreeType.Simple1D)
                EvaluateSimple1DMulti(tree, layerName, paramNames, results);

            // コンテナ: 子の BlendTree を再帰する（ネストした clean トグルを検出）。
            foreach (var c in children)
                if (c.motion is BlendTree childTree)
                    WalkBlendTree(childTree, layerName, paramNames, results, visited, depth + 1);
        }

        // 多バリアント（>2 子）/ 混在の Simple1D を評価する。
        // 全子クリップ → (A) clean 変種トグルとして補正対象化（TryResolveHiddenMulti）。
        // subtree 混在（packed）→ (B) この param が双方向トグルする対象のみ warn（補正しない）。
        //   片方向のみの foreign 対象は subtree 再帰で別途処理されるため warn しない。
        private static void EvaluateSimple1DMulti(
            BlendTree tree,
            string layerName,
            HashSet<string> paramNames,
            List<ToggleHideResult> results)
        {
            var driver = tree.blendParameter;
            if (string.IsNullOrEmpty(driver) || !paramNames.Contains(driver)) return;

            var clipMaps = new List<(float threshold, Dictionary<(string, HideKind), float> map)>();
            bool hasSubtree = false;
            foreach (var c in tree.children)
            {
                if (c.motion is AnimationClip clip)
                    clipMaps.Add((c.threshold, HiddenBindingClassifier.GetHideTargets(clip)));
                else if (c.motion is BlendTree)
                    hasSubtree = true;
            }
            if (clipMaps.Count == 0) return; // 全 subtree → 再帰に委ねる。

            var union = new HashSet<(string, HideKind)>();
            foreach (var cm in clipMaps)
                foreach (var k in cm.map.Keys) union.Add(k);
            if (union.Count == 0) return; // hide 対象なし。

            if (hasSubtree)
            {
                // (B) packed: この param が双方向（0 と 1 の両方）でトグルする対象＝安全補正不可。
                var warnTargets = new List<string>();
                foreach (var key in union)
                {
                    bool any0 = false, any1 = false;
                    foreach (var cm in clipMaps)
                    {
                        if (!cm.map.TryGetValue(key, out var v)) continue;
                        if (Mathf.Abs(v) < 0.0001f) any0 = true;
                        else if (Mathf.Abs(v - 1f) < 0.0001f) any1 = true;
                    }
                    if (any0 && any1) warnTargets.Add($"{key.Item1} ({key.Item2})");
                }
                if (warnTargets.Count > 0)
                    PassiveFilterLog.Info(
                        $"⚠ レイヤー '{layerName}' の packed BlendTree (param '{driver}') の対象 " +
                        $"[{string.Join(", ", warnTargets)}] は packed 構造のため安全に自動補正できません" +
                        "（除外リスト or 手動対応してください）。");
                return; // 補正しない。
            }

            // (A) 全子クリップ → clean 変種トグル判定。
            if (HiddenBindingClassifier.TryResolveHiddenMulti(clipMaps, out float offThreshold, out var targets, out var reason))
            {
                results.Add(new ToggleHideResult
                {
                    LayerName = layerName,
                    DriverParam = driver,
                    IsFloat = true,
                    HiddenValue = offThreshold,
                    StateMachine = null,
                    OffState = null,
                    Targets = targets,
                });
            }
            else if (reason != null)
            {
                // severity は当面 Info（NDMF ベイク中 LogWarning は Console に出ないため）。正式な surfacing は NDMF コンソール実装で対応。
                PassiveFilterLog.Info(
                    $"⚠ レイヤー '{layerName}' の BlendTree (param '{driver}') は{reason}ため補正をスキップしました。");
            }
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
