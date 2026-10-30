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
        [InlineData("Level 1", "L01")]
        [InlineData("Level 2", "L02")]
        [InlineData("Level 12", "L12")]
        [InlineData("NIVEAU 3", "L03")]
        [InlineData("Étage 4", "L04")]
        [InlineData("L2", "L02")]
        [InlineData("Level 1 Mezzanine", "L01M")]
        [InlineData("Ground Floor", "L01")]
        [InlineData("Rez-de-chaussée", "L01")]
        [InlineData("Roof", "RF")]
        [InlineData("Toit", "RF")]
        [InlineData("Basement 1", "B1")]
        [InlineData("Sous-sol 2", "B2")]
        [InlineData("P1", "B1")]
        [InlineData("L02", "L02")]
        [InlineData("L02_TOS", "L02_TOS")]
        [InlineData("T.O. Steel", null)]
        [InlineData("Level 0", null)]
        public void ProposesLevelCodes(string name, string code) => Assert.Equal(code, SetupRules.ProposeLevel(name));

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
            Assert.Equal(new[] { "N", "CON", "020-PLAN-EGRESS", "L02" }, SetupRules.Parse("N-CON-PLAN-EGRESS-L02", Phases));
            Assert.Equal(new[] { "N", "Z-USER-SAM", "010-PLAN", "L02" }, SetupRules.Parse("N-Z-USER-SAM-PLAN-L02", Phases));
            Assert.Equal(new[] { "DRAFT", "CON", "400-DET", "PARAPET" }, SetupRules.Parse("DRAFT-CON-DET-PARAPET", Phases));
            Assert.Equal(new[] { "EX", "W", "200-SECT", "" }, SetupRules.Parse("EX-W-SECT", Phases));
            Assert.Null(SetupRules.Parse("Level 1", Phases));
            Assert.Null(SetupRules.Parse("N-CON-NOPE-L02", Phases));
            Assert.Null(SetupRules.Parse("P1-CON-PLAN-L02", Phases));   // P1 is not a phase of this model
        }

        [Fact]
        public void ProposesViewNames()
        {
            var plan = new ViewFacts { Name = "Level 1", Kind = ViewKind.FloorPlan, Phase = "N", Level = "L01", OnSheet = true };
            Assert.Equal(("N-CON-PLAN-L01", "CON", "010-PLAN"), Short(SetupRules.ProposeView(plan, Phases)));

            var rcp = new ViewFacts { Name = "Level 2", Kind = ViewKind.CeilingPlan, Phase = "N", Level = "L02" };
            Assert.Equal(("N-W-RCP-L02", "W", "030-RCP"), Short(SetupRules.ProposeView(rcp, Phases)));

            var filed = new ViewFacts { Name = "Level 2 egress", Kind = ViewKind.FloorPlan, Phase = "N", Level = "L02", ViewSet = "PERMIT", ViewType = "020-PLAN-EGRESS" };
            Assert.Equal(("N-PERMIT-PLAN-EGRESS-L02", "PERMIT", "020-PLAN-EGRESS"), Short(SetupRules.ProposeView(filed, Phases)));

            var sect = new ViewFacts { Name = "Section 1", Kind = ViewKind.Section, Phase = "EX" };
            Assert.Equal("EX-W-SECT-01", SetupRules.ProposeView(sect, Phases).name);

            var three = new ViewFacts { Name = "{3D}", Kind = ViewKind.Axo, Phase = "N" };
            Assert.Equal(("N-Z-WIP-AXO", "Z-WIP", "000-AXO"), Short(SetupRules.ProposeView(three, Phases)));

            var draft = new ViewFacts { Name = "Parapet detail", Kind = ViewKind.Drafting, OnSheet = true };
            Assert.Equal("DRAFT-CON-DET-PARAPET", SetupRules.ProposeView(draft, Phases).name);

            var dep = new ViewFacts { Name = "Level 2 - Dependent 1", Kind = ViewKind.FloorPlan, Phase = "N", Level = "L02", IsDependent = true, ParentName = "N-CON-PLAN-L02", Zone = "A" };
            Assert.Equal("N-CON-PLAN-L02-A", SetupRules.ProposeView(dep, Phases).name);
        }

        [Fact]
        public void RuleNamesStayAsTheyAre()
        {
            var v = new ViewFacts { Name = "N-CON-PLAN-EGRESS-L02", Kind = ViewKind.FloorPlan, Phase = "N", Level = "L02" };
            Assert.Equal(("N-CON-PLAN-EGRESS-L02", "CON", "020-PLAN-EGRESS"), Short(SetupRules.ProposeView(v, Phases)));
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
                new SetupLine("View", SetupAction.Rename, "1", "Section 1", "N-W-SECT-01", "views"),
                new SetupLine("View", SetupAction.Rename, "2", "Section 1 copy", "N-W-SECT-01", "views"),
                new SetupLine("View", SetupAction.Rename, "3", "x", "N-W-SECT-02", "views") };
            SetupRules.MakeUnique(lines, new[] { "N-W-SECT-02" });
            Assert.Equal(new[] { "N-W-SECT-01", "N-W-SECT-01-2", "N-W-SECT-02-2" }, lines.Select(l => l.New));
            Assert.Contains("taken", lines[1].Note);
        }

        [Fact]
        public void PlanFile_RoundTrips_AndFlagsBadLines()
        {
            var lines = new[] {
                new SetupLine("Level", SetupAction.Rename, "311", "Level 1", "L01", "Levels"),
                new SetupLine("Phase filter", SetupAction.Create, "", "", "Show Existing", "Phase filters", "adds, Revit, \"quotes\""),
                new SetupLine("View", SetupAction.Fill, "900", "N-CON-PLAN-L01", "OB_ViewSet=CONSTRUCTION", "Views") };
            var back = SetupRules.ReadPlanText(SetupRules.WritePlanText(lines), out var w);
            Assert.Empty(w);
            Assert.Equal(lines.Select(l => (l.Kind, l.Action, l.Element, l.Current, l.New, l.Rule, l.Note)), back.Select(l => (l.Kind, l.Action, l.Element, l.Current, l.New, l.Rule, l.Note)));

            var bad = SetupRules.ReadPlanText("Kind,Action,Element,Current name,New name\nView,Smash,1,a,b\nView,Rename,,,b\nView,Rename,5,a,\n,Rename,5,a,b\nView,rename,5,a,b\n", out var w2);
            Assert.Single(bad);
            Assert.Equal(4, w2.Count);
        }
    }

}
