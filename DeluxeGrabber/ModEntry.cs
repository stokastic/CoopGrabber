using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using System.Collections.Generic;
using System.Linq;

namespace DeluxeGrabber
{
    public class ModEntry : Mod
    {
        private ModConfig _config;

        private readonly List<string> _quality = new List<string>()
        {
            "basic",
            "silver",
            "gold",
            "3",
            "iridium"
        };

        private const int Farming = 0;
        private const int Foraging = 2;

        public override void Entry(IModHelper helper)
        {
            _config = Helper.ReadConfig<ModConfig>();
            Helper.ConsoleCommands.Add("printLocation", "Print current map and tile location", PrintLocation);
            Helper.ConsoleCommands.Add("setForagerLocation", "Set current location as global grabber location",
                SetForagerLocation);
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.World.ObjectListChanged += OnObjectListChanged;
        }

        private void SetForagerLocation(string arg1, string[] arg2)
        {
            if (!Context.IsWorldReady)
            {
                return;
            }

            _config.GlobalForageMap = Game1.player.currentLocation.Name;
            _config.GlobalForageTileX = Game1.player.getTileX();
            _config.GlobalForageTileY = Game1.player.getTileY();
            Helper.WriteConfig(_config);
        }

        private void PrintLocation(string arg1, string[] arg2)
        {
            if (!Context.IsWorldReady)
            {
                return;
            }

            Monitor.Log($"Map: {Game1.player.currentLocation.Name}");
            Monitor.Log($"Tile: {Game1.player.getTileLocation()}");
        }

        private void OnObjectListChanged(object sender, ObjectListChangedEventArgs e)
        {
            if (!_config.DoHarvestTruffles)
            {
                return;
            }

            var foragerMap = Game1.getLocationFromName(_config.GlobalForageMap);
            if (foragerMap == null)
            {
                return;
            }

            foragerMap.Objects.TryGetValue(new Vector2(_config.GlobalForageTileX, _config.GlobalForageTileY),
                out var grabber);
            if (grabber == null || !grabber.Name.Contains("Grabber"))
            {
                return;
            }

            var random = new System.Random();
            foreach (var pair in e.Added)
            {
                if (pair.Value.ParentSheetIndex != 430 || pair.Value.bigCraftable.Value)
                {
                    continue;
                }

                if (((Chest) grabber.heldObject.Value).items.Count >= 36)
                {
                    return;
                }

                var obj = pair.Value;
                if (obj.Stack == 0)
                {
                    obj.Stack = 1;
                }

                if (!obj.isForage(null) && !IsGrabbableWorld(obj))
                {
                    continue;
                }

                if (Game1.player.professions.Contains(16))
                {
                    obj.Quality = 4;
                }
                else if (random.NextDouble() < Game1.player.ForagingLevel / 30.0)
                {
                    obj.Quality = 2;
                }
                else if (random.NextDouble() < Game1.player.ForagingLevel / 15.0)
                {
                    obj.Quality = 1;
                }

                if (Game1.player.professions.Contains(13))
                {
                    while (random.NextDouble() < 0.2)
                    {
                        obj.Stack += 1;
                    }
                }

                Monitor.Log($"Grabbing truffle: {obj.Stack}x{_quality[obj.Quality]}");
                ((Chest) grabber.heldObject.Value).addItem(obj);
                e.Location.Objects.Remove(pair.Key);
                if (_config.DoGainExperience)
                {
                    GainExperience(Foraging, 7);
                }
            }

            if (((Chest) grabber.heldObject.Value).items.Count > 0)
            {
                grabber.showNextIndex.Value = true;
            }
        }

        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            AutograbBuildings();
            AutograbCrops();
            AutograbWorld();
        }

