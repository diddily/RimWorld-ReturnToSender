using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using Verse.AI;

namespace ReturnToSender
{
	[DefOf]
	public class RTS_DefOf
	{
		public static SoundDef RTS_CorpsePod_Fall;
		public static SoundDef RTS_CorpsePod_Leaving;
		public static SoundDef RTS_CorpsePod_Open;

		public static ThingDef RTS_CorpsePod;
		public static ThingDef RTS_ActiveCorpsePod;
		public static ThingDef RTS_CorpsePodIncoming;
		public static ThingDef RTS_CorpsePodLeaving;
		public static ThingDef RTS_Mote_Giblets;

		public static ThoughtDef RTS_InThisTogether;
		public static ThoughtDef RTS_CorpseThoughtGeneral;
		public static ThoughtDef RTS_CorpseThoughtSameFaction;
		public static ThoughtDef RTS_CorpseThoughtHappyCannibal;
		public static ThoughtDef RTS_CorpseThoughtSadCannibal;
		public static ThoughtDef RTS_CorpseThoughtBloodlust;
		public static ThoughtDef RTS_CorpseThoughtAnnoyed;

		public static ThoughtDef RTS_CleanedOkayCorpse;
		public static ThoughtDef RTS_CleanedGrossCorpse;
		public static ThoughtDef RTS_CleanedPutridCorpse;

		public static ThoughtDef RTS_ObservedLayingGibletCorpse;
		public static ThoughtDef RTS_ObservedLayingVaporizedCorpse;
	}
}