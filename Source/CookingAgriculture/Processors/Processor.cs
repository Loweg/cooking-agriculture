using System.Collections.Generic;

using Verse;

namespace CookingAgriculture.Processors {
    public class CompProcessor : ThingComp {
        public CompProperties_Processor Props => (CompProperties_Processor)props;
    }

    public class CompProperties_Processor : CompProperties {
        public List<ProcessDef> processes = new List<ProcessDef>();

        public CompProperties_Processor() {
            compClass = typeof(CompProcessor);
        }

        public override void ResolveReferences(ThingDef parentDef) {
            base.ResolveReferences(parentDef);
            foreach (ProcessDef processDef in processes) {
                processDef.ResolveReferences();
            }
        }
    }
}
