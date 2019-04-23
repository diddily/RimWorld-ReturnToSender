using Harmony;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Verse;

using ReturnToSender.Buildings;

namespace ReturnToSender.Harmony
{
    [HarmonyPatch(typeof(DropPodUtility), "MakeDropPodAt")]
    class DropPodUtility_MakeDropPodAt
    {
        public static bool Prefix(IntVec3 c, Map map, ActiveDropPodInfo info)
        {
            ActiveCorpsePodInfo acpi = info as ActiveCorpsePodInfo;
            if (acpi != null)
            {
                ActiveCorpsePod activeCorpsePod = (ActiveCorpsePod)ThingMaker.MakeThing(RTS_DefOf.RTS_ActiveCorpsePod, acpi.stuff);
                activeCorpsePod.Contents = info;
                SkyfallerMaker.SpawnSkyfaller(RTS_DefOf.RTS_CorpsePodIncoming, activeCorpsePod, c, map);
                return false;
            }
            return true;
        }
    }
}
