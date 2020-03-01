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
	[HarmonyPatch(typeof(RestUtility), "CurrentBed")]
	static class RestUtility_CurrentBed
	{
		public static void Postfix(ref Building_Bed __result, Pawn p)
		{
			if (__result == null && ColonySimulation.CurrentSimulation != null)
			{
				__result = ColonySimulation.CurrentSimulation.GetPawnBed(p);
			}
		}
	}
}
