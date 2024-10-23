using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace CookingAgriculture.Processors {
    public class ITab_IngredientSelection : ITab_Storage {
        public ITab_IngredientSelection() {
            labelKey = "ITab_Processor";
        }
        protected override bool IsPrioritySettingVisible => false;
        public override bool IsVisible => Find.Selector.SelectedObjects.All(x => x is Thing thing && thing.Faction == Faction.OfPlayerSilentFail && thing.TryGetComp<CompProcessor>() != null);

        /*protected override void FillTab() {
            List<object> selectedObjects = Find.Selector.SelectedObjects;
            processorComps = selectedObjects.Select(o => (o as ThingWithComps)?.TryGetComp<CompProcessor>());
            if (processorComps.EnumerableNullOrEmpty()) return;

            List<ProcessDef> processDefs = processorComps.First().Props.processes;

            Rect outRect = new Rect(default, size).ContractedBy(12f);
            outRect.yMin += 24;
            int viewRectHeight = processDefs.Count * lineHeight + 80;
            //foreach (KeyValuePair<ProcessDef, bool> keyValuePair in categoryOpen) {
                //adjust scroll area for each node opened
            //    viewRectHeight += processDefs.Contains(keyValuePair.Key) && keyValuePair.Value ? keyValuePair.Key.ingredientFilter.AllowedDefCount * lineHeight : 0;
            //}

            Rect viewRect = new Rect(0f, 0f, outRect.width - GUI.skin.verticalScrollbar.fixedWidth - 1f, viewRectHeight);
            Widgets.DrawMenuSection(outRect);
            Rect buttonRect = new Rect(outRect.x + 1f, outRect.y + 1f, (outRect.width - 2f) / 2f, 24f);
            Text.Font = GameFont.Small;
            outRect.yMin += buttonRect.height + 6;
            Rect listRect = new Rect(0f, 2f, 280, 9999f);

            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            foreach (ProcessDef processDef in processDefs) {
                if (!categoryOpen.ContainsKey(processDef)) {
                    categoryOpen.Add(processDef, false);
                }
                DoItemsList(ref listRect, processDef);
            }
            Widgets.EndScrollView();
        }

        public void DoItemsList(ref Rect listRect, ProcessDef processDef) {
            bool open = categoryOpen[processDef];

            Rect headerRect = listRect.TopPartPixels(24);
            Rect arrowRect = new Rect(headerRect.x, headerRect.y, 18, 18);
            headerRect.xMin += 18;
            Rect checkboxRect = new Rect(headerRect.x + headerRect.width - 48f, headerRect.y, 20, 20);
            Texture2D tex = open ? TexButton.Collapse : TexButton.Reveal;
            if (Widgets.ButtonImage(arrowRect, tex, true)) {
                if (open) SoundDefOf.TabClose.PlayOneShotOnCamera(null);
                else SoundDefOf.TabOpen.PlayOneShotOnCamera(null);
                categoryOpen[processDef] = !open;
            }

            //Widgets.Label(headerRect, processDef.thingDef.LabelCap);
            
            MultiCheckboxState processState = ProcessStateOf(processDef);
            MultiCheckboxState multiCheckboxState = Widgets.CheckboxMulti(checkboxRect, processState, true);
            if (processState != multiCheckboxState && multiCheckboxState != MultiCheckboxState.Partial) {
                foreach (CompProcessor compProcessor in processorComps) {
                    compProcessor.ToggleProcess(processDef, multiCheckboxState == MultiCheckboxState.On);
                }
            }

            if (open) {
                headerRect.xMin += 12;
                var ingredients = new ThingFilter();
                foreach (var i in processDef.ingredients) {
                    ingredients.SetAllowAll(i.filter);
                }
                List<ThingDef> sortedIngredients = ingredients.AllowedThingDefs.ToList();
                sortedIngredients.SortBy(x => x.label);

                foreach (ThingDef ingredient in sortedIngredients) {
                    checkboxRect.y += lineHeight;
                    headerRect.y += lineHeight;

                    Widgets.Label(headerRect, ingredient.LabelCap);
                    MultiCheckboxState ingredientState = IngredientStateOf(processDef, ingredient);
                    MultiCheckboxState multiCheckboxState2 = Widgets.CheckboxMulti(checkboxRect, ingredientState, true);
                    if (ingredientState != multiCheckboxState2 && multiCheckboxState2 != MultiCheckboxState.Partial)
                    {
                        foreach (CompProcessor compProcessor in processorComps)
                        {
                            //compProcessor.ToggleIngredient(processDef, ingredient, multiCheckboxState2 == MultiCheckboxState.On);
                        }
                    }
                    listRect.y += lineHeight;
                }
            }
            listRect.y += lineHeight;
        }

        public MultiCheckboxState ProcessStateOf(ProcessDef processDef) {
            int count = processorComps.Count(x => x.enabledProcesses.ContainsKey(processDef));
            if (count > 0)
            {
                if (count == processorComps.Count())
                {
                    return MultiCheckboxState.On;
                }
                return MultiCheckboxState.Partial;
            }
            return MultiCheckboxState.Off;
        }
        public MultiCheckboxState IngredientStateOf(ProcessDef processDef, ThingDef ingredient)
        {
            int count = processorComps.Count(x => x.enabledProcesses.TryGetValue(processDef, out ProcessFilter processFilter) && processFilter.allowedIngredients.Contains(ingredient));
            if (count > 0)
            {
                if (count == processorComps.Count())
                {
                    return MultiCheckboxState.On;
                }
                return MultiCheckboxState.Partial;
            }
            return MultiCheckboxState.Off;
        }*/
    }
}