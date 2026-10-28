using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace OpenBepSync.Tests
{
    public class MaterialRulesTests
    {
        static readonly string[] Model = { "Oak Flooring", "Wood - Oak", "Chair wood", "Toaster steel", "Black Plastic", "OB_Wood_Oak_Flooring", "OB_Generic_Metal" };

        static ResolvedRule One(IEnumerable<ResolvedRule> rs, string old) => rs.Single(r => r.Rule.Old == old);

        [Fact]
        public void Reads_OpenBepAndPolynineHeaders_AndHeaderless()
        {
            var a = MaterialRules.ReadText("Old name,New name\r\nChair wood,OB_Generic_Wood\r\n", out var w1);
            var b = MaterialRules.ReadText("﻿EXISTING MATERIAL,NEW MATERIAL\nChair wood,OB_Generic_Wood\n", out var w2);
            var c = MaterialRules.ReadText("Chair wood,OB_Generic_Wood\n\n# comment\n", out var w3);
            foreach (var r in new[] { a, b, c }) Assert.Equal(("Chair wood", "OB_Generic_Wood"), (r.Single().Old, r.Single().New));
            Assert.Empty(w1.Concat(w2).Concat(w3));
        }

        [Fact]
        public void Reads_ColumnsInAnyOrder_AndFlagsHalfRules()
        {
            var r = MaterialRules.ReadText("Note,New name,Old name\nx,OB_Generic_Wood,Chair wood\ny,,Black Plastic\n", out var w);
            Assert.Equal("Chair wood", r.Single().Old);
            Assert.Single(w);
        }

        [Fact]
        public void WriteThenRead_KeepsCommasAndQuotes()
        {
            var rules = new List<MaterialRule> { new MaterialRule("Concrete, \"cast\" 25 MPa", "OB_Concrete_CastInPlace_25MPa") };
            var back = MaterialRules.ReadText(MaterialRules.WriteText(rules), out var w);
            Assert.Empty(w);
            Assert.Equal(rules[0].Old, back.Single().Old);
        }

        [Fact]
        public void ExistingTarget_IsReplace_NewTarget_IsRename()
        {
            var rs = MaterialRules.Resolve(new[] {
                new MaterialRule("Oak Flooring", "OB_Wood_Oak_Flooring"),
                new MaterialRule("Toaster steel", "OB_Generic_Metal"),
                new MaterialRule("Black Plastic", "OB_Generic_Plastic") }, Model);
            Assert.Equal(RuleAction.Replace, One(rs, "Oak Flooring").Action);
            Assert.Equal(RuleAction.Replace, One(rs, "Toaster steel").Action);
            Assert.Equal(RuleAction.Rename, One(rs, "Black Plastic").Action);
            Assert.All(rs, r => Assert.Equal("", r.Note));
        }

        [Fact]
        public void SeveralOldsToOneNewName_RenamesTheMostUsed_MergesTheRest()
        {
            var uses = new Dictionary<string, int> { ["Wood - Oak"] = 3, ["Chair wood"] = 40 };
            var rs = MaterialRules.Resolve(new[] {
                new MaterialRule("Wood - Oak", "OB_Generic_Wood"),
                new MaterialRule("Chair wood", "OB_Generic_Wood") }, Model, n => uses[n]);
            Assert.Equal(RuleAction.Rename, One(rs, "Chair wood").Action);
            Assert.Equal(RuleAction.Replace, One(rs, "Wood - Oak").Action);
            Assert.Contains("after Chair wood is renamed", One(rs, "Wood - Oak").Note);
        }

        [Fact]
        public void ByCategory_Removes()
        {
            var rs = MaterialRules.Resolve(new[] { new MaterialRule("Black Plastic", MaterialRules.ByCategory) }, Model);
            Assert.Equal(RuleAction.Remove, rs.Single().Action);
        }

        [Fact]
        public void Skips_Missing_Self_DuplicateAndConflict()
        {
            var rs = MaterialRules.Resolve(new[] {
                new MaterialRule("Not here", "OB_Generic_Wood"),
                new MaterialRule("Chair wood", "Chair wood"),
                new MaterialRule("Wood - Oak", "OB_Wood_Oak_Flooring"),
                new MaterialRule("Wood - Oak", "OB_Wood_Oak_Flooring"),
                new MaterialRule("Wood - Oak", "OB_Generic_Wood") }, Model);
            Assert.Equal(new[] { RuleAction.Skip, RuleAction.Skip, RuleAction.Replace, RuleAction.Skip, RuleAction.Skip }, rs.Select(r => r.Action));
            Assert.Equal("not in this model", rs[0].Note);
            Assert.Equal("same rule twice", rs[3].Note);
            Assert.Contains("already goes to", rs[4].Note);
        }

        [Fact]
        public void FollowsChains_AndSkipsLoops()
        {
            var chain = MaterialRules.Resolve(new[] {
                new MaterialRule("Wood - Oak", "Oak Flooring"),
                new MaterialRule("Oak Flooring", "OB_Wood_Oak_Flooring") }, Model);
            Assert.All(chain, r => Assert.Equal("OB_Wood_Oak_Flooring", r.Target));
            Assert.All(chain, r => Assert.Equal(RuleAction.Replace, r.Action));

            var loop = MaterialRules.Resolve(new[] {
                new MaterialRule("Wood - Oak", "Chair wood"),
                new MaterialRule("Chair wood", "Wood - Oak") }, Model);
            Assert.All(loop, r => { Assert.Equal(RuleAction.Skip, r.Action); Assert.StartsWith("loop", r.Note); });
        }

        [Fact]
        public void WarnsWhenTheNewNameIsNotAnOpenBepName()
        {
            var rs = MaterialRules.Resolve(new[] { new MaterialRule("Chair wood", "Wood") }, Model);
            Assert.Equal(RuleAction.Rename, rs.Single().Action);
            Assert.Contains("OB_<Class>", rs.Single().Note);
        }

        [Theory]
        [InlineData("OB_Wood_Oak_Flooring", true)]
        [InlineData("OB_Masonry_Brick_Red-Matte", true)]
        [InlineData("OB_Concrete_CastInPlace_35MPa", true)]
        [InlineData("OB_Generic_Metal", true)]
        [InlineData("OB_Wood_CLT", true)]
        [InlineData("OB_Wood", false)]
        [InlineData("Wood - Oak", false)]
        [InlineData("ob_wood_oak", false)]
        [InlineData("OB_Wood_Oak Flooring", false)]
        public void OpenBepMaterialNames(string name, bool ok) => Assert.Equal(ok, MaterialRules.IsOpenBepMaterialName(name));
    }
}
