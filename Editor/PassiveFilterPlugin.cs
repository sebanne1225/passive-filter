using nadena.dev.ndmf;
using Sebanne.PassiveFilter.Editor.Core;

[assembly: ExportsPlugin(typeof(Sebanne.PassiveFilter.Editor.PassiveFilterPlugin))]

namespace Sebanne.PassiveFilter.Editor
{
    /// <summary>
    /// Passive Filter の NDMF プラグイン。Modular Avatar / VRCFury がベイクし終わった後
    /// （Transforming.AfterPlugin("nadena.dev.modular-avatar")）に走り、ベイク済 FX の
    /// トグルを検出して初期状態を hidden 側へ倒す。
    /// </summary>
    public sealed class PassiveFilterPlugin : Plugin<PassiveFilterPlugin>
    {
        public override string DisplayName => "Passive Filter";
        public override string QualifiedName => "com.sebanne.passive-filter";

        protected override void Configure()
        {
            // 早期パス: MA がアニメータを merge する前（Generating < Transforming）に base FX の
            // パラメータ名を捕捉する。遅延パスの option 2（base 由来トグル除外）に使う。
            InPhase(BuildPhase.Generating)
                .Run("Passive Filter: capture base FX parameters", PassiveFilterProcessor.CaptureBaseParameters);

            // 遅延パス: MA / VRCFury のベイク後にトグルを検出し初期状態を hidden 側へ倒す。
            InPhase(BuildPhase.Transforming)
                .AfterPlugin("nadena.dev.modular-avatar")
                .Run("Passive Filter: fold toggles to hidden", PassiveFilterProcessor.Run);
        }
    }
}
