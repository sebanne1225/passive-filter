using System.Collections.Generic;
using nadena.dev.ndmf;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Sebanne.PassiveFilter.Editor.Core
{
    /// <summary>
    /// MA merge 前（Generating）に捕捉した base FX のパラメータ名集合。NDMF の BuildContext 状態として
    /// 早期パス → 遅延パス間で共有し、option 2（base avatar 由来トグルを除外）に使う。
    /// </summary>
    internal sealed class BaseFxParameterState
    {
        public bool Captured;
        public readonly HashSet<string> Names = new HashSet<string>();
    }

    /// <summary>
    /// Passive Filter のオーケストレーション。設定収集 → トグル走査（古典 + BlendTree）→
    /// スコープ / 除外 / base 除外フィルタ → 反映（param 既定 + レイヤー初期 state + scene disable）→ ログ。
    /// NDMF のビルドクローン上で動くため元アバターは無傷。
    /// </summary>
    internal static class PassiveFilterProcessor
    {
        // =====================================================================
        // 早期パス（Generating）: MA merge 前に base FX param 名を捕捉する。
        // =====================================================================
        public static void CaptureBaseParameters(BuildContext ctx)
        {
            var settings = ctx.AvatarRootObject.GetComponent<PassiveFilterSettings>();
            if (settings == null || !settings.Enabled) return;

            var descriptor = ctx.AvatarRootObject.GetComponent<VRCAvatarDescriptor>();
            if (descriptor == null) return;

            var state = ctx.GetState<BaseFxParameterState>();
            state.Captured = true;

            var fx = FindLayerController(descriptor, VRCAvatarDescriptor.AnimLayerType.FX);
            if (fx == null) return; // base FX 無し → Names 空（除外対象なし）。

            foreach (var p in fx.parameters) state.Names.Add(p.name);
            PassiveFilterLog.Info($"base FX パラメータ {state.Names.Count} 件を捕捉（MA merge 前）。");
        }

        // =====================================================================
        // 遅延パス（Transforming.AfterPlugin(MA)）: 検出 → フィルタ → 反映。
        // =====================================================================
        public static void Run(BuildContext ctx)
        {
            var settings = ctx.AvatarRootObject.GetComponent<PassiveFilterSettings>();
            if (settings == null) return;

            try
            {
                if (!settings.Enabled)
                {
                    PassiveFilterLog.Info("無効化されているためスキップしました。");
                    return;
                }

                var descriptor = ctx.AvatarRootObject.GetComponent<VRCAvatarDescriptor>();
                if (descriptor == null)
                {
                    PassiveFilterLog.Warn("VRCAvatarDescriptor が見つかりません。");
                    return;
                }

                var fx = FindLayerController(descriptor, VRCAvatarDescriptor.AnimLayerType.FX);
                if (fx == null)
                {
                    PassiveFilterLog.Info("FX コントローラが見つからないためスキップしました。");
                    return;
                }

                bool excludeBase = !settings.IncludeBaseAvatarToggles;
                var baseState = ctx.GetState<BaseFxParameterState>();
                if (excludeBase && !baseState.Captured)
                {
                    // 早期パス未実行 ＝ base param 集合が不明。base/MA を区別できないまま畳むと
                    // base default-ON（衣装など）を誤って隠すリスクがあるため、安全側に倒して何もしない。
                    PassiveFilterLog.Warn(
                        "base パラメータの捕捉に失敗（早期パス未実行）。誤補正回避のためスキップしました。");
                    return;
                }

                var boolParams = CollectBoolParams(fx);   // 古典 2 ステートの driver 候補
                var allParams = CollectParamNames(fx);     // BlendTree blendParameter の妥当性確認用

                var menuParams = MenuParameterCollector.Collect(descriptor.expressionsMenu);
                var exclusionRoots = NormalizeExclusions(settings.Exclusions);
                bool menuOnly = settings.Scope == PassiveFilterSettings.TargetScope.MenuTogglesOnly;

                var classicResults = ToggleScanner.Scan(fx, boolParams);
                var blendResults = BlendTreeToggleScanner.Scan(fx, allParams);
                PassiveFilterLog.Info(
                    $"検出: 古典 {classicResults.Count} 件 / BlendTree {blendResults.Count} 件。");

                var all = new List<ToggleHideResult>(classicResults.Count + blendResults.Count);
                all.AddRange(classicResults);
                all.AddRange(blendResults);

                // 同一 driver param の結果を統合（scene disable 対象を union）。hidden 値が矛盾する
                // param は安全側に倒してスキップ（先勝ちで誤った既定を書かない）。
                var merged = MergeByDriver(all, out int conflictSkip);

                int applied = 0, menuSkip = 0, baseSkip = 0, resolveSkip = 0;

                foreach (var result in merged)
                {
                    if (menuOnly && !menuParams.Contains(result.DriverParam)) { menuSkip++; continue; }
                    if (excludeBase && baseState.Names.Contains(result.DriverParam)) { baseSkip++; continue; }

                    var resolved = new List<KeyValuePair<HideTarget, Transform>>();
                    bool blocked = false;
                    foreach (var target in result.Targets)
                    {
                        var tr = ResolvePath(ctx.AvatarRootTransform, target.Path);
                        if (tr == null)
                        {
                            PassiveFilterLog.Warn($"レイヤー '{result.LayerName}': パス '{target.Path}' を解決できずスキップしました。");
                            blocked = true;
                            break;
                        }
                        if (IsExcluded(tr, exclusionRoots))
                        {
                            // 除外対象を含むトグルは丸ごと触らない（同 param で除外対象も隠れてしまうため）。
                            blocked = true;
                            break;
                        }
                        resolved.Add(new KeyValuePair<HideTarget, Transform>(target, tr));
                    }
                    if (blocked) { resolveSkip++; continue; }

                    SetAnimatorDefault(fx, result.DriverParam, result.HiddenValue);
                    SetExpressionDefault(descriptor.expressionParameters, result.DriverParam, result.HiddenValue);
                    if (result.StateMachine != null && result.OffState != null)
                        result.StateMachine.defaultState = result.OffState; // 古典のみ
                    foreach (var pair in resolved) ApplySceneDisable(pair.Value, pair.Key.Kind);

                    applied++;
                    PassiveFilterLog.Info(
                        $"[{(result.IsFloat ? "BlendTree" : "古典")}] レイヤー '{result.LayerName}' " +
                        $"(param '{result.DriverParam}'={result.HiddenValue}) を初期非表示へ補正（対象 {resolved.Count} 件）。");
                }

                PassiveFilterLog.Info(
                    $"完了: 補正 {applied} / メニュー外 {menuSkip} / base 除外 {baseSkip} / " +
                    $"解決不可・除外 {resolveSkip} / 値矛盾 {conflictSkip}。");
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        // =====================================================================
        // helpers
        // =====================================================================

        private static AnimatorController FindLayerController(
            VRCAvatarDescriptor descriptor,
            VRCAvatarDescriptor.AnimLayerType layerType)
        {
            foreach (var layer in descriptor.baseAnimationLayers)
            {
                if (layer.type != layerType) continue;
                if (layer.isDefault || layer.animatorController == null) return null;
                return layer.animatorController as AnimatorController;
            }
            return null;
        }

        private static HashSet<string> CollectBoolParams(AnimatorController controller)
        {
            var set = new HashSet<string>();
            foreach (var p in controller.parameters)
                if (p.type == AnimatorControllerParameterType.Bool) set.Add(p.name);
            return set;
        }

        private static HashSet<string> CollectParamNames(AnimatorController controller)
        {
            var set = new HashSet<string>();
            foreach (var p in controller.parameters) set.Add(p.name);
            return set;
        }

        /// <summary>
        /// 検出結果を driver param 単位で統合する。同一 param の複数トグル（古典/BlendTree や
        /// BlendTree の複数葉）は scene disable 対象を union し、1 回だけ反映する。hidden 値が
        /// 矛盾する param は安全側に倒してスキップする（走査順依存で誤った既定を書かないため）。
        /// </summary>
        private static List<ToggleHideResult> MergeByDriver(List<ToggleHideResult> results, out int conflictSkip)
        {
            var byParam = new Dictionary<string, ToggleHideResult>();
            var order = new List<string>();
            var conflicted = new HashSet<string>();

            foreach (var r in results)
            {
                if (conflicted.Contains(r.DriverParam)) continue;

                if (!byParam.TryGetValue(r.DriverParam, out var existing))
                {
                    byParam[r.DriverParam] = r;
                    order.Add(r.DriverParam);
                    continue;
                }

                bool sameValue = existing.IsFloat == r.IsFloat
                                 && Mathf.Abs(existing.HiddenValue - r.HiddenValue) < 0.0001f;
                if (!sameValue)
                {
                    conflicted.Add(r.DriverParam);
                    byParam.Remove(r.DriverParam);
                    PassiveFilterLog.Warn(
                        $"param '{r.DriverParam}' は複数トグルで hidden 値が矛盾するため安全のためスキップしました。");
                    continue;
                }

                // hidden 値が一致 → scene disable 対象を union（取りこぼし防止）。
                foreach (var t in r.Targets)
                    if (!existing.Targets.Contains(t)) existing.Targets.Add(t);
            }

            conflictSkip = conflicted.Count;
            var list = new List<ToggleHideResult>(order.Count);
            foreach (var name in order)
                if (byParam.TryGetValue(name, out var r)) list.Add(r);
            return list;
        }

        private static List<Transform> NormalizeExclusions(IReadOnlyList<UnityEngine.Object> exclusions)
        {
            var roots = new List<Transform>();
            if (exclusions == null) return roots;
            foreach (var obj in exclusions)
            {
                switch (obj)
                {
                    case null:
                        break;
                    case GameObject go:
                        roots.Add(go.transform);
                        break;
                    case Component comp:
                        roots.Add(comp.transform);
                        break;
                }
            }
            return roots;
        }

        private static Transform ResolvePath(Transform avatarRoot, string path)
        {
            if (string.IsNullOrEmpty(path)) return avatarRoot;
            return avatarRoot.Find(path);
        }

        private static bool IsExcluded(Transform target, List<Transform> exclusionRoots)
        {
            foreach (var root in exclusionRoots)
            {
                if (root == null) continue;
                var t = target;
                while (t != null)
                {
                    if (t == root) return true;
                    t = t.parent;
                }
            }
            return false;
        }

        /// <summary>driver param の既定値を hidden 値へ。param 型（Bool/Int/Float）に応じて設定する。</summary>
        private static void SetAnimatorDefault(AnimatorController controller, string name, float value)
        {
            var parameters = controller.parameters;
            bool changed = false;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name != name) continue;
                switch (parameters[i].type)
                {
                    case AnimatorControllerParameterType.Bool:
                        parameters[i].defaultBool = value > 0.5f;
                        changed = true;
                        break;
                    case AnimatorControllerParameterType.Int:
                        parameters[i].defaultInt = Mathf.RoundToInt(value);
                        changed = true;
                        break;
                    case AnimatorControllerParameterType.Float:
                        parameters[i].defaultFloat = value;
                        changed = true;
                        break;
                    // Trigger は対象外。
                }
            }
            if (changed) controller.parameters = parameters;
        }

        private static void SetExpressionDefault(VRCExpressionParameters expressionParameters, string name, float value)
        {
            if (expressionParameters == null || expressionParameters.parameters == null) return;
            foreach (var p in expressionParameters.parameters)
            {
                if (p == null || p.name != name) continue;
                // Saved フラグは触らない（凍結設計: 初期値だけ hidden）。型に合わせて既定値を正規化し
                // animator 側（SetAnimatorDefault）と対称にする。
                switch (p.valueType)
                {
                    case VRCExpressionParameters.ValueType.Bool:
                        p.defaultValue = value > 0.5f ? 1f : 0f;
                        break;
                    case VRCExpressionParameters.ValueType.Int:
                        p.defaultValue = Mathf.Round(value);
                        break;
                    default: // Float
                        p.defaultValue = value;
                        break;
                }
            }
        }

        private static void ApplySceneDisable(Transform target, HideKind kind)
        {
            switch (kind)
            {
                case HideKind.GameObjectActive:
                    target.gameObject.SetActive(false);
                    break;
                case HideKind.RendererEnabled:
                    var renderer = target.GetComponent<Renderer>();
                    if (renderer != null) renderer.enabled = false;
                    break;
                case HideKind.ParticleEmission:
                    var ps = target.GetComponent<ParticleSystem>();
                    if (ps != null)
                    {
                        var emission = ps.emission;
                        emission.enabled = false;
                    }
                    break;
                case HideKind.LightEnabled:
                    var light = target.GetComponent<Light>();
                    if (light != null) light.enabled = false;
                    break;
                case HideKind.AudioEnabled:
                    var audio = target.GetComponent<AudioSource>();
                    if (audio != null) audio.enabled = false;
                    break;
            }
        }
    }
}
