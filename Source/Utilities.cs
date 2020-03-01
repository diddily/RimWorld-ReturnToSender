using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using Verse.AI;
using ReturnToSender.Buildings;

namespace ReturnToSender
{
	static class Utilities
	{
		public static bool HasCorpsePodInfo(IEnumerable<ActiveDropPodInfo> pods)
		{
			return pods.Any(ith => ith is ActiveCorpsePodInfo);
		}

		public static bool HasCorpsePod(IEnumerable<IThingHolder> pods)
		{
			return pods.Any(ith => ith is Building_CorpsePod || ith is ActiveCorpsePod || ith is ActiveCorpsePodInfo || (ith is CompTransporter && ((CompTransporter) ith).parent is Building_CorpsePod));
		}

		public static bool HasCorpsePodTransporters(IEnumerable<CompTransporter> pods)
		{
			return pods.Any(ct => ct.parent is Building_CorpsePod);
		}

		public static GraphicData GetPodGraphicData(float fillPercent)
		{
			int drawIndex = ReturnToSender.Instance.PodGraphics.Count - 1;
			while (drawIndex > 0 && ReturnToSender.Instance.PodGraphics[drawIndex].Threshold > fillPercent)
			{
				drawIndex--;
			}
			return ReturnToSender.Instance.PodGraphics[drawIndex].GraphicData;
		}

		public static GraphicData GetActivePodGraphicData(float fillPercent) 
		{
			int drawIndex = ReturnToSender.Instance.ActivePodGraphics.Count - 1;
			while (drawIndex > 0 && ReturnToSender.Instance.ActivePodGraphics[drawIndex].Threshold > fillPercent)
			{
				drawIndex--;
			}
			return ReturnToSender.Instance.ActivePodGraphics[drawIndex].GraphicData;
		}

		public static bool MakeFilth(IntVec3 c, Map map, ThingDef filthDef, string source, int count = 1)
		{
#if VERSION_1_0
			return FilthMaker.MakeFilth(c, map, filthDef, source, count);
#else
			return FilthMaker.TryMakeFilth(c, map, filthDef, source, count);
#endif
		}
	}
}
