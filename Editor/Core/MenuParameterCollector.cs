using System.Collections.Generic;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Sebanne.PassiveFilter.Editor.Core
{
    /// <summary>
    /// VRCExpressionsMenu を再帰的に辿り、メニューに出ているパラメータ名を集める。
    /// 「メニュー由来トグルのみ」スコープのフィルタに使う。
    /// </summary>
    internal static class MenuParameterCollector
    {
        private const int MaxDepth = 16;

        public static HashSet<string> Collect(VRCExpressionsMenu rootMenu)
        {
            var result = new HashSet<string>();
            Walk(rootMenu, result, new HashSet<VRCExpressionsMenu>(), 0);
            return result;
        }

        private static void Walk(
            VRCExpressionsMenu menu,
            HashSet<string> acc,
            HashSet<VRCExpressionsMenu> visited,
            int depth)
        {
            if (menu == null || depth > MaxDepth || !visited.Add(menu)) return;
            if (menu.controls == null) return;

            foreach (var control in menu.controls)
            {
                if (control == null) continue;

                if (control.parameter != null && !string.IsNullOrEmpty(control.parameter.name))
                    acc.Add(control.parameter.name);

                if (control.subParameters != null)
                {
                    foreach (var sub in control.subParameters)
                        if (sub != null && !string.IsNullOrEmpty(sub.name)) acc.Add(sub.name);
                }

                if (control.subMenu != null)
                    Walk(control.subMenu, acc, visited, depth + 1);
            }
        }
    }
}
