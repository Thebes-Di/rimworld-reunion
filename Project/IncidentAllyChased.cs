using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;


namespace Kyrun.Reunion
{
    public static class IncidentAllyChased
    {
        public static bool Do()
        {
            var root = DefDatabase<QuestScriptDef>.GetNamed("Reunion_AllyChased");
            var points = StorytellerUtility.DefaultThreatPointsNow(Current.Game.AnyPlayerHomeMap);
            QuestUtility.SendLetterQuestAvailable(QuestUtility.GenerateQuestAndMakeAvailable(root, points));

            return true;
        }
    }

    public class IncidentAllyChased_Join : QuestNode
    {
        protected override void RunInt()
        {
            Pawn pawn = GameComponent.GetRandomAllyForSpawning();
            if (pawn == null)
            {
                return;
            }

            //Add quest active tag
            GameComponent.IsQuestActive = true;
            try
            {
                Slate slate = QuestGen.slate;

                if (storeAs.GetValue(slate) != null)
                {
                    QuestGen.slate.Set(storeAs.GetValue(slate), pawn, false);
                }
                if (addToList.GetValue(slate) != null)
                {
                    QuestGenUtility.AddToOrMakeList(QuestGen.slate, addToList.GetValue(slate), pawn);
                }
                QuestGen.AddToGeneratedPawns(pawn);

                // Vanilla code: adds the pawn to the World.
                // For this mod, remove them from the available list and put them in the spawned list instead.
            }
            catch
            {
                Util.Error("Failed to generate AllyChased quest");
                GameComponent.IsQuestActive = false;
            }
        }

        protected override bool TestRunInt(Slate slate)
        {
            return true;
        }

        [NoTranslate]
        public SlateRef<string> storeAs;

        [NoTranslate]
        public SlateRef<string> addToList;

    }

    public class IncidentAllyChased_PawnsArrive : QuestNode_PawnsArrive
    {
        public SlateRef<IntVec3?> dropSpot;
        protected override void RunInt()
        {
            Slate slate = QuestGen.slate;
            Map map = QuestGen.slate.Get<Map>("map", null, false);
            

            PawnsArrivalModeDef surfaceArrivalMode = this.arrivalMode.GetValue(slate) ?? PawnsArrivalModeDefOf.EdgeWalkIn;
            PawnsArrivalModeDef pawnsArrivalModeDef = map.Tile.LayerDef.isSpace ? PawnsArrivalModeDefOf.EdgeDrop : surfaceArrivalMode;

            // this line is the only thing changed (we are using custom QuestPart)
            var pawnsArrive = new Kyrun.Reunion.QuestPart_PawnsArrive();

            pawnsArrive.inSignal = (QuestGenUtility.HardcodedSignalWithQuestID(this.inSignal.GetValue(slate)) ?? QuestGen.slate.Get<string>("inSignal", null, false));
            pawnsArrive.pawns.AddRange(this.pawns.GetValue(slate));
            pawnsArrive.arrivalMode = pawnsArrivalModeDef;
            pawnsArrive.joinPlayer = this.joinPlayer.GetValue(slate);
            pawnsArrive.mapParent = QuestGen.slate.Get<Map>("map", null, false).Parent;
            if (pawnsArrivalModeDef.walkIn)
            {
                pawnsArrive.spawnNear = (this.walkInSpot.GetValue(slate) ?? (QuestGen.slate.Get<IntVec3?>("walkInSpot", null, false) ?? IntVec3.Invalid));
            }
            else
            {
                pawnsArrive.spawnNear = (this.dropSpot.GetValue(slate) ?? (QuestGen.slate.Get<IntVec3?>("dropSpot", null, false) ?? IntVec3.Invalid));
            }
            if (!this.customLetterLabel.GetValue(slate).NullOrEmpty() || this.customLetterLabelRules.GetValue(slate) != null)
            {
                QuestGen.AddTextRequest("root", delegate (string x)
                {
                    pawnsArrive.customLetterLabel = x;
                }, QuestGenUtility.MergeRules(this.customLetterLabelRules.GetValue(slate), this.customLetterLabel.GetValue(slate), "root"));
            }
            if (!this.customLetterText.GetValue(slate).NullOrEmpty() || this.customLetterTextRules.GetValue(slate) != null)
            {
                QuestGen.AddTextRequest("root", delegate (string x)
                {
                    pawnsArrive.customLetterText = x;
                }, QuestGenUtility.MergeRules(this.customLetterTextRules.GetValue(slate), this.customLetterText.GetValue(slate), "root"));
            }
            QuestGen.quest.AddPart(pawnsArrive);
        }
    }

