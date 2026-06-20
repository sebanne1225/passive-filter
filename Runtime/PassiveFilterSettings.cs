using System.Collections.Generic;
using nadena.dev.ndmf;
using UnityEngine;

namespace Sebanne.PassiveFilter
{
    /// <summary>
    /// アバタールートに付ける設定コンポーネント。ビルド時に NDMF プラグインが読み取り、
    /// トグルギミックの初期状態を hidden 側へ倒す。
    /// <see cref="INDMFEditorOnly"/>（= VRChat の IEditorOnly）なのでアップロード時に strip される。
    /// </summary>
    [AddComponentMenu("Sebanne/Passive Filter")]
    [DisallowMultipleComponent]
    public sealed class PassiveFilterSettings : MonoBehaviour, INDMFEditorOnly
    {
        /// <summary>対象範囲。</summary>
        public enum TargetScope
        {
            /// <summary>Expression メニューに出ているトグルだけを対象にする（既定・安全）。</summary>
            MenuTogglesOnly,

            /// <summary>メニュー外も含め検出した全トグルを対象にする（全防ぎ）。</summary>
            AllToggles,
        }

        [SerializeField] private bool enable = true;
        [SerializeField] private TargetScope scope = TargetScope.MenuTogglesOnly;

        // 指定オブジェクト以下のサブツリーは対象外（GameObject / Component を許容）。
        [SerializeField] private List<UnityEngine.Object> exclusions = new List<UnityEngine.Object>();

        // option 3 用 seam（3-ready）。既定 false = base avatar 由来トグルを除外し MA 追加分のみ対象（option 2）。
        // true にすると base FX のトグルも対象に含める（将来 opt-in。現状 Inspector には未公開）。
        [SerializeField] private bool includeBaseAvatarToggles = false;

        public bool Enabled => enable;
        public TargetScope Scope => scope;
        public IReadOnlyList<UnityEngine.Object> Exclusions => exclusions;
        public bool IncludeBaseAvatarToggles => includeBaseAvatarToggles;
    }
}