        private void AutograbBuildings()
        {
            var grabables = new List<Vector2>(); // stores a list of all items which can be grabbed
            var itemsAdded = new Dictionary<string, int>();
            foreach (var building in Game1.getFarm().buildings)
            {
                Object grabber = null; // stores a reference to the autograbber
                grabables.Clear();
                itemsAdded.Clear();
                if (building.buildingType.Contains("Coop") || building.buildingType.Contains("Slime"))
                {
                    Monitor.Log(
                        $"Searching {building.buildingType} at <{building.tileX},{building.tileY}> for auto-grabber");
                    var location = building.indoors.Value;
                    if (location != null)
                    {
                        // populate list of objects which are grabbable, and find an autograbber
                        foreach (var pair in location.Objects.Pairs)
                        {
                            if (pair.Value.Name.Contains("Grabber"))
                            {
                                Monitor.Log($"  Grabber found  at {pair.Key}");
                                grabber = pair.Value;
                            }

                            if (!IsGrabbableCoop(pair.Value)) continue;
                            Monitor.Log($"    Found grabbable item at {pair.Key}: {pair.Value.Name}");
                            grabables.Add(pair.Key);
                        }

                        if (grabber == null)
                        {
                            Monitor.Log("  No grabber found");
                            continue;
                        }

                        // add items to grabber and remove from floor
                        var full = false;
                        foreach (var tile in grabables)
                        {
                            if (((Chest) grabber.heldObject.Value).items.Count >= 36)
                            {
                                Monitor.Log($"  Grabber is full");
                                full = true;
                                break;
                            }

                            if (location.objects[tile].Name.Contains("Slime Ball"))
                            {
                                var random = new System.Random((int) Game1.stats.daysPlayed +
                                                               (int) Game1.uniqueIDForThisGame + (int) tile.X * 77 +
                                                               (int) tile.Y * 777 + 2);
                                ((Chest) grabber.heldObject.Value)?.addItem(new Object(766, random.Next(10, 21)));
                                var i = 0;
                                while (random.NextDouble() < 0.33)
                                {
                                    i++;
                                }

                                if (i > 0)
                                {
                                    ((Chest) grabber.heldObject.Value)?.addItem(new Object(557, i));
                                }
                            }
                            else if (((Chest) grabber.heldObject.Value)?.addItem(location.Objects[tile]) != null)
                            {
                                continue;
                            }

                            var name = location.Objects[tile].Name;
                            if (!itemsAdded.ContainsKey(name))
                            {
                                itemsAdded.Add(name, 1);
                            }
                            else
                            {
                                itemsAdded[name] += 1;
                            }

                            location.Objects.Remove(tile);
                            if (_config.DoGainExperience)
                            {
                                GainExperience(Farming, 5);
                            }
                        }

                        if (full)
                        {
                            continue;
                        }

                        foreach (var pair in itemsAdded)
                        {
                            var plural = "";
                            if (pair.Value != 1) plural = "s";
                            Monitor.Log($"  Added {pair.Value} {pair.Key}{plural}");
                        }
                    }
                }

                // update sprite if grabber has items in it
                if (grabber != null && ((Chest) grabber.heldObject.Value).items.Count > 0)
                {
                    grabber.showNextIndex.Value = true;
                }
            }
        }

        private void AutograbCrops()
        {
            if (!_config.DoHarvestCrops)
            {
                return;
            }

            var range = _config.GrabberRange;
            foreach (var location in Game1.locations)
            {
                foreach (var pair in location.Objects.Pairs)
                {
                    if (!pair.Value.Name.Contains("Grabber")) continue;
                    var grabber = pair.Value;
                    if (grabber.heldObject.Value == null || (grabber.heldObject.Value as Chest).items.Count >= 36)
                    {
                        continue;
                    }

                    var full = (grabber.heldObject.Value as Chest).items.Count >= 36;
                    for (var x = (int) pair.Key.X - range; x < pair.Key.X + range + 1; x++)
                    {
                        for (var y = (int) pair.Key.Y - range; y < pair.Key.Y + range + 1 && !full; y++)
                        {
                            var tile = new Vector2(x, y);
                            if (location.terrainFeatures.ContainsKey(tile) &&
                                location.terrainFeatures[tile] is HoeDirt dirt)
                            {
                                var harvest = GetHarvest(dirt, tile);
                                if (harvest != null)
                                {
                                    (grabber.heldObject.Value as Chest).addItem(harvest);
                                    if (_config.DoGainExperience)
                                    {
                                        GainExperience(Foraging, 3);
                                    }
                                }
                            }
                            else if (location.Objects.ContainsKey(tile) && location.Objects[tile] is IndoorPot pot)
                            {
                                var harvest = GetHarvest(pot.hoeDirt.Value, tile);
                                if (harvest != null)
                                {
                                    (grabber.heldObject.Value as Chest).addItem(harvest);
                                    if (_config.DoGainExperience)
                                    {
                                        GainExperience(Foraging, 3);
                                    }
                                }
                            }
                            else if (location.terrainFeatures.ContainsKey(tile) &&
                                     location.terrainFeatures[tile] is FruitTree fruitTree)
                            {
                                var fruit = GetFruit(fruitTree);
                                if (fruit != null)
                                {
                                    (grabber.heldObject.Value as Chest).addItem(fruit);
                                    if (_config.DoGainExperience)
                                    {
                                        GainExperience(Foraging, 3);
                                    }
                                }
                            }

                            full = (grabber.heldObject.Value as Chest).items.Count >= 36;
                        }
                    }

                    if (grabber != null && (grabber.heldObject.Value as Chest).items.Count > 0)
                    {
                        grabber.showNextIndex.Value = true;
                    }
                }
            }
        }

