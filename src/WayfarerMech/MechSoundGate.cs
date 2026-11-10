using System;
using HarmonyLib;
using UnityEngine;

namespace WayfarerMech
{
    /// <summary>
    /// DamageBlock plays a hit and a step sound for every broken cell. A wide drill pulse can
    /// break many cells in one frame, so native one-shot sounds are muted only for the duration
    /// of our synchronous terrain loop. Drops, health and particle prefabs remain untouched.
    /// </summary>
    public static class NativeDrillSound
    {
        public static bool Suppress;
    }

    [HarmonyPatch(typeof(Sound), "Play", new[]
    {
        typeof(string), typeof(Vector2), typeof(bool), typeof(bool), typeof(Transform),
        typeof(float), typeof(float), typeof(bool), typeof(bool)
    })]
    internal static class SuppressNativeDrillSound
    {
        private static bool Prefix() => !NativeDrillSound.Suppress;
    }
}
