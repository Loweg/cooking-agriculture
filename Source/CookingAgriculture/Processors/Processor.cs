using RimWorld;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

using UnityEngine;
using Verse;

namespace CookingAgriculture.Processors {
	public abstract class Building_Processor : Building, IThingHolder {
		// * Config *
		// Speed
		public abstract float CurrentSpeedFactor();
		public virtual float DefaultSpeedFactor => 1f;

		// Production
		public abstract Thing Empty();
		public abstract void Fill(Thing ingredient);
		public virtual bool ShouldEmpty() {
			return progressBar.Progress >= 1f;
		}
		public virtual bool ShouldFill() {
			return SpaceLeft > 0;
		}
		public virtual int WantedOf(ThingDef ingredient) {
			return SpaceLeft;
		}
		public virtual void Reset() {
			progressBar.Progress = 0.0f;
		}

		// Processes
		public virtual List<ProcessDef> Processes => new List<ProcessDef>();
		public virtual List<ProcessBill> DefaultBills => new List<ProcessBill>();
		public virtual int Capacity => 75;

		// Jobs
		public virtual JobDef Job => CA_DefOf.CA_EmptyProcessor;
		public virtual int EmptyDuration => 200;
		public virtual int FillDuration => 200;

		// Drawing
		public virtual bool ShowProductIcon => true;
		public virtual Vector2 ProductIconSize => new Vector2(1f, 1f);

		public virtual Vector2 BarOffset => new Vector2(0f, 0.25f);
		public virtual Vector2 BarScale => new Vector2(1f, 1f);

		// * Provided *
		private ProgressBar progressBar;
		public float Progress {
			get => progressBar.Progress;
		}

		private int activeBill = -1;
		public List<ProcessBill> bills;

		// Production
		public bool Running() {
			return activeBill != -1;
		}
		private HashSet<ThingDef> cachedValidIngredients;
		private bool cacheDirty = true;
		public HashSet<ThingDef> ValidIngredients {
			get {
				if (cacheDirty) {
					cachedValidIngredients = CalculateValidIngredients();
					cacheDirty = false;
				}
				return cachedValidIngredients;
			}
		}
		private HashSet<ThingDef> CalculateValidIngredients() {
			HashSet<ThingDef> validIngredients = new HashSet<ThingDef>();
			if (activeBill != -1) {
				validIngredients.AddRange(bills[activeBill].ingredientFilter.AllowedThingDefs);
			} else {
				foreach (var bill in bills) {
					validIngredients.AddRange(bill.ingredientFilter.AllowedThingDefs);
				}
			}
			return validIngredients;
		}

		public int FirstStartableBill() {
			for (int i = 0; i < bills.Count; i++) {
				if (bills[i].CanStart(Map)) {
					return i;
				}
			}
			return -1;
		}
		public bool CanStartAnyBill() {
			return FirstStartableBill() != -1;
		}

		// Container
		public ThingOwner innerContainer = new ThingOwner<Thing>();
		public int TotalVolume => innerContainer.Count;
		public int SpaceLeft => Capacity - TotalVolume;


		public ThingOwner GetDirectlyHeldThings() {
			return innerContainer;
		}
		public void GetChildHolders(List<IThingHolder> outChildren) {
			ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
		}

		// Tick
		public float ProgressPerTick => Mathf.Max((1f / progressBar.ticksToComplete) * CurrentSpeedFactor(), 0f);
		public int EstimatedTicksLeft => Mathf.RoundToInt((1f - progressBar.Progress) / ProgressPerTick);

		public void DoTick() {
			base.Tick();
			progressBar.Progress = Mathf.Min(progressBar.Progress + ProgressPerTick, 1f);
		}
		public void DoTickRare() {
			base.TickRare();
			progressBar.Progress = Mathf.Min(progressBar.Progress + (ProgressPerTick * GenTicks.TickRareInterval), 1f);
		}
		public void DoTickLong() {
			base.TickLong();
			progressBar.Progress = Mathf.Min(progressBar.Progress + (ProgressPerTick * GenTicks.TickLongInterval), 1f);
		}

		// Other
		public override IEnumerable<Gizmo> GetGizmos() {
			foreach (Gizmo c in base.GetGizmos()) {
				yield return c;
			}
			if (Prefs.DevMode) {
				yield return progressBar.GetGizmo();
			}
		}
		public override void ExposeData() {
			base.ExposeData();
			progressBar.ExposeData();

			Scribe_Values.Look(ref activeBill, "activeBill");
			Scribe_Collections.Look(ref bills, "bills");
			Scribe_Deep.Look(ref innerContainer, "innerContainer");
		}
		public override void Destroy(DestroyMode mode) {
			base.Destroy(mode);
			if (mode != DestroyMode.Vanish && bills[activeBill].def.cancelAction == ProcessCancelAction.Drop) {
				foreach (Thing thing in innerContainer) {
					GenSpawn.Spawn(thing, this.Position, this.Map);
				}
			}
		}
	}
}