    public class QuestPart_PawnsArrive : RimWorld.QuestPart_PawnsArrive
    {

        public override void Notify_QuestSignalReceived(Signal signal)
        {
            if (signal.tag != inSignal)
            {
                return;
            }

            pawns.RemoveAll((Pawn x) => x.Destroyed);
            if (mapParent == null || !mapParent.HasMap || !quest.IsParentSuitableForQuest(mapParent))
            {
                mapParent = quest.TryFindNewSuitableMapParentForRetarget();
                if (spawnNear.IsValid)
                {
                    spawnNear = IntVec3.Invalid;
                }
            }

            if (mapParent == null || !mapParent.HasMap || !pawns.Any())
            {
                return;
            }

            for (int i = 0; i < pawns.Count; i++)
            {
                if (joinPlayer && pawns[i].Faction != Faction.OfPlayer)
                {
                    pawns[i].SetFaction(Faction.OfPlayer);
                }
            }

            IncidentParms incidentParms = new IncidentParms();
            incidentParms.target = mapParent.Map;
            incidentParms.spawnCenter = spawnNear;
            PawnsArrivalModeDef pawnsArrivalModeDef = arrivalMode ?? PawnsArrivalModeDefOf.EdgeWalkIn;
            if (!pawnsArrivalModeDef.Worker.CanUseOnMap(mapParent.Map))
            {
                foreach (PawnsArrivalModeDef item in DefDatabase<PawnsArrivalModeDef>.AllDefsListForReading.InRandomOrder())
                {
                    if (item.canBeBackup && item.Worker.CanUseOnMap(mapParent.Map))
                    {
                        pawnsArrivalModeDef = item;
                        break;
                    }
                }
            }

            if (!pawnsArrivalModeDef.Worker.CanUseOnMap(mapParent.Map))
            {
                Log.Error($"Tried to do pawns arrive on map {mapParent.Map} but could not find a legal arrival mode, current method: {pawnsArrivalModeDef.defName}");
                return;
            }

            pawnsArrivalModeDef.Worker.TryResolveRaidSpawnCenter(incidentParms);
            //this is the only thing changed
            if (mapParent.Map.Tile.Tile.PrimaryBiome.inVacuum)
            {
                foreach (var pawn in pawns)
                {
                    Util.EquipSpaceSuit(pawn);
                }
            }
            pawnsArrivalModeDef.Worker.Arrive(pawns, incidentParms);
            if (!sendStandardLetter)
            {
                return;
            }

            TaggedString title;
            TaggedString text;
            if (joinPlayer && pawns.Count == 1 && pawns[0].RaceProps.Humanlike)
            {
                text = "LetterRefugeeJoins".Translate(pawns[0].Named("PAWN"));
                title = "LetterLabelRefugeeJoins".Translate(pawns[0].Named("PAWN"));
                PawnRelationUtility.TryAppendRelationsWithColonistsInfo(ref text, ref title, pawns[0]);
            }
            else
            {
                if (joinPlayer)
                {
                    text = "LetterPawnsArriveAndJoin".Translate(GenLabel.ThingsLabel(pawns.Cast<Thing>()));
                    title = "LetterLabelPawnsArriveAndJoin".Translate();
                }
                else
                {
                    text = "LetterPawnsArrive".Translate(GenLabel.ThingsLabel(pawns.Cast<Thing>()));
                    title = "LetterLabelPawnsArrive".Translate();
                }

                PawnRelationUtility.Notify_PawnsSeenByPlayer_Letter(pawns, ref title, ref text, "LetterRelatedPawnsNeutralGroup".Translate(Faction.OfPlayer.def.pawnsPlural), informEvenIfSeenBefore: true);
            }

            title = (customLetterLabel.NullOrEmpty() ? title : customLetterLabel.Formatted(title.Named("BASELABEL")));
            text = (customLetterText.NullOrEmpty() ? text : customLetterText.Formatted(text.Named("BASETEXT")));
            Find.LetterStack.ReceiveLetter(title, text, customLetterDef ?? LetterDefOf.PositiveEvent, pawns, null, quest);
        }

