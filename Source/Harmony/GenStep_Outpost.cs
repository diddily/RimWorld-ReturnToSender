using Harmony;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Verse;

namespace ReturnToSender.Harmony
{
	[HarmonyPatch(typeof(GenStep_Outpost), "Generate")]
	class GenStep_Outpost_Generate
	{
		public static void Postfix(ref GenStep_Outpost __instance, Map map, GenStepParams parms)
		{
			Log.Message("Outpost: " + string.Join(",", map.mapPawns.AllPawnsSpawned.Select(p => p.ToString()).ToArray()));
			ColonySimulation sim = new ColonySimulation(map, false);
			sim.DoSimulation();
		}
	}

	[HarmonyPatch(typeof(GenStep_Settlement), "Generate")]
	class GenStep_Settlement_Generate
	{
		public static void Postfix(ref GenStep_Settlement __instance, Map map, GenStepParams parms)
		{
			Log.Message("Settlement: " + string.Join(",", map.mapPawns.AllPawnsSpawned.Select(p => p.ToString()).ToArray()));
			ColonySimulation sim = new ColonySimulation(map, true);
			sim.DoSimulation();
		}
	}
}
