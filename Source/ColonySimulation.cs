#if VERSION_1_0
using Harmony;
#else
using HarmonyLib;
#endif
using RimWorld;
using RimWorld.BaseGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

using ReturnToSender.Harmony;
using ReturnToSender.Storage;

namespace ReturnToSender
{
	public class ColonySimulation
	{
		static public bool _verbose = false;

		static public bool Verbose
		{
			get { return _verbose || Prefs.DevMode; }
			set { _verbose = value; }
		}

		public enum MapLevel
		{
			Location,
			Site,
			Outpost,
			Settlement
		};

		static private MapLevel _highestMapLevel = MapLevel.Location;
		static private float DamageLevelMinorRatio = 1.0f / 4.0f;
		static private float DamageLevelMediumRatio = 2.0f / 3.0f;
		static private float RecoveryLevelMinorRatio = 1.0f / 3.0f;
		static private float RecoveryLevelMajorRatio = 2.0f / 3.0f;
		static private double IdleChancePerBodyPerHourPlague = 0.0001333333333333333f;
		static private float ChancePawnSeeksMedicalRestPerHour = 0.65f;
		static private float RottenChancePlagueMult = 1.75f;
		static private float DescChancePlagueMult = 0.5f;
		static private float CleaningCorpseChancePlagueMult = 3f;
		static private double IdleSickColonistChancePlague = 0.0084142527461614863098f;
		static private float IdleChancePerBodyPerHourObserve = 0.01333333333333333f;
		static private float DoctoringChancePlagueMult = 3f;
		static private FieldInfo ticksUntilInfectField = AccessTools.Field(typeof(HediffComp_Infecter), "ticksUntilInfect");
		static private FieldInfo ticksToImpactField = AccessTools.Field(typeof(Projectile), "ticksToImpact");
		static private FieldInfo baseCenterField = AccessTools.Field(typeof(LordJob_DefendBase), "baseCenter");
		static private FieldInfo deteriorationRateField = AccessTools.Field(typeof(SteadyEnvironmentEffects), "deteriorationRate");

		static private MethodInfo impactMethod = AccessTools.Method(typeof(Projectile), "Impact");
		static private MethodInfo getNonMissChanceMethod = AccessTools.Method(typeof(Verb_MeleeAttack), "GetNonMissChance");
		static private MethodInfo getDodgeChanceMethod = AccessTools.Method(typeof(Verb_MeleeAttack), "GetDodgeChance");
		static private MethodInfo applyMeleeDamageToTargetMethod = AccessTools.Method(typeof(Verb_MeleeAttack), "ApplyMeleeDamageToTarget");
		static private MethodInfo getShotsPerBurstMethod = AccessTools.Method(typeof(Verb), "get_ShotsPerBurst");
		static private MethodInfo immunityHandlerTickMethod = AccessTools.Method(typeof(ImmunityHandler), "ImmunityHandlerTick");
		static private MethodInfo tryDoDeteriorateMethod = AccessTools.Method(typeof(SteadyEnvironmentEffects), "TryDoDeteriorate");

		public static ColonySimulation CurrentSimulation;
		public bool DontDropAndForbid = false;

		private List<IntVec3> freeStorage;
		private List<CorpseInfo> currentCorpses;
		private List<PawnInfo> currentPawns;
		private Building_Bed restingBed;
		private Map map;
		private string mapName;
		private int intervalCarry150;
		private int intervalCarry200;
		private int stepTicksGame;
		private int savedTicksGame;

		public ColonySimulation(Map m)
		{
			map = m;
			switch (_highestMapLevel)
			{
				case MapLevel.Location:
					mapName = "RTS_Location".Translate();
					break;
				case MapLevel.Site:
					mapName = "RTS_Site".Translate();
					break;
				case MapLevel.Outpost:
					mapName = "RTS_Outpost".Translate();
					break;
				case MapLevel.Settlement:
					mapName = "RTS_Settlement".Translate();
					break;
			}
			_highestMapLevel = MapLevel.Location;
			currentCorpses = new List<CorpseInfo>();
			currentPawns = new List<PawnInfo>();
			intervalCarry150 = 0;
			intervalCarry200 = 0;
			savedTicksGame = Find.TickManager.TicksGame;
			// TODO: Prisoners?
			//foreach (Pawn current in map.mapPawns.SpawnedPawnsInFaction(map.ParentFaction))
			foreach (Pawn current in map.mapPawns.AllPawnsSpawned.Where(p => p.RaceProps.Humanlike))
			{
				PawnInfo pawnInfo = new PawnInfo();
				pawnInfo.pawn = current;
				pawnInfo.jobStatus = PawnJobStatus.Idle;
				pawnInfo.plagueStatus = PawnPlagueStatus.Normal;
				//pawnInfo.pawn.mindState.duty = null;
				pawnInfo.isPrisoner = current.IsPrisoner;
				currentPawns.Add(pawnInfo);
			}
		}

		public enum CorpseStatus
		{
			Normal,
			Giblets,
			Vaporized
		}

		public static void SetMaxMapLevel(MapLevel mapLevel)
		{
			if (mapLevel > _highestMapLevel)
			{
				_highestMapLevel = mapLevel;
			}
		}

		public class CorpseInfo
		{
			public Corpse corpse;
			public CorpseStatus status;
			public PawnInfo carriedByInfo;
			public int cleanupHoursLeft;
			public int tick;
			public PawnInfo colonistPawnInfo;
			public IntVec3 cell;
			public bool sameFaction;
			public bool fromPod;
			public CorpseInfo(Corpse c, CorpseStatus s, int h, int t, IntVec3 ce, bool sf, bool fp)
			{
				corpse = c;
				status = s;
				carriedByInfo = null;
				cleanupHoursLeft = h;
				tick = t;
				colonistPawnInfo = null;
				cell = ce;
				sameFaction = sf;
				fromPod = fp;
			}

			public double GetSafeChance(double mult = 1)
			{
				RotStage rot = corpse.GetRotStage();
				if (rot == RotStage.Rotting)
				{
					return (1 - IdleChancePerBodyPerHourPlague * RottenChancePlagueMult * mult);
				}
				else if (rot == RotStage.Dessicated)
				{
					return (1 - IdleChancePerBodyPerHourPlague * DescChancePlagueMult * mult);
				}
				else
				{
					return (1 - IdleChancePerBodyPerHourPlague * mult);
				}
			}

			public ThoughtDef GetCleanupThought()
			{
				RotStage rot = corpse.GetRotStage();
				if (rot == RotStage.Rotting)
				{
					if (status == CorpseStatus.Normal)
					{
						return RTS_DefOf.RTS_CleanedGrossCorpse;
					}
					else
					{
						return RTS_DefOf.RTS_CleanedPutridCorpse;
					}
				}
				else if (rot == RotStage.Dessicated)
				{
					if (status == CorpseStatus.Normal)
					{
						return RTS_DefOf.RTS_CleanedOkayCorpse;
					}
					else
					{
						// Must have rotted since arrival. Gross.
						return RTS_DefOf.RTS_CleanedPutridCorpse;
					}
				}
				else
				{
					if (status == CorpseStatus.Normal)
					{
						return RTS_DefOf.RTS_CleanedOkayCorpse;
					}
					else
					{
						return RTS_DefOf.RTS_CleanedGrossCorpse;
					}
				}
			}

			public ThoughtDef GetObserveThought()
			{
				RotStage rot = corpse.GetRotStage();
				if (status == CorpseStatus.Normal)
				{
					if (rot == RotStage.Fresh)
					{
						return ThoughtDef.Named("ObservedLayingCorpse");
					}
					else
					{
						return ThoughtDef.Named("ObservedLayingRottingCorpse");
					}
				}
				else if (status == CorpseStatus.Giblets)
				{
					return RTS_DefOf.RTS_ObservedLayingGibletCorpse;
				}
				else
				{
					return RTS_DefOf.RTS_ObservedLayingVaporizedCorpse;
				}
			}
		};

		public enum PawnJobStatus
		{
			Idle,
			MovingCorpse,
			Fighting,
			Doctoring,
			Resting,
			// Start useless
			MentalBreakingViolent,
			MentalBreakingUseless,
			RanAway
		};


		public enum PawnPlagueStatus
		{
			Normal,
			Sick,
			Immune
		};

		public class SpawnedInfo
		{
			public Thing thing;
			object[] parms;

