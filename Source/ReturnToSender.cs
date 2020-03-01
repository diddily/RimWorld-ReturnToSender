using RimWorld;
#if VERSION_1_0
using Harmony;
#else
using HarmonyLib;
#endif
using HugsLib;
using HugsLib.Utils;
using HugsLib.Settings;
using System;
using System.Collections.Generic;
using Verse;
using Verse.Noise;
using UnityEngine;

using ReturnToSender.Storage;

namespace ReturnToSender
{
	public class ReturnToSender : ModBase
	{
		public static ReturnToSender Instance { get; private set; }
		public class PodGraphicsStruct
		{
			public PodGraphicsStruct(float t, GraphicData gd)
			{
				Threshold = t;
				GraphicData = gd;
			}
			public float Threshold;
			public GraphicData GraphicData;
		}
		public List<PodGraphicsStruct> PodGraphics = new List<PodGraphicsStruct>();
		public List<PodGraphicsStruct> ActivePodGraphics = new List<PodGraphicsStruct>();
		public SentCorpsePodsStorage sentCorpsePodsStorage;
		public override string ModIdentifier
		{
			get { return "ReturnToSender"; }
		}

		public ReturnToSender()
		{
			//HarmonyInstance.DEBUG = true;
			Instance = this;
		}

		public override void WorldLoaded()
		{
			sentCorpsePodsStorage = UtilityWorldObjectManager.GetUtilityWorldObject<SentCorpsePodsStorage>();
			base.WorldLoaded();
		}

		public SentCorpsePodsStorage GetSentCorpsePodsStorage()
		{
			return sentCorpsePodsStorage;
		}

		public override void DefsLoaded()
		{
			base.DefsLoaded();
			Settings.EntryName = "Return to Sender";
			int count = 4;
			for (int i = 0; i < count; ++i)
			{
				GraphicData gd = new GraphicData();
				gd.drawSize.Set(2, 2);
				gd.texPath = "Things/Buildings/CorpsePodBag" + i;
				//gd.shaderType = ShaderTypeDefOf.CutoutComplex;
				gd.graphicClass = typeof(Graphic_Single);

				PodGraphics.Add(new PodGraphicsStruct(((float)i) / (count + 1), gd));
			}

			for (int i = 0; i < count; ++i)
			{
				GraphicData gd = new GraphicData();
				gd.drawSize.Set(1.9f, 1.9f);
				gd.texPath = "Things/Buildings/CorpsePodBag" + i;
				//gd.shaderType = ShaderTypeDefOf.CutoutComplex;
				gd.graphicClass = typeof(Graphic_Single);
				gd.shadowData = new ShadowData();
				gd.shadowData.volume.Set(0.8f, 0.6f, 0.8f);

				ActivePodGraphics.Add(new PodGraphicsStruct(((float)i) / (count + 1), gd));
			}
		}
	}
}