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
    [HarmonyPatch(typeof(TransportPodsArrivalAction_LandInSpecificCell), "Arrived")]
    class TransportPodsArrivalAction_LandInSpecificCell_Arrived
    {
        public static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> instructions, ILGenerator ilg)
        {
            Label branchDetour = ilg.DefineLabel();
            Label branchResume = ilg.DefineLabel();
            var instructionsList = new List<CodeInstruction>(instructions);

            for (int i = 0; i < instructionsList.Count; ++i)
            {
                if (instructionsList[i].opcode == OpCodes.Ldstr && instructionsList[i].operand.ToString() == "MessageTransportPodsArrived")
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Call, typeof(Utilities).GetMethod("HasCorpsePodInfo"));
                    yield return new CodeInstruction(OpCodes.Brtrue, branchDetour);
                    yield return instructionsList[i];
                    yield return new CodeInstruction(OpCodes.Br, branchResume);
                    CodeInstruction detourStart = new CodeInstruction(OpCodes.Ldstr, "RTS_MessageTransportPodsArrived");
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