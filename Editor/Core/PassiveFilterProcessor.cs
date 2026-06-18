using System.Collections.Generic;
using nadena.dev.ndmf;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Sebanne.PassiveFilter.Editor.Core
{
    /// <summary>
    /// Passive Filter のオーケストレーション。設定収集 → トグル走査 → スコープ/除外フィルタ →
    /// 反映（param 既定 + レイヤー初期 state + scene disable）→ ログ、を行う。
    /// NDMF のビルドクローン上で動くため元アバターは無傷。
    /// </summary>
    internal static class PassiveFilterProcessor
    {
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

                var boolParams = CollectBoolParams(fx);
                if (boolParams.Count == 0)
                {
                    PassiveFilterLog.Info("bool パラメータが無いためスキップしました。");
                    return;
                }

                var menuParams = MenuParameterCollector.Collect(descriptor.expressionsMenu);
                var exclusionRoots = NormalizeExclusions(settings.Exclusions);
                bool menuOnly = settings.Scope == PassiveFilterSettings.TargetScope.MenuTogglesOnly;

                var results = ToggleScanner.Scan(fx, boolParams);

                int applied = 0;
                int skipped = 0;

                foreach (var result in results)
                {
                    if (menuOnly && !menuParams.Contains(result.DriverParam))
                    {
                        skipped++;
                        continue;
                    }

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
                    if (blocked)
                    {
                        skipped++;
                        continue;
                    }

                    SetAnimatorBoolDefault(fx, result.DriverParam, result.HiddenValue);
                    SetExpressionBoolDefault(descriptor.expressionParameters, result.DriverParam, result.HiddenValue);
                    result.StateMachine.defaultState = result.OffState;
                    foreach (var pair in resolved) ApplySceneDisable(pair.Value, pair.Key.Kind);

                    applied++;
                    PassiveFilterLog.Info(
                        $"レイヤー '{result.LayerName}' (param '{result.DriverParam}'={result.HiddenValue}) を初期非表示へ補正（対象 {resolved.Count} 件）。");
                }

                PassiveFilterLog.Info($"完了: {applied} レイヤー補正 / {skipped} スキップ。");
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

        private static void SetAnimatorBoolDefault(AnimatorController controller, string name, bool value)
        {
            var parameters = controller.parameters;
            bool changed = false;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name == name && parameters[i].type == AnimatorControllerParameterType.Bool)
                {
                    parameters[i].defaultBool = value;
                    changed = true;
                }
            }
            if (changed) controller.parameters = parameters;
        }

        private static void SetExpressionBoolDefault(VRCExpressionParameters expressionParameters, string name, bool value)
        {
            if (expressionParameters == null || expressionParameters.parameters == null) return;
            foreach (var p in expressionParameters.parameters)
            {
                if (p != null
                    && p.name == name
                    && p.valueType == VRCExpressionParameters.ValueType.Bool)
                {
                    // Saved フラグは触らない（凍結設計: 初期値だけ hidden）。
                    p.defaultValue = value ? 1f : 0f;
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
