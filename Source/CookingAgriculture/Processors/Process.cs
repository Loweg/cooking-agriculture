using RimWorld;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;

using UnityEngine;
using Verse;
using Verse.AI;

namespace CookingAgriculture.Processors {
    // From Bill, mostly
    public class ProcessSettings : IExposable {
        public ProcessDef def;
        public Building parent;

        public ThingFilter ingredientFilter = new ThingFilter();
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
            ingredientFilter = def.defaultIngredientFilter ?? new ThingFilter();
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
            return !limitToAllowedStuff || ingredientFilter.Allows(thing.Stuff);
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

        // From TryFindBestIngredientsHelper
        public bool FindIngredients(Pawn pawn, List<ThingCount> chosen, List<IngredientCount> missingIngredients) {
            if (def.ingredients.Count == 0) return true;

            // First, make a list of everything around that might be useful
            Predicate<Thing> validator = t => t.Spawned && ingredientFilter.Allows(t) && !t.IsForbidden(pawn) && pawn.CanReserve(t, 1);
            Region rootReg = parent.InteractionCell.GetRegion(pawn.Map);
            if (rootReg == null) return false;

            List<Thing> foundThings = new List<Thing>();
            RegionProcessor regionProcessor = r => {
                List<Thing> ingredients = r.ListerThings.ThingsMatching(ThingRequest.ForGroup(ThingRequestGroup.HaulableEver));
                for (int i = 0; i < ingredients.Count; ++i) {
                    Thing thing = ingredients[i];
                    if (!foundThings.Contains(thing) && ReachabilityWithinRegion.ThingFromRegionListerReachable(thing, r, PathEndMode.ClosestTouch, pawn) && validator(thing)) {
                        foundThings.Add(thing);
                    }
                }
                return false;
            };

            TraverseParms traverseParams = TraverseParms.For(pawn);
            RegionEntryPredicate entryCondition = (_, r) => r.Allows(traverseParams, false);
            RegionTraverser.BreadthFirstTraverse(rootReg, entryCondition, regionProcessor);

            // Chose which ingredients to use
            Comparison<Thing> comparison = (t1, t2) => ((float)(t1.PositionHeld - parent.InteractionCell).LengthHorizontalSquared).CompareTo((t2.PositionHeld - parent.InteractionCell).LengthHorizontalSquared);
            foundThings.Sort(comparison);
            for (int i = 0; i < def.ingredients.Count; i++) {
                IngredientCount ingredient = def.ingredients[i];
                float baseCount = ingredient.GetBaseCount();
                for (int j = 0; j < foundThings.Count; ++j) {
                    Thing thing = foundThings[j];
                    if (ingredient.filter.Allows(thing) && (ingredient.IsFixedIngredient || ingredientFilter.Allows(thing))) {
                        float value = 1f;
                        if (def.valueType == ValueType.Nutrition) {
                            if (!thing.def.IsNutritionGivingIngestible) continue;
                            value = thing.def.GetStatValueAbstract(StatDefOf.Nutrition);
                        }
                        int countToAdd = Mathf.Min(Mathf.CeilToInt(baseCount / value), thing.stackCount);
                        ThingCountUtility.AddToList(chosen, thing, countToAdd);
                        baseCount -= countToAdd * value;
                        if ((double)baseCount <= 0d) break;
                    }
                }
                if ((double)baseCount > 0d) {
                    missingIngredients.Add(ingredient);
                }
            }
            return missingIngredients.Count == 0;
        }

        // * Interface *
        protected virtual void DoConfigInterface(Rect rect, Color baseColor) {
            rect.yMin += 29f;
            float y = rect.center.y;
            Widgets.InfoCardButton(rect.xMax - (rect.yMax - y) - 12f, y - 12f, def);
        }

        public virtual void ExposeData() {
            Scribe_Values.Look(ref def, "def");

            Scribe_Values.Look(ref suspended, "suspended");
            Scribe_Values.Look(ref paused, "paused");

            Scribe_Values.Look(ref ingredientFilter, "ingredientFilter");
            Scribe_Values.Look(ref includeFromZone, "includeFromZone");
            Scribe_Values.Look(ref limitToAllowedStuff, "limitToAllowedStuff");

            Scribe_Values.Look(ref repeatMode, "repeatMode");
            Scribe_Values.Look(ref repeatCount, "repeatCount");
            Scribe_Values.Look(ref targetCount, "targetCount");
            Scribe_Values.Look(ref pauseWhenSatisfied, "pauseWhenSatisfied");
            Scribe_Values.Look(ref unpauseWhenYouHave, "unpauseWhenYouHave");

            Scribe_Values.Look(ref storeMode, "storeMode");
            Scribe_Values.Look(ref storeZone, "storeZone");
        }
    }

    public class ProcessDef : Def {
        public List<IngredientCount> ingredients = new List<IngredientCount>();
        public ThingFilter defaultIngredientFilter = new ThingFilter();
        public List<ThingDefCountClass> outputs = new List<ThingDefCountClass>();

        public float days = 0.5f;

        public ProcessCancelAction cancelAction = ProcessCancelAction.Drop;
        public ValueType valueType = 0;

        public ProcessDef() {}
        public ProcessDef(List<IngredientCount> ingredients, float days, ValueType valueType) {
            this.ingredients = ingredients;
            this.days = days;
            this.valueType = valueType;
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
    public enum ValueType {
        Count,
        Nutrition,
    }
}