        public override void Cleanup()
        {
            base.Cleanup(); // there's nothing in the base call (at the moment), but just in case in the future there's something

            // get pawns that spawned from Reunion
            var listToReturn = pawns.FindAll((pawn) =>
            {
                return pawn.Faction != Faction.OfPlayer && GameComponent.ListAllySpawned.Contains(pawn.GetUniqueLoadID());
            });

            GameComponent.FlagNextEventReadyForScheduling();
            foreach (var pawn in listToReturn)
            {
                GameComponent.ReturnToAvailable(pawn);
            }

            if (quest.State == QuestState.EndedOfferExpired) saveByReference = true;
            else GameComponent.TryScheduleNextEvent(ScheduleMode.Forced);

            //Clear quest active tag
            GameComponent.IsQuestActive = false;
        }


        public override void ExposeData()
        {
            Scribe_Values.Look<string>(ref this.inSignal, "inSignal", null, false);

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                foreach (var pawn in pawns)
                {
                    if (pawn.Faction == Faction.OfPlayer) // if any pawn has already joined, that means it has spawned
                    {
                        saveByReference = true;
                        break;
                    }
                }
            }

            Scribe_Values.Look<bool>(ref this.saveByReference, "saveByReference", saveByReference, true);

            var lookModeForPawn = saveByReference ? LookMode.Reference : LookMode.Deep;
            Scribe_Collections.Look<Pawn>(ref this.pawns, "pawns", lookModeForPawn, Array.Empty<object>());

            Scribe_Defs.Look<PawnsArrivalModeDef>(ref this.arrivalMode, "arrivalMode");
            Scribe_References.Look<MapParent>(ref this.mapParent, "mapParent", false);
            Scribe_Values.Look<IntVec3>(ref this.spawnNear, "spawnNear", default(IntVec3), false);
            Scribe_Values.Look<bool>(ref this.joinPlayer, "joinPlayer", false, false);
            Scribe_Values.Look<string>(ref this.customLetterLabel, "customLetterLabel", null, false);
            Scribe_Values.Look<string>(ref this.customLetterText, "customLetterText", null, false);
            Scribe_Defs.Look<LetterDef>(ref this.customLetterDef, "customLetterDef");
            Scribe_Values.Look<bool>(ref this.sendStandardLetter, "sendStandardLetter", true, false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                this.pawns.RemoveAll((Pawn x) => x == null);
            }
        }

