using DevTools.Testing;
using RimWorld;
using Stockpile_Ranking;
using Verse;

namespace Stockpile_Ranking.Tests;

[TestFixture(TestType.Playing)]
public class HopperStorageGroupTests
{
	[Test]
	public void AddingFirstRankToLinkedHoppersRefreshesStorageGroup()
	{
		var map = Find.CurrentMap;
		var comp = RankComp.Get();
		Expect.IsNotNull(map);
		Expect.IsNotNull(comp);
		if (map is null || comp is null)
			return;

		var cells = map.AllCells
			.Where(cell => cell.Standable(map) && !cell.GetThingList(map).Any())
			.Take(2)
			.ToList();
		Expect.AreEqual(2, cells.Count, "The test map needs two free hopper cells.");
		if (cells.Count < 2)
			return;

		var hopperDef = DefDatabase<ThingDef>.GetNamed("Hopper");
		var firstHopper = (Building_Storage)ThingMaker.MakeThing(hopperDef);
		var secondHopper = (Building_Storage)ThingMaker.MakeThing(hopperDef);
		var group = map.storageGroups.NewGroup();
		var settings = group.GetStoreSettings();
		var wasDirty = comp.dirty;
		var previousUsedFilter = comp.usedFilter.TryGetValue(settings, out var usedFilter)
			? usedFilter
			: null;

		try
		{
			GenSpawn.Spawn(firstHopper, cells[0], map);
			GenSpawn.Spawn(secondHopper, cells[1], map);
			group.InitFrom(firstHopper);
			firstHopper.SetStorageGroup(group);
			secondHopper.SetStorageGroup(group);

			comp.dirty = false;
			comp.usedFilter.Remove(settings);
			comp.AddFilter(settings);

			Expect.IsTrue(comp.dirty,
				"Adding the first rank must schedule a used-filter refresh after the rank is registered.");
			Expect.AreEqual(2, group.MemberCount);
			Expect.IsTrue(group.members.Contains(firstHopper));
			Expect.IsTrue(group.members.Contains(secondHopper));

			comp.GameComponentTick();
			var ranks = comp.GetRanks(settings, create: false);
			Expect.IsNotNull(ranks);
			if (ranks is not null)
				Expect.ReferencesAreEqual(ranks[0], RankComp.UsedFilter(settings));

			Expect.AreEqual(2, group.MemberCount,
				"Refreshing ranked settings must not unlink existing hopper members.");
		}
		finally
		{
			comp.rankedSettings.Remove(settings);
			comp.usedFilter.Remove(settings);
			if (previousUsedFilter is not null)
				comp.usedFilter[settings] = previousUsedFilter;
			comp.dirty = wasDirty;
			if (firstHopper.Spawned)
				firstHopper.Destroy();
			if (secondHopper.Spawned)
				secondHopper.Destroy();
		}
	}
}
