using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace ReturnToSender.Buildings
{
	public class Building_CorpsePod : Building
	{
		public override Graphic Graphic
		{
			get
			{
				return Utilities.GetPodGraphicData(FillPercent).GraphicColoredFor(this);
			}
		}

		public float FillPercent
		{
			get
			{
				return CollectionsMassCalculator.MassUsage<Thing>(GetComp<CompTransporter>().GetDirectlyHeldThings().ToList(), IgnorePawnsInventoryMode.DontIgnore, true, false) / 300.0f;
			}
		}
	}
}
