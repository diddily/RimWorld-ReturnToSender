using Harmony;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.Sound;

using ReturnToSender.Buildings;

namespace ReturnToSender
{
	public class CorpsePodIncoming : DropPodIncoming
	{
		private static MethodInfo SpawnShrapnelMethod = AccessTools.Method(typeof(SkyfallerShrapnelUtility), "SpawnShrapnel");
		private static MethodInfo DrawDropSpotShadowMethod = AccessTools.Method(typeof(DropPodIncoming), "DrawDropSpotShadow");
		public override Color DrawColor
		{
			get
			{
				return innerContainer[0].DrawColor;
			}
		}

		public override Graphic Graphic
		{
			get
			{
				return base.Graphic.data.GraphicColoredFor(this);
			}
		}

		public override void DrawAt(Vector3 drawLoc, bool flip = false)
		{
			Thing thingForGraphic = innerContainer[0];
			float extraRotation = (!this.def.skyfaller.rotateGraphicTowardsDirection) ? 0f : this.angle;
			this.Graphic.Draw(drawLoc, (!flip) ? thingForGraphic.Rotation : thingForGraphic.Rotation.Opposite, thingForGraphic, extraRotation);
			DrawDropSpotShadowMethod.Invoke(this, null);
		}

		protected override void Impact()
		{
			for (int i = 0; i < 4; i++)
			{
				Vector3 loc = base.Position.ToVector3Shifted() + Gen.RandomHorizontalVector(1f);
				if (!loc.ShouldSpawnMotesAt(Map) || Map.moteCounter.SaturatedLowPriority)
				{
					continue;
				}
				MoteThrown moteThrown = (MoteThrown)ThingMaker.MakeThing(RTS_DefOf.RTS_Mote_Giblets, null);
				moteThrown.Scale = 1.9f * 0.2f;
				moteThrown.rotationRate = (float)Rand.Range(-60, 60);
				moteThrown.exactPosition = loc;
				moteThrown.SetVelocity((float)Rand.Range(0, 360), Rand.Range(0.6f, 0.75f));
				GenSpawn.Spawn(moteThrown, loc.ToIntVec3(), Map, WipeMode.Vanish);
			}

			SpawnShrapnelMethod.Invoke(null, new object[] { ThingDefOf.Filth_RubbleBuilding, def.skyfaller.rubbleShrapnelCountRange.RandomInRange * -1, Position, Map, shrapnelDirection, def.skyfaller.shrapnelDistanceFactor });
			SpawnShrapnelMethod.Invoke(null, new object[] { ThingDefOf.Filth_Blood, def.skyfaller.metalShrapnelCountRange.RandomInRange * -1, Position, Map, shrapnelDirection, def.skyfaller.shrapnelDistanceFactor });

			base.Impact();

		}
	}

	public class CorpsePodLeaving : DropPodLeaving
	{
		public override Color DrawColor
		{
			get
			{
				return innerContainer[0].DrawColor;
			}
		}

		public override Graphic Graphic
		{
			get
			{
				return base.Graphic.data.GraphicColoredFor(this);
			}
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			base.SpawnSetup(map, respawningAfterLoad);
			if (!respawningAfterLoad)
			{
				for (int i = Contents.innerContainer.Count - 1; i >= 0; i--)
				{
					Corpse c = Contents.innerContainer[i] as Corpse;
					if (c != null)
					{
						int r = Rand.RangeInclusive(-3, 2);
						if (r > 0)
						{
							FilthMaker.MakeFilth(Position, Map, c.InnerPawn.RaceProps.BloodDef, c.InnerPawn.LabelIndefinite(), r);
						}
					}
				}
			}
		}
	}

	public class ActiveCorpsePodInfo : ActiveDropPodInfo
	{
		public ThingDef stuff;
		public float fillPercent;

		public ActiveCorpsePodInfo(CompTransporter ct)
		{
			fillPercent = CollectionsMassCalculator.MassUsage<Thing>(ct.GetDirectlyHeldThings().ToList(), IgnorePawnsInventoryMode.DontIgnore, true, false) / 300.0f;
			stuff = ct.parent.Stuff;
		}

		public void PostPostExpose()
		{
			Scribe_Defs.Look<ThingDef>(ref stuff, "stuff");
			Scribe_Values.Look<float>(ref fillPercent, "fillPercent");
		}
	}

	public class ActiveCorpsePod : ActiveDropPod
	{
		public override Graphic Graphic
		{
			get
			{
				return Utilities.GetActivePodGraphicData(FillPercent).GraphicColoredFor(this);
			}
		}

		public float FillPercent
		{
			get
			{
				return CollectionsMassCalculator.MassUsage<Thing>(Contents.GetDirectlyHeldThings().ToList(), IgnorePawnsInventoryMode.DontIgnore, true, false) / 300.0f;
			}
		 }

		public override void Tick()
		{
			if (Contents == null)
			{
				return;
			}
			Contents.innerContainer.ThingOwnerTick(true);
			if (base.Spawned)
			{
				this.age++;
			   // if (this.age > Contents.openDelay)
				{
					this.PodOpen2();
				}
			}
		}

