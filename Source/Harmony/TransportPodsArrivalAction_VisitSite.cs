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
	[HarmonyPatch(typeof(TransportPodsArrivalAction_VisitSite), "GetFloatMenuOptions")]
	class TransportPodsArrivalAction_GetFloatMenuOptions
	{
		public static bool ReturnTrue()
		{
			return true;
		}
		public static bool Prefix(ref IEnumerable<FloatMenuOption> __result, CompLaunchable representative, IEnumerable<IThingHolder> pods, Site site)
		{
			if (site.Faction != Faction.OfPlayer && Utilities.HasCorpsePod(pods))
			{
				__result = TransportPodsArrivalActionUtility.GetFloatMenuOptions<TransportPodsArrivalAction_VisitSite>(() => ReturnTrue(), () => new TransportPodsArrivalAction_VisitSite(site, PawnsArrivalModeDefOf.CenterDrop), "RTS_SendBodiesToLocation".Translate(), representative, site.Tile);
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(TransportPodsArrivalAction_VisitSite), "CanVisit")]
	class TransportPodsArrivalAction_VisitSite_CanVisit
	{
		public static void Postfix(ref FloatMenuAcceptanceReport __result, IEnumerable<IThingHolder> pods, Site site)
		{
			if (!__result)
			{
				__result = Utilities.HasCorpsePod(pods);
			}
		}
	}

	[HarmonyPatch(typeof(TransportPodsArrivalAction_VisitSite), "Arrived")]
	class TransportPodsArrivalAction_VisitSite_Arrived
	{
		public static bool Prefix(List<ActiveDropPodInfo> pods, int tile)
		{
			if (Utilities.HasCorpsePodInfo(pods))
			{
				Thing lookTarget = TransportPodsArrivalActionUtility.GetLookTarget(pods);
				Messages.Message("RTS_MessageTransportPodsArrived".Translate(), lookTarget, MessageTypeDefOf.TaskCompletion, true);
				foreach (ActiveCorpsePodInfo pod in pods.OfType<ActiveCorpsePodInfo>().Cast<ActiveCorpsePodInfo>())
				{
					ReturnToSender.Instance.GetSentCorpsePodsStorage().AddPodToTile(tile, pod);
				}
				return false;
			}
			return true;
		}
	}
}
