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

using ReturnToSender.Buildings;

namespace ReturnToSender.Harmony
{
	[HarmonyPatch(typeof(CompLaunchable), "TryLaunch")]
	class CompLaunchable_TryLaunch
	{
		public static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> instructions, ILGenerator ilg)
		{
			MethodInfo CompTransporter_GetDirectlyHeldThingsMethod = typeof(CompTransporter).GetMethod("GetDirectlyHeldThings");
			MethodInfo ThingMaker_MakeThingMethod = typeof(ThingMaker).GetMethod("MakeThing");
			FieldInfo ThingDefOf_ActiveDropPodField = typeof(ThingDefOf).GetField("ActiveDropPod");
			FieldInfo ThingDefOf_DropPodLeaving = typeof(ThingDefOf).GetField("DropPodLeaving");
			FieldInfo RTS_DefOf_ActiveCorpsePodField = typeof(RTS_DefOf).GetField("RTS_ActiveCorpsePod");
			FieldInfo RTS_DefOf_CorpsePodLeaving = typeof(RTS_DefOf).GetField("RTS_CorpsePodLeaving");
			FieldInfo CompTransporter_ParentField = typeof(CompTransporter).GetField("parent");
			ConstructorInfo ActiveDropPodInfoCtor = AccessTools.Constructor(typeof(RimWorld.ActiveDropPodInfo));
			Label branch1Detour = ilg.DefineLabel();
			Label branch1Resume = ilg.DefineLabel();
			Label branch2Detour = ilg.DefineLabel();
			Label branch2Resume = ilg.DefineLabel();
			Label branch3Detour = ilg.DefineLabel();
			Label branch3Resume = ilg.DefineLabel();
			int foundCount = 0;
			object compTransporterLocal = null;
			var instructionsList = new List<CodeInstruction>(instructions);

			for (int i = 0; i < instructionsList.Count; ++i)
			{
				if (instructionsList[i].opcode == OpCodes.Callvirt && instructionsList[i].operand == CompTransporter_GetDirectlyHeldThingsMethod &&
					instructionsList[i - 1].opcode == OpCodes.Ldloc_S)
				{
					compTransporterLocal = instructionsList[i - 1].operand;
				}

				if (compTransporterLocal != null)
				{
					if (instructionsList[i].opcode == OpCodes.Ldsfld && instructionsList[i].operand == ThingDefOf_ActiveDropPodField)
					{
						foundCount++;
						yield return new CodeInstruction(OpCodes.Ldloc_S, compTransporterLocal);
						yield return new CodeInstruction(OpCodes.Ldfld, CompTransporter_ParentField);
						yield return new CodeInstruction(OpCodes.Isinst, typeof(Building_CorpsePod));
						yield return new CodeInstruction(OpCodes.Brtrue, branch1Detour);
					}

					if (instructionsList[i].opcode == OpCodes.Call && instructionsList[i].operand == ThingMaker_MakeThingMethod)
					{
						foundCount++;
						instructionsList[i].labels.Add(branch1Resume);
						yield return new CodeInstruction(OpCodes.Br, branch1Resume);
						CodeInstruction detourStart = new CodeInstruction(OpCodes.Ldsfld, RTS_DefOf_ActiveCorpsePodField);
						detourStart.labels.Add(branch1Detour);
						yield return detourStart;
						yield return new CodeInstruction(OpCodes.Ldloc_S, compTransporterLocal);
						yield return new CodeInstruction(OpCodes.Ldfld, CompTransporter_ParentField);
						yield return new CodeInstruction(OpCodes.Callvirt, typeof(Building_CorpsePod).GetMethod("get_Stuff"));
					}

					if (instructionsList[i].opcode == OpCodes.Newobj && instructionsList[i].operand == ActiveDropPodInfoCtor)
					{
						foundCount++;
						yield return new CodeInstruction(OpCodes.Ldloc_S, compTransporterLocal);
						yield return new CodeInstruction(OpCodes.Ldfld, CompTransporter_ParentField);
						yield return new CodeInstruction(OpCodes.Isinst, typeof(Building_CorpsePod));
						yield return new CodeInstruction(OpCodes.Brtrue, branch2Detour);
						yield return instructionsList[i];
						yield return new CodeInstruction(OpCodes.Br, branch2Resume);
						CodeInstruction detourStart = new CodeInstruction(OpCodes.Ldloc_S, compTransporterLocal);
						detourStart.labels.Add(branch2Detour);
						yield return detourStart;
						yield return new CodeInstruction(OpCodes.Newobj, AccessTools.Constructor(typeof(ActiveCorpsePodInfo), new Type[] { typeof(CompTransporter) }));
						instructionsList[i + 1].labels.Add(branch2Resume);
						i++;
					}

					if (instructionsList[i].opcode == OpCodes.Ldsfld && instructionsList[i].operand == ThingDefOf_DropPodLeaving)
					{
						foundCount++;
						yield return new CodeInstruction(OpCodes.Ldloc_S, compTransporterLocal);
						yield return new CodeInstruction(OpCodes.Ldfld, CompTransporter_ParentField);
						yield return new CodeInstruction(OpCodes.Isinst, typeof(Building_CorpsePod));
						yield return new CodeInstruction(OpCodes.Brtrue, branch3Detour);
						yield return instructionsList[i];
						yield return new CodeInstruction(OpCodes.Br, branch3Resume);
						CodeInstruction detourStart = new CodeInstruction(OpCodes.Ldsfld, RTS_DefOf_CorpsePodLeaving);
						detourStart.labels.Add(branch3Detour);
						yield return detourStart;
						instructionsList[i + 1].labels.Add(branch3Resume);
						i++;
					}
				}

				yield return instructionsList[i];
			}

			if (foundCount != 4)
			{
				Log.Error("Failed to fully patch CompLaunchable.TryLaunch");
			}
		}
	}
}
