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

        public bool Enabled => enable;
        public TargetScope Scope => scope;
        public IReadOnlyList<UnityEngine.Object> Exclusions => exclusions;
    }
}