        private Object GetHarvest(HoeDirt dirt, Vector2 tile)
        {
            var crop = dirt.crop;
            var stack = 0;
            if (crop is null)
            {
                return null;
            }

            if (!_config.DoHarvestFlowers)
            {
                switch (crop.indexOfHarvest.Value)
                {
                    case 421: return null; // sunflower
                    case 593: return null; // summer spangle
                    case 595: return null; // fairy rose
                    case 591: return null; // tulip
                    case 597: return null; // blue jazz
                    case 376: return null; // poppy
                }
            }

            if (crop == null || crop.currentPhase.Value < crop.phaseDays.Count - 1 ||
                (crop.fullyGrown.Value && crop.dayOfCurrentPhase.Value > 0)) return null;
            var num1 = 1;
            var num2 = 0;
            var num3 = 0;
            if (crop.indexOfHarvest.Value == 0) return null;
            var random = new System.Random((int) tile.X * 7 + (int) tile.Y * 11 + (int) Game1.stats.DaysPlayed +
                                           (int) Game1.uniqueIDForThisGame);
            switch (dirt.fertilizer.Value)
            {
                case 368:
                    num3 = 1;
                    break;
                case 369:
                    num3 = 2;
                    break;
            }

            var num4 = 0.2 * (Game1.player.FarmingLevel / 10.0) +
                       0.2 * num3 * ((Game1.player.FarmingLevel + 2.0) / 12.0) + 0.01;
            var num5 = System.Math.Min(0.75, num4 * 2.0);
            if (random.NextDouble() < num4) num2 = 2;
            else if (random.NextDouble() < num5) num2 = 1;
            if (crop.minHarvest.Value > 1 || crop.maxHarvest.Value > 1)
                num1 = random.Next(crop.minHarvest.Value,
                    System.Math.Min(crop.minHarvest.Value + 1,
                        crop.maxHarvest.Value + 1 +
                        Game1.player.FarmingLevel / crop.maxHarvestIncreasePerFarmingLevel.Value));
            if (crop.chanceForExtraCrops.Value > 0.0)
            {
                while (random.NextDouble() < System.Math.Min(0.9, crop.chanceForExtraCrops.Value)) ++num1;
            }

            if (crop.harvestMethod.Value == 1)
            {
                for (var i = 0; i < num1; i++)
                {
                    stack += 1;
                }

                if (crop.regrowAfterHarvest.Value != -1)
                {
                    crop.dayOfCurrentPhase.Value = crop.regrowAfterHarvest.Value;
                    crop.fullyGrown.Value = true;
                }
                else
                {
                    dirt.crop = null;
                }

                return new Object(crop.indexOfHarvest.Value, stack, false, -1, num2);
            }
            else
            {
                var harvest = !crop.programColored.Value
                    ? new Object(crop.indexOfHarvest.Value, 1, false, -1, num2)
                    : new ColoredObject(crop.indexOfHarvest.Value, 1, crop.tintColor.Value) {Quality = num2};
                if (crop.regrowAfterHarvest.Value != -1)
                {
                    crop.dayOfCurrentPhase.Value = crop.regrowAfterHarvest.Value;
                    crop.fullyGrown.Value = true;
                }
                else
                {
                    dirt.crop = null;
                }

                return harvest;
            }
        }

        private Object GetFruit(FruitTree fruitTree)
        {
            var quality = 0;
            if (fruitTree is null)
            {
                return null;
            }

            if (!_config.DoHarvestFruitTrees)
            {
                return null;
            }

            if (fruitTree.growthStage.Value < 4) return null;
            if (fruitTree.fruitsOnTree.Value <= 0) return null;
            if (fruitTree.daysUntilMature.Value <= -112)
            {
                quality = 1;
            }

            if (fruitTree.daysUntilMature.Value <= -224)
            {
                quality = 2;
            }

            if (fruitTree.daysUntilMature.Value <= -336)
            {
                quality = 4;
            }

            if (fruitTree.struckByLightningCountdown.Value > 0)
            {
                quality = 0;
            }

            var fruit = new Object(fruitTree.indexOfFruit.Value, fruitTree.fruitsOnTree.Value, false, -1, quality);
            fruitTree.fruitsOnTree.Value = 0;
            return fruit;
        }

