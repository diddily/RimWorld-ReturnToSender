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
	[HarmonyPatch(typeof(SkyfallerMaker), "MakeSkyfaller")]
	[HarmonyPatch(new Type[] { typeof(ThingDef), typeof(Thing) })]
	class SkyfallerMaker_MakeSkyfaller
	{
		public static Skyfaller MakeSkyfallerWithStuff(ThingDef skyfaller, Thing innerThing)
		{
			return (Skyfaller)ThingMaker.MakeThing(skyfaller, innerThing.Stuff);
		}

		public static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> instructions, ILGenerator ilg)
		{
			MethodInfo SkyfallerMaker_MakeSkyfallerMethod = typeof(SkyfallerMaker).GetMethod("MakeSkyfaller", new Type[] { typeof(ThingDef) });
			var instructionsList = new List<CodeInstruction>(instructions);
			for (int i = 0; i < instructionsList.Count; ++i)
			{
				if (instructionsList[i].opcode == OpCodes.Call && instructionsList[i].operand == SkyfallerMaker_MakeSkyfallerMethod)
				{
					yield return new CodeInstruction(OpCodes.Ldarg_1);
					yield return new CodeInstruction(OpCodes.Call, typeof(SkyfallerMaker_MakeSkyfaller).GetMethod("MakeSkyfallerWithStuff"));
				}
				else
				{
					yield return instructionsList[i];
				}
			}
		}
	}
}
