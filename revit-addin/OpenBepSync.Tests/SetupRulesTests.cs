using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace OpenBepSync.Tests
{
    public class SetupRulesTests
    {
        static readonly string[] Phases = { "EX", "N" };
        static (string, string, string) Short((string name, string set, string type, string why) p) => (p.name, p.set, p.type);

        [Theory]
        [InlineData("Level 1", "1ST-FLR")]
        [InlineData("Level 2", "2ND-FLR")]
        [InlineData("Level 12", "12TH-FLR")]
        [InlineData("Level 21", "21ST-FLR")]
        [InlineData("NIVEAU 3", "3RD-FLR")]
        [InlineData("Étage 4", "4TH-FLR")]
        [InlineData("2nd floor", "2ND-FLR")]
        [InlineData("2e étage", "2ND-FLR")]
        [InlineData("L2", "2ND-FLR")]
        [InlineData("L02", "2ND-FLR")]
        [InlineData("Level 1 Mezzanine", "1.5-MEZZ")]
        [InlineData("L01M", "1.5-MEZZ")]
        [InlineData("Ground Floor", "1ST-FLR")]
        [InlineData("Rez-de-chaussée", "1ST-FLR")]
        [InlineData("Basement 1", "BSMNT-1")]
        [InlineData("Sous-sol 2", "BSMNT-2")]
        [InlineData("P1", "BSMNT-1")]
        [InlineData("B1", "BSMNT-1")]
        [InlineData("2ND-FLR", "2ND-FLR")]
        [InlineData("1.9-US-2ND-FLR", "1.9-US-2ND-FLR")]
        [InlineData("L02_TOS", null)]
        [InlineData("T.O. Steel", null)]
        [InlineData("Level 0", null)]
        [InlineData("Roof", null)]   // the roof needs to know the top floor
        public void ProposesLevelCodes(string name, string code) => Assert.Equal(code, SetupRules.ProposeLevel(name));

        [Theory]
        [InlineData("Roof", "4-ROOF")]
        [InlineData("Toit", "4-ROOF")]
        [InlineData("RF", "4-ROOF")]
        public void RoofTakesTheNextNumber(string name, string code) => Assert.Equal(code, SetupRules.ProposeLevel(name, 4));

        [Theory]
        [InlineData("Section 1", "01")]
        [InlineData("East", "EAST")]
        [InlineData("Stair section A", "STAIR-A")]
        [InlineData("Coupe - Escalier Nord", "COUPE-ESCALIER-NORD")]
        [InlineData("{3D}", "")]
        public void Suffixes(string name, string suffix) => Assert.Equal(suffix, SetupRules.Suffix(name));

        [Fact]
        public void ParsesRuleNames()
        {
            Assert.Equal(new[] { "N", "CON", "020-PLAN-EGRESS", "2ND-FLR" }, SetupRules.Parse("N-CON-PLAN-EGRESS-2ND-FLR", Phases));
            Assert.Equal(new[] { "N", "Z-USER-SAM", "010-PLAN", "2ND-FLR" }, SetupRules.Parse("N-Z-USER-SAM-PLAN-2ND-FLR", Phases));
            Assert.Equal(new[] { "DRAFT", "CON", "400-DET", "PARAPET" }, SetupRules.Parse("DRAFT-CON-DET-PARAPET", Phases));
            Assert.Equal(new[] { "EX", "WORK", "200-SECT", "" }, SetupRules.Parse("EX-WORK-SECT", Phases));
            Assert.Null(SetupRules.Parse("Level 1", Phases));
            Assert.Null(SetupRules.Parse("N-CON-NOPE-2ND-FLR", Phases));
            Assert.Null(SetupRules.Parse("P1-CON-PLAN-2ND-FLR", Phases));   // P1 is not a phase of this model
        }

        [Fact]
        public void ProposesViewNames()
        {
            var plan = new ViewFacts { Name = "Level 1", Kind = ViewKind.FloorPlan, Phase = "N", Level = "1ST-FLR", OnSheet = true };
            Assert.Equal(("N-CON-PLAN-1ST-FLR", "CON", "010-PLAN"), Short(SetupRules.ProposeView(plan, Phases)));

            var rcp = new ViewFacts { Name = "Level 2", Kind = ViewKind.CeilingPlan, Phase = "N", Level = "2ND-FLR" };
            Assert.Equal(("N-WORK-RCP-2ND-FLR", "WORK", "030-RCP"), Short(SetupRules.ProposeView(rcp, Phases)));

            var filed = new ViewFacts { Name = "Level 2 egress", Kind = ViewKind.FloorPlan, Phase = "N", Level = "2ND-FLR", ViewSet = "PERMIT", ViewType = "020-PLAN-EGRESS" };
            Assert.Equal(("N-PERMIT-PLAN-EGRESS-2ND-FLR", "PERMIT", "020-PLAN-EGRESS"), Short(SetupRules.ProposeView(filed, Phases)));

            var site = new ViewFacts { Name = "Site", Kind = ViewKind.FloorPlan, Phase = "N", Level = "1ST-FLR" };
            Assert.Equal(("N-WORK-PLAN-SITE-1ST-FLR", "WORK", "005-PLAN-SITE"), Short(SetupRules.ProposeView(site, Phases)));

            var sect = new ViewFacts { Name = "Section 1", Kind = ViewKind.Section, Phase = "EX" };
            Assert.Equal("EX-WORK-SECT-01", SetupRules.ProposeView(sect, Phases).name);

            var three = new ViewFacts { Name = "{3D}", Kind = ViewKind.Axo, Phase = "N" };
            Assert.Equal(("N-Z-WIP-AXO", "Z-WIP", "000-AXO"), Short(SetupRules.ProposeView(three, Phases)));

            var draft = new ViewFacts { Name = "Parapet detail", Kind = ViewKind.Drafting, OnSheet = true };
            Assert.Equal("DRAFT-CON-DET-PARAPET", SetupRules.ProposeView(draft, Phases).name);

            var dep = new ViewFacts { Name = "Level 2 - Dependent 1", Kind = ViewKind.FloorPlan, Phase = "N", Level = "2ND-FLR", IsDependent = true, ParentName = "N-CON-PLAN-2ND-FLR", Zone = "A" };
            Assert.Equal("N-CON-PLAN-2ND-FLR-A", SetupRules.ProposeView(dep, Phases).name);
        }

        [Fact]
        public void RuleNamesStayAsTheyAre()
        {
            var v = new ViewFacts { Name = "N-CON-PLAN-EGRESS-2ND-FLR", Kind = ViewKind.FloorPlan, Phase = "N", Level = "2ND-FLR" };
            Assert.Equal(("N-CON-PLAN-EGRESS-2ND-FLR", "CON", "020-PLAN-EGRESS"), Short(SetupRules.ProposeView(v, Phases)));
            var s = new ViewFacts { Name = "N-CON-SECT-STAIR-01", Kind = ViewKind.Section, Phase = "N" };
            Assert.Equal("N-CON-SECT-STAIR-01", SetupRules.ProposeView(s, Phases).name);
        }

        [Fact]
        public void SetCodesFromFolders()
        {
            Assert.Equal("CON", SetupRules.SetCode("CONSTRUCTION"));
            Assert.Equal("CON", SetupRules.SetCode("CON"));
            Assert.Equal("Z-USER-SAM", SetupRules.SetCode("Z-USER-SAM"));
            Assert.Equal("", SetupRules.SetCode("Working"));
        }

        [Fact]
        public void MakesNamesUnique()
        {
            var lines = new List<SetupLine> {
                new SetupLine("View", SetupAction.Rename, "1", "Section 1", "N-WORK-SECT-01", "views"),
                new SetupLine("View", SetupAction.Rename, "2", "Section 1 copy", "N-WORK-SECT-01", "views"),
                new SetupLine("View", SetupAction.Rename, "3", "x", "N-WORK-SECT-02", "views") };
            SetupRules.MakeUnique(lines, new[] { "N-WORK-SECT-02" });
            Assert.Equal(new[] { "N-WORK-SECT-01", "N-WORK-SECT-01-2", "N-WORK-SECT-02-2" }, lines.Select(l => l.New));
            Assert.Contains("taken", lines[1].Note);
        }

        [Fact]
        public void PlanFile_RoundTrips_AndFlagsBadLines()
        {
            var lines = new[] {
                new SetupLine("Level", SetupAction.Rename, "311", "Level 1", "1ST-FLR", "Levels"),
                new SetupLine("Phase filter", SetupAction.Create, "", "", "Show Existing", "Phase filters", "adds, Revit, \"quotes\""),
                new SetupLine("View template", SetupAction.Rename, "77", "Architectural Plan", "", "View templates") { Apply = false },
                new SetupLine("View", SetupAction.Check, "", "", "", "Project browser", "run Sync parameters first") { Apply = false },
                new SetupLine("View", SetupAction.Fill, "900", "N-CON-PLAN-1ST-FLR", "OB_ViewSet=CONSTRUCTION", "Views") };
            var back = SetupRules.ReadPlanText(SetupRules.WritePlanText(lines), out var w);
            Assert.Empty(w);
            Assert.Equal(lines.Select(l => (l.Kind, l.Action, l.Element, l.Current, l.New, l.Rule, l.Note)), back.Select(l => (l.Kind, l.Action, l.Element, l.Current, l.New, l.Rule, l.Note)));

            var bad = SetupRules.ReadPlanText("Kind,Action,Element,Current name,New name\nView,Smash,1,a,b\nView,Rename,,,b\nView,Rename,5,a,\n,Rename,5,a,b\nView,rename,5,a,b\n", out var w2);
            Assert.Equal(2, bad.Count);
            Assert.Equal(3, w2.Count);
            Assert.False(bad[0].Apply);   // a rename with no new name comes back unticked, for a person to type
        }
    }

}
