using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace OpenBepSync
{
    /// <summary>What the replace list works on: materials, or one kind of material asset.</summary>
    public enum ReplaceKind { Materials, AppearanceAssets, PhysicalAssets, ThermalAssets }

    /// <summary>One place where a material (or asset) is used.</summary>
    public class MaterialUse
    {
        public ElementId ItemId;     // the material or asset used
        public ElementId Owner;      // element, type, category or family parameter that holds it; unticking it in the window leaves it alone
        public string OwnerLabel;    // "Walls : Basic Wall : Exterior - Brick : 397"
        public string Where;         // "layer 2", "Structural Material", "painted face", "object style"
        public bool Locked;          // seen, but this tool cannot change it (read-only, formula, integral sweep)
    }

    /// <summary>
    /// Every material (or asset) in the document and every place it is used.
    /// The same walk finds the uses and, in Apply, changes them, so what the window previews is what changes.
    /// Materials only: element and type parameters (instance, type, built-in and shared), layers of walls, floors,
    /// roofs, ceilings and pads, painted faces, object styles, and in the Family Editor the family types' material parameters.
    /// </summary>
    public class MaterialIndex
    {
        public readonly Document Doc;
        public readonly ReplaceKind Kind;
        public readonly Dictionary<ElementId, string> Names = new Dictionary<ElementId, string>();
        public readonly Dictionary<ElementId, List<MaterialUse>> Uses = new Dictionary<ElementId, List<MaterialUse>>();

        public bool CanRename => Kind == ReplaceKind.Materials;
        public bool CanRemove => Kind == ReplaceKind.Materials;   // <By Category>

        MaterialIndex(Document doc, ReplaceKind kind) { Doc = doc; Kind = kind; }

        public static MaterialIndex Build(Document doc, ReplaceKind kind)
        {
            var ix = new MaterialIndex(doc, kind);
            foreach (var (id, name) in ix.Items()) { ix.Names[id] = name; ix.Uses[id] = new List<MaterialUse>(); }
            ix.Walk((use, set) => { if (ix.Uses.TryGetValue(use.ItemId, out var l)) l.Add(use); });
            return ix;
        }

        public int UseCount(string name) => Names.Where(kv => kv.Value == name).Sum(kv => Uses[kv.Key].Count);
        public ElementId IdOf(string name) => Names.FirstOrDefault(kv => kv.Value == name).Key;

        IEnumerable<(ElementId, string)> Items()
        {
            switch (Kind)
            {
                case ReplaceKind.Materials:
                    return new FilteredElementCollector(Doc).OfClass(typeof(Material)).Select(e => (e.Id, e.Name));
                case ReplaceKind.AppearanceAssets:
                    return new FilteredElementCollector(Doc).OfClass(typeof(AppearanceAssetElement)).Select(e => (e.Id, e.Name));
                default:
                    // Physical (structural) and thermal assets are both property sets; tell them apart by what they hold.
                    bool thermal = Kind == ReplaceKind.ThermalAssets;
                    return new FilteredElementCollector(Doc).OfClass(typeof(PropertySetElement)).Cast<PropertySetElement>()
                        .Where(p => thermal ? Try(() => p.GetThermalAsset() != null) : Try(() => p.GetStructuralAsset() != null))
                        .Select(e => (e.Id, e.Name));
            }
        }

        /// <summary>
        /// Changes every use found in map (old id to new id; InvalidElementId means By Category), except those
        /// whose owner is in skipOwners. Must run inside a transaction. Returns the number of uses changed.
        /// </summary>
        public int Apply(IDictionary<ElementId, ElementId> map, ISet<ElementId> skipOwners, List<string> failures)
        {
            int changed = 0;
            Walk((use, set) =>
            {
                if (use.Locked || skipOwners.Contains(use.Owner) || !map.TryGetValue(use.ItemId, out var to)) return;
                try { set(to); changed++; }
                catch (Exception ex) { failures.Add($"{use.OwnerLabel} ({use.Where}): {ex.Message}"); }
            });
            return changed;
        }

        /// <summary>Calls found(use, setter) for each use. The setter changes that use when called (inside a transaction).</summary>
        void Walk(Action<MaterialUse, Action<ElementId>> found)
        {
            if (Kind != ReplaceKind.Materials) { WalkAssets(found); return; }
            var isMaterial = new HashSet<ElementId>(Names.Keys);
            // Model elements and types only: Revit's internal elements (no category) can hold parameters whose
            // definition is not valid, and reading those crashed Revit 2022 with an access violation.
            bool Model(Element e) => Try(() => e.Category != null && e.Category.CategoryType == CategoryType.Model);
            var instances = new FilteredElementCollector(Doc).WhereElementIsNotElementType().Where(Model).ToList();
            var types = new FilteredElementCollector(Doc).WhereElementIsElementType().Where(Model).ToList();

            // 1. Painted faces first: they read the geometry, which the layer changes below would regenerate.
            foreach (var e in instances.Where(e => e is HostObject || e is Part || e is GenericForm))
            {
                GeometryElement g;
                try { g = e.get_Geometry(new Options()); } catch { continue; }
                if (g == null) continue;
                int n = 0;
                foreach (var solid in g.OfType<Solid>())
                    foreach (Face f in solid.Faces)
                    {
                        n++;
                        if (!Try(() => Doc.IsPainted(e.Id, f))) continue;
                        var face = f;
                        found(new MaterialUse { ItemId = Doc.GetPaintedMaterial(e.Id, f), Owner = e.Id, OwnerLabel = Label(e), Where = $"painted face {n}" },
                              to => { if (to == ElementId.InvalidElementId) Doc.RemovePaint(e.Id, face); else Doc.Paint(e.Id, face, to); });
                    }
            }

            // 2. Material parameters of every element and type: built-in (Structural Material, Material), shared and family ones.
            foreach (var e in instances.Concat(types))
            {
                if (e is Material) continue;
                foreach (Parameter p in e.Parameters)
                {
                    if (p?.Definition == null || p.StorageType != StorageType.ElementId) continue;
                    var id = p.AsElementId();
                    if (!isMaterial.Contains(id)) continue;
                    var prm = p;
                    found(new MaterialUse { ItemId = id, Owner = e.Id, OwnerLabel = Label(e), Where = p.Definition.Name, Locked = p.IsReadOnly },
                          to => { if (!prm.Set(to)) throw new InvalidOperationException("Revit refused the value"); });
                }
            }

            // 3. Layers of walls, floors, roofs, ceilings and pads (one SetCompoundStructure per type).
            foreach (var ha in types.OfType<HostObjAttributes>())
            {
                CompoundStructure cs;
                try { cs = ha.GetCompoundStructure(); } catch { continue; }
                if (cs == null) continue;
                bool dirty = false;
                var layers = cs.GetLayers();
                for (int i = 0; i < layers.Count; i++)
                {
                    if (!isMaterial.Contains(layers[i].MaterialId)) continue;
                    int li = i;
                    found(new MaterialUse { ItemId = layers[i].MaterialId, Owner = ha.Id, OwnerLabel = Label(ha), Where = $"layer {i + 1} ({layers[i].Function})" },
                          to => { cs.SetMaterialId(li, to); dirty = true; });
                }
                foreach (var sweep in Try(() => cs.GetWallSweepsInfo(WallSweepType.Sweep)) ?? new List<WallSweepInfo>())
                    if (isMaterial.Contains(sweep.MaterialId))
                        found(new MaterialUse { ItemId = sweep.MaterialId, Owner = ha.Id, OwnerLabel = Label(ha), Where = "integral sweep (change it in Edit Structure)", Locked = true }, _ => { });
                if (dirty) ha.SetCompoundStructure(cs);
            }

            // 4. Object styles: the material a category or subcategory gives elements set to By Category.
            foreach (Category c in Doc.Settings.Categories)
                foreach (var sc in new[] { c }.Concat(Try(() => c.SubCategories.Cast<Category>().ToList()) ?? new List<Category>()))
                {
                    var m = Try(() => sc.Material);
                    if (m == null || !isMaterial.Contains(m.Id)) continue;
                    var cat = sc;
                    found(new MaterialUse { ItemId = m.Id, Owner = sc.Id, OwnerLabel = "Object style : " + (sc.Parent != null ? sc.Parent.Name + " : " : "") + sc.Name, Where = "object style" },
                          to => cat.Material = to == ElementId.InvalidElementId ? null : Doc.GetElement(to) as Material);
                }

            // 5. Family Editor: material parameters of each family type.
            if (Doc.IsFamilyDocument)
            {
                var fm = Doc.FamilyManager;
                foreach (FamilyParameter fp in fm.Parameters)
                {
                    if (fp.StorageType != StorageType.ElementId) continue;
                    foreach (FamilyType t in fm.Types)
                    {
                        var id = t.AsElementId(fp);
                        if (id == null || !isMaterial.Contains(id)) continue;
                        var (param, type) = (fp, t);
                        found(new MaterialUse { ItemId = id, Owner = fp.Id, OwnerLabel = $"Family parameter : {fp.Definition.Name}", Where = $"type {t.Name}", Locked = fp.IsDeterminedByFormula },
                              to => { fm.CurrentType = type; fm.Set(param, to); });
                    }
                }
            }
        }

        void WalkAssets(Action<MaterialUse, Action<ElementId>> found)
        {
            foreach (var m in new FilteredElementCollector(Doc).OfClass(typeof(Material)).Cast<Material>())
            {
                var mat = m;
                var use = new MaterialUse { Owner = m.Id, OwnerLabel = "Material : " + m.Name, Where = Kind == ReplaceKind.AppearanceAssets ? "appearance" : Kind == ReplaceKind.PhysicalAssets ? "physical" : "thermal" };
                switch (Kind)
                {
                    case ReplaceKind.AppearanceAssets: use.ItemId = m.AppearanceAssetId; found(use, to => mat.AppearanceAssetId = to); break;
                    case ReplaceKind.PhysicalAssets: use.ItemId = m.StructuralAssetId; found(use, to => mat.StructuralAssetId = to); break;
                    case ReplaceKind.ThermalAssets: use.ItemId = m.ThermalAssetId; found(use, to => mat.ThermalAssetId = to); break;
                }
            }
        }

        /// <summary>"Category : Family : Type : id", the way the Polynine tool and Revit's own lists show elements.</summary>
        public static string Label(Element e)
        {
            var cat = e.Category?.Name ?? e.GetType().Name;
            var typeName = e is ElementType et ? (et.FamilyName + " : " + et.Name) : (e.Document.GetElement(e.GetTypeId()) is ElementType t ? t.FamilyName + " : " + t.Name : e.Name);
            return $"{cat} : {typeName} : {e.Id}";
        }

        static T Try<T>(Func<T> f) { try { return f(); } catch { return default; } }
    }
}
