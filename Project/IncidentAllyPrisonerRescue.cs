using System.Collections.Generic;
using RimWorld;
using RimWorld.QuestGen;
using RimWorld.Planet;
using Verse;
using Verse.Grammar;

namespace Kyrun.Reunion
{
    public static class IncidentAllyPrisonerRescue
    {
        public static bool Do()
        {
            var root = DefDatabase<QuestScriptDef>.GetNamed("Reunion_PrisonerRescue");
            var points = StorytellerUtility.DefaultThreatPointsNow(Current.Game.AnyPlayerHomeMap);
            QuestUtility.SendLetterQuestAvailable(QuestUtility.GenerateQuestAndMakeAvailable(root, points));

            return true;
        }
    }

    public class SitePartWorker_PrisonerRescue : RimWorld.Planet.SitePartWorker_PrisonerWillingToJoin
    {
        public override void Notify_GeneratedByQuestGen(SitePart part, Slate slate, List<Rule> outExtraDescriptionRules, Dictionary<string, string> outExtraDescriptionConstants)
        {
            // Duplicate code so modularize in Util
            Util.SitePartWorker_Base_Notify_GeneratedByQuestGen(part, outExtraDescriptionRules, outExtraDescriptionConstants);

            // Replaces PrisonerWillingToJoinQuestUtility.GeneratePrisoner
            Pawn pawn = GameComponent.GetRandomAllyForSpawning();
            if (pawn == null)
            {
                return;
            }

            //Add quest active tag
            GameComponent.IsQuestActive = true;

            try {
                //Remove wasLeftBehindStartingPawn when pawn has spawned
                if (pawn.wasLeftBehindStartingPawn)
                {
                    pawn.wasLeftBehindStartingPawn = false;
                }

                pawn.guest.SetGuestStatus(part.site.Faction, GuestStatus.Prisoner);
                Util.DressPawnIfCold(pawn, part.site.Tile);

                //Add immune to temperature hediff
                if (pawn.health != null)
                {
                    var immunity = HediffMaker.MakeHediff(GameComponent.m_ReunionImmunity, pawn);
                    immunity.Severity = 1f;
                    pawn.health.AddHediff(immunity);
                }

                part.things = new ThingOwner<Pawn>(part, true, LookMode.Deep);
                part.things.TryAdd(pawn, true);
                string text;
                PawnRelationUtility.Notify_PawnsSeenByPlayer(Gen.YieldSingle<Pawn>(pawn), out text, true, false);
                outExtraDescriptionRules.AddRange(GrammarUtility.RulesForPawn("prisoner", pawn, outExtraDescriptionConstants, true, true));
                string output;
                if (!text.NullOrEmpty())
                {
                    output = "\n\n" + "PawnHasTheseRelationshipsWithColonists".Translate(pawn.LabelShort, pawn) + "\n\n" + text;
                }
                else
                {
                    output = "";
                }
                outExtraDescriptionRules.Add(new Rule_String("prisonerFullRelationInfo", output));
            }
            catch
            {
                Util.Error("Failed to generate prisoner rescue site");
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
