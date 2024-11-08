using RimWorld;
using UnityEngine;
using Verse;

namespace CookingAgriculture {
    public class CookingAgriculture : Mod {
        static CookingAgriculture() { }
        public CookingAgriculture(ModContentPack content) : base(content) { }
        public static void ApplySettings() {
            var settings = LoadedModManager.GetMod<CookingAgriculture>().GetSettings<Settings>();
        }

        public override string SettingsCategory() => "Cooking and Agriculture";

        public override void DoSettingsWindowContents(Rect inRect) {
            GetSettings<Settings>().DoSettingsWindowContents(inRect);
        }
    }
    public class Settings : ModSettings {
        public bool ineffective_freezing = true;

        public override void ExposeData() {
            Scribe_Values.Look(ref ineffective_freezing, "ineffective_freezing", true);
        }

        public void DoSettingsWindowContents(Rect inRect) {
            Rect rectWeCanSee = inRect.ContractedBy(10f);
            rectWeCanSee.height -= 100f; // "close" button
            bool scrollBarVisible = totalContentHeight > rectWeCanSee.height;
            Rect rectThatHasEverything = new Rect(0f, 0f, rectWeCanSee.width - (scrollBarVisible ? ScrollBarWidthMargin : 0), totalContentHeight);
            Widgets.BeginScrollView(rectWeCanSee, ref scrollPosition, rectThatHasEverything);
            float curY = 0f;
            Rect r = new Rect(0, curY, rectThatHasEverything.width, LabelHeight);

            Widgets.Label(r, "LowegSettingsRestartWarning".Translate());
            curY += LabelHeight + 5f;

            Widgets.DrawLineHorizontal(10, curY + 7, rectThatHasEverything.width - 10);
            curY += 10;

            MakeBoolButton(ref curY, rectThatHasEverything.width,
                "SettingIneffectiveFreezing", ref ineffective_freezing);

            Widgets.EndScrollView();
            totalContentHeight = curY + 50f;
        }
        private static Vector2 scrollPosition = new Vector2(0f, 0f);
        private static float totalContentHeight = 1000f;
        private const float ScrollBarWidthMargin = 18f;
        private const float LabelHeight = 22f;

        void MakeBoolButton(ref float curY, float width, string labelKey, ref bool setting) {
            Rect r = new Rect(0, curY, width, LabelHeight);
            Widgets.CheckboxLabeled(r, labelKey.Translate(), ref setting);
            TooltipHandler.TipRegion(r, (labelKey + "Desc").Translate());
            if (Mouse.IsOver(r)) Widgets.DrawHighlight(r);
            curY += LabelHeight + 1f;
        }
    }
}