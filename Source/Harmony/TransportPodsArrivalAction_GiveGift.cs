using Harmony;
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
	[HarmonyPatch(typeof(TransportPodsArrivalAction_GiveGift), "GetFloatMenuOptions")]
	class TransportPodsArrivalAction_GiveGift_GetFloatMenuOptions
	{
		public static bool Prefix(ref IEnumerable<FloatMenuOption> __result, CompLaunchable representative, IEnumerable<IThingHolder> pods, SettlementBase settlement)
		{
			if (settlement.Faction != Faction.OfPlayer && Utilities.HasCorpsePod(pods))
			{
				__result = TransportPodsArrivalActionUtility.GetFloatMenuOptions<TransportPodsArrivalAction_GiveGift>(() => TransportPodsArrivalAction_GiveGift.CanGiveGiftTo(pods, settlement), () => new TransportPodsArrivalAction_GiveGift(settlement), "RTS_GiveBodiesViaCorpsePods".Translate(settlement.Faction.Name, FactionGiftUtility.GetGoodwillChange(pods, settlement).ToStringWithSign()), representative, settlement.Tile);
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(TransportPodsArrivalAction_GiveGift), "CanGiveGiftTo")]
	class TransportPodsArrivalAction_GiveGift_CanGiveGiftTo
	{
		public static void Postfix(ref FloatMenuAcceptanceReport __result, IEnumerable<IThingHolder> pods, SettlementBase settlement)
		{
			if (!__result && Utilities.HasCorpsePod(pods))
			{
				__result = settlement != null && settlement.Spawned && settlement.Faction != null && settlement.Faction != Faction.OfPlayer && settlement.Faction.def.humanlikeFaction && !settlement.HasMap;
			}
		}
	}
}
