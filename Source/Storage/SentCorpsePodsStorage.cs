using HugsLib.Utils;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace ReturnToSender.Storage
{
    public class SentCorpsePodInfo : IExposable
    {
        public int tickLanded = 0;
        public List<Thing> sentCorpses = null;
        public void ExposeData()
        {
            Scribe_Values.Look(ref tickLanded, "tickLanded");
            Scribe_Collections.Look(ref sentCorpses, "sentCorpses", LookMode.Deep);
        }
    }

    public class TileInfo : IExposable
    {
        public List<SentCorpsePodInfo> sentPods = null;

        public TileInfo()
        {
            sentPods = new List<SentCorpsePodInfo>();
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref sentPods, "sentPods", LookMode.Deep);
        }

    }

    public class SentCorpsePodsStorage : UtilityWorldObject, IExposable
    {
        private Dictionary<int, TileInfo> tileInfoStorage = new Dictionary<int, TileInfo>();
        private List<int> tileWorkingList;
        private List<TileInfo> infoStorageWorkingList;
        private int nextRemoveTick = int.MaxValue;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref tileInfoStorage, "tileInfoStorage", LookMode.Value, LookMode.Deep, ref tileWorkingList, ref infoStorageWorkingList);
            Scribe_Values.Look(ref nextRemoveTick, "nextRemoveTick", int.MaxValue);
        }

        public override void Tick()
        {
            base.Tick();
            if (Find.TickManager.TicksAbs >= nextRemoveTick)
            {
                int removeBefore = nextRemoveTick - GenDate.TicksPerYear;
                int nextOldest = int.MaxValue - GenDate.TicksPerYear;
                foreach (TileInfo info in tileInfoStorage.Values)
                {
                    while (info.sentPods.Count() > 0)
                    {
                        int tickLanded = info.sentPods.First().tickLanded;
                        if (tickLanded <= removeBefore)
                        {
                            info.sentPods.RemoveAt(0);
                        }
                        else
                        {
                            if (tickLanded < nextOldest)
                            {
                                nextOldest = tickLanded;
                            }

                            break;
                        }
                    }
                }

                tileInfoStorage.RemoveAll(kvp => kvp.Value.sentPods.Count() == 0);

                nextRemoveTick = nextOldest + GenDate.TicksPerYear;
            }
        }

        public void AddPodToTile(int tile, ActiveCorpsePodInfo corpsePod)
        {
            if (!tileInfoStorage.ContainsKey(tile))
            {
                tileInfoStorage[tile] = new TileInfo();
            }
            SentCorpsePodInfo info = new SentCorpsePodInfo();
            info.tickLanded = Find.TickManager.TicksAbs;
            info.sentCorpses = corpsePod.innerContainer.ToList();
            tileInfoStorage[tile].sentPods.Add(info);
            
            if (info.tickLanded + GenDate.TicksPerYear < nextRemoveTick)
            {
                nextRemoveTick = info.tickLanded + GenDate.TicksPerYear;
            }
        }

        public bool IsStoredCorpse(Corpse c)
        {
            return tileInfoStorage.Values.Any(l => l.sentPods.Any(i => i.sentCorpses.Contains(c)));
        }

        public bool TryGetPodInfoForTile(int tile, out List<SentCorpsePodInfo> infos)
        {
            TileInfo tmp;
            infos = null;
            if (tileInfoStorage.TryGetValue(tile, out tmp))
            {
                infos = tmp.sentPods;
                return true;
            }
            return false;
        }

        public void RemoveAllPodInfoForTile(int tile)
        {
            if (tileInfoStorage.Remove(tile))
            {
                int nextOldest = int.MaxValue - GenDate.TicksPerYear;
                foreach (TileInfo info in tileInfoStorage.Values)
                {
                    if (info.sentPods.Count() > 0)
                    {
                        int tickLanded = info.sentPods.First().tickLanded;
                        
                        if (tickLanded < nextOldest)
                        {
                            nextOldest = tickLanded;
                        }
                    }
                }

                nextRemoveTick = nextOldest + GenDate.TicksPerYear;
            }
        }
    }
}