			public int startTick;
			public CompRottable rotComp;

			public SpawnedInfo(Thing t, int st)
			{
				thing = t;
				bool roofed = t.Position.Roofed(t.Map);
				Room room = t.GetRoom(RegionType.Set_Passable);
				bool roomUsesOutdoorTemperature = room != null && room.UsesOutdoorTemperature;
				Building edifice = t.Position.GetEdifice(t.Map);
				bool protectedByEdifice = edifice != null && edifice.def.building != null && edifice.def.building.preventDeteriorationOnTop;

				startTick = st;
				rotComp = t.TryGetComp<CompRottable>();
				parms = new object[] { thing, t.Position.Roofed(t.Map), roomUsesOutdoorTemperature, protectedByEdifice, t.Position.GetTerrain(t.Map) };
			}

			public void StepDecomp(int tick, float temperature)
			{
				if (tick < startTick || thing == null || thing.Destroyed)
				{
					return;
				}

				tryDoDeteriorateMethod.Invoke(thing.Map.steadyEnvironmentEffects, parms);
				float interval = GenDate.TicksPerDay / 36.0f;
				if (rotComp != null)
				{
					float rotProgress = rotComp.RotProgress;
					float num = GenTemperature.RotRateAtTemperature(temperature);
					rotComp.RotProgress += num * interval;
					if (rotComp.Stage == RotStage.Rotting && rotComp.PropsRot.rotDestroys)
					{
						thing.Destroy(DestroyMode.Vanish);
						return;
					}
					bool flag = Mathf.FloorToInt(rotProgress / 60000f) != Mathf.FloorToInt(rotComp.RotProgress / 60000f);
					if (flag)
					{
						if (rotComp.Stage == RotStage.Rotting && rotComp.PropsRot.rotDamagePerDay > 0f)
						{
							thing.TakeDamage(new DamageInfo(DamageDefOf.Rotting, (float)GenMath.RoundRandom(rotComp.PropsRot.rotDamagePerDay), 0f, -1f, null, null, null, DamageInfo.SourceCategory.ThingOrUnknown, null));
						}
						else if (rotComp.Stage == RotStage.Dessicated && rotComp.PropsRot.dessicatedDamagePerDay > 0f)
						{
							thing.TakeDamage(new DamageInfo(DamageDefOf.Rotting, (float)GenMath.RoundRandom(rotComp.PropsRot.dessicatedDamagePerDay), 0f, -1f, null, null, null, DamageInfo.SourceCategory.ThingOrUnknown, null));
						}
					}
				}
			}
		}

		public Building_Bed GetPawnBed(Pawn p)
		{
			if (currentPawns.Any(pi => pi.pawn == p && pi.jobStatus == PawnJobStatus.Resting))
			{
				return restingBed;
			}

			return null;
		}

		public class PawnInfo
		{
			public Pawn pawn;
			public CorpseInfo carryInfo;
			public PawnInfo combatTarget;
			public PawnJobStatus jobStatus;
			public PawnInfo patient;
			public int ticksToAttack;
			public int deathTick;
			public bool cooldownActive;
			public bool deathDetected;
			public bool corpseCleaned;
			public bool isPrisoner;
			public PawnPlagueStatus plagueStatus;

			public bool CanBeAttacked
			{
				get
				{
					return !(pawn.Dead  || pawn.Downed || jobStatus == PawnJobStatus.RanAway || isPrisoner);
				}
			}

			public bool IsDeadOrGone
			{
				get
				{
					return pawn.Dead|| jobStatus == PawnJobStatus.RanAway;
				}
			}

			public bool IsUseless
			{
				get
				{
					return pawn.Dead || pawn.Downed || jobStatus >= PawnJobStatus.MentalBreakingViolent || isPrisoner;
				}
			}

			public bool CanTakeJob
			{
				get
				{
					return !IsUseless && jobStatus == PawnJobStatus.Idle;
				}
			}

			public float GetPlagueSeverity()
			{
				Hediff plague = pawn?.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.Plague);
				if (plague != null)
				{
					return plague.Severity;
				}
				return 0;
			}

			public float GetPlagueImmunity()
			{
				var imm = pawn?.health?.immunity;
				if (imm != null)
				{
					return imm.GetImmunity(HediffDefOf.Plague);
				}
				return 0;
			}

			public float GetPlagueTendQuality()
			{
				HediffComp_TendDuration tend = pawn?.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.Plague)?.TryGetComp<HediffComp_TendDuration>();
				if (tend != null)
				{
					return tend.tendQuality;
				}
				return 0.0f;
			}

			public void FightPawn(PawnInfo info)
			{
				if (info != null)
				{
					jobStatus = PawnJobStatus.Fighting;
					combatTarget = info;
				}
				else
				{
					jobStatus = PawnJobStatus.Idle;
					combatTarget = null;
				}
			}

