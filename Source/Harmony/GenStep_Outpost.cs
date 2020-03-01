#if VERSION_1_0
using Harmony;
#else
using HarmonyLib;
#endif
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
			if (ColonySimulation.Verbose)
			{
				Log.Message("Outpost: " + string.Join(",", map.mapPawns.AllPawnsSpawned.Select(p => p.ToString()).ToArray()));
			}
			ColonySimulation.SetMaxMapLevel(ColonySimulation.MapLevel.Outpost);
			
		}
	}

	[HarmonyPatch(typeof(GenStep_Scatterer), "Generate")]
	class GenStep_Scatterer_Generate
	{
		public static void Postfix(ref GenStep_Scatterer __instance, Map map, GenStepParams parms)
		{
			if (__instance is GenStep_Settlement)
			{ 
				if (ColonySimulation.Verbose)
				{
					Log.Message("Settlement: " + string.Join(",", map.mapPawns.AllPawnsSpawned.Select(p => p.ToString()).ToArray()));
				}
				ColonySimulation.SetMaxMapLevel(ColonySimulation.MapLevel.Settlement);
			}
		}
	}
}
