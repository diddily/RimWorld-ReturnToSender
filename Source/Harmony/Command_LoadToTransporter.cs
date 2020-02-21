using Harmony;
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
	[HarmonyPatch(typeof(Command_LoadToTransporter), "ProcessInput")]
	static class Command_LoadToTransporter_ProcessInput
	{
		private static MethodInfo get_WindowStackMethod = typeof(Find).GetMethod("get_WindowStack");
		private static MethodInfo AllOrNoneCorpsePodsMethod = typeof(Command_LoadToTransporter_ProcessInput).GetMethod("AllOrNoneCorpsePods");
		private static FieldInfo transportersField = AccessTools.Field(typeof(Command_LoadToTransporter), "transporters");

		public static bool AllOrNoneCorpsePods(List<CompTransporter> transporters)
		{
			bool valid = !Utilities.HasCorpsePodTransporters(transporters) || transporters.All(ct => ct.parent is Building_CorpsePod);
			if (!valid)
			{
				Messages.Message("RTS_MessageTransporterTypesInvalid".Translate(), new LookTargets(transporters.Select(ct => ct.parent)), MessageTypeDefOf.RejectInput, false);
			}

			return valid;
		}

		public static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> instructions, ILGenerator ilg)
		{
			Label get_WindowsStackLabel = ilg.DefineLabel();
			var instructionsList = new List<CodeInstruction>(instructions);
			for (int i = 0; i < instructionsList.Count; ++i)
			{
				if (instructionsList[i].opcode == OpCodes.Call && instructionsList[i].operand == get_WindowStackMethod)
				{
					instructionsList[i].labels.Add(get_WindowsStackLabel);

					yield return new CodeInstruction(OpCodes.Ldarg_0);
					yield return new CodeInstruction(OpCodes.Ldfld, transportersField);
					yield return new CodeInstruction(OpCodes.Call, AllOrNoneCorpsePodsMethod);
					yield return new CodeInstruction(OpCodes.Brtrue, get_WindowsStackLabel);
					yield return new CodeInstruction(OpCodes.Ret);
				}

				yield return instructionsList[i];
			}
		}
	}
}
