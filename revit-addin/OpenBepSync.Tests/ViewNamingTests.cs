using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace OpenBepSync.Tests
{
    public class ViewNamingTests
    {
        static string NamingRules()
        {
            for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            {
                var p = Path.Combine(d.FullName, "docs", "naming-rules.md");
                if (File.Exists(p)) return File.ReadAllText(p).Replace("\r\n", "\n");   // Windows checkouts may have CRLF
            }
            throw new FileNotFoundException("docs/naming-rules.md");
        }

        /// <summary>Rows of the markdown table under a heading, as cells without backticks.</summary>
        static string[][] Table(string md, string heading)
        {
            var after = md.Substring(md.IndexOf("\n" + heading + "\n", StringComparison.Ordinal) + heading.Length + 2);
            var next = after.IndexOf("\n#", StringComparison.Ordinal);
            if (next >= 0) after = after.Substring(0, next);
            return after.Split('\n').Where(l => l.StartsWith("|") && !l.StartsWith("| ---"))
                .Skip(1).Select(l => l.Trim().Trim('|').Split('|').Select(c => c.Trim().Trim('`')).ToArray()).ToArray();
        }

        [Fact]
        public void ViewTypes_MatchTheNamingRules()
        {
            var rows = Table(NamingRules(), "### View types");
            Assert.Equal(rows.Select(r => (r[0], r[1], r[2])), ViewNaming.Types.Select(t => (t.Number, t.Label, t.LabelFr)));
        }

        [Fact]
        public void DrawingSets_MatchTheNamingRules()
        {
            var rows = Table(NamingRules(), "### Drawing sets").Where(r => !r[0].Contains("<")).ToArray();
            Assert.Equal(rows.Select(r => (r[0], r[1], r[3])), ViewNaming.Sets.Select(s => (s.Code, s.Folder, s.FolderFr)));
        }

        [Theory]
        [InlineData("Existing", "EX")]
        [InlineData("New Construction", "N")]
        [InlineData("Nouvelle construction", "N")]
        [InlineData("Phase 1", "P1")]
        [InlineData("P2", "P2")]
        [InlineData("EX Existing", "EX")]
        [InlineData("demolition work", "DW")]
        public void PhaseCodes(string phase, string code) => Assert.Equal(code, ViewNaming.PhaseCode(phase));

        [Fact]
        public void Names_FollowPhaseSetTypeLevel()
        {
            Assert.Equal("N-CON-PLAN-EGRESS-L02", ViewNaming.Name(new PlannedView("N", "CON", "020-PLAN-EGRESS", "L02")));
            Assert.Equal("P1-W-PLAN-L01-OPT2", ViewNaming.Name(new PlannedView("P1", "W", "010-PLAN", "L01", "OPT2")));
            Assert.Equal("N-CON-PLAN-L02-A", ViewNaming.Name(new PlannedView("N", "CON", "PLAN", "L02", "", "A")));
            Assert.Equal("N-Z-USER-SAM-RCP-L02", ViewNaming.Name(new PlannedView("N", "Z-USER-SAM", "030-RCP", "L02")));
        }

        [Fact]
        public void Sets_HaveFolders_AndAPersonalSet()
        {
            Assert.Equal("CONSTRUCTION", ViewNaming.SetFolder("CON"));
            Assert.Equal("Z-USER-SAM", ViewNaming.SetFolder("Z-USER-SAM"));
            Assert.Equal("Z-USER-SAMAB", ViewNaming.SetCodes("sam.ab").Last());
        }

        [Fact]
        public void OnlyPlansAndCeilingPlansArePerLevel()
        {
            var perLevel = ViewNaming.Types.Where(t => t.PerLevel).Select(t => t.Code).ToList();
            Assert.Contains("PLAN-EGRESS", perLevel);
            Assert.Contains("RCP-LIGHT", perLevel);
            Assert.DoesNotContain("SECT", perLevel);
            Assert.DoesNotContain("AXO", perLevel);
            Assert.Equal(ViewKind.CeilingPlan, ViewNaming.Type("030-RCP").Kind);
            Assert.Equal(ViewKind.Camera, ViewNaming.Type("500-3D").Kind);
        }

        [Theory]
        [InlineData("L01", true)]
        [InlineData("L01M", true)]
        [InlineData("L12", true)]
        [InlineData("B1", true)]
        [InlineData("RF", true)]
        [InlineData("L02_TOS", true)]
        [InlineData("Level 1", false)]
        [InlineData("L1", false)]
        [InlineData("l02", false)]
        public void LevelNames(string name, bool ok) => Assert.Equal(ok, ViewNaming.IsLevelName(name));

        [Fact]
        public void TitleOnSheet_UsesWordsNotCodes()
        {
            Assert.Equal("Level 2 - Egress plan", ViewNaming.TitleOnSheet(new PlannedView("N", "CON", "020-PLAN-EGRESS", "L02"), false));
            Assert.Equal("Rez-de-chaussée - Plan d'étage", ViewNaming.TitleOnSheet(new PlannedView("N", "CON", "010-PLAN", "L01"), true));
            Assert.Equal("First basement - Reflected ceiling plan - area A", ViewNaming.TitleOnSheet(new PlannedView("N", "CON", "030-RCP", "B1", "", "A"), false));
            Assert.Equal("Floor plan", ViewNaming.TitleOnSheet(new PlannedView("N", "CON", "010-PLAN", "Level 1"), false));
        }

        [Fact]
        public void Templates_MatchByDisciplineTypeAndSet()
        {
            var t = new[] { "ARCH-PLAN-CON-50", "ARCH-PLAN-CON-100", "ARCH-PLAN-EGRESS-CON-200", "ARCH-PLAN-PERMIT-100", "ARCH-PLAN-CON-100 copy" };
            Assert.Equal("ARCH-PLAN-CON-100", ViewNaming.MatchTemplate(t, "ARCH", "010-PLAN", "CON"));
            Assert.Equal("ARCH-PLAN-EGRESS-CON-200", ViewNaming.MatchTemplate(t, "ARCH", "PLAN-EGRESS", "CON"));
            Assert.Null(ViewNaming.MatchTemplate(t, "MECH", "010-PLAN", "CON"));
            Assert.Null(ViewNaming.MatchTemplate(t, "ARCH", "030-RCP", "CON"));
        }

        [Fact]
        public void ScopeBoxZones()
        {
            Assert.Equal("A", ViewNaming.ZoneOf("SB-A"));
            Assert.Equal("NORTH", ViewNaming.ZoneOf("north"));
            Assert.Equal("WINGB", ViewNaming.ZoneOf("Wing B"));
        }

        [Fact]
        public void PhaseFilters_FollowTheRule()
        {
            Assert.Equal("Show Previous + Demo", ViewNaming.DefaultPhaseFilter("N", "009-PLAN-DEMO"));
            Assert.Equal("Show Complete", ViewNaming.DefaultPhaseFilter("EX", "010-PLAN"));
            Assert.Equal("Show Previous + New", ViewNaming.DefaultPhaseFilter("N", "010-PLAN"));
        }

        [Fact]
        public void PlanFile_RoundTrips_AndFlagsBadLines()
        {
            var views = new[] { new PlannedView("N", "CON", "010-PLAN", "L01"), new PlannedView("N", "CON", "010-PLAN", "L01", "", "A"), new PlannedView("EX", "W", "030-RCP", "L02", "OPT1") };
            var back = ViewNaming.ReadPlanText(ViewNaming.WritePlanText(views), out var w);
            Assert.Empty(w);
            Assert.Equal(views, back);

            var odd = ViewNaming.ReadPlanText("Level,Type,Set,Phase\nL02,PLAN-EGRESS,CON,N\nL02,PLAN-NOPE,CON,N\n,PLAN,CON,N\nL02,PLAN-EGRESS,CON,N\n", out var w2);
            Assert.Equal(new[] { new PlannedView("N", "CON", "020-PLAN-EGRESS", "L02") }, odd);
            Assert.Equal(2, w2.Count);
        }
    }
}