		public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
		{
			if (Contents != null)
			{
				if (mode == DestroyMode.KillFinalize)
				{
					for (int i = Contents.innerContainer.Count - 1; i >= 0; i--)
					{
						Corpse c = Contents.innerContainer[i] as Corpse;
						if (c != null)
						{
							FilthMaker.MakeFilth(Position, Map, c.InnerPawn.RaceProps.BloodDef, c.InnerPawn.LabelIndefinite(), Rand.RangeInclusive(2,5));
						}
					}
				}
				Contents.innerContainer.ClearAndDestroyContents(DestroyMode.Vanish);
			}
			base.Destroy(mode);
		}

		private void PodOpen2()
		{
			for (int i = Contents.innerContainer.Count - 1; i >= 0; i--)
			{
				Thing thing = Contents.innerContainer[i];
				Thing thing2 = null;
				GenPlace.TryPlaceThing(thing, base.Position, base.Map, ThingPlaceMode.Near, out thing2, delegate (Thing placedThing, int count)
				{
					
				}, null);

				Corpse c = thing2 as Corpse;
				if (c != null)
				{
					RotStage rot = c.GetRotStage();
					int spray = 1;
					int giblet = 5;
					if (rot == RotStage.Rotting)
					{
						spray = 3;
						giblet = 7;
					}
					
					int r = Rand.RangeInclusive(0, 9);
					if (rot != RotStage.Dessicated || r >= giblet)
					{
						foreach (Pawn current in Map.mapPawns.SpawnedPawnsInFaction(c.InnerPawn.Faction))
						{
							if (current.needs != null && current.needs.mood != null && current.needs.mood.thoughts != null)
							{
								current.needs.mood.thoughts.memories.TryGainMemory(RTS_DefOf.RTS_SameFactionCorpse, null);
							}
						}
					}

					if (r < spray) // Blood spray
					{
						if (rot != RotStage.Dessicated)
						{
							foreach (Pawn current in Map.mapPawns.SpawnedPawnsInFaction(Map.ParentFaction))
							{
								if (current.needs != null && current.needs.mood != null && current.needs.mood.thoughts != null)
								{
									current.needs.mood.thoughts.memories.TryGainMemory(RTS_DefOf.RTS_SentVaporizedCorpse0, null);
									current.needs.mood.thoughts.memories.TryGainMemory(RTS_DefOf.RTS_SentVaporizedCorpse1, null);
									current.needs.mood.thoughts.memories.TryGainMemory(RTS_DefOf.RTS_SentVaporizedCorpse2, null);
								}
							}
							FilthMaker.MakeFilth(c.Position, Map, c.InnerPawn.RaceProps.BloodDef, c.InnerPawn.LabelIndefinite(), Rand.RangeInclusive(5, 8));
						}
						c.Destroy(DestroyMode.KillFinalize);
					}
					else if (r < giblet) // Blood and meat
					{
						if (rot != RotStage.Dessicated)
						{
							foreach (Pawn current in Map.mapPawns.SpawnedPawnsInFaction(Map.ParentFaction))
							{
								if (current.needs != null && current.needs.mood != null && current.needs.mood.thoughts != null)
								{
									current.needs.mood.thoughts.memories.TryGainMemory(RTS_DefOf.RTS_SentGibletCorpse0, null);
									current.needs.mood.thoughts.memories.TryGainMemory(RTS_DefOf.RTS_SentGibletCorpse1, null);
									current.needs.mood.thoughts.memories.TryGainMemory(RTS_DefOf.RTS_SentGibletCorpse2, null);
								}
							}
							FilthMaker.MakeFilth(c.Position, Map, c.InnerPawn.RaceProps.BloodDef, c.InnerPawn.LabelIndefinite(), Rand.RangeInclusive(2, 3));
							float eff = Rand.Range(0.5f, 0.7071067811865f);
							Thing thing3 = null;
							foreach (Thing pb in c.InnerPawn.ButcherProducts(c.InnerPawn, eff * eff))
							{
								GenPlace.TryPlaceThing(pb, c.Position, base.Map, ThingPlaceMode.Near, out thing3);
							}
						}
						c.Destroy(DestroyMode.KillFinalize);
					}
					else
					{
						foreach (Pawn current in Map.mapPawns.SpawnedPawnsInFaction(Map.ParentFaction))
						{
							if (current.needs != null && current.needs.mood != null && current.needs.mood.thoughts != null)
							{
								current.needs.mood.thoughts.memories.TryGainMemory(RTS_DefOf.RTS_SentIntactCorpse0, null);
								current.needs.mood.thoughts.memories.TryGainMemory(RTS_DefOf.RTS_SentIntactCorpse1, null);
							}
						}
						FilthMaker.MakeFilth(c.Position, Map, c.InnerPawn.RaceProps.BloodDef, c.InnerPawn.LabelIndefinite(), 1);
					}
				}
			}
			Contents.innerContainer.ClearAndDestroyContents(DestroyMode.Vanish);
		  
			RTS_DefOf.RTS_CorpsePod_Open.PlayOneShot(new TargetInfo(base.Position, base.Map, false));
			this.Destroy(DestroyMode.Vanish);
		}
	}
}
