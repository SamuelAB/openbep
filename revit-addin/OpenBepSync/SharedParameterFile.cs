using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace OpenBepSync
{
    /// <summary>One PARAM line of a Revit shared parameter file.</summary>
    public class SharedParameterEntry
    {
        public Guid Guid;
        public string Name = "";
        public string DataType = "";
        public int Group;
        public string Description = "";
    }

    /// <summary>Reads a Revit shared parameter file without Revit, to check it against the binding list before Revit opens it.</summary>
    public static class SharedParameterFile
    {
        public static List<SharedParameterEntry> Parse(string text, out Dictionary<int, string> groups, out List<string> warnings)
        {
            var gs = new Dictionary<int, string>();
            groups = gs;
            warnings = new List<string>();
            var list = new List<SharedParameterEntry>();
            var lines = text.Split('\n');
            for (int n = 0; n < lines.Length; n++)
            {
                var line = lines[n].TrimEnd('\r').TrimStart('﻿');
                if (line.Length == 0 || line[0] == '#' || line[0] == '*') continue;
                var f = line.Split('\t');
                if (f[0] == "GROUP" && f.Length >= 3 && int.TryParse(f[1], out var gid)) gs[gid] = f[2];
                else if (f[0] == "PARAM")
                {
                    // PARAM GUID NAME DATATYPE DATACATEGORY GROUP VISIBLE DESCRIPTION USERMODIFIABLE
                    if (f.Length < 6 || !Guid.TryParse(f[1], out var g)) { warnings.Add($"Line {n + 1}: unreadable PARAM line"); continue; }
                    int.TryParse(f[5], out var grp);
                    list.Add(new SharedParameterEntry { Guid = g, Name = f[2], DataType = f[3], Group = grp, Description = f.Length > 7 ? f[7] : "" });
                }
            }
            foreach (var e in list.Where(e => !gs.ContainsKey(e.Group))) warnings.Add($"{e.Name}: group {e.Group} is not defined");
            return list;
        }
    }

    /// <summary>Naming and GUID rules shared by the Builder, gen_sp.py and the add-in.</summary>
    public static class OpenBepNames
    {
        /// <summary>OpenBEP parameters start with OB_; CA_ is the prefix used before October 2026.</summary>
        public static bool IsOpenBep(string name) =>
            name != null && (name.StartsWith("OB_", StringComparison.Ordinal) || name.StartsWith("CA_", StringComparison.Ordinal));

        /// <summary>
        /// The fixed GUID of an OpenBEP parameter: UUID v5 (URL namespace) of urn:canbim:parameter:CA_name.
        /// OB_X keeps the GUID of CA_X, so models made before the rename keep their data. Same rule as gen_sp.py.
        /// </summary>
        public static Guid Expected(string name)
        {
            var key = name.StartsWith("OB_", StringComparison.Ordinal) ? "CA_" + name.Substring(3) : name;
            return Uuid5(new Guid("6ba7b811-9dad-11d1-80b4-00c04fd430c8"), "urn:canbim:parameter:" + key);
        }

        /// <summary>The GUID the Builder gives an owner-added parameter, hashed from its name (cusGuid in bep-builder.html).</summary>
        public static Guid Owner(string name)
        {
            var k = "openbep-owner|" + name;
            var sb = new StringBuilder();
            foreach (uint seed in new uint[] { 0x811c9dc5, 0x01000193, 0x9e3779b9, 0x85ebca6b })
            {
                uint x = seed;
                for (int i = 0; i < k.Length; i++)
                {
                    x ^= (uint)char.ConvertToUtf32(k, i);
                    if (char.IsHighSurrogate(k[i])) i++;
                    x = unchecked(x * 16777619u);
                }
                sb.Append(x.ToString("x8"));
            }
            var h = sb.ToString();
            int v = 8 | (Convert.ToInt32(h.Substring(16, 1), 16) & 3);
            return new Guid($"{h.Substring(0, 8)}-{h.Substring(8, 4)}-5{h.Substring(13, 3)}-{v:x}{h.Substring(17, 3)}-{h.Substring(20, 12)}");
        }

        /// <summary>RFC 4122 name-based UUID, SHA-1 (version 5).</summary>
        public static Guid Uuid5(Guid ns, string name)
        {
            var nsb = ns.ToByteArray();
            Swap(nsb);
            var nb = Encoding.UTF8.GetBytes(name);
            byte[] hash;
            using (var sha = SHA1.Create()) hash = sha.ComputeHash(nsb.Concat(nb).ToArray());
            var b = new byte[16];
            Array.Copy(hash, b, 16);
            b[6] = (byte)((b[6] & 0x0F) | 0x50);
            b[8] = (byte)((b[8] & 0x3F) | 0x80);
            Swap(b);
            return new Guid(b);
        }

        // System.Guid stores the first three fields little-endian; RFC 4122 is big-endian.
        static void Swap(byte[] b)
        {
            Array.Reverse(b, 0, 4);
            Array.Reverse(b, 4, 2);
            Array.Reverse(b, 6, 2);
        }
    }
}
