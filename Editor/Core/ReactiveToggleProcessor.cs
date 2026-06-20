#if PASSIVEFILTER_MA
using System.Collections.Generic;
using nadena.dev.ndmf;
using Sebanne.PassiveFilter.Editor.NDMF;
using UnityEngine;

namespace Sebanne.PassiveFilter.Editor.Core
{
    /// <summary>
    /// 方式(b) の反映パス。MA がリアクティブをベイクする前（Transforming.BeforePlugin(MA)）に走り、
    /// ReactiveToggleScanner が判定した初期 ON の単純トグルを「MA の入力を倒す」方式で hidden 化する:
    /// 制御メニューの isDefault=false ＋ 対象 GameObject の activeSelf=false（ビルドクローン上）。
    /// その後 MA が initialState=0 を計算し、scene / base clip / override 層に整合ベイクする。
    /// 複雑系は ReactiveToggleScanner が skip 理由を付けるので、ここでは警告して触らない。
    /// </summary>
    internal static class ReactiveToggleProcessor
    {
        public static void Run(BuildContext ctx)
        {
            var settings = ctx.AvatarRootObject.GetComponent<PassiveFilterSettings>();
            if (settings == null || !settings.Enabled) return;
            PassiveFilterLog.Verbose = settings.VerboseLogging;
            // settings はここでは破棄しない（後段 PassiveFilterProcessor.Run の finally が破棄する）。

            var results = ReactiveToggleScanner.Scan(ctx.AvatarRootObject);
            if (results.Count == 0) return;

            var exclusionRoots = NormalizeExclusions(settings.Exclusions);

            // 軸1(scope): MA の単純トグルは常にメニュー連動（無条件は #1 で skip 済み）のため、
            //   MenuTogglesOnly / AllToggles のいずれでも対象に含まれる（実質 no-op）。
            // 軸2(base 除外): 方式(b) は定義上 MA 追加分のみを触るため、常に通す（research §4.3）。

            var diagnostics = new List<PassiveFilterDiagnostic>();
            int applied = 0, complexSkip = 0, exclSkip = 0;

            foreach (var result in results)
            {
                if (result.SkipReason != null)
                {
                    diagnostics.Add(new PassiveFilterDiagnostic
                    {
                        Category = DiagnosticCategory.MaReactiveSkip,
                        Label = ToggleName(result),
                        Reason = result.SkipReason,
                        ContextObjects = result.Toggle != null
                            ? new List<UnityEngine.Object> { result.Toggle.gameObject }
                            : null,
                    });
                    complexSkip++;
                    continue;
                }

                // 除外: いずれかの対象が除外サブツリー配下ならトグル丸ごとスキップ。
                bool blocked = false;
                foreach (var go in result.Targets)
                {
                    if (IsExcluded(go.transform, exclusionRoots)) { blocked = true; break; }
                }
                if (blocked) { exclSkip++; continue; }

                // apply A（クローン上）: ルール駆動表示なら制御メニューを非 default へ倒し、
                // 併せて対象を scene 非アクティブにする（scene 由来表示は後者だけでよい）。
                if (result.NeedsFlip && result.MenuItem != null) result.MenuItem.isDefault = false;
                foreach (var go in result.Targets) go.SetActive(false);

                applied++;
                PassiveFilterLog.Info(
                    $"[MA] トグル '{ToggleName(result)}' を初期非表示へ補正しました（対象 {result.Targets.Count} 件）。");
            }

            PassiveFilterLog.Info(
                $"MA リアクティブ完了: 補正 {applied} / 複雑スキップ {complexSkip} / 除外 {exclSkip}。");

            // MA パスは summary を出さず（主パス PassiveFilterProcessor が出す）、診断のみ surface。
            PassiveFilterNdmfConsoleReporter.Report(ctx.AvatarRootTransform, diagnostics, null);
        }

        private static string ToggleName(ReactiveToggleResult result)
        {
            return result.Toggle != null ? result.Toggle.gameObject.name : "(unknown)";
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
    }
}
#endif
