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
        
        public static ThoughtDef RTS_SameFactionCorpse;

        public static ThoughtDef RTS_SentIntactCorpse0;
        public static ThoughtDef RTS_SentIntactCorpse1;

        public static ThoughtDef RTS_SentGibletCorpse0;
        public static ThoughtDef RTS_SentGibletCorpse1;
        public static ThoughtDef RTS_SentGibletCorpse2;

        public static ThoughtDef RTS_SentVaporizedCorpse0;
        public static ThoughtDef RTS_SentVaporizedCorpse1;
        public static ThoughtDef RTS_SentVaporizedCorpse2;
    }
}