        public bool saveByReference;
    }

    public class IncidentAllyChased_GetFaction : QuestNode_GetFaction
    {
        protected override void RunInt()
        {
            Slate slate = QuestGen.slate;
            Map map = slate.Get<Map>("map");
            // Add PassLayerFilter check
            if ((!QuestGen.slate.TryGet<Faction>(storeAs.GetValue(slate), out var var) || !IsGoodFaction(var, QuestGen.slate) || !PassLayerFilter(var, map)) && TryFindFaction(out var, QuestGen.slate, map))
            {
                QuestGen.slate.Set(storeAs.GetValue(slate), var);
                if (!var.Hidden)
                {
                    QuestPart_InvolvedFactions questPart_InvolvedFactions = new QuestPart_InvolvedFactions();
                    questPart_InvolvedFactions.factions.Add(var);
                    QuestGen.quest.AddPart(questPart_InvolvedFactions);
                }
            }
        }

        private bool PassLayerFilter(Faction faction, Map map)
        {
            if (!map.Tile.LayerDef.isSpace)
                return true;

            if (faction.def.arrivalLayerWhitelist.NullOrEmpty() &&
                !faction.def.arrivalLayerWhitelist.Contains(map.Tile.LayerDef))
                return false;

            return true;
        }

        private bool TryFindFaction(out Faction faction, Slate slate, Map map)
        {
            return Find.FactionManager.GetFactions(true)
                   // Add PassLayerFilter check
                   .Where(f =>IsGoodFaction(f, slate) && PassLayerFilter(f, map))
                   .TryRandomElement(out faction);
        }
        // Vanilla code
        private bool IsGoodFaction(Faction faction, Slate slate)
        {
            if (faction.Hidden && (allowedHiddenFactions.GetValue(slate) == null || !allowedHiddenFactions.GetValue(slate).Contains(faction)))
            {
                return false;
            }

            if (ofPawn.GetValue(slate) != null && faction != ofPawn.GetValue(slate).Faction)
            {
                return false;
            }

            if (exclude.GetValue(slate) != null && exclude.GetValue(slate).Contains(faction))
            {
                return false;
            }

            if (mustBePermanentEnemy.GetValue(slate) && !faction.def.permanentEnemy)
            {
                return false;
            }

            if (!allowEnemy.GetValue(slate) && faction.HostileTo(Faction.OfPlayer))
            {
                return false;
            }

            if (!allowNeutral.GetValue(slate) && faction.PlayerRelationKind == FactionRelationKind.Neutral)
            {
                return false;
            }

            if (!allowAlly.GetValue(slate) && faction.PlayerRelationKind == FactionRelationKind.Ally)
            {
                return false;
            }

            bool? value = allowPermanentEnemy.GetValue(slate);
            if (value.HasValue && !value.GetValueOrDefault() && faction.def.permanentEnemy)
            {
                return false;
            }

            if (playerCantBeAttackingCurrently.GetValue(slate) && SettlementUtility.IsPlayerAttackingAnySettlementOf(faction))
            {
                return false;
            }

            if (mustHaveGoodwillRewardsEnabled.GetValue(slate) && !faction.allowGoodwillRewards)
            {
                return false;
            }

            if (peaceTalksCantExist.GetValue(slate))
            {
                if (PeaceTalksExist(faction))
                {
                    return false;
                }

                string tag = QuestNode_QuestUnique.GetProcessedTag("PeaceTalks", faction);
                if (Find.QuestManager.questsInDisplayOrder.Any((Quest q) => q.tags.Contains(tag)))
                {
                    return false;
                }
            }

            if (leaderMustBeSafe.GetValue(slate) && (faction.leader == null || faction.leader.Spawned || faction.leader.IsPrisoner))
            {
                return false;
            }

            Thing value2 = mustBeHostileToFactionOf.GetValue(slate);
            if (value2 != null && value2.Faction != null && (value2.Faction == faction || !faction.HostileTo(value2.Faction)))
            {
                return false;
            }

            return true;
        }

        private bool PeaceTalksExist(Faction faction)
        {
            List<PeaceTalks> peaceTalks = Find.WorldObjects.PeaceTalks;
            for (int i = 0; i < peaceTalks.Count; i++)
            {
                if (peaceTalks[i].Faction == faction)
                {
                    return true;
                }
            }

            return false;
        }

    }

    public class QuestNode_IsSpaceMap : QuestNode
    {
        public QuestNode node;
        public bool shouldInSpaceMap;

        protected override bool TestRunInt(Slate slate)
        {
            Map map = slate.Get<Map>("map");

            return map != null
                && map.Tile.LayerDef.isSpace == shouldInSpaceMap
                && (node == null || node.TestRun(slate));
        }

        protected override void RunInt()
        {
            Map map = QuestGen.slate.Get<Map>("map");

            if (map != null &&
                map.Tile.LayerDef.isSpace == shouldInSpaceMap)
            {
                node?.Run();
            }
        }
    }
}
