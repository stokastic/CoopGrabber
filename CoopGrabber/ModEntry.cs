using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Objects;
using System.Collections.Generic;

namespace CoopGrabber
{
    public class ModEntry : Mod {
        public override void Entry(IModHelper helper) {
            TimeEvents.AfterDayStarted += TimeEvents_AfterDayStarted;
        }

        private void TimeEvents_AfterDayStarted(object sender, System.EventArgs e) {

            Object grabber = null; // stores a reference to the autograbber
            List<Vector2> grabbables = new List<Vector2>(); // stores a list of all items which can be grabbed

            foreach (Building building in Game1.getFarm().buildings) {
                if (building is Coop) {
                    Monitor.Log($"{building.nameOfIndoors}");
                    GameLocation location = (building as Coop).indoors.Value;
                    if (location != null) {

                        // populate list of objects which are grabbable, and find an autograbber
                        foreach (KeyValuePair<Vector2, Object> pair in location.Objects.Pairs) {
                            if (pair.Value.Name.Contains("Grabber")) {
                                Monitor.Log($"{pair.Key}: {pair.Value.Name}");
                                grabber = pair.Value;
                            }
                            if (IsGrabbable(pair.Value)) {
                                grabbables.Add(pair.Key);
                            }
                        }

                        if (grabber == null) {
                            return;
                        }

                        // add items to grabber and remove from floor
                        foreach (Vector2 tile in grabbables) {
                            Item item = (grabber.heldObject.Value as Chest).addItem(location.Objects[tile]);
                            if (item != null) {
                                Monitor.Log($"Added {item.Name}");
                            } else {
                                Monitor.Log($"Added null");
                            }
                            location.Objects.Remove(tile);
                        }
                    }
                }
            }
        }

        private bool IsGrabbable(Object obj) {
            if (obj.Name.Contains("Egg") || obj.Name.Contains("Wool") || obj.Name.Contains("Foot") || obj.Name.Contains("Feather")) {
                return true;
            }
            return false;
        }
    }
}
