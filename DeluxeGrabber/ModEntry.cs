using Microsoft.Xna.Framework;
using Netcode;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using System;
using System.Collections.Generic;
using Object = StardewValley.Object;

namespace DeluxeGrabber
{
    public class ModEntry : Mod {

        private ModConfig Config;
        private List<string> quality = new List<string>() { "basic", "silver", "gold", "3", "iridium" };
        private readonly int FARMING = 0;
        private readonly int FORAGING = 2;

        public override void Entry(IModHelper helper) {

            Config = Helper.ReadConfig<ModConfig>();

            Helper.ConsoleCommands.Add("printLocation", "Print current map and tile location", PrintLocation);
            Helper.ConsoleCommands.Add("setForagerLocation", "Set current location as global grabber location", SetForagerLocation);

            TimeEvents.AfterDayStarted += TimeEvents_AfterDayStarted;
            LocationEvents.ObjectsChanged += LocationEvents_ObjectsChanged;
        }

        private void SetForagerLocation(string arg1, string[] arg2) {
            if (!Context.IsWorldReady) {
                return;
            }
            Config.GlobalForageMap = Game1.player.currentLocation.Name;
            Config.GlobalForageTileX = Game1.player.getTileX();
            Config.GlobalForageTileY = Game1.player.getTileY();
            Helper.WriteConfig(Config);
        }

        private void PrintLocation(string arg1, string[] arg2) {
            if (!Context.IsWorldReady) {
                return;
            }
            Monitor.Log($"Map: {Game1.player.currentLocation.Name}");
            Monitor.Log($"Tile: {Game1.player.getTileLocation()}");
        }

        private void LocationEvents_ObjectsChanged(object sender, EventArgsLocationObjectsChanged e) {
            if (!Config.DoGlobalForage) {
                return;
            }

            GameLocation foragerMap;
            foragerMap = Game1.getLocationFromName(Config.GlobalForageMap);
            if (foragerMap == null) {
                return;
            }

            foragerMap.Objects.TryGetValue(new Vector2(Config.GlobalForageTileX, Config.GlobalForageTileY), out Object grabber);

            if (grabber == null || !grabber.Name.Contains("Grabber")) {
                return;
            }

            
            System.Random random = new System.Random();
            foreach (KeyValuePair<Vector2, Object> pair in e.Added) {

                if (pair.Value.ParentSheetIndex != 430 || pair.Value.bigCraftable.Value) {
                    continue;
                }

                if ((grabber.heldObject.Value as Chest).items.Count >= 36) {
                    return;
                }

                Object obj = pair.Value;
                if (obj.Stack == 0) {
                    obj.Stack = 1;
                }

                if (!obj.isForage(null) && !IsGrabbableWorld(obj)) {
                    continue;
                }

                if (Game1.player.professions.Contains(16)) {
                    obj.Quality = 4;
                } else if (random.NextDouble() < Game1.player.ForagingLevel / 30.0) {
                    obj.Quality = 2;
                } else if (random.NextDouble() < Game1.player.ForagingLevel / 15.0) {
                    obj.Quality = 1;
                }

                if (Game1.player.professions.Contains(13)) {
                    while (random.NextDouble() < 0.2) {
                        obj.Stack += 1;
                    }
                }

                Monitor.Log($"Grabbing truffle: {obj.Stack}x{quality[obj.Quality]}", LogLevel.Trace);
                (grabber.heldObject.Value as Chest).addItem(obj);
                e.Location.Objects.Remove(pair.Key);

                if (Config.DoGainExperience) {
                    gainExperience(FORAGING, 7);
                }
            }

            if ((grabber.heldObject.Value as Chest).items.Count > 0) {
                grabber.showNextIndex.Value = true;
            }
        }

        private void TimeEvents_AfterDayStarted(object sender, System.EventArgs e) {

            AutograbBuildings();
            AutograbCrops();
            AutograbWorld();
        }

