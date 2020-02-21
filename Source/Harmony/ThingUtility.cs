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
	[HarmonyPatch(typeof(ThingUtility), "DestroyOrPassToWorld")]
	static class ThingUtility_DestroyOrPassToWorld
	{
		public static bool Prefix(Thing t)
		{
			Corpse c = t as Corpse;
			if (c != null && ReturnToSender.Instance.GetSentCorpsePodsStorage().IsStoredCorpse(c))
			{
				return false;
			}

			return true;
		}
	}
}
