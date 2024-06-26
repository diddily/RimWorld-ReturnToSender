﻿#if VERSION_1_0
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
	[HarmonyPatch(typeof(MapDrawer), "MapMeshDirty")]
	[HarmonyPatch(new Type[] { typeof(IntVec3), typeof(ulong), typeof(bool), typeof(bool) })]
	static class MapDrawer_MapMeshDirty
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