        private void AutograbBuildings() {
            Object grabber = null; // stores a reference to the autograbber
            List<Vector2> grabbables = new List<Vector2>(); // stores a list of all items which can be grabbed
            Dictionary<string, int> itemsAdded = new Dictionary<string, int>();
            GameLocation location;

            foreach (Building building in Game1.getFarm().buildings) {

                grabber = null;
                grabbables.Clear();
                itemsAdded.Clear();

                if (building.buildingType.Contains("Coop") || building.buildingType.Contains("Slime")) {

                    Monitor.Log($"Searching {building.buildingType} at <{building.tileX},{building.tileY}> for auto-grabber", LogLevel.Trace);

                    location = building.indoors.Value;
                    if (location != null) {

                        // populate list of objects which are grabbable, and find an autograbber
                        foreach (KeyValuePair<Vector2, Object> pair in location.Objects.Pairs) {
                            if (pair.Value.Name.Contains("Grabber")) {
                                Monitor.Log($"  Grabber found  at {pair.Key}", LogLevel.Trace);
                                grabber = pair.Value;
                            }
                            if (IsGrabbableCoop(pair.Value)) {
                                Monitor.Log($"    Found grabbable item at {pair.Key}: {pair.Value.Name}", LogLevel.Trace);
                                grabbables.Add(pair.Key);
                            }
                        }

                        if (grabber == null) {
                            Monitor.Log("  No grabber found", LogLevel.Trace);
                            continue;
                        }

                        // add items to grabber and remove from floor
                        bool full = false;
                        foreach (Vector2 tile in grabbables) {

                            if ((grabber.heldObject.Value as Chest).items.Count >= 36) {
                                Monitor.Log($"  Grabber is full", LogLevel.Trace);
                                full = true;
                                break;
                            }

                            if (location.objects[tile].Name.Contains("Slime Ball")) {
                                System.Random random = new System.Random((int)Game1.stats.daysPlayed + (int)Game1.uniqueIDForThisGame + (int)tile.X * 77 + (int)tile.Y * 777 + 2);
                                (grabber.heldObject.Value as Chest).addItem(new Object(766, random.Next(10, 21), false, -1, 0));
                                int i = 0;
                                while (random.NextDouble() < 0.33) {
                                    i++;
                                }
                                if (i > 0) {
                                    (grabber.heldObject.Value as Chest).addItem(new Object(557, i, false, -1, 0));
                                }
                            } else if ((grabber.heldObject.Value as Chest).addItem(location.Objects[tile]) != null) {
                                continue;
                            }
                            string name = location.Objects[tile].Name;
                            if (!itemsAdded.ContainsKey(name)) {
                                itemsAdded.Add(name, 1);
                            } else {
                                itemsAdded[name] += 1;
                            }
                            location.Objects.Remove(tile);
                            if (Config.DoGainExperience) {
                                gainExperience(FARMING, 5);
                            }
                        }

                        if (full) {
                            continue;
                        }

                        foreach (KeyValuePair<string, int> pair in itemsAdded) {
                            string plural = "";
                            if (pair.Value != 1) plural = "s";
                            Monitor.Log($"  Added {pair.Value} {pair.Key}{plural}", LogLevel.Trace);
                        }
                    }
                }

                // update sprite if grabber has items in it
                if (grabber != null && (grabber.heldObject.Value as Chest).items.Count > 0) {
                    grabber.showNextIndex.Value = true;
                }
            }
        }

        private void AutograbCrops() {

            if (!Config.DoHarvestCrops) {
                return;
            }

            int range = Config.GrabberRange;
            foreach (GameLocation location in Game1.locations) {
                foreach (KeyValuePair<Vector2, Object> pair in location.Objects.Pairs) {
                    if (pair.Value.Name.Contains("Grabber")) {
                        Object grabber = pair.Value;
                        if ((grabber.heldObject.Value as Chest).items.Count >= 36) {
                            continue;
                        }
                        bool full = (grabber.heldObject.Value as Chest).items.Count >= 36;
                        for (int x = (int)pair.Key.X - range; x < pair.Key.X + range + 1 && !full; x ++) {
                            for (int y = (int)pair.Key.Y - range; y < pair.Key.Y + range + 1 && !full; y++) {
                                Vector2 tile = new Vector2(x, y);
                                if (location.terrainFeatures.ContainsKey(tile) && location.terrainFeatures[tile] is HoeDirt dirt)
                                {
                                    HarvestIntoGrabber(dirt, tile, location, grabber.heldObject.Value as Chest);
                                }
                                else if (location.Objects.ContainsKey(tile) && location.Objects[tile] is IndoorPot pot)
                                {
                                    HarvestIntoGrabber(pot.hoeDirt.Value, tile, location, grabber.heldObject.Value as Chest);
                                }
                                full = (grabber.heldObject.Value as Chest).items.Count >= 36;
                            }
                        }
                        if (grabber != null && (grabber.heldObject.Value as Chest).items.Count > 0) {
                            grabber.showNextIndex.Value = true;
                        }
                    }
                }
            }
        }

