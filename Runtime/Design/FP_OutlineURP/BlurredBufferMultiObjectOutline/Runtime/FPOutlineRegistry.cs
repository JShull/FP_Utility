// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility
{
    using System.Collections.Generic;

    /// <summary>
    /// Shared runtime registry consumed by the URP outline renderer feature.
    /// </summary>
    public static class FPOutlineRegistry
    {
        private static readonly List<FPOutlineTarget> Targets = new List<FPOutlineTarget>();

        public static int Count => Targets.Count;

        public static void Register(FPOutlineTarget target)
        {
            if (!target || Targets.Contains(target))
                return;

            Targets.Add(target);
        }

        public static void Unregister(FPOutlineTarget target)
        {
            if (!target)
                return;

            Targets.Remove(target);
        }

        public static void GetActiveTargets(List<FPOutlineTarget> results)
        {
            results.Clear();

            for (int i = Targets.Count - 1; i >= 0; i--)
            {
                FPOutlineTarget target = Targets[i];
                if (!target)
                {
                    Targets.RemoveAt(i);
                    continue;
                }

                if (target.isActiveAndEnabled && target.HasRenderableRenderers)
                    results.Add(target);
            }
        }
    }
}
