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
using System.Text;
using Verse;

namespace ReturnToSender.Harmony
{
	[HarmonyPatch(typeof(MapGenerator), "GenerateMap")]
	class MapGenerator_GenerateMap
	{
		public static void Postfix(ref Map __result)
		{
			ColonySimulation sim = new ColonySimulation(__result);
			sim.DoSimulation();
		}
	}
}
