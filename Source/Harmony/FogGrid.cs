using Harmony;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace ReturnToSender.Harmony
{
    [HarmonyPatch(typeof(FogGrid), "Notify_FogBlockerRemoved")]
    static class FogGrid_Notify_FogBlockerRemoved
    {
        public static bool Prefix()
        {
            if (ColonySimulation.CurrentSimulation != null)
            {
                return false;
            }

            return true;
        }
    }
}
