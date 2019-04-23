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
    [HarmonyPatch(typeof(Pawn), "DropAndForbidEverything")]
    static class Pawn_DropAndForbidEverything
    {
        public static bool Prefix()
        {
            ColonySimulation currentSimulation = ColonySimulation.CurrentSimulation;
            if (currentSimulation != null && currentSimulation.DontDropAndForbid)
            {
                return false;
            }
            return true;
        }
    }
}
