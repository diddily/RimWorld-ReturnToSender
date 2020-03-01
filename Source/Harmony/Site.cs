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
	[HarmonyPatch(typeof(Site), "PostMapGenerate")]
	class Site_PostMapGenerate
	{
		public static void Postfix(ref Site __instance)
		{
			if (ColonySimulation.Verbose)
			{
				Log.Message("Site: " + string.Join(",", __instance.Map.mapPawns.AllPawnsSpawned.Select(p => p.ToString()).ToArray()));
			}

			ColonySimulation.SetMaxMapLevel(ColonySimulation.MapLevel.Site);
		}
	}
}
