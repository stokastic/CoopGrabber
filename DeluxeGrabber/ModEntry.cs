using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using System.Collections.Generic;

namespace DeluxeGrabber
{
    public class ModEntry : Mod {

        private ModConfig Config;

        public override void Entry(IModHelper helper) {

            Config = Helper.ReadConfig<ModConfig>();

            TimeEvents.AfterDayStarted += TimeEvents_AfterDayStarted;
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

            foreach (Building building in Game1.getFarm().buildings) {

                grabber = null;
                grabbables.Clear();
                itemsAdded.Clear();

                if (building is Coop) {
                    Monitor.Log($"{building.nameOfIndoors}", LogLevel.Trace);
                    GameLocation location = (building as Coop).indoors.Value;
                    if (location != null) {

                        // populate list of objects which are grabbable, and find an autograbber
                        foreach (KeyValuePair<Vector2, Object> pair in location.Objects.Pairs) {
                            if (pair.Value.Name.Contains("Grabber")) {
                                Monitor.Log($"  {pair.Key}: {pair.Value.Name}", LogLevel.Trace);
                                grabber = pair.Value;
                            }
                            if (IsGrabbableCoop(pair.Value)) {
                                grabbables.Add(pair.Key);
                            }
                        }

                        if (grabber == null) {
                            return;
                        }

                        // add items to grabber and remove from floor
                        foreach (Vector2 tile in grabbables) {
                            if ((grabber.heldObject.Value as Chest).addItem(location.Objects[tile]) != null) {
                                continue;
                            }
                            string name = location.Objects[tile].Name;
                            if (!itemsAdded.ContainsKey(name)) {
                                itemsAdded.Add(name, 1);
                            } else {
                                itemsAdded[name] += 1;
                            }
                            location.Objects.Remove(tile);
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
            int range = Config.GrabberRange;
            foreach (GameLocation location in Game1.locations) {
                foreach (KeyValuePair<Vector2, Object> pair in location.Objects.Pairs) {
                    if (pair.Value.Name.Contains("Grabber")) {
                        Object grabber = pair.Value;
                        if ((grabber.heldObject.Value as Chest).items.Count >= 36) {
                            continue;
                        }
                        for (int x = (int)pair.Key.X - range; x < pair.Key.X + range + 1; x ++) {
                            for (int y = (int)pair.Key.Y - range; y < pair.Key.Y + range + 1; y++) {
                                Vector2 tile = new Vector2(x, y);
                                if (location.terrainFeatures.ContainsKey(tile) && location.terrainFeatures[tile] is HoeDirt dirt) {
                                    Object harvest = GetHarvest(dirt, tile, location);
                                    if (harvest != null) {
                                        (grabber.heldObject.Value as Chest).addItem(harvest);
                                    }
                                } else if (location.Objects.ContainsKey(tile) && location.Objects[tile] is IndoorPot pot) {
                                    Object harvest = GetHarvest(pot.hoeDirt.Value, tile, location);
                                    if (harvest != null) {
                                        (grabber.heldObject.Value as Chest).addItem(harvest);
                                    }
                                }
                            }
                        }
                        if (grabber != null && (grabber.heldObject.Value as Chest).items.Count > 0) {
                            grabber.showNextIndex.Value = true;
                        }
                    }
                }
            }
        }

        private Object GetHarvest(HoeDirt dirt, Vector2 tile, GameLocation location) {

            Crop crop = dirt.crop;
            Object harvest;
            int stack = 0;

            if (crop != null && crop.currentPhase.Value >= crop.phaseDays.Count - 1 && (!crop.fullyGrown.Value || crop.dayOfCurrentPhase.Value <= 0)) {
                int num1 = 1;
                int num2 = 0;
                int num3 = 0;
                if (crop.indexOfHarvest.Value == 0)
                    return null;
                System.Random random = new System.Random((int)tile.X * 7 + (int)tile.Y * 11 + (int)Game1.stats.DaysPlayed + (int)Game1.uniqueIDForThisGame);
                switch ((dirt.fertilizer.Value)) {
                    case 368:
                        num3 = 1;
                        break;
                    case 369:
                        num3 = 2;
                        break;
                }
                double num4 = 0.2 * (Game1.player.FarmingLevel / 10.0) + 0.2 * num3 * ((Game1.player.FarmingLevel + 2.0) / 12.0) + 0.01;
                double num5 = System.Math.Min(0.75, num4 * 2.0);
                if (random.NextDouble() < num4)
                    num2 = 2;
                else if (random.NextDouble() < num5)
                    num2 = 1;
                if ((crop.minHarvest.Value) > 1 || (crop.maxHarvest.Value) > 1)
                    num1 = random.Next(crop.minHarvest.Value, System.Math.Min(crop.minHarvest.Value + 1, crop.maxHarvest.Value + 1 + Game1.player.FarmingLevel / crop.maxHarvestIncreasePerFarmingLevel.Value));
                if (crop.chanceForExtraCrops.Value > 0.0) {
                    while (random.NextDouble() < System.Math.Min(0.9, crop.chanceForExtraCrops.Value))
                        ++num1;
                }
                if (crop.harvestMethod.Value == 1) {
                    for (int i = 0; i < num1; i++) {
                        stack += 1;
                    }
                    if (crop.regrowAfterHarvest.Value != -1) {
                        crop.dayOfCurrentPhase.Value = crop.regrowAfterHarvest.Value;
                        crop.fullyGrown.Value = true;
                    } else {
                        dirt.crop = null;
                    }
                    return new Object(crop.indexOfHarvest.Value, stack, false, -1, num2);
                } else {
                    if (!crop.programColored.Value) {
                        harvest = new Object(crop.indexOfHarvest.Value, 1, false, -1, num2);
                    } else {
                        harvest = new ColoredObject(crop.indexOfHarvest.Value, 1, crop.tintColor.Value) { Quality = num2 };
                    }
                    if (crop.regrowAfterHarvest.Value != -1) {
                        crop.dayOfCurrentPhase.Value = crop.regrowAfterHarvest.Value;
                        crop.fullyGrown.Value = true;
                    } else {
                        dirt.crop = null;
                    }
                    return harvest;
                }
            }
            return null;
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
                    if (pair.Value.isForage(null)) {
                        grabbables.Add(pair.Key);
                    }
                }

                // add items to grabber and remove from floor
                foreach (Vector2 tile in grabbables) {
                    Object obj = location.Objects[tile];

                    if (Game1.player.professions.Contains(16)) {
                        obj.Quality = 4;
                    } else if (random.NextDouble() < Game1.player.ForagingLevel / 30.0) {
                        obj.Quality = 2;
                    } else if (random.NextDouble() < Game1.player.ForagingLevel / 15.0) {
                        obj.Quality = 1;
                    }

                    Item item = (grabber.heldObject.Value as Chest).addItem(obj);
                    string name = location.Objects[tile].Name;
                    if (!itemsAdded.ContainsKey(name)) {
                        itemsAdded.Add(name, 1);
                    } else {
                        itemsAdded[name] += 1;
                    }
                    location.Objects.Remove(tile);
                }

                foreach (KeyValuePair<string, int> pair in itemsAdded) {
                    string plural = "";
                    if (pair.Value != 1) plural = "s";
                    Monitor.Log($"  Added {pair.Value} {pair.Key}{plural}", LogLevel.Trace);
                }

                if ((grabber.heldObject.Value as Chest).items.Count > 0) {
                    grabber.showNextIndex.Value = true;
                }
            }
        }

        private bool IsGrabbableCoop(Object obj) {
            if (obj.Name.Contains("Egg") || obj.Name.Contains("Wool") || obj.Name.Contains("Foot") || obj.Name.Contains("Feather")) {
                return true;
            }
            return false;
        }

        private bool IsGrabbableWorld(Object obj) {
            switch (obj.ParentSheetIndex) {
                case 16: // Wild Horseradish
                case 18: // Daffodil
                case 20: // Leek
                case 22: // Dandelion
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
    }
}
