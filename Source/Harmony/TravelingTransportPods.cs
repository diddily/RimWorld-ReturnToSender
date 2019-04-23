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
    [HarmonyPatch(typeof(TravelingTransportPods), "DoArrivalAction")]
    class TravelingTransportPods_DoArrivalAction
    {
        public static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> instructions, ILGenerator ilg)
        {
            Label branchDetour = ilg.DefineLabel();
            Label branchResume = ilg.DefineLabel();
            var instructionsList = new List<CodeInstruction>(instructions);

            for (int i = 0; i < instructionsList.Count; ++i)
            {
                if (instructionsList[i].opcode == OpCodes.Ldstr && instructionsList[i].operand.ToString() == "MessageTransportPodsArrivedAndLost")
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(TravelingTransportPods), "pods"));
                    yield return new CodeInstruction(OpCodes.Call, typeof(Utilities).GetMethod("HasCorpsePodInfo"));
                    yield return new CodeInstruction(OpCodes.Brtrue, branchDetour);
                    yield return instructionsList[i];
                    yield return new CodeInstruction(OpCodes.Br, branchResume);
                    CodeInstruction detourStart = new CodeInstruction(OpCodes.Ldstr, "RTS_MessageTransportPodsArrivedAndLost");
                    detourStart.labels.Add(branchDetour);
                    yield return detourStart;
                    instructionsList[i + 1].labels.Add(branchResume);
                    i++;
                }
                yield return instructionsList[i];
            }
        }
    }
}
