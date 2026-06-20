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
        /// <summary>対象範囲（軸1）＝どの広さのトグルを見るか。</summary>
        public enum TargetScope
        {
            /// <summary>Expression メニューに出ているトグルだけを対象にする（既定・安全）。</summary>
            [InspectorName("メニューにあるトグルだけ（おすすめ）")]
            MenuTogglesOnly,

            /// <summary>メニュー外も含め検出した全トグルを対象にする（全防ぎ）。</summary>
            [InspectorName("すべてのトグル（メニュー外も含む）")]
            AllToggles,
        }

        /// <summary>補正する対象（軸2）＝後付けギミックだけか、元アバターのトグルまで触るか。</summary>
        public enum BaseToggleHandling
        {
            /// <summary>MA / VRCFury などで後付けしたトグルだけを補正（既定・安全。旧 option 2）。</summary>
            [InspectorName("あとから足したギミックだけ（安全）")]
            AddedOnly,

            /// <summary>元アバターに最初から入っているトグルも含めて補正する（裸化リスク。旧 option 3）。</summary>
            [InspectorName("全部（元アバターのトグルも含む）")]
            IncludeBase,
        }

        [SerializeField] private bool enable = true;
        [SerializeField] private TargetScope scope = TargetScope.MenuTogglesOnly;

        // 補正する対象（軸2）。既定 AddedOnly = base avatar 由来トグルを除外し後付け分のみ（旧 option 2）。
        [SerializeField] private BaseToggleHandling baseToggleHandling = BaseToggleHandling.AddedOnly;

        // 指定オブジェクト以下のサブツリーは対象外（GameObject / Component を許容）。
        [SerializeField] private List<UnityEngine.Object> exclusions = new List<UnityEngine.Object>();

        // 詳細ログ（検出・補正の内訳）を Unity コンソールへ出すか。既定 OFF。
        // 補正できなかった通知（NDMF ErrorReport）はこのフラグに関わらず常に表示する。
        [SerializeField] private bool verboseLogging = false;

        public bool Enabled => enable;
        public TargetScope Scope => scope;
        public BaseToggleHandling BaseHandling => baseToggleHandling;
        public IReadOnlyList<UnityEngine.Object> Exclusions => exclusions;

        /// <summary>詳細ログを Unity コンソールへ出すか（既定 OFF）。NDMF の補正不可通知は常時表示。</summary>
        public bool VerboseLogging => verboseLogging;

        /// <summary>軸2 を bool 視点で見るアダプタ。true = 元アバター由来トグルも含めて補正する。</summary>
        public bool IncludeBaseAvatarToggles => baseToggleHandling == BaseToggleHandling.IncludeBase;
    }
}
