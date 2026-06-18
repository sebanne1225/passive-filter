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
            InPhase(BuildPhase.Transforming)
                .AfterPlugin("nadena.dev.modular-avatar")
                .Run("Passive Filter: fold toggles to hidden", PassiveFilterProcessor.Run);
        }
    }
}