        private void AutograbWorld()
        {
            if (!_config.DoGlobalForage)
            {
                return;
            }

            var grabbables = new List<Vector2>(); // stores a list of all items which can be grabbed
            var itemsAdded = new Dictionary<string, int>();
            var random = new System.Random();
            var foragerMap = Game1.getLocationFromName(_config.GlobalForageMap);
            if (foragerMap == null)
            {
                Monitor.Log($"Invalid GlobalForageMap '{_config.GlobalForageMap}");
                return;
            }

            foragerMap.Objects.TryGetValue(new Vector2(_config.GlobalForageTileX, _config.GlobalForageTileY),
                out var grabber);
            if (grabber == null || !grabber.Name.Contains("Grabber"))
            {
                Monitor.Log(
                    $"No auto-grabber at {_config.GlobalForageMap}: <{_config.GlobalForageTileX}, {_config.GlobalForageTileY}>");
                return;
            }

            foreach (var location in Game1.locations)
            {
                grabbables.Clear();
                itemsAdded.Clear();
                grabbables.AddRange(from pair in location.Objects.Pairs
                    where !pair.Value.bigCraftable.Value
                    where IsGrabbableWorld(pair.Value) || pair.Value.isForage(null)
                    select pair.Key);

                // Check for spring onions
                if (location.Name.Equals("Forest"))
                {
                    foreach (var feature in location.terrainFeatures.Values)
                    {
                        if ((grabber.heldObject.Value as Chest).items.Count >= 36)
                        {
                            Monitor.Log("Global grabber full", LogLevel.Info);
                            return;
                        }

                        if (!(feature is HoeDirt dirt)) continue;
                        if (dirt.crop == null) continue;
                        if (!dirt.crop.forageCrop.Value || dirt.crop.whichForageCrop.Value != 1
                        ) continue; // spring onion
                        var onion = new Object(399, 1);
                        if (Game1.player.professions.Contains(16))
                        {
                            onion.Quality = 4;
                        }
                        else if (random.NextDouble() < Game1.player.ForagingLevel / 30.0)
                        {
                            onion.Quality = 2;
                        }
                        else if (random.NextDouble() < Game1.player.ForagingLevel / 15.0)
                        {
                            onion.Quality = 1;
                        }

                        if (Game1.player.professions.Contains(13))
                        {
                            while (random.NextDouble() < 0.2)
                            {
                                onion.Stack += 1;
                            }
                        }

                        (grabber.heldObject.Value as Chest).addItem(onion);
                        if (!itemsAdded.ContainsKey("Spring Onion"))
                        {
                            itemsAdded.Add("Spring Onion", 1);
                        }
                        else
                        {
                            itemsAdded["Spring Onion"] += 1;
                        }

                        dirt.crop = null;
                        if (_config.DoGainExperience)
                        {
                            GainExperience(Foraging, 3);
                        }
                    }
                }

                // Check for berry bushes
                foreach (var feature in location.largeTerrainFeatures)
                {
                    int berryIndex;
                    string berryType;
                    if (Game1.currentSeason == "spring")
                    {
                        berryType = "Salmon Berry";
                        berryIndex = 296;
                    }
                    else if (Game1.currentSeason == "fall")
                    {
                        berryType = "Blackberry";
                        berryIndex = 410;
                    }
                    else
                    {
                        break;
                    }

                    if ((grabber.heldObject.Value as Chest).items.Count >= 36)
                    {
                        Monitor.Log("Global grabber full", LogLevel.Info);
                        return;
                    }

                    if (!(feature is Bush bush)) continue;
                    if (!bush.inBloom(Game1.currentSeason, Game1.dayOfMonth) ||
                        bush.tileSheetOffset.Value != 1) continue;
                    var berry = new Object(berryIndex, 1 + Game1.player.FarmingLevel / 4);
                    if (Game1.player.professions.Contains(16))
                    {
                        berry.Quality = 4;
                    }

                    bush.tileSheetOffset.Value = 0;
                    bush.setUpSourceRect();
                    (grabber.heldObject.Value as Chest).addItem(berry);
                    if (!itemsAdded.ContainsKey(berryType))
                    {
                        itemsAdded.Add(berryType, 1);
                    }
                    else
                    {
                        itemsAdded[berryType] += 1;
                    }
                }

                // add items to grabber and remove from floor
                foreach (var tile in grabbables)
                {
                    if ((grabber.heldObject.Value as Chest).items.Count >= 36)
                    {
                        Monitor.Log("Global grabber full", LogLevel.Info);
                        return;
                    }

                    var obj = location.Objects[tile];
                    if (Game1.player.professions.Contains(16))
                    {
                        obj.Quality = 4;
                    }
                    else if (random.NextDouble() < Game1.player.ForagingLevel / 30.0)
                    {
                        obj.Quality = 2;
                    }
                    else if (random.NextDouble() < Game1.player.ForagingLevel / 15.0)
                    {
                        obj.Quality = 1;
                    }

                    if (Game1.player.professions.Contains(13))
                    {
                        while (random.NextDouble() < 0.2)
                        {
                            obj.Stack += 1;
                        }
                    }

                    (grabber.heldObject.Value as Chest).addItem(obj);
                    var name = location.Objects[tile].Name;
                    if (!itemsAdded.ContainsKey(name))
                    {
                        itemsAdded.Add(name, 1);
                    }
                    else
                    {
                        itemsAdded[name] += 1;
                    }

                    location.Objects.Remove(tile);
                    if (_config.DoGainExperience)
                    {
                        GainExperience(Foraging, 7);
                    }
                }

                // check farm cave for mushrooms
                if (_config.DoHarvestFarmCave)
                {
                    if (location is FarmCave)
                    {
                        foreach (var obj in location.Objects.Values)
                        {
                            if ((grabber.heldObject.Value as Chest).items.Count >= 36)
                            {
                                Monitor.Log("Global grabber full", LogLevel.Info);
                                return;
                            }

                            if (obj.bigCraftable.Value && obj.ParentSheetIndex == 128)
                            {
                                if (obj.heldObject.Value != null)
                                {
                                    (grabber.heldObject.Value as Chest).addItem(obj.heldObject.Value);
                                    var name = grabber.heldObject.Value.Name;
                                    if (!itemsAdded.ContainsKey(name))
                                    {
                                        itemsAdded.Add(name, 1);
                                    }
                                    else
                                    {
                                        itemsAdded[name] += 1;
                                    }

                                    obj.heldObject.Value = null;
                                }
                            }
                        }
                    }
                }

                foreach (var pair in itemsAdded)
                {
                    var plural = "";
                    if (pair.Value != 1) plural = "s";
                    Monitor.Log($"  {location} - found {pair.Value} {pair.Key}{plural}");
                }

                if ((grabber.heldObject.Value as Chest).items.Count > 0)
                {
                    grabber.showNextIndex.Value = true;
                }
            }
        }

