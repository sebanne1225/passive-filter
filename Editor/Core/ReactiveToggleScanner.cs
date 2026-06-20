#if PASSIVEFILTER_MA
using System.Collections.Generic;
using nadena.dev.modular_avatar.core;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Sebanne.PassiveFilter.Editor.Core
{
    /// <summary>1 つの ModularAvatarObjectToggle に対する方式(b)の判定結果。</summary>
    internal sealed class ReactiveToggleResult
    {
        /// <summary>対象トグル。</summary>
        public ModularAvatarObjectToggle Toggle;

        /// <summary>制御メニュー（無条件トグルなら null）。NeedsFlip 時に isDefault を倒す対象。</summary>
        public ModularAvatarMenuItem MenuItem;

        /// <summary>初期 ON ＝ hidden へ倒す対象 GameObject。</summary>
        public readonly List<GameObject> Targets = new List<GameObject>();

        /// <summary>
        /// true なら制御メニューが default ON でルール駆動表示のため、isDefault=false への反転が必要。
        /// false なら scene の activeSelf 由来表示のため、対象を非アクティブにするだけでよい。
        /// </summary>
        public bool NeedsFlip;

        /// <summary>非 null なら複雑系として skip（理由）。null なら単純トグル＝補正対象。</summary>
        public string SkipReason;
    }

    /// <summary>
    /// MA がリアクティブをベイクする前に、ビルドクローン上の ModularAvatarObjectToggle を直読し、
    /// 「初期 ON の単純トグル」を判定する。初期 ON 判定は MA と同じく
    /// 「制御メニューが default 成立ならルール値（Active^Inverted）、非成立なら対象の scene activeSelf」。
    /// 複雑系（無条件 / default ON で表示非表示混在 / 複数同 param / 複数トグル同対象 / 非 leaf）は
    /// MA 実ロジックとの乖離による誤補正を避けるため skip 理由付きで返す（research §2.2 ツリー）。
    /// 反映（apply）はせず判定だけ行う純ロジック。A 方式 / B 方式どちらでも再利用できる。
    /// </summary>
    internal static class ReactiveToggleScanner
    {
        public static List<ReactiveToggleResult> Scan(GameObject avatarRoot)
        {
            var results = new List<ReactiveToggleResult>();
            if (avatarRoot == null) return results;

            var toggles = avatarRoot.GetComponentsInChildren<ModularAvatarObjectToggle>(true);
            if (toggles == null || toggles.Length == 0) return results;

            // 事前集計: 対象 GameObject → それを制御する ObjectToggle 数（#4）。併せて全対象集合（#6）。
            var targetToggleCount = new Dictionary<GameObject, int>();
            var allTargets = new HashSet<GameObject>();
            foreach (var t in toggles)
            {
                foreach (var entry in t.Objects)
                {
                    var go = entry.Object != null ? entry.Object.Get(t) : null;
                    if (go == null) continue;
                    allTargets.Add(go);
                    targetToggleCount.TryGetValue(go, out var c);
                    targetToggleCount[go] = c + 1;
                }
            }

            // 事前集計: param 名 → それを駆動する Toggle/Button メニュー数（#3）。auto/空 param は対象外。
            var paramMenuCount = new Dictionary<string, int>();
            foreach (var mi in avatarRoot.GetComponentsInChildren<ModularAvatarMenuItem>(true))
            {
                var p = GetDrivenParam(mi);
                if (string.IsNullOrEmpty(p)) continue;
                paramMenuCount.TryGetValue(p, out var c);
                paramMenuCount[p] = c + 1;
            }

            foreach (var toggle in toggles)
            {
                var result = Analyze(toggle, avatarRoot, targetToggleCount, allTargets, paramMenuCount);
                if (result != null) results.Add(result);
            }
            return results;
        }

        // 1 トグルを判定。初期 ON 対象が無ければ null（そもそも対象外）。
        private static ReactiveToggleResult Analyze(
            ModularAvatarObjectToggle toggle,
            GameObject avatarRoot,
            Dictionary<GameObject, int> targetToggleCount,
            HashSet<GameObject> allTargets,
            Dictionary<string, int> paramMenuCount)
        {
            var menuItem = FindControllingMenuItem(toggle.transform, avatarRoot);
            bool menuDefault = menuItem == null ? true : menuItem.isDefault; // 無条件 or default=初期成立

            var onTargets = new List<GameObject>();
            bool anyForcedOnByRule = false; // ルールが ON へ駆動している対象があるか
            bool allEntriesShowDirection = true; // 全 entry が表示方向（ruleValue=true）か

            foreach (var entry in toggle.Objects)
            {
                var go = entry.Object != null ? entry.Object.Get(toggle) : null;
                if (go == null) continue;

                bool ancestorsActive = AllAncestorsActive(go.transform, avatarRoot);
                bool ruleActive = menuDefault && ancestorsActive;  // frame-0 でルールが効くか
                bool ruleValue = entry.Active ^ toggle.Inverted;   // true=表示 / false=非表示

                if (!ruleValue) allEntriesShowDirection = false;

                // MA の initialState: ルール成立時はルール値、非成立時は対象の scene activeSelf。
                bool initialOn = ruleActive ? ruleValue : (go.activeSelf && ancestorsActive);
                if (!initialOn) continue; // frame-0 で非表示＝既に hidden 側＝対象外

                onTargets.Add(go);
                if (ruleActive && ruleValue) anyForcedOnByRule = true;
            }

            if (onTargets.Count == 0) return null;

            var result = new ReactiveToggleResult { Toggle = toggle, MenuItem = menuItem };
            result.Targets.AddRange(onTargets);

            // 【B】単純トグル判定（複雑系は skip+warn）。research §5 #1〜#6 + 表示非表示混在。
            if (menuItem == null)
            {
                // 無条件で ON へ駆動されている＝倒す手段（isDefault）が無い。
                result.SkipReason = "メニュー連動が無い無条件トグル（初期 OFF へ倒す手段が無い）"; // #1
                return result;
            }

            if (menuItem.isDefault)
            {
                // メニュー default ON ＝ルール駆動表示。isDefault=false への反転が必要だが、反転は
                // トグルの全 entry に効くため、表示方向のみ（全 entry が表示）でないと非表示 entry を誤って表示してしまう。
                if (!allEntriesShowDirection)
                {
                    result.SkipReason = "メニュー default ON で表示・非表示が混在するトグル";
                    return result;
                }
                result.NeedsFlip = true;
            }
            else
            {
                // メニュー非 default ＝ scene activeSelf 由来表示。対象を非アクティブにするだけでよい。
                result.NeedsFlip = false;
            }

            var drivenParam = GetDrivenParam(menuItem);
            if (!string.IsNullOrEmpty(drivenParam)
                && paramMenuCount.TryGetValue(drivenParam, out var pc) && pc > 1)
            {
                result.SkipReason = $"同一パラメータ '{drivenParam}' を複数のメニュー項目が駆動している"; // #3
                return result;
            }

            foreach (var go in onTargets)
            {
                if (targetToggleCount.TryGetValue(go, out var tc) && tc > 1)
                {
                    result.SkipReason = $"対象 '{go.name}' を複数のトグルが制御している"; // #4
                    return result;
                }
                if (HasReactiveDescendantTarget(go, allTargets))
                {
                    result.SkipReason = $"対象 '{go.name}' の配下に別のリアクティブ対象がある（非 leaf）"; // #6
                    return result;
                }
            }

            return result; // SkipReason == null ＝ 単純トグル＝補正対象
        }

        // toggle の transform から avatar root まで遡り、最も近い param 駆動メニュー（Toggle/Button）を返す。
        private static ModularAvatarMenuItem FindControllingMenuItem(Transform start, GameObject avatarRoot)
        {
            var t = start;
            while (t != null)
            {
                var mi = t.GetComponent<ModularAvatarMenuItem>();
                if (mi != null && DrivesParam(mi)) return mi;
                if (t.gameObject == avatarRoot) break;
                t = t.parent;
            }
            return null;
        }

        // 対象の祖先（avatar root を除く）が全て activeSelf か。1 つでも非アクティブなら初期表示されない。
        private static bool AllAncestorsActive(Transform target, GameObject avatarRoot)
        {
            var t = target.parent;
            while (t != null && t.gameObject != avatarRoot)
            {
                if (!t.gameObject.activeSelf) return false;
                t = t.parent;
            }
            return true;
        }

        // 対象配下に「別のリアクティブ対象」があるか（activeSelf を倒すと子孫の条件に波及するため）。
        private static bool HasReactiveDescendantTarget(GameObject go, HashSet<GameObject> allTargets)
        {
            foreach (var other in allTargets)
            {
                if (other == null || other == go) continue;
                if (other.transform.IsChildOf(go.transform)) return true;
            }
            return false;
        }

        private static bool DrivesParam(ModularAvatarMenuItem mi)
        {
            var c = mi.Control;
            return c != null
                   && (c.type == VRCExpressionsMenu.Control.ControlType.Toggle
                       || c.type == VRCExpressionsMenu.Control.ControlType.Button);
        }

        private static string GetDrivenParam(ModularAvatarMenuItem mi)
        {
            var c = mi.Control;
            if (c == null) return null;
            if (c.type != VRCExpressionsMenu.Control.ControlType.Toggle
                && c.type != VRCExpressionsMenu.Control.ControlType.Button) return null;
            return c.parameter != null ? c.parameter.name : null;
        }
    }
}
#endif
