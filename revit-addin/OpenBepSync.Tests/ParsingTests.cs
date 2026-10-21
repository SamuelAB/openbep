using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace OpenBepSync.Tests
{
    public class ParsingTests
    {
        // Rail, road and tunnel elements the Builder gives no Revit category (Revit 2022 has none for them).
        const int NoRevitCategoryInRail = 45;

        static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", name));
        static SharedParameterEntry[] Txt(string example) =>
            SharedParameterFile.Parse(Fixture($"OpenBEP-shared-parameters-{example}.txt"), out _, out _).ToArray();
        static RequiredParameter[] Csv(string example, out string version, out List<string> warnings) =>
            BindingList.ReadText(Fixture($"OpenBEP-parameter-bindings-{example}.csv"), out version, out warnings).ToArray();

        [Fact]
        public void SharedParameterFile_ReadsGroupsAndParams()
        {
            var e = SharedParameterFile.Parse(Fixture("OpenBEP-shared-parameters-school-example.txt"), out var groups, out var warnings);
            Assert.Empty(warnings);
            Assert.Equal(129, e.Count);
            Assert.Equal(7, groups.Count);
            Assert.Equal("OpenBEP 01 Identity and asset", groups[1]);
            var a = e.Single(x => x.Name == "OB_AcousticRating");
            Assert.Equal(new Guid("783c7406-f04c-5678-99c4-1c7190c1de23"), a.Guid);
            Assert.Equal("TEXT", a.DataType);
            Assert.Equal(2, a.Group);
            Assert.StartsWith("Acoustic rating", a.Description);
        }

        [Fact]
        public void SharedParameterFile_FlagsBadLinesAndUndefinedGroups()
        {
            var text = "# c\r\n*META\tVERSION\tMINVERSION\r\nMETA\t2\t1\r\nGROUP\t1\tA\r\nPARAM\tnot-a-guid\tX\tTEXT\t\t1\t1\t\t1\r\n" +
                       "PARAM\t783c7406-f04c-5678-99c4-1c7190c1de23\tOB_AcousticRating\tTEXT\t\t4\t1\t\t1\r\n";
            var e = SharedParameterFile.Parse(text, out _, out var warnings);
            Assert.Single(e);
            Assert.Equal(2, warnings.Count);
        }

        [Theory]
        [InlineData("school-example", 129, 0)]
        [InlineData("rail-example", 124, NoRevitCategoryInRail)]
        [InlineData("custom-levels", 129, 0)]
        public void BuilderExports_ReadAndMatchTheirSharedParameterFile(string example, int count, int withoutCategory)
        {
            var req = Csv(example, out var version, out var warnings);
            Assert.Empty(warnings);
            Assert.Equal("0.1", version);
            Assert.Equal(count, req.Length);
            Assert.Equal(withoutCategory, req.Count(p => p.CategoryIds.Count == 0));   // read, then skipped by the sync as "no usable category"
            Assert.All(req, p => Assert.NotNull(p.FromLod));
            Assert.Empty(BindingList.Check(req, Txt(example)));
        }

        [Fact]
        public void CustomLevels_NoRowRequiresADroppedLevel()
        {
            // The custom-levels export keeps 100, 200, 300 and 400, drops 350 and 500, and adds 351 and 450.
            var req = Csv("custom-levels", out _, out _);
            Assert.DoesNotContain(req, p => p.FromLod == 350 || p.FromLod == 500);
        }

        [Fact]
        public void CustomLod_AnyColumnOrderExtraColumnsAndLooseLevels()
        {
            var req = Csv("custom-lod", out var version, out var warnings);
            Assert.Equal("0.1-custom", version);
            Assert.Equal(4, req.Length);   // Mark is built-in and left out
            var ac = req.Single(p => p.Name == "OB_AcousticRating");
            Assert.Equal(350, ac.FromLod);  // lowest of "LOD 351", "450 (custom)" and "350"
            Assert.True(ac.ScopeConflict);
            Assert.True(ac.IsInstance);
            Assert.Equal(new[] { "OST_Doors", "OST_Floors", "OST_StructuralFoundation", "OST_Walls" }, ac.CategoryIds.OrderBy(x => x));
            Assert.Single(warnings, w => w.Contains("OB_AcousticRating"));
            Assert.Null(req.Single(p => p.Name == "Owner keying zone").FromLod);
            Assert.Equal(500, req.Single(p => p.Name == "Numéro d’actif").FromLod);
            Assert.Empty(BindingList.Check(req, Txt("custom-lod")));
        }

        [Theory]
        [InlineData("LOD 351", 351)]
        [InlineData("351", 351)]
        [InlineData("450 (custom)", 450)]
        [InlineData("LOD350", 350)]
        [InlineData("", null)]
        [InlineData("n/a", null)]
        [InlineData("1000", null)]
        public void Lod_ReadsTheFirstThreeDigitLevel(string cell, int? expected) => Assert.Equal(expected, BindingList.Lod(cell));

        [Fact]
        public void BindingList_MissingRequiredColumnThrows()
        {
            Assert.Throws<InvalidDataException>(() => BindingList.ReadText("\"Revit parameter\",\"GUID\"\r\n\"a\",\"b\"\r\n", out _, out _));
        }

        [Theory]
        [InlineData("OB_FireRating", true)]
        [InlineData("CA_FireRating", true)]
        [InlineData("ob_FireRating", false)]
        [InlineData("OBFireRating", false)]
        [InlineData("Owner keying zone", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void Prefix_OnlyUpperCaseObAndCaCount(string name, bool expected) => Assert.Equal(expected, OpenBepNames.IsOpenBep(name));

        [Fact]
        public void GuidRule_ObKeepsTheCaGuid()
        {
            Assert.Equal(new Guid("783c7406-f04c-5678-99c4-1c7190c1de23"), OpenBepNames.Expected("CA_AcousticRating"));
            Assert.Equal(OpenBepNames.Expected("CA_AcousticRating"), OpenBepNames.Expected("OB_AcousticRating"));
            Assert.Equal(new Guid("df6a6e8d-c787-58d3-bc75-fda37239a312"), OpenBepNames.Expected("OB_FireRating"));
            Assert.NotEqual(OpenBepNames.Expected("OB_FireRating"), OpenBepNames.Expected("OB_AcousticRating"));
        }

        [Fact]
        public void GuidRule_HoldsForEveryParameterInTheExports()
        {
            foreach (var ex in new[] { "school-example", "rail-example", "custom-levels" })
                Assert.All(Txt(ex), e => Assert.Equal(OpenBepNames.Expected(e.Name), e.Guid));
        }

        [Fact]
        public void OwnerGuid_MatchesTheBuilder()
        {
            Assert.Equal(new Guid("f2c96c94-3cfa-57e2-a7b6-57901298395a"), OpenBepNames.Owner("Owner keying zone"));
            Assert.Equal(new Guid("3a0a0f8c-8736-5232-a0bd-b758fa32670a"), OpenBepNames.Owner("Numéro d’actif"));
        }

        [Fact]
        public void Check_ReportsMissingRenamedAndWrongGuids()
        {
            var req = Csv("school-example", out _, out _).ToList();
            var txt = Txt("school-example").ToList();
            var ac = txt.Single(e => e.Name == "OB_AcousticRating");
            txt.Remove(ac);
            txt.Add(new SharedParameterEntry { Guid = ac.Guid, Name = "OB_AcousticRatingX", Group = 2 });   // renamed in the file
            txt.Single(e => e.Name == "OB_FireProtection").Guid = Guid.NewGuid();                                // breaks the rule; the list's GUID goes missing
            var w = BindingList.Check(req, txt);
            Assert.Contains(w, x => x.StartsWith("OB_AcousticRating:") && x.Contains("OB_AcousticRatingX"));
            Assert.Contains(w, x => x.StartsWith("OB_FireProtection:") && x.Contains("not in the shared parameter file"));
            Assert.Contains(w, x => x.StartsWith("OB_FireProtection:") && x.Contains("not the fixed OpenBEP GUID"));
            Assert.Contains(w, x => x.StartsWith("OB_AcousticRatingX:") && x.Contains("not the fixed OpenBEP GUID"));
        }
    }
}
