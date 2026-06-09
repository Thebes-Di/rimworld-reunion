using System.Collections.Generic;
using RimWorld;
using RimWorld.QuestGen;
using RimWorld.Planet;
using Verse;
using Verse.Grammar;

namespace Kyrun.Reunion
{
    public static class IncidentAllyDownedRefugee
    {
        public static bool Do()
        {
            var root = DefDatabase<QuestScriptDef>.GetNamed("Reunion_DownedRefugee");
            var points = StorytellerUtility.DefaultThreatPointsNow(Current.Game.AnyPlayerHomeMap);
            QuestUtility.SendLetterQuestAvailable(QuestUtility.GenerateQuestAndMakeAvailable(root, points));

            return true;
        }
    }


    public class SitePartWorker_DownedRefugee : RimWorld.Planet.SitePartWorker_DownedRefugee
    {
        public override void Notify_GeneratedByQuestGen(SitePart part, Slate slate, List<Rule> outExtraDescriptionRules, Dictionary<string, string> outExtraDescriptionConstants)
        {
            // Duplicate code so modularize in Util
            Util.SitePartWorker_Base_Notify_GeneratedByQuestGen(part, outExtraDescriptionRules, outExtraDescriptionConstants);

            // Replaces DownedRefugeeQuestUtility.GenerateRefugee
            Pawn pawn = GameComponent.GetRandomAllyForSpawning();
            if (pawn == null)
            {
                return;
            }

            //Add quest active tag
            GameComponent.IsQuestActive = true;
            try
            {
                //Remove wasLeftBehindStartingPawn when pawn has spawned
                if (pawn.wasLeftBehindStartingPawn)
                {
                    pawn.wasLeftBehindStartingPawn = false;
                }

                Util.DressPawnIfCold(pawn, part.site.Tile);

                HealthUtility.DamageUntilDowned(pawn, false);
                HealthUtility.DamageLegsUntilIncapableOfMoving(pawn, false);

                //Add immune to temperature hediff
                if (pawn.health != null)
                {
                    var immunity = HediffMaker.MakeHediff(GameComponent.m_ReunionImmunity, pawn);
                    immunity.Severity = 1f;
                    pawn.health.AddHediff(immunity);
                }

                part.things = new ThingOwner<Pawn>(part, true, LookMode.Deep);
                part.things.TryAdd(pawn, true);
                if (pawn.relations != null)
                {
                    pawn.relations.everSeenByPlayer = true;
                }
                Pawn mostImportantColonyRelative = PawnRelationUtility.GetMostImportantColonyRelative(pawn);
                if (mostImportantColonyRelative != null)
                {
                    PawnRelationDef mostImportantRelation = mostImportantColonyRelative.GetMostImportantRelation(pawn);
                    TaggedString taggedString = "";
                    if (mostImportantRelation != null && mostImportantRelation.opinionOffset > 0)
                    {
                        pawn.relations.relativeInvolvedInRescueQuest = mostImportantColonyRelative;
                        taggedString = "\n\n" + "RelatedPawnInvolvedInQuest".Translate(mostImportantColonyRelative.LabelShort, mostImportantRelation.GetGenderSpecificLabel(pawn), mostImportantColonyRelative.Named("RELATIVE"), pawn.Named("PAWN")).AdjustedFor(pawn, "PAWN", true);
                    }
                    else
                    {
                        PawnRelationUtility.TryAppendRelationsWithColonistsInfo(ref taggedString, pawn);
                    }
                    outExtraDescriptionRules.Add(new Rule_String("pawnInvolvedInQuestInfo", taggedString));
                }
                outExtraDescriptionRules.AddRange(GrammarUtility.RulesForPawn("refugee", pawn, outExtraDescriptionConstants, true, true));
            }
            catch
            {
                Util.Error("Failed to generate downed refugee site");
                GameComponent.IsQuestActive = false;
            }
        }

        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);

            //Remove immune to temperature hediff
            Site site = Find.World.worldObjects.Sites.FirstOrDefault(s => s.Map == map);
            if (site != null)
            {
                var sitePart = site.parts.FirstOrDefault(p => p.def == this.def);
                if (sitePart != null && sitePart.things != null && sitePart.things.Any)
                {
                    Pawn pawn = (Pawn)sitePart.things[0];
                    if (pawn != null && GameComponent.ListAllySpawned.Contains(pawn.GetUniqueLoadID()) && pawn.health != null)
                    {
                        var immunity = pawn.health.hediffSet.GetFirstHediffOfDef(GameComponent.m_ReunionImmunity);
                        if (immunity != null)
                        {
                            pawn.health.RemoveHediff(immunity);
                        }
                    }
                }
            }

            GameComponent.FlagNextEventReadyForScheduling();
        }

        public override void PostDestroy(SitePart sitePart)
        {
            base.PostDestroy(sitePart);
            Util.OnPostDestroyReschedule(sitePart);
        }
    }
}
