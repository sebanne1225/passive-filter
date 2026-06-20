using System.Collections.Generic;

namespace Sebanne.PassiveFilter.Editor.Core
{
    /// <summary>診断カテゴリ。NDMF Console での severity と表示文言を決める。</summary>
    internal enum DiagnosticCategory
    {
        /// <summary>(B) packed 構造（1 param が複数対象を多重切替）のため安全補正不可。</summary>
        PackedUnsupported,
        /// <summary>方向矛盾 / threshold 同一 / hidden 値不明 など曖昧でスキップ。</summary>
        AmbiguousSkip,
        /// <summary>同一 param で複数トグルの hidden 値が矛盾しスキップ。</summary>
        ConflictSkip,
        /// <summary>VRCAvatarDescriptor など前提が見つからず処理中止。</summary>
        PrereqMissing,
        /// <summary>base 捕捉失敗など、安全のため補正を中止。</summary>
        SafetyAbort,
        /// <summary>対象パスを解決できずスキップ。</summary>
        PathUnresolved,
        /// <summary>MA リアクティブの複雑系をスキップ。</summary>
        MaReactiveSkip,
    }

    /// <summary>
    /// 1 件の診断。scanner / processor が積み、Reporter がカテゴリ集約して NDMF Console へ surface する。
    /// scanner 層は scene 参照を持たないため、対象は <see cref="TargetPaths"/>（avatar root からの相対パス）で運ぶ。
    /// MA パスなど実オブジェクトを持てる場合は <see cref="ContextObjects"/> に直接入れる（クリックでジャンプ可能に）。
    /// </summary>
    internal sealed class PassiveFilterDiagnostic
    {
        public DiagnosticCategory Category;
        public string LayerName;
        public string Driver;

        /// <summary>レイヤー / param で識別できない対象の名前（例: MA トグル名）。</summary>
        public string Label;

        public string Reason;

        /// <summary>avatar root からの相対パス。Reporter が解決して ObjectReference を添付する。null 可。</summary>
        public List<string> TargetPaths;

        /// <summary>実オブジェクト（MA トグルの GameObject 等）の直接参照。null 可。</summary>
        public List<UnityEngine.Object> ContextObjects;
    }
}
