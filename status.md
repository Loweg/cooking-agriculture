# State of the Mod
## TODO
* Yeast, risen bread, beer
* Salt pan
* Stew
* Traders should carry flour and sugar
* Vanilla berries should be fruit
* Wort should be cooking ingredient
* Some buildings should not be reinstallable

# Need Textures
* Grinding machine
* Centrifuge
* Jam
* Flour
* Syrup
* Sugar
* Vinegar
* Risen bread
* Flatbread
* Apple tree
* Cherry tree
* Date tree
* Mulberry tree
* Wheat plant
* Really all of the textures could use improvements. Only the meals, salt pan, and stew pot are anywhere near where I want them. Replacing open-source RimCuisine textures is a low priority.

# Issues
* Masks don't work in the architect menu because of a vanilla rimworld bug.
* Processors don't display progress properly due to processor framework bug.
* Manual mill needs simple mask.
* Smoked meat can be used to make smoked meat.
* Stew pot mask may be misaligned.
* Stew pot eaters want to sit on the stewpot. Maybe causing traders to throw exceptions too. If occupied:
JobDriver threw exception in toil CarryIngestibleToChewSpot's initAction for pawn Lawrence driver=JobDriver_Ingest (toilIndex=2) driver.job=(Ingest (Job_2553) A = Thing_CA_Soup6661 Giver = JobGiver_GetFood [workGiverDef: null])
System.IndexOutOfRangeException: Index was outside the bounds of the array.
[Ref BEA71E59]
 at Verse.EdificeGrid.get_Item (Verse.IntVec3 c) [0x00017] in <2a40c3593b334f29ac3cb3d32d652351>:0
 at Verse.GridsUtility.GetEdifice (Verse.IntVec3 c, Verse.Map map) [0x00000] in <2a40c3593b334f29ac3cb3d32d652351>:0
 at Verse.AI.ReservationUtility.ReserveSittableOrSpot (Verse.Pawn pawn, Verse.IntVec3 exactSittingPos, Verse.AI.Job job, System.Boolean errorOnFailed) [0x00007] in <2a40c3593b334f29ac3cb3d32d652351>:0
 at RimWorld.Toils_Ingest+<>c__DisplayClass3_0.<CarryIngestibleToChewSpot>b__0 () [0x001d9] in <2a40c3593b334f29ac3cb3d32d652351>:0
 at Verse.AI.JobDriver.TryActuallyStartNextToil () [0x001b0] in <2a40c3593b334f29ac3cb3d32d652351>:0
UnityEngine.StackTraceUtility:ExtractStackTrace ()
Verse.Log:Error (string)
Verse.AI.JobUtility:TryStartErrorRecoverJob (Verse.Pawn,string,System.Exception,Verse.AI.JobDriver)
Verse.AI.JobDriver:TryActuallyStartNextToil ()
Verse.AI.JobDriver:ReadyForNextToil ()
Verse.AI.JobDriver:DriverTick ()
Verse.AI.Pawn_JobTracker:JobTrackerTick ()
Verse.Pawn:Tick ()
Verse.TickList:Tick ()
Verse.TickManager:DoSingleTick ()
Verse.TickManager:TickManagerUpdate ()
Verse.Game:UpdatePlay ()
Verse.Root_Play:Update ()

