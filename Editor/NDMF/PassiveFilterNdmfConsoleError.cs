using System;
using System.Collections.Generic;
using nadena.dev.ndmf;
using nadena.dev.ndmf.localization;

namespace Sebanne.PassiveFilter.Editor.NDMF
{
    /// <summary>
    /// NDMF Console（ErrorReport ウィンドウ）へ日本語固定文字列を出すための SimpleError 派生。
    /// SimpleError は localization key 前提（未登録なら &lt;key&gt; 表示）のため、空 Localizer ＋ FormatXxx
    /// の override で翻訳解決をバイパスし、固定文字列を直接返す（avatar-audio-safety-guard 準拠）。
    /// ToMessage は override 不要: SimpleError の既定実装が FormatTitle/FormatDetails を使うため、
    /// それらを override すれば Unity Console 側の "[NDMF] Error Reported:" 行も日本語になる。
    /// </summary>
    internal sealed class PassiveFilterNdmfConsoleError : SimpleError
    {
        private static readonly Localizer EmptyLocalizer =
            new Localizer("en-US", () => new List<(string, Func<string, string>)>());

        private readonly ErrorSeverity _severity;
        private readonly string _title;
        private readonly string _details;

        public PassiveFilterNdmfConsoleError(ErrorSeverity severity, string title, string details)
        {
            _severity = severity;
            _title = title ?? string.Empty;
            _details = details;
        }

        public override Localizer Localizer => EmptyLocalizer;

        public override string TitleKey => "passive-filter.ndmf.console";

        public override ErrorSeverity Severity => _severity;

        public override string FormatTitle() => _title;

        public override string FormatDetails() => string.IsNullOrEmpty(_details) ? null : _details;

        public override string FormatHint() => null;
    }
}
