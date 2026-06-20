using System.Collections.Generic;
using System.Text;
using nadena.dev.ndmf;
using Sebanne.PassiveFilter.Editor.Core;
using UnityEngine;

namespace Sebanne.PassiveFilter.Editor.NDMF
{
    /// <summary>補正結果サマリ（統合カード末尾の集計行用）。主パスのみ渡す。</summary>
    internal sealed class PassiveFilterBuildSummary
    {
        public int Applied;
        public int MenuSkip;
        public int BaseSkip;
        public int ResolveSkip;
        public int ConflictSkip;

        public int TotalSkip => MenuSkip + BaseSkip + ResolveSkip + ConflictSkip;
    }

    /// <summary>
    /// 診断リスト（＋任意のサマリ）を NDMF Console（ErrorReport ウィンドウ）へ surface する。
    /// 全診断を 1 枚のカードへ統合する（種別見出し＋内訳＋末尾の補正/スキップ集計）。(layer, driver) で
    /// 重複排除し、種別ごとの内訳は <see cref="MaxListed"/> 件抜粋。severity は全種別の最大（NonFatal が
    /// 1 つでもあれば NonFatal、なければ Information）。診断が 1 件も無ければ何も報告しない（クリーンな
    /// ベイクでは窓を開かない。NDMF は Errors&gt;0 で窓を自動 open するため）。
    /// ベイクパス（Pass.Run）内から呼ぶこと（ErrorReport.CurrentReport が有効な文脈）。
    /// </summary>
    internal static class PassiveFilterNdmfConsoleReporter
    {
        private const int MaxListed = 3;

        public static void Report(
            Transform avatarRoot,
            IReadOnlyList<PassiveFilterDiagnostic> diagnostics,
            PassiveFilterBuildSummary summary)
        {
            int diagCount = diagnostics?.Count ?? 0;
            // 出すべき診断が無ければ NDMF Console を開かない（補正のみの正常ベイクでは沈黙し Console ログのみ）。
            if (diagCount == 0) return;

            // 種別単位で集約（(layer, driver) で重複排除し、入力順を保つ）。
            var byCategory = new Dictionary<DiagnosticCategory, List<PassiveFilterDiagnostic>>();
            var order = new List<DiagnosticCategory>();
            var seen = new HashSet<(DiagnosticCategory, string, string)>();
            foreach (var d in diagnostics)
            {
                var dedupKey = (d.Category, d.LayerName ?? string.Empty, d.Driver ?? string.Empty);
                if (!seen.Add(dedupKey)) continue;
                if (!byCategory.TryGetValue(d.Category, out var list))
                {
                    list = new List<PassiveFilterDiagnostic>();
                    byCategory[d.Category] = list;
                    order.Add(d.Category);
                }
                list.Add(d);
            }

            // severity = 全種別の最大（要対応=NonFatal が 1 つでもあれば黄、なければ青）。
            var severity = ErrorSeverity.Information;
            foreach (var category in order)
                if (SeverityOf(category) == ErrorSeverity.NonFatal) { severity = ErrorSeverity.NonFatal; break; }

            int uniqueTotal = 0;
            foreach (var category in order) uniqueTotal += byCategory[category].Count;

            // 1 枚のカードへ統合: 種別見出し → 内訳 → 末尾に補正/スキップ集計。
            var sb = new StringBuilder();
            foreach (var category in order)
            {
                sb.AppendLine($"【{CategoryHeading(category)}】");
                sb.AppendLine(BuildCategoryDetails(byCategory[category]));
            }
            if (summary != null)
                sb.Append($"── 補正 {summary.Applied} / スキップ {summary.TotalSkip}");

            var error = new PassiveFilterNdmfConsoleError(
                severity, $"Passive Filter: 注意トグル {uniqueTotal} 件", sb.ToString().TrimEnd());

            foreach (var category in order)
                AttachReferences(error, avatarRoot, byCategory[category]);

            ErrorReport.ReportError(error);
        }

        // =====================================================================
        // severity / 文言
        // =====================================================================