Trader:
Could not find sitting spot on chewing chair! This is not supposed to happen - we looked for a free spot in a previous check!
UnityEngine.StackTraceUtility:ExtractStackTrace ()
Verse.Log:Error (string)
RimWorld.Toils_Ingest/<>c__DisplayClass3_0:<CarryIngestibleToChewSpot>b__0 ()
Verse.AI.JobDriver:TryActuallyStartNextToil ()
Verse.AI.JobDriver:ReadyForNextToil ()
Verse.AI.JobDriver:TryActuallyStartNextToil ()
Verse.AI.JobDriver:ReadyForNextToil ()
Verse.AI.JobDriver:Notify_PatherArrived ()
Verse.AI.Pawn_PathFollower:PatherArrived ()
Verse.AI.Pawn_PathFollower:StartPath (Verse.LocalTargetInfo,Verse.AI.PathEndMode)
Verse.AI.Toils_Goto/<>c__DisplayClass1_0:<GotoThing>b__0 ()
Verse.AI.JobDriver:TryActuallyStartNextToil ()
Verse.AI.JobDriver:ReadyForNextToil ()
Verse.AI.JobDriver:JumpToToil (Verse.AI.Toil)
Verse.AI.Toils_Jump/<>c__DisplayClass1_0:<JumpIf>b__0 ()
Verse.AI.JobDriver:TryActuallyStartNextToil ()
Verse.AI.JobDriver:ReadyForNextToil ()
Verse.AI.JobDriver:TryActuallyStartNextToil ()
Verse.AI.JobDriver:ReadyForNextToil ()
Verse.AI.Pawn_JobTracker:StartJob (Verse.AI.Job,Verse.AI.JobCondition,Verse.AI.ThinkNode,bool,bool,Verse.ThinkTreeDef,System.Nullable`1<Verse.AI.JobTag>,bool,bool,System.Nullable`1<bool>,bool,bool,bool)
Verse.AI.Pawn_JobTracker:TryFindAndStartJob ()
Verse.AI.Pawn_JobTracker:EndCurrentJob (Verse.AI.JobCondition,bool,bool)
Verse.AI.Pawn_JobTracker:JobTrackerTick ()
Verse.Pawn:Tick ()
Verse.TickList:Tick ()
Verse.TickManager:DoSingleTick ()
Verse.TickManager:TickManagerUpdate ()
Verse.Game:UpdatePlay ()
Verse.Root_Play:Update ()

JobDriver threw exception in toil CarryIngestibleToChewSpot's initAction for pawn Dolly driver=JobDriver_Ingest (toilIndex=6) driver.job=(Ingest (Job_7321) A = Thing_CA_Soup6976 Giver = JobGiver_GetFood [workGiverDef: null])
System.IndexOutOfRangeException: Index was outside the bounds of the array.
[Ref BEA71E59] Duplicate stacktrace, see ref for original
UnityEngine.StackTraceUtility:ExtractStackTrace ()
Verse.Log:Error (string)
Verse.AI.JobUtility:TryStartErrorRecoverJob (Verse.Pawn,string,System.Exception,Verse.AI.JobDriver)
Verse.AI.JobDriver:TryActuallyStartNextToil ()
Verse.AI.JobDriver:ReadyForNextToil ()
Verse.AI.JobDriver:TryActuallyStartNextToil ()
Verse.AI.JobDriver:ReadyForNextToil ()
Verse.AI.JobDriver:Notify_PatherArrived ()
Verse.AI.Pawn_PathFollower:PatherArrived ()
Verse.AI.Pawn_PathFollower:StartPath (Verse.LocalTargetInfo,Verse.AI.PathEndMode)
Verse.AI.Toils_Goto/<>c__DisplayClass1_0:<GotoThing>b__0 ()
Verse.AI.JobDriver:TryActuallyStartNextToil ()
Verse.AI.JobDriver:ReadyForNextToil ()
Verse.AI.JobDriver:JumpToToil (Verse.AI.Toil)
Verse.AI.Toils_Jump/<>c__DisplayClass1_0:<JumpIf>b__0 ()
Verse.AI.JobDriver:TryActuallyStartNextToil ()
Verse.AI.JobDriver:ReadyForNextToil ()
Verse.AI.JobDriver:TryActuallyStartNextToil ()
Verse.AI.JobDriver:ReadyForNextToil ()
Verse.AI.Pawn_JobTracker:StartJob (Verse.AI.Job,Verse.AI.JobCondition,Verse.AI.ThinkNode,bool,bool,Verse.ThinkTreeDef,System.Nullable`1<Verse.AI.JobTag>,bool,bool,System.Nullable`1<bool>,bool,bool,bool)
Verse.AI.Pawn_JobTracker:TryFindAndStartJob ()
Verse.AI.Pawn_JobTracker:EndCurrentJob (Verse.AI.JobCondition,bool,bool)
Verse.AI.Pawn_JobTracker:JobTrackerTick ()
Verse.Pawn:Tick ()
Verse.TickList:Tick ()
Verse.TickManager:DoSingleTick ()
Verse.TickManager:TickManagerUpdate ()
Verse.Game:UpdatePlay ()
Verse.Root_Play:Update ()
