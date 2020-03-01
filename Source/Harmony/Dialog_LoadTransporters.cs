#if VERSION_1_0
using Harmony;
#else
using HarmonyLib;
#endif
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
	[HarmonyPatch(typeof(Dialog_LoadTransporters), "AddToTransferables")]
	static class Dialog_LoadTransporters_AddToTransferables
	{
		public static bool Prefix(Thing t, List<CompTransporter> ___transporters)
		{
			if (Utilities.HasCorpsePodTransporters(___transporters))
			{
				Corpse c = t as Corpse;
				if (c != null)
				{
					if (c.InnerPawn.RaceProps.Humanlike)
					{
						return true;
					}
				}
				return false;
			}
			return true;
		}
	}
}
