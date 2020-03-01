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
	[HarmonyPatch(typeof(MoteMaker), "MakeMoodThoughtBubble")]
	[HarmonyPatch(new Type[] { typeof(Pawn), typeof(Thought) })]
	static class MoteMaker_MakeMoodThoughtBubble
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
