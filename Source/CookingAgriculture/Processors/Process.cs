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
	public class ProcessBill : IExposable {
		public ProcessDef def;
		public Building_Processor parent;

		public bool suspended = false;
		public bool paused = false;

		public ThingFilter ingredientFilter = new ThingFilter();
		public const int MaxIngredientSearchRadius = 999;
		public int ingredientSearchRadius = MaxIngredientSearchRadius;
		public bool includeEquipped = false;
		public bool includeTainted = false;
		public Zone_Stockpile includeFromZone;
		public FloatRange hpRange = FloatRange.ZeroToOne;
		public QualityRange qualityRange = QualityRange.All;
		public bool limitToAllowedStuff = false;

		public BillRepeatModeDef repeatMode = BillRepeatModeDefOf.RepeatCount;
		public int repeatCount = 1;
		public int targetCount = 10;
		public bool pauseWhenSatisfied = false;
		public int unpauseWhenYouHave = 5;

		public BillStoreModeDef storeMode = BillStoreModeDefOf.BestStockpile;
		public Zone_Stockpile storeZone;

		protected virtual Color BaseColor => suspended ? new Color(1f, 0.7f, 0.7f, 0.7f) : Color.white;

		public ProcessBill(ProcessDef def, Building_Processor parent) {
			this.def = def;
			this.parent = parent;
			this.ingredientFilter = new ThingFilter();
			ingredientFilter.SetAllowAll(def.defaultIngredientFilter);
		}

		public bool ShouldStart(Map map) {
			if (!CanCountProducts()) {
				return true;
			}
			if (suspended || paused) {
				return false;
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
		public bool CanStart(Map map) {
			return true;
		}

        // * Ingredient Counting *
        // Based on WorkGiver_DoBill TryFindBestIngredientsHelper
        public Thing FindIngredient(Pawn pawn) {
            var radiusSq = ingredientSearchRadius * ingredientSearchRadius;
			Predicate<Thing> validator = t => t.Spawned && ingredientFilter.Allows(t) && (t.Position - parent.Position).LengthHorizontalSquared < radiusSq && !t.IsForbidden(pawn) && pawn.CanReserve(t, 1, Mathf.Min(parent.SpaceLeft, t.stackCount, pawn.carryTracker.AvailableStackSpace(t.def)));
			return GenClosest.ClosestThingReachable(parent.InteractionCell, parent.Map, ThingRequest.ForGroup(ThingRequestGroup.HaulableEver), PathEndMode.ClosestTouch, TraverseParms.For(pawn), 9999f, validator);
            /*chosen.Clear();

            Region rootReg = parent.InteractionCell.GetRegion(pawn.Map);
			TraverseParms traverseParams = TraverseParms.For(pawn);
			RegionEntryPredicate entryCondition = (_, r) => {
				if (!r.Allows(traverseParams, false)) {
					return false;
				}
				if (ingredientSearchRadius == MaxIngredientSearchRadius) {
					return true;
				}
				CellRect extentsClose = r.extentsClose;
				int distX = Math.Abs(parent.Position.x - Math.Max(extentsClose.minX, Math.Min(parent.Position.x, extentsClose.maxX)));
				if (distX > ingredientSearchRadius) {
					return false;
				}
				int distZ = Math.Abs(parent.Position.z - Math.Max(extentsClose.minZ, Math.Min(parent.Position.z, extentsClose.maxZ)));
				return distZ <= ingredientSearchRadius && (distX * distX + distZ * distZ) <= radiusSq;
			};

			List<Thing> foundThings = new List<Thing>();
			int adjacentRegionsAvailable = rootReg.Neighbors.Count((region => entryCondition(rootReg, region)));
            int regionsProcessed = 0;
            bool foundAll = false;
            RegionProcessor regionProcessor = (r => {
                List<Thing> ingredients = r.ListerThings.ThingsMatching(ThingRequest.ForGroup(ThingRequestGroup.HaulableEver));
                for (int i = 0; i < ingredients.Count; ++i) {
                    Thing thing = ingredients[i];
                    if (!foundThings.Contains(thing) && ingredientFilter.Allows(thing) && ReachabilityWithinRegion.ThingFromRegionListerReachable(thing, r, PathEndMode.ClosestTouch, pawn) && baseValidator(thing)) {
                        foundThings.Add(thing);
                    }
                }
                ++regionsProcessed;
                if (regionsProcessed > adjacentRegionsAvailable) {
					// TODO: Common Sense method
                    Comparison<Thing> comparison = (t1, t2) => ((float)(t1.PositionHeld - parent.InteractionCell).LengthHorizontalSquared).CompareTo((t2.PositionHeld - parent.InteractionCell).LengthHorizontalSquared);
                    foundThings.Sort(comparison);
					for (int i = 0;	i < foundThings.Count; i++) {
                        

                    }
                    if (foundAllIngredientsAndChoose(foundThings)) {
                        foundAll = true;
                        return true;
                    }
                }
                return false;
            });
            RegionTraverser.BreadthFirstTraverse(rootReg, entryCondition, regionProcessor, 99999);
            return foundAll;*/
        }

        // * Product Counting *
        public bool CanCountProducts() => def.outputs != null && def.outputs.Count == 1;
		public bool CanPossiblyStoreInStockpile(Zone_Stockpile stockpile) => !CanCountProducts() || stockpile.GetStoreSettings().AllowedToAccept(def.outputs[0].thingDef);
		public bool CanUnpause(Map map) => paused && CountProducts(map) < targetCount;

		public int CountProducts(Map map) {
			ThingDefCountClass product = def.outputs[0];
			ThingDef thingDef = product.thingDef;

			// Simple counter
			if (product.thingDef.CountAsResource && !includeEquipped &&
				(includeTainted || !product.thingDef.IsApparel || !product.thingDef.apparel.careIfWornByCorpse) &&
				includeFromZone == null && hpRange.min == 0.0 && hpRange.max == 1.0 && qualityRange.min == QualityCategory.Awful && qualityRange.max == QualityCategory.Legendary && !limitToAllowedStuff) {
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

			if (includeEquipped) {
				foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned) {
					List<ThingWithComps> equipmentListForReading = pawn.equipment.AllEquipmentListForReading;
					for (int index = 0; index < equipmentListForReading.Count; ++index) {
						if (IsValidThing(equipmentListForReading[index], thingDef)) {
							count += equipmentListForReading[index].stackCount;
						}
					}
					List<Apparel> wornApparel = pawn.apparel.WornApparel;
					for (int index = 0; index < wornApparel.Count; ++index) {
						if (IsValidThing(wornApparel[index], thingDef)) {
							count += wornApparel[index].stackCount;
						}
					}
					ThingOwner directlyHeldThings = pawn.inventory.GetDirectlyHeldThings();
					for (int index = 0; index < directlyHeldThings.Count; ++index) {
						if (IsValidThing(directlyHeldThings[index], thingDef)) {
							count += directlyHeldThings[index].stackCount;
						}
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
			ThingDef def1 = thing.def;
			if (def1 != def || !includeTainted && def1.IsApparel && ((Apparel)thing).WornByCorpse || thing.def.useHitPoints && !hpRange.IncludesEpsilon(thing.HitPoints / thing.MaxHitPoints)) {
				return false;
			}
			CompQuality comp = thing.TryGetComp<CompQuality>();
			return (comp == null || qualityRange.Includes(comp.Quality)) && (!limitToAllowedStuff || ingredientFilter.Allows(thing.Stuff));
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
			Scribe_Values.Look(ref def, "def");

			Scribe_Values.Look(ref suspended, "suspended");
			Scribe_Values.Look(ref paused, "paused");

			Scribe_Values.Look(ref ingredientFilter, "ingredientFilter");
			Scribe_Values.Look(ref ingredientSearchRadius, "ingredientSearchRadius");
			Scribe_Values.Look(ref includeEquipped, "includeEquipped");
			Scribe_Values.Look(ref includeTainted, "includeTainted");
			Scribe_Values.Look(ref includeFromZone, "includeFromZone");
			Scribe_Values.Look(ref hpRange, "hpRange", FloatRange.ZeroToOne);
			Scribe_Values.Look(ref qualityRange, "qualityRange", QualityRange.All);
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
		public List<ThingFilter> ingredients = new List<ThingFilter>();
		public ThingFilter defaultIngredientFilter;
		public List<ThingDefCountClass> outputs = new List<ThingDefCountClass>();

		public float days;

		public ProcessCancelAction cancelAction = ProcessCancelAction.Drop;

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
		Nothing,
	}
}
