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

using ReturnToSender.Buildings;

namespace ReturnToSender.Harmony
{
    [HarmonyPatch(typeof(FactionGiftUtility), "GetGoodwillChange")]
    [HarmonyPatch(new Type[] { typeof(IEnumerable<IThingHolder>), typeof(SettlementBase) })]
    public static class FactionGiftUtility_GetGoodwillChange
    {
        private static float GetFactor(Thing t)
        {
            switch (t.GetRotStage())
            {
                case RotStage.Dessicated:
                    return -0.5f;
                case RotStage.Rotting:
                    return -3.0f;
                case RotStage.Fresh:
                    return -1.0f;
            }
            return 0.0f;
        }

        public static void Postfix(ref int __result, IEnumerable<IThingHolder> pods)
        {
            if (Utilities.HasCorpsePod(pods))
            {
                __result = (int) Math.Round(__result * pods.SelectMany(ith => ith.GetDirectlyHeldThings()).Average(t => GetFactor(t)));
            }
        }
    }
    [HarmonyPatch(typeof(FactionGiftUtility), "GiveGift")]
    [HarmonyPatch(new Type[] { typeof(List<ActiveDropPodInfo>), typeof(SettlementBase) })]
    public static class FactionGiftUtility_GiveGift
    {
        private static MethodInfo SendGiftNotAppreciatedMessageMethod = AccessTools.Method(typeof(FactionGiftUtility), "SendGiftNotAppreciatedMessage");
        private static MethodInfo SendCorpsesNotAppreciatedMessageMethod = AccessTools.Method(typeof(FactionGiftUtility_GiveGift), "SendCorpsesNotAppreciatedMessage");

        private static void SendCorpsesNotAppreciatedMessage(Faction giveTo, GlobalTargetInfo lookTarget)
        {
            Messages.Message("RTS_MessageGiftGivenButNotAppreciated".Translate(giveTo.Name).CapitalizeFirst(), lookTarget, MessageTypeDefOf.NegativeEvent, true);

        }

        public static bool Prefix(List<ActiveDropPodInfo> pods, SettlementBase giveTo)
        {
            foreach (ActiveCorpsePodInfo pod in pods.OfType<ActiveCorpsePodInfo>().Cast<ActiveCorpsePodInfo>())
            {
                ReturnToSender.Instance.GetSentCorpsePodsStorage().AddPodToTile(giveTo.Tile, pod);
            }

            return true;
        }

        public static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> instructions, ILGenerator ilg)
        {
            Label branch1Detour = ilg.DefineLabel();
            Label branch1Resume = ilg.DefineLabel();
            Label branch2Detour = ilg.DefineLabel();
            Label branch2Resume = ilg.DefineLabel();
            var instructionsList = new List<CodeInstruction>(instructions);

            for (int i = 0; i < instructionsList.Count; ++i)
            {
                if (instructionsList[i].opcode == OpCodes.Ldstr && instructionsList[i].operand.ToString() == "GoodwillChangedReason_ReceivedGift")
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, typeof(Utilities).GetMethod("HasCorpsePodInfo"));
                    yield return new CodeInstruction(OpCodes.Brtrue, branch1Detour);
                    yield return instructionsList[i];
                    yield return new CodeInstruction(OpCodes.Br, branch1Resume);
                    CodeInstruction detourStart = new CodeInstruction(OpCodes.Ldstr, "RTS_GoodwillChangedReason_ReceivedGift");
                    detourStart.labels.Add(branch1Detour);
                    yield return detourStart;
                    instructionsList[i + 1].labels.Add(branch1Resume);
                    i++;
                }
                if (instructionsList[i].opcode == OpCodes.Call && instructionsList[i].operand == SendGiftNotAppreciatedMessageMethod)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, typeof(Utilities).GetMethod("HasCorpsePodInfo"));
                    yield return new CodeInstruction(OpCodes.Brtrue, branch2Detour);
                    yield return instructionsList[i];
                    yield return new CodeInstruction(OpCodes.Br, branch2Resume);
                    CodeInstruction detourStart = new CodeInstruction(OpCodes.Call, SendCorpsesNotAppreciatedMessageMethod);
                    detourStart.labels.Add(branch2Detour);
                    yield return detourStart;
                    instructionsList[i + 1].labels.Add(branch2Resume);
                    i++;
                }
                yield return instructionsList[i];
            }
        }
    }
}
