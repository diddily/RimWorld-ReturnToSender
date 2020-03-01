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
using System.Text;
using Verse;

namespace ReturnToSender.Harmony
{
	[HarmonyPatch(typeof(Projectile_Explosive), "Impact")]
	static class Projectile_Explosive_Impact
	{
		static MethodInfo explodeMethod = AccessTools.Method(typeof(Projectile_Explosive), "Explode");

		public static bool Prefix(Projectile_Explosive __instance)
		{
			if (ColonySimulation.CurrentSimulation != null)
			{
				if (Rand.Chance(0.75f))
				{
					explodeMethod.Invoke(__instance, null);
				}
				else
				{
					__instance.Destroy(DestroyMode.Vanish);
				}
				return false;
			}

			return true;
		}
	}
}