        private void HarvestIntoGrabber(HoeDirt dirt, Vector2 tile, GameLocation location, Chest grabberContents)
        {
            Crop crop = dirt.crop;
            Object harvest;

            if (crop is null || crop.dead || crop.forageCrop) {
                return;
            }

            if (!Config.DoHarvestFlowers) {
                switch(crop.indexOfHarvest.Value) {
                    case 421: return; // sunflower
                    case 593: return; // summer spangle
                    case 595: return; // fairy rose
                    case 591: return; // tulip
                    case 597: return; // blue jazz
                    case 376: return; // poppy
                }
            }

            if (crop.currentPhase.Value >= crop.phaseDays.Count - 1 && (!crop.fullyGrown.Value || crop.dayOfCurrentPhase.Value <= 0)) {
                int harvest_amount = 1; // num1
                int quality = 0; // num2
                int fertilizer_quality_boost = 0; // num3
                if (crop.indexOfHarvest.Value == 0)
                {
                    return;
                }
                System.Random random = new System.Random((int)tile.X * 7 + (int)tile.Y * 11 + (int)Game1.stats.DaysPlayed + (int)Game1.uniqueIDForThisGame);
                switch ((dirt.fertilizer.Value)) {
                    case 368:
                        fertilizer_quality_boost = 1;
                        break;
                    case 369:
                        fertilizer_quality_boost = 2;
                        break;
                }
                // num4
                double gold_chance = 0.2 * (Game1.player.FarmingLevel / 10.0) + 0.2 * fertilizer_quality_boost * ((Game1.player.FarmingLevel + 2.0) / 12.0) + 0.01;
                // num5
                double silver_chance = System.Math.Min(0.75, gold_chance * 2.0);
                if (random.NextDouble() < gold_chance)
                    quality = 2;
                else if (random.NextDouble() < silver_chance)
                    quality = 1;
                if ((crop.minHarvest.Value) > 1 || (crop.maxHarvest.Value) > 1)
                {
                    harvest_amount = random.Next(
                        crop.minHarvest.Value,
                        System.Math.Min(
                            crop.minHarvest.Value + 1,
                            crop.maxHarvest.Value + 1 + Game1.player.FarmingLevel / crop.maxHarvestIncreasePerFarmingLevel.Value));
                }
                if (crop.chanceForExtraCrops.Value > 0.0) {
                    while (random.NextDouble() < System.Math.Min(0.9, crop.chanceForExtraCrops.Value))
                        ++harvest_amount;
                }
                if (crop.harvestMethod.Value == 1) {
                    // harvest with scythe
                    for (int i = 0; i < harvest_amount; i++) {
                        harvest = new Object(parentSheetIndex: crop.indexOfHarvest, initialStack: 1, quality: quality);
                        grabberContents.addItem(harvest);
                    }
                    if (Config.DoGainExperience)
                    {
                        float num6 = (float)(16.0 * Math.Log(0.018 * (double)Convert.ToInt32(Game1.objectInformation[(int)((NetFieldBase<int, NetInt>)crop.indexOfHarvest)].Split('/')[1]) + 1.0, Math.E));
                        Game1.player.gainExperience(0, (int)Math.Round((double)num6));
                    }
                    if (crop.regrowAfterHarvest.Value != -1) {
                        crop.dayOfCurrentPhase.Value = crop.regrowAfterHarvest.Value;
                        crop.fullyGrown.Value = true;
                    } else {
                        dirt.crop = null;
                    }
                } else {
                    if (!crop.programColored.Value)
                    {
                        // not a flower
                        harvest = new Object(crop.indexOfHarvest.Value, 1, false, -1, quality);
                        grabberContents.addItem(harvest);
                    }
                    else
                    {
                        // is a flower
                        harvest = new ColoredObject(crop.indexOfHarvest.Value, 1, crop.tintColor.Value);
                        harvest.Quality = quality;
                        grabberContents.addItem(harvest);
                    }
                    if (random.NextDouble() < (double)Game1.player.LuckLevel / 1500.0 + Game1.dailyLuck / 1200.0 + 9.99999974737875E-05)
                    {
                        harvest_amount *= 2;
                    }
                    if ((int)((NetFieldBase<int, NetInt>)crop.indexOfHarvest) == 421) // sunflower
                    {
                        crop.indexOfHarvest.Value = 431; // sunflower seed
                        harvest_amount = random.Next(1, 4);
                    }
                    for (int index = 0; index < harvest_amount - 1; ++index)
                    {
                        harvest = new Object(parentSheetIndex: crop.indexOfHarvest, initialStack: 1, quality: 0);
                        grabberContents.addItem(harvest);
                        // this may fail, we already used one slot for normal/silver/gold quality crop and now
                        // we are adding normal. We still do need to clear the crop, and parent won't call us again
                    }
                    if (Config.DoGainExperience)
                    {
                        float num8 = (float)(16.0 * Math.Log(0.018 * (double)Convert.ToInt32(Game1.objectInformation[(int)((NetFieldBase<int, NetInt>)crop.indexOfHarvest)].Split('/')[1]) + 1.0, Math.E));
                        Game1.player.gainExperience(0, (int)Math.Round((double)num8));
                    }
                    if (crop.regrowAfterHarvest.Value != -1) {
                        crop.dayOfCurrentPhase.Value = crop.regrowAfterHarvest.Value;
                        crop.fullyGrown.Value = true;
                    } else {
                        dirt.crop = null;
                    }
                }
            }
        }