        private static bool IsGrabbableCoop(Object obj)
        {
            if (obj.bigCraftable.Value)
            {
                return obj.Name.Contains("Slime Ball");
            }

            return obj.Name.Contains("Egg") || obj.Name.Contains("Wool") || obj.Name.Contains("Foot") ||
                   obj.Name.Contains("Feather");
        }

        private bool IsGrabbableWorld(Object obj)
        {
            if (obj.bigCraftable.Value)
            {
                return false;
            }

            switch (obj.ParentSheetIndex)
            {
                case 16: // Wild Horseradish
                case 18: // Daffodil
                case 20: // Leek
                case 22: // Dandelion
                case 430: // Truffle
                case 399: // Spring Onion
                case 257: // Morel
                case 404: // Common Mushroom
                case 296: // Salmonberry
                case 396: // Spice Berry
                case 398: // Grape
                case 402: // Sweet Pea
                case 420: // Red Mushroom
                case 259: // Fiddlehead Fern
                case 406: // Wild Plum
                case 408: // Hazelnut
                case 410: // Blackberry
                case 281: // Chanterelle
                case 412: // Winter Root
                case 414: // Crystal Fruit
                case 416: // Snow Yam
                case 418: // Crocus
                case 283: // Holly
                case 392: // Nautilus Shell
                case 393: // Coral
                case 397: // Sea Urchin
                case 394: // Rainbow Shell
                case 372: // Clam
                case 718: // Cockle
                case 719: // Mussel
                case 723: // Oyster
                case 78: // Cave Carrot
                case 90: // Cactus Fruit
                case 88: // Coconut
                    return true;
            }

            return false;
        }

        private static void GainExperience(int skill, int xp)
        {
            Game1.player.gainExperience(skill, xp);
        }
    }
}