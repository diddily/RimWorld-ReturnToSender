using Harmony;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace ReturnToSender.Harmony
{
    [HarmonyPatch(typeof(ActiveDropPodInfo), "ExposeData")]
    class ActiveDropPodInfo_ExposeData
    {
        public static void Postfix(ActiveDropPodInfo __instance)
        {
            ActiveCorpsePodInfo acpi = __instance as ActiveCorpsePodInfo;
            if (acpi != null)
            {
                acpi.PostPostExpose();
            }
        }
    }
}