        private static ErrorSeverity SeverityOf(DiagnosticCategory category)
        {
            switch (category)
            {
                // 情報系（対応の余地が薄い / 稀）。
                case DiagnosticCategory.AmbiguousSkip:
                case DiagnosticCategory.PathUnresolved:
                    return ErrorSeverity.Information;
                // 要対応系（packed / conflict / 前提欠如 / 安全中止 / MA 複雑系）。Error は使わない（アップロード非ブロック）。
                default:
                    return ErrorSeverity.NonFatal;
            }
        }

        private static string CategoryHeading(DiagnosticCategory category)
        {
            switch (category)
            {
                case DiagnosticCategory.PackedUnsupported: return "packed・自動補正できず（除外 or 手動対応）";
                case DiagnosticCategory.AmbiguousSkip:     return "曖昧・スキップ";
                case DiagnosticCategory.ConflictSkip:      return "値矛盾・スキップ";
                case DiagnosticCategory.PrereqMissing:     return "前提不足・処理中止";
                case DiagnosticCategory.SafetyAbort:       return "安全のため補正中止";
                case DiagnosticCategory.PathUnresolved:    return "パス解決不可・スキップ";
                case DiagnosticCategory.MaReactiveSkip:    return "MA 複雑系・スキップ";
                default:                                   return "スキップ";
            }
        }

        private static string BuildCategoryDetails(List<PassiveFilterDiagnostic> list)
        {
            var sb = new StringBuilder();
            int shown = Mathf.Min(list.Count, MaxListed);
            for (int i = 0; i < shown; i++) sb.AppendLine(Describe(list[i]));
            if (list.Count > shown) sb.AppendLine($"ほか {list.Count - shown} 件");
            return sb.ToString().TrimEnd();
        }

        private static string Describe(PassiveFilterDiagnostic d)
        {
            var head = new List<string>();
            if (!string.IsNullOrEmpty(d.LayerName)) head.Add($"レイヤー '{d.LayerName}'");
            if (!string.IsNullOrEmpty(d.Label)) head.Add($"'{d.Label}'");
            if (!string.IsNullOrEmpty(d.Driver)) head.Add($"(param '{d.Driver}')");

            var sb = new StringBuilder("・");
            if (head.Count > 0) sb.Append(string.Join(" ", head));
            if (!string.IsNullOrEmpty(d.Reason))
            {
                if (head.Count > 0) sb.Append(" — ");
                sb.Append(d.Reason);
            }
            if (head.Count == 0 && string.IsNullOrEmpty(d.Reason)) sb.Append("(詳細なし)");
            if (d.TargetPaths != null && d.TargetPaths.Count > 0)
                sb.Append($" / 対象: {string.Join(", ", LeafNames(d.TargetPaths))}");
            return sb.ToString();
        }

        private static IEnumerable<string> LeafNames(List<string> paths)
        {
            const int Cap = 5;
            int count = 0;
            foreach (var path in paths)
            {
                if (count++ >= Cap) { yield return "…"; yield break; }
                if (string.IsNullOrEmpty(path)) { yield return "(root)"; continue; }
                int slash = path.LastIndexOf('/');
                yield return slash >= 0 && slash + 1 < path.Length ? path.Substring(slash + 1) : path;
            }
        }

        private static void AttachReferences(
            PassiveFilterNdmfConsoleError error, Transform avatarRoot, List<PassiveFilterDiagnostic> list)
        {
            int shown = Mathf.Min(list.Count, MaxListed);
            for (int i = 0; i < shown; i++)
            {
                var d = list[i];
                if (d.ContextObjects != null)
                    foreach (var obj in d.ContextObjects)
                        if (obj != null) error.AddReference(ObjectRegistry.GetReference(obj));

                if (d.TargetPaths != null && avatarRoot != null)
                    foreach (var path in d.TargetPaths)
                    {
                        var tr = string.IsNullOrEmpty(path) ? avatarRoot : avatarRoot.Find(path);
                        if (tr != null) error.AddReference(ObjectRegistry.GetReference(tr.gameObject));
                    }
            }
        }
    }
}