			public void UpdateAttackDelay(bool first)
			{
				Verb attackVerb = pawn.TryGetAttackVerb(combatTarget.pawn);
				float time = 0;
				if (first)
				{
					if (attackVerb.IsMeleeAttack)
					{
						time += Rand.Range(4.0f, 5.0f);
					}
					else
					{
						time += Rand.Range(0.0f, 0.5f);
					}
				}
				else if (cooldownActive && attackVerb.IsMeleeAttack)
				{
					time += (95).TicksToSeconds();
				}

				if (cooldownActive)
				{
					time += attackVerb.verbProps.AdjustedCooldown(attackVerb, pawn);
				}
				else if (attackVerb.verbProps.warmupTime > 0f)
				{
					float statValue = pawn.GetStatValue(StatDefOf.AimingDelayFactor, true);
					time += attackVerb.verbProps.warmupTime * statValue;
				}
				
				ticksToAttack = (time).SecondsToTicks();
			}
		}

		private void SetupRestingBed(TechLevel techLevel)
		{
			ThingDef bedDef = null;
			QualityCategory quality = QualityCategory.Awful;
			switch (techLevel)
			{
				case TechLevel.Undefined:
				case TechLevel.Animal:
					break;
				case TechLevel.Neolithic:
					bedDef = ThingDefOf.Bedroll;
					quality = QualityCategory.Normal;
					break;
				case TechLevel.Medieval:
					bedDef = ThingDefOf.Bed;
					quality = QualityCategory.Normal;
					break;
				case TechLevel.Industrial:
					bedDef = ThingDefOf.Bed;
					quality = QualityCategory.Excellent;

					break;
				case TechLevel.Spacer:
					bedDef = DefDatabase<ThingDef>.GetNamed("HospitalBed", false);
					if (bedDef == null)
					{
						bedDef = ThingDefOf.Bed;
					}
					quality = QualityCategory.Excellent;
					break;

				case TechLevel.Ultra:
				case TechLevel.Archotech:
					bedDef = DefDatabase<ThingDef>.GetNamed("HospitalBed", false);
					if (bedDef == null)
					{
						bedDef = ThingDefOf.Bed;
					}
					quality = QualityCategory.Legendary;
					break;
			}

			if (bedDef != null)
			{
				restingBed = (Building_Bed) ThingMaker.MakeThing(bedDef, ThingDefOf.Steel);
				CompQuality comp = restingBed.TryGetComp<CompQuality>();
				if (comp != null)
				{
					comp.SetQuality(quality, ArtGenerationContext.Outsider);
				}
			}
		}

		private Medicine GetMedicineToUse(TechLevel techLevel)
		{
			float roll = Rand.Value;
			ThingDef medicineDef = null;
			switch (techLevel)
			{
				case TechLevel.Undefined:
				case TechLevel.Animal:
					break;
				case TechLevel.Neolithic:
					if (roll < 0.25f)
					{
						medicineDef = ThingDefOf.MedicineHerbal;
					}
					break;
				case TechLevel.Medieval:
					if (roll < 0.25f)
					{
						medicineDef = ThingDefOf.MedicineIndustrial;
					}
					else if (roll < 0.75f)
					{
						medicineDef = ThingDefOf.MedicineHerbal;
					}
					break;
				case TechLevel.Industrial:
					if (roll < 0.05f)
					{
						medicineDef = ThingDefOf.MedicineUltratech;
					}
					else if (roll < 0.55f)
					{
						medicineDef = ThingDefOf.MedicineIndustrial;
					}
					else if (roll < 0.9f)
					{
						medicineDef = ThingDefOf.MedicineHerbal;
					}
					break;
				case TechLevel.Spacer:
					if (roll < 0.5f)
					{
						medicineDef = ThingDefOf.MedicineUltratech;
					}
					else if (roll < 0.95f)
					{
						medicineDef = ThingDefOf.MedicineIndustrial;
					}
					else if (roll < 0.999f)
					{
						medicineDef = ThingDefOf.MedicineHerbal;
					}
					break;
				case TechLevel.Ultra:
				case TechLevel.Archotech:
					medicineDef = ThingDefOf.MedicineUltratech;
					break;
			}

			if (medicineDef != null)
			{
				return ThingMaker.MakeThing(medicineDef) as Medicine;
			}

			return null;
		}

		public bool TryGetBestDoctor(List<PawnInfo> currentPawns, PawnInfo targetPawn, out PawnInfo bestDoctor)
		{
			float bestOverallTendQuality = -1;
			float bestTendQuality = -1;
			bestDoctor = null;
			foreach (PawnInfo currentPawn in currentPawns)
			{
				if (currentPawn.IsUseless)
				{
					continue;
				}
				float tendQuality = currentPawn.pawn.GetStatValue(StatDefOf.MedicalTendQuality, true);
				if (currentPawn == targetPawn)
				{
					tendQuality *= 0.7f;
				}

				if (tendQuality > bestOverallTendQuality)
				{
					bestOverallTendQuality = tendQuality;
				}

				if (currentPawn.jobStatus != PawnJobStatus.Idle)
				{
					continue;
				}

				if (tendQuality > bestTendQuality)
				{
					bestDoctor = currentPawn;
					bestTendQuality = tendQuality;
				}
			}

			// Don't settle for crap care.
			if (bestTendQuality <= bestOverallTendQuality * 0.5f)
			{
				bestDoctor = null;
			}

			return bestDoctor != null;
		}

		void DropPod(Map map, SentCorpsePodInfo info)
		{
			IntVec3 near;
			if (!DropCellFinder.TryFindRaidDropCenterClose(out near, map))
			{
				near = DropCellFinder.FindRaidDropCenterDistant(map);
			}
			IntVec3 cell = IntVec3.Zero;
			DropCellFinder.TryFindDropSpotNear(near, map, out cell, false, true);

			int cleanupCorpses = 0;
			for (int i = info.sentCorpses.Count - 1; i >= 0; i--)
			{
				Thing thing = info.sentCorpses[i];
				int cleanupHours = 0;
				Corpse c = thing as Corpse;
				if (c != null)
				{
					RotStage rot = c.GetRotStage();
					int spray = 1;
					int giblet = 5;
					if (rot == RotStage.Rotting)
					{
						cleanupHours += 1;
						spray = 3;
						giblet = 7;
					}
					bool detectedSameFaction = false;
					bool hasHead = c.InnerPawn.health.hediffSet.HasHead;
					bool hasClothes = c.InnerPawn.apparel.WornApparelCount > 0;
					int r = Rand.RangeInclusive(0, 9);
					if (rot != RotStage.Dessicated || r >= giblet)
					{
						if (map.ParentFaction == c.InnerPawn.Faction)
						{
							cleanupHours += 2;
							if (r < spray)
							{
								float detectChance = 0.5f;
								if (!hasHead)
								{
									detectChance *= 0.75f;
								}
								if (!hasClothes)
								{
									detectChance *= 0.35f;
								}
								detectedSameFaction = Rand.Chance(detectChance);
							}
							else if (r < giblet)
							{
								float detectChance = 0.8f;
								if (!hasHead)
								{
									detectChance *= 0.75f;
								}
								if (!hasClothes)
								{
									detectChance *= 0.75f;
								}
								detectedSameFaction = Rand.Chance(detectChance);
							}
							else
							{
								if (!hasHead && !hasClothes)
								{
									detectedSameFaction = Rand.Chance(0.8f);
								}
								else
								{
									detectedSameFaction = true;
								}
							}
						}
					}

					if (r < spray) // Blood spray
					{
						if (rot != RotStage.Dessicated)
						{
							cleanupHours += 1;
							currentCorpses.Add(new CorpseInfo(c, CorpseStatus.Vaporized, cleanupHours, info.tickLanded, cell, detectedSameFaction, true));
							cleanupCorpses++;
						}
						else
						{
							c.Destroy(DestroyMode.KillFinalize);
						}
					}
					else if (r < giblet) // Blood and meat
					{
						if (rot != RotStage.Dessicated)
						{
							cleanupHours += 2;
							currentCorpses.Add(new CorpseInfo(c, CorpseStatus.Giblets, cleanupHours, info.tickLanded, cell, detectedSameFaction, true));
							cleanupCorpses++;
						}
						else
						{
							c.Destroy(DestroyMode.KillFinalize);
						}
					}
					else
					{
						cleanupHours += 2;
						currentCorpses.Add(new CorpseInfo(c, CorpseStatus.Normal, cleanupHours, info.tickLanded, cell, detectedSameFaction, true));
						cleanupCorpses++;
					}
				}
			}
		}
			
		public void HandleStory(string storyFormatString)
		{
#if VERSION_1_0
			List<string> stories = new List<string>();
			string story;
#else
			List<TaggedString> stories = new List<TaggedString>();
			TaggedString story;
#endif
			while (Translator.TryTranslate(string.Format(storyFormatString, stories.Count), out story))
			{
				stories.Add(story);
			}

			if (stories.Count > 0)
			{
				string terrainLabel = map.Biome.terrainsByFertility.MaxBy(tt => tt.max - tt.min).terrain.label;

				string text = string.Format(stories.RandomElement(), mapName, terrainLabel);

				LongEventHandler.ExecuteWhenFinished(delegate
				{
					DiaNode diaNode = new DiaNode(text);
					Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
					DiaOption diaOption = new DiaOption();
					diaOption.resolveTree = true;
					diaOption.clickSound = null;
					diaNode.options.Add(diaOption);
					Dialog_NodeTree dialog_NodeTree = new Dialog_NodeTree(diaNode, false, false, null);
					dialog_NodeTree.soundClose = (SoundDefOf.Click);
					dialog_NodeTree.closeAction = delegate
					{
						Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
					};
					Find.WindowStack.Add(dialog_NodeTree);
					Find.Archive.Add(new ArchivedDialog(diaNode.text, null, null));
				});
			}
		}

		public bool DoSimulation()
		{
			if (currentPawns.Count() == 0)
			{
				HandleStory("RTS_Story_Empty_{0}");
				return false;
			}
			bool allDead = false;
			restingBed = null;
			SetupRestingBed(map.ParentFaction.def.techLevel);
			CurrentSimulation = this;
			DontDropAndForbid = true;
			List<SentCorpsePodInfo> podInfos;
			ProgramState programState = Current.ProgramState;
			Current.ProgramState = ProgramState.Playing;
			LordJob_DefendBase originalLordJob = null;

			

			if (ReturnToSender.Instance.GetSentCorpsePodsStorage().TryGetPodInfoForTile(map.Tile, out podInfos))
			{
				Lord originalLord = currentPawns.First().pawn.GetLord();
				if (originalLord != null)
				{
					originalLordJob = originalLord.LordJob as LordJob_DefendBase;
				}

				foreach (PawnInfo pi in currentPawns)
				{
					Lord lord = pi.pawn.GetLord();
					if (lord != null)
					{
						lord.ownedPawns.Remove(pi.pawn);
					}
					pi.pawn.mindState.duty = null;
				}

				int firstTick = podInfos.First().tickLanded;
				int currentTick = firstTick;
				int numTicks = Find.TickManager.TicksAbs - currentTick;
				int podIndex = 0;
				// 3.5 years of sim is about 10k loops, anything more than that sound insane.
				if (numTicks > GenDate.TicksPerYear * 3.5)
				{
					numTicks = (GenDate.TicksPerYear * 7) / 2;
					firstTick = Find.TickManager.TicksAbs - numTicks;
					currentTick = firstTick;
					while (podIndex < podInfos.Count && podInfos[podIndex].tickLanded <= currentTick)
					{
						podIndex++;
					}
				}
				if (podIndex < podInfos.Count)
				{
					int numHours = ((numTicks + (GenDate.TicksPerHour - 1)) / GenDate.TicksPerHour);
					if (Verbose)
					{
						Log.Message("Num hours = " + numHours);
					}
					int hour = 0;
					bool active = true;
					for (; hour < numHours; ++hour)
					{
						currentTick += GenDate.TicksPerHour;
						stepTicksGame = currentTick;
						Find.TickManager.DebugSetTicksGame(stepTicksGame);
						while (podIndex < podInfos.Count && podInfos[podIndex].tickLanded <= currentTick)
						{
							if (Verbose)
							{
								Log.Message(hour + ": added a pod with " + podInfos[podIndex].sentCorpses.Count() + ", now there are " + currentCorpses.Count());
							}
							DropPod(map, podInfos[podIndex]);
							podIndex++;
						}

						if (!DoStep(hour) && podIndex >= podInfos.Count)
						{
							active = false;
							break;
						}
					}

					Find.TickManager.DebugSetTicksGame(savedTicksGame);
					if (Verbose)
					{
						Log.Message("After " + hour + " hours " + currentPawns.Count(pi => !pi.IsDeadOrGone) + " pawns survived out of " + currentPawns.Count() + " with " + currentCorpses.Count() + " left over.");
						Log.Message("Pawns: " + String.Join(",", currentPawns.Select(pi => pi.pawn.ToString() + ": " + (pi.pawn.Dead ? "Dead" : pi.jobStatus.ToString())).ToArray()));
						Log.Message("Corpses: " + String.Join(",", currentCorpses.Select(ci => ci.corpse.ToString() + ": " + (ci.carriedByInfo == null ? "NULL" : ci.carriedByInfo.ToString())).ToArray()));
					}
					freeStorage = new List<IntVec3>();
					if (map == BaseGen.globalSettings.map)
					{
						foreach (IntVec3 current in BaseGen.globalSettings.mainRect)
						{
							if (current.Standable(map) && current.GetFirstItem(map) == null && current.Roofed(map))
							{
								freeStorage.Add(current);
							}
						}

						freeStorage.Shuffle();
					}

					int survivedCount = currentPawns.Count(pi => !pi.IsDeadOrGone);
					int revivedCount = 0;
					int totalCount = currentPawns.Count();
					int lostCount = totalCount - survivedCount;
					List<SpawnedInfo> spawnedStuff = new List<SpawnedInfo>();
					if (currentPawns.Any(pi => !pi.IsDeadOrGone))
					{
						// Settlement health restores over time (after a cooldown)
						currentTick += (int)Rand.Range(GenDate.TicksPerSeason * 0.9f, GenDate.TicksPerSeason * 1.3f);
						while (currentTick < Find.TickManager.TicksAbs)
						{
							IEnumerable<PawnInfo> deadPawns = currentPawns.Where(pi => pi.IsDeadOrGone);
							if (deadPawns.Count() == 0)
							{
								break;
							}
							PawnInfo lazurus = deadPawns.RandomElement();
							if (Verbose)
							{
								Log.Message("Reviving: " + lazurus.pawn);
							}
							if (!lazurus.pawn.Dead)
							{
								lazurus.pawn.Kill(null);
							}

							ResurrectionUtility.TryResurrect(lazurus.pawn);

							revivedCount++;
							lazurus.jobStatus = PawnJobStatus.Idle;
							currentTick += (int)Rand.Range(GenDate.TicksPerDay * 1.25f, GenDate.TicksPerDay * 2.75f);
						}

						foreach (PawnInfo pi in currentPawns)
						{
							if (pi.pawn.Dead)
							{
								DropEverything(pi, ref spawnedStuff);

								if (pi.corpseCleaned)
								{
									pi.pawn.Corpse.Destroy(DestroyMode.KillFinalize);
								}
							}
						}

						if (originalLordJob != null)
						{
							foreach (PawnInfo pi in currentPawns)
							{
								Lord lord = pi.pawn.GetLord();
								if (lord != null)
								{
									lord.ownedPawns.Remove(pi.pawn);
								}
							}
							IntVec3 center = (IntVec3)baseCenterField.GetValue(originalLordJob);
							LordMaker.MakeNewLord(map.ParentFaction, new LordJob_DefendBase(map.ParentFaction, center), map, currentPawns.Where(pi => !pi.IsDeadOrGone).Select(pi => pi.pawn));
						}
					}


					foreach (CorpseInfo ci in currentCorpses)
					{
						if (ci.status == CorpseStatus.Vaporized)
						{
							Utilities.MakeFilth(ci.cell, map, ci.corpse.InnerPawn.RaceProps.BloodDef, ci.corpse.InnerPawn.LabelIndefinite(), Rand.RangeInclusive(5, 8));
							ci.corpse.Destroy(DestroyMode.KillFinalize);
						}
						else if (ci.status == CorpseStatus.Giblets)
						{
							Utilities.MakeFilth(ci.cell, map, ci.corpse.InnerPawn.RaceProps.BloodDef, ci.corpse.InnerPawn.LabelIndefinite(), Rand.RangeInclusive(2, 3));
							float eff = Rand.Range(0.5f, 0.7071067811865f);
							Thing thing3 = null;
							foreach (Thing pb in ci.corpse.InnerPawn.ButcherProducts(ci.corpse.InnerPawn, eff * eff))
							{
								GenPlace.TryPlaceThing(pb, ci.cell, map, ThingPlaceMode.Near, out thing3);
								spawnedStuff.Add(new SpawnedInfo(thing3, ci.tick));
							}
						}
						else
						{
							if (!ci.corpse.Spawned)
							{
								Thing thing2 = null;
								GenPlace.TryPlaceThing(ci.corpse, ci.cell, map, ThingPlaceMode.Near, out thing2, delegate (Thing placedThing, int count)
								{

								}, null);
								spawnedStuff.Add(new SpawnedInfo(thing2, ci.tick));
							}
							else
							{
								spawnedStuff.Add(new SpawnedInfo(ci.corpse, ci.tick));
							}

							Utilities.MakeFilth(ci.corpse.Position, map, ci.corpse.InnerPawn.RaceProps.BloodDef, ci.corpse.InnerPawn.LabelIndefinite(), 1);
						}
					}

					foreach (PawnInfo pi in currentPawns.Where(pi => pi.jobStatus == PawnJobStatus.RanAway))
					{
						if (pi.pawn.Spawned)
						{
							pi.pawn.DeSpawn();
						}
						pi.pawn.DestroyOrPassToWorld();
					}

					deteriorationRateField.SetValue(map.steadyEnvironmentEffects, 1.0f);
					float interval = GenDate.TicksPerDay / 36.0f;
					for (int tick = firstTick; tick <= Find.TickManager.TicksAbs; tick = (int)(tick + interval))
					{
						foreach (SpawnedInfo spawnedInfo in spawnedStuff)
						{
							spawnedInfo.StepDecomp(tick, Find.World.tileTemperatures.OutdoorTemperatureAt(map.Tile, tick));
						}
					}


					string[] damageLevels = { "Annihilation", "NoDamage", "MildDamage", "MediumDamage", "MajorDamage" };
					string[] phaseLevels = { "Active", "Inactive", "MinorRecovery", "MajorRecovery", "FullRecovery" };
					int damageLevel;
					int phaseLevel;

					float lostRatio = ((float)lostCount) / totalCount;
					float recoverRatio = (lostCount > 0 ? ((float)revivedCount) / lostCount : 1.0f);

					if (lostRatio == 1.0f)
					{
						damageLevel = 0;
					}
					else if (lostRatio == 0.0f)
					{
						damageLevel = 1;
					}
					else if (lostRatio <= DamageLevelMinorRatio)
					{
						damageLevel = 2;
					}
					else if (lostRatio <= DamageLevelMediumRatio)
					{
						damageLevel = 3;
					}
					else
					{
						damageLevel = 4;
					}

					if (active)
					{
						phaseLevel = 0;
					}
					else if (recoverRatio == 0.0f)
					{
						phaseLevel = 1;
					}
					else if (recoverRatio == 1.0f)
					{
						phaseLevel = 4;
					}
					else if (recoverRatio <= RecoveryLevelMinorRatio)
					{
						phaseLevel = 2;
					}
					else
					{
						phaseLevel = 3;
					}

					phaseLevel = Math.Min(phaseLevel, damageLevel);

					string storyFormatString = string.Format("RTS_Story_{0}_{1}_{2}", damageLevels[damageLevel], phaseLevels[phaseLevel], "{0}");
					HandleStory(storyFormatString);
				}

				ReturnToSender.Instance.GetSentCorpsePodsStorage().RemoveAllPodInfoForTile(map.Tile);
			}
			
			Current.ProgramState = programState;
			if (restingBed != null)
			{
				restingBed.Destroy();
				restingBed = null;
			}
			DontDropAndForbid = false;
			CurrentSimulation = null;
			return allDead;
		}

		void AddThought(PawnInfo pi, ThoughtDef td)
		{
			if (pi.pawn?.needs?.mood?.thoughts != null)
			{
				pi.pawn.needs.mood.thoughts.memories.TryGainMemory(td, null);
			}
		}

		string GetHediffOutput(Hediff h)
		{
			HediffComp_TendDuration tend = h?.TryGetComp<HediffComp_TendDuration>();
			HediffComp_Immunizable imm = h?.TryGetComp<HediffComp_Immunizable>();
			StringBuilder sb = new StringBuilder();
			sb.AppendFormat("{0}, Sev: {1}", h.LabelCap, h.Severity);
			sb.Append(h.LabelCap);
			if (tend != null)
			{
				sb.AppendFormat(", Tend: {0}", tend.tendQuality);
			}

			if (imm != null)
			{
				sb.AppendFormat(", Imm: {0}", imm.Immunity);
			}

			return sb.ToString();
		}

		void UpdateMemory(MemoryThoughtHandler memories, ThoughtDef td, float m)
		{
			var mem = memories.GetFirstMemoryOfDef(td);

			if (mem == null)
			{
				memories.TryGainMemory(td);
				mem = memories.GetFirstMemoryOfDef(td);
			}

			if (mem != null)
			{
				mem.Renew();
				mem.moodPowerFactor = m;
			}
		}

		bool DoStep(int hour)
		{
			bool continueStepping = false;
			List<string> messages = new List<string>();
			try
			{
				// First we tick job status to free up pawns.
				foreach (PawnInfo pi in currentPawns)
				{
					if (pi.jobStatus == PawnJobStatus.MovingCorpse && !pi.IsUseless)
					{
						// Should not happen...
						if (pi.carryInfo == null)
						{
							pi.jobStatus = PawnJobStatus.Idle;
						}
						else
						{
							pi.carryInfo.cleanupHoursLeft--;
							if (pi.carryInfo.cleanupHoursLeft <= 0)
							{
								messages.Add(pi.pawn + " just finished cleaning up " + pi.carryInfo.corpse);
								if (pi.carryInfo.colonistPawnInfo != null)
								{
									pi.carryInfo.colonistPawnInfo.corpseCleaned = true;
								}
								else
								{
									pi.carryInfo.corpse.Destroy(DestroyMode.KillFinalize);
								}
								currentCorpses.Remove(pi.carryInfo);
								pi.jobStatus = PawnJobStatus.Idle;
								pi.carryInfo = null;
							}
						}
					}
					else if (pi.carryInfo != null)
					{
						messages.Add(pi.pawn + " just dropped " + pi.carryInfo.corpse);
						pi.carryInfo.carriedByInfo = null;
						pi.carryInfo = null;
					}

					if (pi.jobStatus == PawnJobStatus.Doctoring || pi.jobStatus == PawnJobStatus.Resting)
					{
						pi.jobStatus = PawnJobStatus.Idle;
					}
				}
				
				// Next we treat illnesses as best we can.
				foreach (PawnInfo pi in currentPawns.Where(pi => !pi.IsDeadOrGone && Rand.Chance(ChancePawnSeeksMedicalRestPerHour) && HealthAIUtility.ShouldSeekMedicalRestUrgent(pi.pawn) && pi.jobStatus != PawnJobStatus.MentalBreakingViolent))
				{
					PawnInfo doctor;
					if (TryGetBestDoctor(currentPawns, pi, out doctor))
					{
						doctor.jobStatus = PawnJobStatus.Doctoring;
						doctor.patient = pi;
						pi.jobStatus = PawnJobStatus.Resting;

						int tendDuration = (int)(1f / doctor.pawn.GetStatValue(StatDefOf.MedicalTendSpeed, true) * 600f);
						if (tendDuration > GenDate.TicksPerHour)
						{
							tendDuration = GenDate.TicksPerHour;
						}

						int maxTendsByTime = GenDate.TicksPerHour / tendDuration;
						int tendCount = 0;
						while (tendCount < maxTendsByTime && pi.pawn.health.HasHediffsNeedingTend(false))
						{
							Medicine medicine = GetMedicineToUse(map.ParentFaction.def.techLevel);
							float quality = TendUtility.CalculateBaseTendQuality(doctor.pawn, pi.pawn, (medicine == null) ? null : medicine.def);
							List<Hediff> tmpHediffsToTend = new List<Hediff>();
							TendUtility.GetOptimalHediffsToTendWithSingleTreatment(pi.pawn, medicine != null, tmpHediffsToTend, null);
							float xp = 500f * ((medicine != null) ? medicine.def.MedicineTendXpGainFactor : 0.5f);
							doctor.pawn.skills.Learn(SkillDefOf.Medicine, xp, true);
							TendUtility.DoTend(doctor.pawn, pi.pawn, medicine);
							messages.Add(doctor.pawn + " tended to " + pi.pawn + " with " + medicine + " at base quality of " + quality + " on " + String.Join(", ", tmpHediffsToTend.Select(h => GetHediffOutput(h)).ToArray()));

							tendCount++;
						}
					}
				}

				// Now we process moods (only when awake)
				if (hour % 24 <= 17)
				{
					float sfMult = 0.0f;
					float genMult = 0.0f;
					float hcMult = 0.0f;
					float scMult = 0.0f;
					float bMult = 0.0f;
					float aMult = 0.0f;
					int ittMult = currentPawns.Count(pi => !pi.IsUseless && pi.plagueStatus != PawnPlagueStatus.Sick);

					foreach (CorpseInfo ci in currentCorpses.Where(ci => ci.fromPod))
					{
						RotStage rot = ci.corpse.GetRotStage();
						bool sf = ci.sameFaction;
						if (ci.status == CorpseStatus.Normal)
						{
							if (sf)
							{
								sfMult += 1.25f;
							}

							if (rot == RotStage.Fresh)
							{
								genMult += 0.75f;
								hcMult += 1.25f;
								aMult += 1.25f;
							}
							else
							{
								genMult += 1.25f;
								scMult += 1.25f;
								aMult += 1.5f;
							}
						}
						else if (ci.status == CorpseStatus.Giblets)
						{
							if (sf)
							{
								sfMult += 1.5f;
							}

							bMult += 1.0f;

							if (rot == RotStage.Fresh)
							{
								genMult += 1.25f;
								hcMult += 1.5f;
								aMult += 1.35f;
							}
							else
							{
								genMult += 1.75f;
								scMult += 1.5f;
								aMult += 1.65f;
							}
						}
						else
						{
							if (sf)
							{
								sfMult += 1.25f;
							}

							bMult += 2.0f;

							if (rot == RotStage.Fresh)
							{
								genMult += 1.0f;
								scMult += 1.25f;
								aMult += 1.25f;
							}
							else
							{
								genMult += 1.5f;
								scMult += 1.5f;
								aMult += 1.5f;
							}
						}
					}

					aMult = aMult / Math.Max(1, ittMult);

					foreach (PawnInfo pi in currentPawns.Where(pi => !pi.IsDeadOrGone && pi.pawn?.needs?.mood?.thoughts?.memories != null))
					{
						var memories = pi.pawn.needs.mood.thoughts.memories;
						UpdateMemory(memories, RTS_DefOf.RTS_CorpseThoughtGeneral, genMult);
						UpdateMemory(memories, RTS_DefOf.RTS_CorpseThoughtSameFaction, sfMult);
						UpdateMemory(memories, RTS_DefOf.RTS_CorpseThoughtHappyCannibal, hcMult);
						UpdateMemory(memories, RTS_DefOf.RTS_CorpseThoughtSadCannibal, scMult);
						UpdateMemory(memories, RTS_DefOf.RTS_CorpseThoughtBloodlust, bMult);
						UpdateMemory(memories, RTS_DefOf.RTS_InThisTogether, 1.5f * ittMult);

						if (pi.pawn.story.traits.HasTrait(TraitDefOf.Psychopath) || pi.pawn.story.traits.DegreeOfTrait(TraitDefOf.Industriousness) < 0)
						{
							int mult = -pi.pawn.story.traits.DegreeOfTrait(TraitDefOf.Industriousness);
							if (pi.pawn.story.traits.HasTrait(TraitDefOf.Psychopath))
							{
								mult++;
							}
							UpdateMemory(memories, RTS_DefOf.RTS_CorpseThoughtAnnoyed, Math.Max(100.0f, aMult * mult));
						}
					}

					int totalTicks = intervalCarry150 + GenDate.TicksPerHour;
					int numIntervals = totalTicks / 150;
					intervalCarry150 = totalTicks - numIntervals * 150;
					foreach (PawnInfo pi in currentPawns.Where(pi => !pi.IsDeadOrGone && !pi.pawn.Downed && pi.pawn.health.capacities.CanBeAwake))
					{
						Find.TickManager.DebugSetTicksGame(-pi.pawn.HashOffset());
						for (int i = 0; i < numIntervals; ++i)
						{
							pi.pawn.needs.mood.NeedInterval();
							pi.pawn.needs.mood.thoughts.ThoughtInterval();
							pi.pawn.mindState.mentalStateHandler.MentalStateHandlerTick();
							pi.pawn.mindState.mentalBreaker.MentalBreakerTick();
						}

						if (!pi.isPrisoner && pi.pawn.mindState.mentalStateHandler.CurStateDef != null && pi.jobStatus < PawnJobStatus.MentalBreakingViolent)
						{
							MentalStateDef breakDef = pi.pawn.mindState.mentalStateHandler.CurStateDef;
							messages.Add(pi.pawn + " broke with " + breakDef.defName);
							if (breakDef.defName == "GiveUpExit" || breakDef.defName == "RunWild")
							{
								pi.jobStatus = PawnJobStatus.RanAway;

							}
							else if (breakDef.category == MentalStateCategory.Aggro)
							{
								pi.jobStatus = PawnJobStatus.MentalBreakingViolent;
							}
							else
							{
								pi.jobStatus = PawnJobStatus.MentalBreakingUseless;
							}
						}
						else if (pi.jobStatus == PawnJobStatus.MentalBreakingViolent || pi.jobStatus == PawnJobStatus.MentalBreakingUseless)
						{
							pi.jobStatus = PawnJobStatus.Idle;
							messages.Add(pi.pawn + " recovered!");
						}
						Find.TickManager.DebugSetTicksGame(stepTicksGame);
					}
				}
				
				// Acquire targets
				List<PawnInfo> availableTargets = new List<PawnInfo>();
				foreach (PawnInfo pi in currentPawns)
				{
					if (pi.jobStatus == PawnJobStatus.MentalBreakingViolent)
					{
						if (pi.combatTarget == null || !pi.combatTarget.CanBeAttacked)
						{
							currentPawns.Where(pi2 => pi != pi2 && pi2.CanBeAttacked).TryRandomElement(out pi.combatTarget);
							// Fight back!
							if (pi.combatTarget != null)
							{
								messages.Add(pi.pawn + " is rage attacking " + pi.combatTarget.pawn);
								pi.UpdateAttackDelay(true);
								if (!pi.combatTarget.IsUseless)
								{
									messages.Add(pi.combatTarget.pawn + " is counter attacking " + pi.pawn);
									pi.combatTarget.FightPawn(pi);
									pi.combatTarget.UpdateAttackDelay(true);
								}
							}
							else
							{
								messages.Add(pi.pawn + " is rage attacking nobody.");
							}
						}
						int missingAttackers = 3 - currentPawns.Count(pi2 => pi2.jobStatus == PawnJobStatus.Fighting && pi2.combatTarget == pi);
						for (int i = 0; i < missingAttackers; ++i)
						{
							availableTargets.Add(pi);
						}
					}
					else if (pi.jobStatus == PawnJobStatus.Fighting && pi.combatTarget != null && pi.combatTarget.jobStatus != PawnJobStatus.MentalBreakingViolent)
					{
						messages.Add(pi.pawn + " stopped attacking as the break is over for " + pi.combatTarget.pawn);
						pi.combatTarget.FightPawn(null);
						pi.FightPawn(null);
					}
				}

				availableTargets.Shuffle();
				int foundTargets = 0;
				foreach (PawnInfo pi in availableTargets)
				{
					PawnInfo attacker;
					if (!currentPawns.Where(pi2 => pi2.CanTakeJob).TryRandomElement(out attacker))
					{
						break;
					}

					messages.Add(attacker.pawn + " is trying to take down " + pi.pawn);
					attacker.FightPawn(pi);
					attacker.UpdateAttackDelay(true);
					foundTargets++;
				}
				availableTargets.RemoveRange(0, foundTargets);

				int combatTicksLeft = GenDate.TicksPerHour;
				int zeroTicks = 0;
				while (combatTicksLeft > 0 && currentPawns.Any(pi => pi.combatTarget != null))
				{
					IEnumerable<PawnInfo> combatants = currentPawns.Where(pi => pi.combatTarget != null);
					int minTicks = combatants.Min(pi => pi.ticksToAttack);
					if (minTicks == 0)
					{
						if (zeroTicks >= 3)
						{
							if (Verbose)
							{
								Log.Message(hour + ": Breaking combat loop...");
							}
							break;
						}
						zeroTicks++;
					}
					else
					{
						zeroTicks = 0;
					}

					foreach (PawnInfo combatant in combatants)
					{

						if (combatant.pawn.Dead || combatant.pawn.Downed)
						{
							messages.Add(combatTicksLeft + ": " + combatant.pawn + " is " + (combatant.pawn.Dead ? "dead." : "down."));
							if (combatant.combatTarget.jobStatus == PawnJobStatus.MentalBreakingViolent)
							{
								availableTargets.Add(combatant.combatTarget);
							}

							if (combatant.jobStatus == PawnJobStatus.MentalBreakingViolent)
							{
								availableTargets.RemoveAll(pi => pi == combatant);
							}

							combatant.FightPawn(null);
							continue;
						}

						combatant.ticksToAttack -= minTicks;
						if (combatant.ticksToAttack <= 0)
						{
							if (combatant.cooldownActive)
							{
								combatant.cooldownActive = false;
								combatant.UpdateAttackDelay(false);
								if (combatant.ticksToAttack > 0)
								{
									continue;
								}
							}

							if (combatant.combatTarget == null || !combatant.combatTarget.CanBeAttacked)
							{
								if (combatant.jobStatus == PawnJobStatus.MentalBreakingViolent)
								{
									currentPawns.Where(target => combatant != target && combatant.CanBeAttacked).TryRandomElement(out combatant.combatTarget);

									if (combatant.combatTarget != null)
									{
										messages.Add(combatTicksLeft + ": " + combatant.pawn + " is now rage attacking " + combatant.combatTarget.pawn);
										combatant.UpdateAttackDelay(true);
										if (!combatant.combatTarget.IsUseless)
										{
											messages.Add(combatTicksLeft + ": " + combatant.combatTarget.pawn + " is now counter attacking " + combatant.pawn);
											combatant.combatTarget.FightPawn(combatant);
											combatant.combatTarget.UpdateAttackDelay(true);
										}
									}
									else
									{
										messages.Add(combatTicksLeft + ": " + combatant.pawn + " is now rage attacking nobody.");
										// If we can't fight anything lets make sure nothing can fight us...
										availableTargets.RemoveAll(pi => pi == combatant);
										continue;
									}
								}
								else
								{
									messages.Add(combatTicksLeft + ": " + combatant.pawn + " stopped combat.");
									combatant.FightPawn(null);
									continue;
								}
							}

							float beforeSeverity = combatant.combatTarget.pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(hi => hi.Severity);
							string verb;
							
							// Apply ouchies.

							combatant.cooldownActive = true;
							Verb attackVerb = combatant.pawn.TryGetAttackVerb(combatant.combatTarget.pawn);
							if (attackVerb.IsMeleeAttack)
							{
								DoMeleeAttack(combatant, (Verb_MeleeAttack)attackVerb);
								combatant.UpdateAttackDelay(false);
								verb = "swung";
							}
							else
							{

								verb = "shot";
								Verb_LaunchProjectile projectileVerb = attackVerb as Verb_LaunchProjectile;
								if (projectileVerb != null)
								{
									for (int i = 0; i < (int)getShotsPerBurstMethod.Invoke(projectileVerb, null); ++i)
									{
										DoRangedAttack(combatant, projectileVerb);
									}
									combatant.UpdateAttackDelay(false);
								}
								else
								{
									// ???
									messages.Add(combatTicksLeft + ": " + "Killing " + combatant.pawn+ " because wtf?");
									combatant.pawn.Kill(null);
								}
							}
							float afterSeverity = combatant.combatTarget.pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(hi => hi.Severity);
							float deltaSeverity = afterSeverity - beforeSeverity;
							if (deltaSeverity == 0)
							{
								messages.Add(combatTicksLeft + ": " + combatant.pawn + " " + verb + " at " + combatant.combatTarget.pawn + " and missed.");
							}
							else
							{
								messages.Add(combatTicksLeft + ": " + combatant.pawn + " " + verb + " at " + combatant.combatTarget.pawn + " for " + deltaSeverity + " severity. (" + beforeSeverity + " -> " + afterSeverity + ")");
							}

							if (combatant.combatTarget.pawn.Dead)
							{
								messages.Add(combatTicksLeft + ": " + combatant.pawn + " killed " + combatant.combatTarget.pawn);
							}
						}
					}

					combatTicksLeft -= minTicks;
				}


				// Clean up corpses (if it's not night)
				if (hour % 24 <= 17)
				{
					int currentCarryCount = currentPawns.Count(pi => pi.jobStatus == PawnJobStatus.MovingCorpse);
					int targetCarryCount = Math.Max(1, (int)Math.Round(currentPawns.Count(pi => !pi.IsUseless) * 0.25f));
					if (targetCarryCount > currentCorpses.Count())
					{
						targetCarryCount = currentCorpses.Count();
					}

					int jobsLeft = targetCarryCount - currentCarryCount;
					while (jobsLeft > 0)
					{
						PawnInfo worker;
						bool rest = false;
						if (!currentPawns.Where(pi => pi.CanTakeJob && !HealthAIUtility.ShouldSeekMedicalRest(pi.pawn)).TryRandomElement(out worker))
						{
							if (!currentPawns.Where(pi => pi.CanTakeJob).TryRandomElement(out worker))
							{
								break;
							}
							rest = true;
						}
						CorpseInfo corpse;
						if (!currentCorpses.Where(ci => ci.carriedByInfo == null).TryRandomElement(out corpse))
						{
							break;
						}
						worker.jobStatus = PawnJobStatus.MovingCorpse;
						worker.carryInfo = corpse;
						corpse.carriedByInfo = worker;

						AddThought(worker, corpse.GetCleanupThought());

						if (rest)
						{
							messages.Add(worker.pawn + " interrupted rest to clean up " + corpse.corpse);
						}
						else
						{
							messages.Add(worker.pawn + " is cleaning up " + corpse.corpse);
						}
						jobsLeft--;
					}
				}

				// See corpses
				if (hour % 24 <= 17)
				{
					foreach (PawnInfo pi in currentPawns.Where(pi => pi.CanTakeJob))
					{
						foreach (CorpseInfo ci in currentCorpses.Where(ci => ci.carriedByInfo == null && Rand.Chance(IdleChancePerBodyPerHourObserve)))
						{
							AddThought(pi, ci.GetObserveThought());
						}
					}
				}

				// Apply plague
				{
					double generalSafeChance = 1.0;
					foreach (CorpseInfo ci in currentCorpses)
					{
						generalSafeChance *= ci.GetSafeChance();
					}

					foreach (PawnInfo pi in currentPawns.Where(pi => !pi.IsDeadOrGone && pi.pawn.health.hediffSet.HasHediff(HediffDefOf.Plague)))
					{
						generalSafeChance *= (1 - IdleSickColonistChancePlague);
					}

					foreach (PawnInfo pi in currentPawns)
					{
						if (pi.pawn.Dead || pi.jobStatus == PawnJobStatus.RanAway || pi.plagueStatus != PawnPlagueStatus.Normal)
						{
							continue;
						}

						double safeChance = generalSafeChance;

						if (pi.patient != null)
						{
							if (pi.pawn.health.hediffSet.HasHediff(HediffDefOf.Plague))
							{
								safeChance /= (1 - IdleSickColonistChancePlague);
								safeChance *= (1 - IdleSickColonistChancePlague * DoctoringChancePlagueMult);
							}
							pi.patient = null;
						}

						if (pi.jobStatus == PawnJobStatus.MovingCorpse)
						{
							safeChance /= pi.carryInfo.GetSafeChance();
							safeChance *= pi.carryInfo.GetSafeChance(CleaningCorpseChancePlagueMult);
						}

						if (!Rand.Chance((float)safeChance))
						{
							Hediff hediff = HediffMaker.MakeHediff(HediffDefOf.Plague, pi.pawn, null);
							pi.pawn.health.AddHediff(hediff, null, null, null);
							pi.plagueStatus = PawnPlagueStatus.Sick;

							messages.Add(pi.pawn + " got the plague! " + safeChance);
						}
					}
				}

				// Step health
				{
					int totalTicks = intervalCarry200 + GenDate.TicksPerHour;
					int numIntervals = totalTicks / 200;
					intervalCarry200 = totalTicks - numIntervals * 200;
					foreach (PawnInfo pi in currentPawns.Where(pi => !pi.IsDeadOrGone))
					{
						if (pi.CanTakeJob && HealthAIUtility.ShouldSeekMedicalRest(pi.pawn))
						{
							pi.jobStatus = PawnJobStatus.Resting;
						}

						Find.TickManager.DebugSetTicksGame(-pi.pawn.HashOffset());

						for (int i = 0; i < numIntervals; ++i)
						{
							foreach (HediffComp_Infecter hci in pi.pawn.health.hediffSet.hediffs.Select(h => h.TryGetComp<HediffComp_Infecter>()))
							{
								if (hci != null)
								{
									int ticksUntilInfect = (int)ticksUntilInfectField.GetValue(hci);
									if (ticksUntilInfect > 0)
									{
										ticksUntilInfect = Math.Max(ticksUntilInfect - 199, 1);
										ticksUntilInfectField.SetValue(hci, ticksUntilInfect);
									}
								}
							}
							pi.pawn.health.HealthTick();
							if (!pi.IsDeadOrGone)
							{
								foreach (Hediff h in pi.pawn.health.hediffSet.hediffs)
								{
									h.ageTicks += 199;
									HediffComp_TendDuration tender = h.TryGetComp<HediffComp_TendDuration>();
									if (tender != null)
									{
										tender.tendTicksLeft = Math.Max(tender.tendTicksLeft - 199, 0);
									}
								}

								for (int j = 0; j < 199; ++j)
								{

									immunityHandlerTickMethod.Invoke(pi.pawn.health.immunity, null);
								}
								if (pi.plagueStatus == PawnPlagueStatus.Sick && pi.GetPlagueImmunity() >= 1.0f)
								{
									messages.Add(pi.pawn + " IS IMMUNE! (" + pi.GetPlagueSeverity() + ")");
									pi.plagueStatus = PawnPlagueStatus.Immune;
								}
							}
							else
							{
								messages.Add(pi.pawn + " JUST DIED!");
								break;
							}
						}

						if (pi.plagueStatus == PawnPlagueStatus.Sick)
						{
							Hediff plague = pi.pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Plague);
							if (plague != null)
							{
								messages.Add(pi.pawn + " plague at: " + GetHediffOutput(plague));
							}
						}
						Find.TickManager.DebugSetTicksGame(stepTicksGame);
					}
				}

				foreach (PawnInfo pi in currentPawns)
				{
					if (pi.pawn.Dead && !pi.deathDetected)
					{
						foreach (PawnInfo pi2 in currentPawns)
						{
							if (!pi2.IsUseless)
							{
								AddThought(pi2, ThoughtDefOf.WitnessedDeathAlly);
								AddThought(pi2, ThoughtDefOf.WitnessedDeathBloodlust);
							}
							if (!pi2.IsDeadOrGone)
							{
								AddThought(pi2, ThoughtDefOf.KnowColonistDied);
							}
						}
						pi.deathDetected = true;
						pi.deathTick = stepTicksGame;
						CorpseInfo newCorpse = new CorpseInfo(pi.pawn.Corpse, CorpseStatus.Normal, 7, stepTicksGame, pi.pawn.Position, true, false);
						newCorpse.colonistPawnInfo = pi;
						currentCorpses.Add(newCorpse);
					}
				}

				if (messages.Count() > 0 && Verbose)
				{
					Log.Message(hour + ":  " + String.Join("\n     ", messages.ToArray()));
				}

				// Determine if we need to keep stepping by hour.
				//  * Are any corpses laying around?
				//  * Anyone in a mental break?
				//  * Anyone hurt/sick?
				continueStepping = continueStepping || hour < 100;
				continueStepping = continueStepping || currentPawns.Where(pi => pi.CanBeAttacked).Any(pi => pi.jobStatus != PawnJobStatus.Idle || HealthAIUtility.ShouldSeekMedicalRest(pi.pawn));
				continueStepping = continueStepping || currentCorpses.Count() > 0;
			}
			catch (Exception e)
			{
				if (messages.Count() > 0 && Verbose)
				{
					Log.Message(hour + ":  " + String.Join("\n     ", messages.ToArray()));
				}
				throw;
			}
			return continueStepping && currentPawns.Count(pi => !pi.IsDeadOrGone) > 0;
		}

		public void DoMeleeAttack(PawnInfo attacker, Verb_MeleeAttack attackVerb)
		{
			object[] parms = new object[] { new LocalTargetInfo(attacker.combatTarget.pawn) };
			if (Rand.Chance((float)getNonMissChanceMethod.Invoke(attackVerb, parms)))
			{
				if (!Rand.Chance((float)getDodgeChanceMethod.Invoke(attackVerb, parms)))
				{
					applyMeleeDamageToTargetMethod.Invoke(attackVerb, parms);
				}
			}
		}

		public void DoRangedAttack(PawnInfo attacker, Verb_LaunchProjectile attackVerb)
		{
			ThingDef projectile = attackVerb.Projectile;
			if (projectile == null)
			{
				return;
			}
			ShootLine shootLine = new ShootLine(attacker.pawn.Position, attacker.combatTarget.pawn.Position);

			Thing launcher = attackVerb.caster;
			Thing equipment = attackVerb.EquipmentSource;

			Vector3 drawPos = attackVerb.caster.DrawPos;
			Projectile projectile2 = (Projectile)GenSpawn.Spawn(projectile, shootLine.Source, launcher.Map, WipeMode.Vanish);
			if (attackVerb.verbProps.ForcedMissRadius > 0.5f)
			{
				float num = VerbUtility.CalculateAdjustedForcedMiss(attackVerb.verbProps.ForcedMissRadius, attacker.combatTarget.pawn.Position - attackVerb.caster.Position);
				if (num > 0.5f)
				{
					int max = GenRadial.NumCellsInRadius(num);
					int num2 = Rand.Range(0, max);
					if (num2 > 0)
					{
						IntVec3 c = attacker.combatTarget.pawn.Position + GenRadial.RadialPattern[num2];
						
						ProjectileHitFlags projectileHitFlags = ProjectileHitFlags.NonTargetWorld;
						if (Rand.Chance(0.5f))
						{
							projectileHitFlags = ProjectileHitFlags.All;
						}

						projectileHitFlags &= ~ProjectileHitFlags.NonTargetPawns;

						projectile2.Launch(launcher, drawPos, c, attacker.combatTarget.pawn.Position, projectileHitFlags, false, equipment, null);
						ticksToImpactField.SetValue(projectile2, 0);
						projectile2.Position = projectile2.ExactPosition.ToIntVec3();
						impactMethod.Invoke(projectile2, new object[] { null });
						return;
					}
				}
			}

			ShotReport shotReport = ShotReport.HitReportFor(attackVerb.caster, attackVerb, attacker.combatTarget.pawn);
			Thing randomCoverToMissInto = shotReport.GetRandomCoverToMissInto();
			ThingDef targetCoverDef = (randomCoverToMissInto == null) ? null : randomCoverToMissInto.def;
			if (!Rand.Chance(shotReport.AimOnTargetChance_IgnoringPosture))
			{
				shootLine.ChangeDestToMissWild(shotReport.AimOnTargetChance_StandardTarget, launcher.Map);
				
				ProjectileHitFlags projectileHitFlags2 = ProjectileHitFlags.NonTargetWorld;
				projectile2.Launch(launcher, drawPos, shootLine.Dest, attacker.combatTarget.pawn, projectileHitFlags2, false, equipment, targetCoverDef);
				ticksToImpactField.SetValue(projectile2, 0);
				projectile2.Position = projectile2.ExactPosition.ToIntVec3();
				impactMethod.Invoke(projectile2, new object[] { null });
				return;
			}

			if (!Rand.Chance(shotReport.PassCoverChance))
			{
				ProjectileHitFlags projectileHitFlags3 = ProjectileHitFlags.NonTargetWorld;
				projectile2.Launch(launcher, drawPos, randomCoverToMissInto, attacker.combatTarget.pawn, projectileHitFlags3, false, equipment, targetCoverDef);
				ticksToImpactField.SetValue(projectile2, 0);
				projectile2.Position = projectile2.ExactPosition.ToIntVec3();
				impactMethod.Invoke(projectile2, new object[] { randomCoverToMissInto });
				return;
			}

			ProjectileHitFlags projectileHitFlags4 = ProjectileHitFlags.IntendedTarget;

			projectile2.Launch(launcher, drawPos, attacker.combatTarget.pawn, attacker.combatTarget.pawn, projectileHitFlags4, false, equipment, targetCoverDef);
			ticksToImpactField.SetValue(projectile2, 0);
			projectile2.Position = projectile2.ExactPosition.ToIntVec3();
			impactMethod.Invoke(projectile2, new object[] { attacker.combatTarget.pawn });
		}

		private IntVec3 GetDropPosition(PawnInfo pi)
		{
			if (pi.corpseCleaned && freeStorage.Count() > 0)
			{
				return freeStorage.Pop();
			}
			else
			{
				return pi.pawn.PositionHeld;
			}
		}

		public void DropEverything(PawnInfo pi, ref List<SpawnedInfo> spawnedStuff)
		{
			if (pi.pawn.kindDef.destroyGearOnDrop)
			{
				pi.pawn.equipment.DestroyAllEquipment(DestroyMode.Vanish);
				pi.pawn.apparel.DestroyAll(DestroyMode.Vanish);
			}
			if (pi.pawn.InContainerEnclosed)
			{
				if (pi.pawn.carryTracker != null && pi.pawn.carryTracker.CarriedThing != null)
				{
					pi.pawn.carryTracker.innerContainer.TryTransferToContainer(pi.pawn.carryTracker.CarriedThing, pi.pawn.holdingOwner, true);
				}
				if (pi.pawn.equipment != null && pi.pawn.equipment.Primary != null)
				{
					pi.pawn.equipment.TryTransferEquipmentToContainer(pi.pawn.equipment.Primary, pi.pawn.holdingOwner);
				}
				if (pi.pawn.inventory != null)
				{
					pi.pawn.inventory.innerContainer.TryTransferAllToContainer(pi.pawn.holdingOwner, true);
				}
			}
			else if (pi.pawn.SpawnedOrAnyParentSpawned)
			{
				if (pi.pawn.carryTracker != null && pi.pawn.carryTracker.CarriedThing != null)
				{
					Thing thing;
					pi.pawn.carryTracker.TryDropCarriedThing(GetDropPosition(pi), ThingPlaceMode.Near, out thing, null);
					spawnedStuff.Add(new SpawnedInfo(thing, pi.deathTick));
				}
				
				if (pi.pawn.equipment != null)
				{
					for (int i = pi.pawn.equipment.AllEquipmentListForReading.Count - 1; i >= 0; i--)
					{
						ThingWithComps thingWithComps;
						pi.pawn.equipment.TryDropEquipment(pi.pawn.equipment.AllEquipmentListForReading[i], out thingWithComps, GetDropPosition(pi), false);
						spawnedStuff.Add(new SpawnedInfo(thingWithComps, pi.deathTick));
					}
				}
				if (pi.pawn.inventory != null && pi.pawn.inventory.innerContainer.TotalStackCount > 0)
				{
					for (int i = pi.pawn.inventory.innerContainer.Count - 1; i >= 0; i--)
					{
						Thing thing;
						pi.pawn.inventory.innerContainer.TryDrop(pi.pawn.inventory.innerContainer[i], GetDropPosition(pi), map, ThingPlaceMode.Near, out thing, delegate (Thing t, int unused)
						{
							t.SetForbidden(false, false);
						}, null);
						spawnedStuff.Add(new SpawnedInfo(thing, pi.deathTick));
					}
				}
			}
		}
	}

}
