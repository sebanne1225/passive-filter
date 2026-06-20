using UnityEngine;

namespace Sebanne.PassiveFilter.Editor.Core
{
    /// <summary>NDMF コンソール（Unity Console）向けログ。接頭辞で集約。</summary>
    internal static class PassiveFilterLog
    {
        private const string Prefix = "[Passive Filter] ";

        /// <summary>詳細ログ（Info）を出すか。各ビルドパス開始時に設定から再セットされる。既定 OFF。</summary>
        public static bool Verbose;

        public static void Info(string message)
        {
            if (!Verbose) return;
            Debug.Log(Prefix + message);
        }

        public static void Warn(string message) => Debug.LogWarning(Prefix + message);
        public static void Error(string message) => Debug.LogError(Prefix + message);
    }
}
