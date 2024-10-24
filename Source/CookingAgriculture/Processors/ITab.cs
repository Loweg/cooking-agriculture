using System.Linq;
using RimWorld;
using Verse;

namespace CookingAgriculture.Processors {
    public class ITab_IngredientSelection : ITab_Storage {
        public ITab_IngredientSelection() {
            labelKey = "ITab_Processor";
        }
        protected override bool IsPrioritySettingVisible => false;
        public override bool IsVisible => Find.Selector.SelectedObjects.All(x => x is Thing thing && thing.Faction == Faction.OfPlayerSilentFail && thing.TryGetComp<CompProcessor>() != null);
    }
}