using UnityEngine;

namespace Sebanne.PassiveFilter.Editor.Core
{
    /// <summary>NDMF コンソール（Unity Console）向けログ。接頭辞で集約。</summary>
    internal static class PassiveFilterLog
    {
        private const string Prefix = "[Passive Filter] ";

        public static void Info(string message) => Debug.Log(Prefix + message);
        public static void Warn(string message) => Debug.LogWarning(Prefix + message);
        public static void Error(string message) => Debug.LogError(Prefix + message);
    }
}