        private void AutograbWorld() {

            if (!Config.DoGlobalForage) {
                return;
            }

            List<Vector2> grabbables = new List<Vector2>(); // stores a list of all items which can be grabbed
            Dictionary<string, int> itemsAdded = new Dictionary<string, int>();
            System.Random random = new System.Random();
            GameLocation foragerMap;

            foragerMap = Game1.getLocationFromName(Config.GlobalForageMap);
            if (foragerMap == null) {
                Monitor.Log($"Invalid GlobalForageMap '{Config.GlobalForageMap}", LogLevel.Trace);
                return;
            }

            foragerMap.Objects.TryGetValue(new Vector2(Config.GlobalForageTileX, Config.GlobalForageTileY), out Object grabber);

            if (grabber == null || !grabber.Name.Contains("Grabber")) {
                Monitor.Log($"No auto-grabber at {Config.GlobalForageMap}: <{Config.GlobalForageTileX}, {Config.GlobalForageTileY}>", LogLevel.Trace);
                return;
            }

            foreach (GameLocation location in Game1.locations) {
                grabbables.Clear();
                itemsAdded.Clear();
                foreach (KeyValuePair<Vector2, Object> pair in location.Objects.Pairs) {
                    if (pair.Value.bigCraftable.Value) {
                        continue;
                    }
                    if (IsGrabbableWorld(pair.Value) || pair.Value.isForage(null)) {
                        grabbables.Add(pair.Key);
                    }
                }

                // Check for spring onions
                if (location.Name.Equals("Forest")) {
                    foreach (TerrainFeature feature in location.terrainFeatures.Values) {

                        if ((grabber.heldObject.Value as Chest).items.Count >= 36) {
                            Monitor.Log("Global grabber full", LogLevel.Info);
                            return;
                        }

                        if (feature is HoeDirt dirt) {
                            if (dirt.crop != null) {
                                if (dirt.crop.forageCrop.Value && dirt.crop.whichForageCrop.Value == 1) { // spring onion
                                    Object onion = new Object(399, 1, false, -1, 0);

                                    if (Game1.player.professions.Contains(16)) {
                                        onion.Quality = 4;
                                    } else if (random.NextDouble() < Game1.player.ForagingLevel / 30.0) {
                                        onion.Quality = 2;
                                    } else if (random.NextDouble() < Game1.player.ForagingLevel / 15.0) {
                                        onion.Quality = 1;
                                    }

                                    if (Game1.player.professions.Contains(13)) {
                                        while (random.NextDouble() < 0.2) {
                                            onion.Stack += 1;
                                        }
                                    }

                                    (grabber.heldObject.Value as Chest).addItem(onion);
                                    if (!itemsAdded.ContainsKey("Spring Onion")) {
                                        itemsAdded.Add("Spring Onion", 1);
                                    } else {
                                        itemsAdded["Spring Onion"] += 1;
                                    }

                                    dirt.crop = null;

                                    if (Config.DoGainExperience) {
                                        gainExperience(FORAGING, 3);
                                    }
                                }
                            }
                        }
                    }
                }

                // Check for berry bushes
                int berryIndex;
                string berryType;
                
                foreach (LargeTerrainFeature feature in location.largeTerrainFeatures) {

                    if (Game1.currentSeason == "spring") {
                        berryType = "Salmon Berry";
                        berryIndex = 296;
                    } else if (Game1.currentSeason == "fall") {
                        berryType = "Blackberry";
                        berryIndex = 410;
                    } else {
                        break;
                    }

                    if ((grabber.heldObject.Value as Chest).items.Count >= 36) {
                        Monitor.Log("Global grabber full", LogLevel.Info);
                        return;
                    }

                    if (feature is Bush bush) {
                        if (bush.inBloom(Game1.currentSeason, Game1.dayOfMonth) && bush.tileSheetOffset.Value == 1) {
                            
                            Object berry = new Object(berryIndex, 1 + Game1.player.FarmingLevel / 4, false, -1, 0);
                            
                            if (Game1.player.professions.Contains(16)) {
                                berry.Quality = 4;
                            }

                            bush.tileSheetOffset.Value = 0;
                            bush.setUpSourceRect();

                            Item item = (grabber.heldObject.Value as Chest).addItem(berry);
                            if (!itemsAdded.ContainsKey(berryType)) {
                                itemsAdded.Add(berryType, 1);
                            } else {
                                itemsAdded[berryType] += 1;
                            }
                        }
                    }
                }

                // add items to grabber and remove from floor
                foreach (Vector2 tile in grabbables) {

                    if ((grabber.heldObject.Value as Chest).items.Count >= 36) {
                        Monitor.Log("Global grabber full", LogLevel.Info);
                        return;
                    }

                    Object obj = location.Objects[tile];

                    if (Game1.player.professions.Contains(16)) {
                        obj.Quality = 4;
                    } else if (random.NextDouble() < Game1.player.ForagingLevel / 30.0) {
                        obj.Quality = 2;
                    } else if (random.NextDouble() < Game1.player.ForagingLevel / 15.0) {
                        obj.Quality = 1;
                    }

                    if (Game1.player.professions.Contains(13)) {
                        while (random.NextDouble() < 0.2) {
                            obj.Stack += 1;
                        }
                    }

                    Item item = (grabber.heldObject.Value as Chest).addItem(obj);
                    string name = location.Objects[tile].Name;
                    if (!itemsAdded.ContainsKey(name)) {
                        itemsAdded.Add(name, 1);
                    } else {
                        itemsAdded[name] += 1;
                    }
                    location.Objects.Remove(tile);

                    if (Config.DoGainExperience) {
                        gainExperience(FORAGING, 7);
                    }
                }

                foreach (KeyValuePair<string, int> pair in itemsAdded) {
                    string plural = "";
                    if (pair.Value != 1) plural = "s";
                    Monitor.Log($"  {location} - found {pair.Value} {pair.Key}{plural}", LogLevel.Trace);
                }

                if ((grabber.heldObject.Value as Chest).items.Count > 0) {
                    grabber.showNextIndex.Value = true;
                }
            }
        }

        private bool IsGrabbableCoop(Object obj) {
            if (obj.bigCraftable.Value) {
                return obj.Name.Contains("Slime Ball");
            }

            if (obj.Name.Contains("Egg") || obj.Name.Contains("Wool") || obj.Name.Contains("Foot") || obj.Name.Contains("Feather")) {
                return true;
            }
            return false;
        }

        private bool IsGrabbableWorld(Object obj) {
            if (obj.bigCraftable.Value) {
                return false;
            }
            switch (obj.ParentSheetIndex) {
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

        private void gainExperience(int skill, int xp) {
            Game1.player.gainExperience(skill, xp);
        }
    }
}
