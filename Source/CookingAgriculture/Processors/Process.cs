using RimWorld;
using System;
using System.Collections.Generic;

using UnityEngine;
using Verse;
using Verse.AI;

namespace CookingAgriculture.Processors {
    // From Bill, mostly
    public class ProcessSettings : IExposable {
        public ProcessDef def;
        public Building parent;
        public StorageSettings storageSettings;
        public ThingFilter IngredientFilter => storageSettings.filter;
        public BillRepeatModeDef repeatMode = BillRepeatModeDefOf.RepeatCount;
        public int repeatCount = 1;
        public int targetCount = 10;
        public bool pauseWhenSatisfied = false;
        public bool suspended = false;
        public bool paused = false;
        public BillStoreModeDef storeMode = BillStoreModeDefOf.BestStockpile;
        public Zone_Stockpile storeZone;

        // Counting
        public bool limitToAllowedStuff = false;
        public int unpauseWhenYouHave = 5;
        public Zone_Stockpile includeFromZone;

        public ProcessSettings(ProcessDef def, Building parent) {
            this.def = def;
            this.parent = parent;
            storageSettings = new StorageSettings();
            if (def.defaultIngredientFilter != null) {
                storageSettings.filter = def.defaultIngredientFilter;
            } else {
                storageSettings.filter = def.GetFixedIngredientFilter();
            }
        }

        public bool ShouldStart(Map map) {
            if (suspended || paused) {
                return false;
            }
            if (!CanCountProducts()) {
                return true;
            }

            if (repeatMode == BillRepeatModeDefOf.Forever) {
                return true;
            } else if (repeatMode == BillRepeatModeDefOf.RepeatCount) {
                if (repeatCount <= 0) {
                    return false;
                } else {
                    return true;
                }
            } else if (repeatMode == BillRepeatModeDefOf.TargetCount) {
                if (CountProducts(map) < targetCount) {
                    return true;
                }
            }
            return false;
        }

        // * Product Counting *
        public bool CanCountProducts() => def.outputs != null && def.outputs.Count == 1;
        public bool CanPossiblyStoreInStockpile(Zone_Stockpile stockpile) => !CanCountProducts() || stockpile.GetStoreSettings().AllowedToAccept(def.outputs[0].thingDef);
        public bool CanUnpause(Map map) => paused && CountProducts(map) < targetCount;

        public int CountProducts(Map map) {
            ThingDefCountClass product = def.outputs[0];
            ThingDef thingDef = product.thingDef;

            // Simple counter
            if (product.thingDef.CountAsResource && includeFromZone == null && !limitToAllowedStuff) {
                return map.resourceCounter.GetCount(product.thingDef) + GetCarriedCount(map, thingDef);
            }

            // Complex counter
            int count = 0;

            if (includeFromZone == null) {
                int num2 = CountValidThings(map.listerThings.ThingsOfDef(product.thingDef), thingDef);
                if (product.thingDef.Minifiable) {
                    List<Thing> thingList = map.listerThings.ThingsInGroup(ThingRequestGroup.MinifiedThing);
                    for (int index = 0; index < thingList.Count; ++index) {
                        MinifiedThing minifiedThing = (MinifiedThing)thingList[index];
                        if (IsValidThing(minifiedThing.InnerThing, thingDef)) {
                            num2 += minifiedThing.stackCount * minifiedThing.InnerThing.stackCount;
                        }
                    }
                }
                count = num2 + GetCarriedCount(map, thingDef);
            } else {
                foreach (Thing allContainedThing in includeFromZone.AllContainedThings) {
                    Thing innerIfMinified = allContainedThing.GetInnerIfMinified();
                    if (IsValidThing(innerIfMinified, thingDef)) {
                        count += innerIfMinified.stackCount;
                    }
                }
            }

            return count;
        }

        public int CountValidThings(List<Thing> things, ThingDef def) {
            int num = 0;
            for (int index = 0; index < things.Count; ++index) {
                if (IsValidThing(things[index], def)) {
                    ++num;
                }
            }
            return num;
        }

        public bool IsValidThing(Thing thing, ThingDef def) {
            return !limitToAllowedStuff || IngredientFilter.Allows(thing.Stuff);
        }

        public int GetCarriedCount(Map map, ThingDef prodDef) {
            int carriedCount = 0;
            foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned) {
                Thing carriedThing = pawn.carryTracker.CarriedThing;
                if (carriedThing != null) {
                    int stackCount = carriedThing.stackCount;
                    if (IsValidThing(carriedThing.GetInnerIfMinified(), prodDef)) {
                        carriedCount += stackCount;
                    }
                }
            }
            return carriedCount;
        }

        // * Interface *
        protected virtual void DoConfigInterface(Rect rect, Color baseColor) {
            rect.yMin += 29f;
            float y = rect.center.y;
            Widgets.InfoCardButton(rect.xMax - (rect.yMax - y) - 12f, y - 12f, def);
        }

        public virtual void ExposeData() {
            storageSettings.ExposeData();

            Scribe_References.Look(ref storeZone, "storeZone");
            Scribe_References.Look(ref includeFromZone, "includeFromZone");

            Scribe_Defs.Look(ref def, "def");

            Scribe_Values.Look(ref suspended, "suspended");
            Scribe_Values.Look(ref paused, "paused");
            Scribe_Values.Look(ref limitToAllowedStuff, "limitToAllowedStuff");

            Scribe_Defs.Look(ref repeatMode, "repeatMode");
            Scribe_Values.Look(ref repeatCount, "repeatCount");
            Scribe_Values.Look(ref targetCount, "targetCount");
            Scribe_Values.Look(ref pauseWhenSatisfied, "pauseWhenSatisfied");
            Scribe_Values.Look(ref unpauseWhenYouHave, "unpauseWhenYouHave");

            Scribe_Defs.Look(ref storeMode, "storeMode");
        }
    }

    public class ProcessDef : Def {
        public List<IngredientCount> ingredients = new List<IngredientCount>();
        public ThingFilter defaultIngredientFilter;
        public List<ThingDefCountClass> outputs = new List<ThingDefCountClass>();

        public float days = 0.5f;

        public ProcessCancelAction cancelAction = ProcessCancelAction.Drop;
        public RecipeValueType valueType = 0;

        public ThingFilter GetFixedIngredientFilter() {
            var f = new ThingFilter();
            foreach (var i in ingredients) f.SetAllowAll(i.filter);
            return f;
        }

        public override void ResolveReferences() {
            defaultIngredientFilter.ResolveReferences();
            foreach (var ingredient in ingredients) {
                ingredient.ResolveReferences();
            }
            foreach (var output in outputs) {
                output.thingDef.ResolveReferences();
            }
        }
    }

    public enum ProcessCancelAction {
        Delete,
        Drop,
    }
    public enum RecipeValueType {
        Count,
        Nutrition,
    }
}
