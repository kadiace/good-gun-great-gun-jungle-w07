using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace WeaponWorkshop
{
    public static class WeaponShareCode
    {
        // 0123456789ABCDEF0123456789ABCDEF; shared by name encryption and decryption.
        private static readonly byte[] NameIV = { 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF,
            0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF };
        [Serializable] public sealed class Blueprint
        {
            public int version = 2;
            public string name;
            public Block[] blocks;
        }
        [Serializable] public sealed class Block
        {
            public string module;
            public Vector3Int cell, output, normal;
            public bool hasColor;
            public Color color;
        }
        // A shared application key makes codes portable; this is not a private vault or anti-cheat signature.
        private static byte[] Key(string purpose)
        {
            using (var hash = SHA256.Create()) return hash.ComputeHash(Encoding.UTF8.GetBytes("WGG.weapon-code.v1." + purpose));
        }
        public static string Export(EquipableWeapon weapon)
            => CompactExport(new Blueprint { name = weapon.WeaponName, blocks = CollectBlocks(weapon) });

        private static Block[] CollectBlocks(EquipableWeapon weapon)
        {
            if (weapon == null) throw new ArgumentException("무기가 없습니다.");
            var assembly = weapon.GetComponentInChildren<CubeAssembly>();
            if (assembly == null || !assembly.TryCaptureLayout(out var layout)) throw new ArgumentException("무기 구성을 읽을 수 없습니다.");
            var blocks = layout.blocks.Select(b => new Block
            {
                module = assembly.GetModuleData(b.slot)?.CodeId, cell = b.cell,
                output = b.outputDirection, normal = b.orientationNormal, hasColor = b.hasColor, color = b.color
            }).ToArray();
            if (blocks.Length > 1000 || blocks.Any(b => string.IsNullOrEmpty(b.module))) throw new ArgumentException("공유할 수 없는 모듈 또는 너무 큰 무기입니다.");
            return blocks;
        }
        // Same layout capture as sharing; names, picked colors, prices and encoding are not performance identity.
        public static string GetAssemblyKey(EquipableWeapon weapon)
        {
            var blueprint = Validate(new Blueprint { name = "나만의 총", blocks = CollectBlocks(weapon) });
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(1);
                foreach (var block in blueprint.blocks.OrderBy(b => b.cell.x).ThenBy(b => b.cell.y).ThenBy(b => b.cell.z))
                {
                    writer.Write(block.module);
                    foreach (var vector in new[] { block.cell, block.output, block.normal })
                    { writer.Write(vector.x); writer.Write(vector.y); writer.Write(vector.z); }
                }
                writer.Flush();
                using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(stream.ToArray()));
            }
        }
        private static readonly Vector3Int[] Directions = { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, Vector3Int.forward, Vector3Int.back };
        private static void Number(BinaryWriter w, uint n)
        {
            while (n >= 128) { w.Write((byte)(n | 128)); n >>= 7; }
            w.Write((byte)n);
        }
        private static uint Number(BinaryReader r)
        {
            uint n = 0;
            for (int shift = 0; shift < 35; shift += 7)
            {
                byte b = r.ReadByte();
                if (shift == 28 && (b & 240) != 0) throw new ArgumentException("잘못된 숫자입니다.");
                n |= (uint)(b & 127) << shift;
                if ((b & 128) == 0) return n;
            }
            throw new ArgumentException("잘못된 숫자입니다.");
        }
        private static void Coordinate(BinaryWriter w, int n) => Number(w, unchecked((uint)((n << 1) ^ (n >> 31))));
        private static int Coordinate(BinaryReader r) { uint n = Number(r); return unchecked((int)(n >> 1) ^ -(int)(n & 1)); }
        private static byte[] Exact(BinaryReader r, int count)
        {
            var bytes = r.ReadBytes(count);
            if (bytes.Length != count) throw new ArgumentException("무기 코드가 잘렸습니다.");
            return bytes;
        }
        private static string CompactExport(Blueprint blueprint)
        {
            Validate(blueprint);
            byte[] body;
            using (var stream = new MemoryStream())
            using (var w = new BinaryWriter(stream))
            {
                // Omit the known default name; encrypt custom names separately.
                if (blueprint.name == "나만의 총") Number(w, 0);
                else using (var aes = Aes.Create())
                {
                    aes.Key = Key("name");
                    // Shared application IV avoids carrying 16 extra bytes in every code.
                    aes.IV = NameIV;
                    using (var cipher = aes.CreateEncryptor())
                    {
                        var plain = Encoding.UTF8.GetBytes(blueprint.name);
                        var encrypted = cipher.TransformFinalBlock(plain, 0, plain.Length);
                        Number(w, (uint)encrypted.Length); w.Write(encrypted);
                    }
                }
                var modules = blueprint.blocks.Select(b => b.module).Distinct().OrderBy(id => id, StringComparer.Ordinal).ToArray();
                Number(w, (uint)modules.Length);
                foreach (var id in modules) w.Write(Guid.ParseExact(id, "N").ToByteArray());
                Number(w, (uint)blueprint.blocks.Length);
                foreach (var b in blueprint.blocks.OrderBy(b => b.cell.x).ThenBy(b => b.cell.y).ThenBy(b => b.cell.z))
                {
                    Number(w, (uint)Array.IndexOf(modules, b.module));
                    Coordinate(w, b.cell.x); Coordinate(w, b.cell.y); Coordinate(w, b.cell.z);
                    w.Write((byte)(Array.IndexOf(Directions, b.output) | (Array.IndexOf(Directions, b.normal) << 3) | (b.hasColor ? 64 : 0)));
                    if (b.hasColor) { w.Write(b.color.r); w.Write(b.color.g); w.Write(b.color.b); w.Write(b.color.a); }
                }
                body = stream.ToArray();
            }
            byte[] compressed;
            using (var buffer = new MemoryStream())
            {
                using (var compressor = new DeflateStream(buffer, System.IO.Compression.CompressionLevel.Optimal, true))
                    compressor.Write(body, 0, body.Length);
                compressed = buffer.ToArray();
            }
            bool smaller = compressed.Length < body.Length;
            var payload = new[] { (byte)(smaller ? 130 : 2) }.Concat(smaller ? compressed : body).ToArray();
            using (var mac = new HMACSHA256(Key("compact-integrity")))
                return "WSC:" + Convert.ToBase64String(payload.Concat(mac.ComputeHash(payload).Take(16)).ToArray()).TrimEnd('=');
        }
        public static Blueprint Decode(string code)
        {
            code = (code ?? "").Trim();
            if (!code.StartsWith("WSC:", StringComparison.Ordinal)) throw new ArgumentException("지원하지 않는 무기 코드입니다.");
            if (code.Length > 1000000) throw new ArgumentException("무기 데이터가 너무 큽니다.");
            var encoded = code.Substring(4);
            var bytes = Convert.FromBase64String(encoded.PadRight((encoded.Length + 3) / 4 * 4, '='));
            if (bytes.Length < 20) throw new ArgumentException("무기 코드가 손상되었습니다.");
            var payload = bytes.Take(bytes.Length - 16).ToArray();
            using (var mac = new HMACSHA256(Key("compact-integrity")))
            {
                var expected = mac.ComputeHash(payload); int difference = 0;
                for (int i = 0; i < 16; i++) difference |= expected[i] ^ bytes[payload.Length + i];
                if (difference != 0) throw new ArgumentException("무기 코드가 손상되었습니다.");
            }
            if ((payload[0] & 127) != 2) throw new ArgumentException("지원하지 않는 무기 코드입니다.");
            var body = payload.Skip(1).ToArray();
            if ((payload[0] & 128) != 0)
            {
                using (var input = new MemoryStream(body))
                using (var inflater = new DeflateStream(input, CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    var buffer = new byte[4096]; int count;
                    while ((count = inflater.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (output.Length + count > 1000000) throw new ArgumentException("무기 데이터가 너무 큽니다.");
                        output.Write(buffer, 0, count);
                    }
                    body = output.ToArray();
                }
            }
            using (var stream = new MemoryStream(body))
            using (var r = new BinaryReader(stream))
            {
                var result = new Blueprint { name = "나만의 총" };
                uint length = Number(r);
                if (length != 0)
                {
                    if (length > 256 || length % 16 != 0) throw new ArgumentException("잘못된 무기 이름입니다.");
                    using (var aes = Aes.Create())
                    {
                        aes.Key = Key("name"); aes.IV = NameIV;
                        using (var cipher = aes.CreateDecryptor())
                        {
                            var encrypted = Exact(r, (int)length);
                            result.name = new UTF8Encoding(false, true).GetString(cipher.TransformFinalBlock(encrypted, 0, encrypted.Length));
                        }
                    }
                }
                uint moduleCount = Number(r);
                if (moduleCount > 1000) throw new ArgumentException("모듈 종류가 너무 많습니다.");
                var modules = new string[(int)moduleCount];
                for (int i = 0; i < modules.Length; i++) modules[i] = new Guid(Exact(r, 16)).ToString("N");
                uint blockCount = Number(r);
                if (blockCount > 1000) throw new ArgumentException("모듈이 너무 많습니다.");
                result.blocks = new Block[(int)blockCount];
                for (int i = 0; i < result.blocks.Length; i++)
                {
                    uint id = Number(r);
                    if (id >= moduleCount) throw new ArgumentException("잘못된 모듈 번호입니다.");
                    var b = new Block { module = modules[(int)id], cell = new Vector3Int(Coordinate(r), Coordinate(r), Coordinate(r)) };
                    byte flags = r.ReadByte(); int direction = flags & 7, normal = (flags >> 3) & 7;
                    if ((flags & 128) != 0 || direction >= 6 || normal >= 6) throw new ArgumentException("잘못된 모듈 방향입니다.");
                    b.output = Directions[direction]; b.normal = Directions[normal]; b.hasColor = (flags & 64) != 0;
                    if (b.hasColor) b.color = new Color(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    result.blocks[i] = b;
                }
                if (stream.Position != stream.Length) throw new ArgumentException("잘못된 무기 데이터입니다.");
                return Validate(result);
            }
        }
        private static Blueprint Validate(Blueprint result)
        {
            if (result == null || result.version != 2 || result.blocks == null || result.blocks.Length > 1000 ||
                result.name == null || result.name.Length > 40) throw new ArgumentException("지원하지 않는 무기 데이터입니다.");
            var cells = new HashSet<Vector3Int> { Vector3Int.zero };
            foreach (var b in result.blocks)
            {
                if (b == null || string.IsNullOrEmpty(b.module) || !cells.Add(b.cell) ||
                    Math.Abs((long)b.cell.x) > 1000 || Math.Abs((long)b.cell.y) > 1000 || Math.Abs((long)b.cell.z) > 1000 ||
                    !Axis(b.output) || !Axis(b.normal) || !Finite(b.color)) throw new ArgumentException("잘못된 모듈 배치입니다.");
            }
            var reached = new HashSet<Vector3Int> { Vector3Int.zero }; var pending = new Queue<Vector3Int>(); pending.Enqueue(Vector3Int.zero);
            var axes = new[] { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, Vector3Int.forward, Vector3Int.back };
            while (pending.Count > 0) { var p = pending.Dequeue(); foreach (var axis in axes) if (cells.Contains(p + axis) && reached.Add(p + axis)) pending.Enqueue(p + axis); }
            if (reached.Count != cells.Count) throw new ArgumentException("코어와 연결되지 않은 모듈이 있습니다.");
            return result;
        }
        private static bool Axis(Vector3Int v) => Math.Abs((long)v.x) + Math.Abs((long)v.y) + Math.Abs((long)v.z) == 1;
        private static bool Finite(Color c) => !float.IsNaN(c.r + c.g + c.b + c.a) && !float.IsInfinity(c.r + c.g + c.b + c.a);

        internal static CubeAssembly.LayoutSnapshot Resolve(Blueprint blueprint, CubeAssembly assembly, PlayerModuleUnlocks unlocks,
            int coreCost, out int dollars, out int materials)
        {
            if (unlocks == null) throw new ArgumentException("모듈 해금 정보를 확인할 수 없습니다.");
            long money = 0, credits = 0; var blocks = new List<CubeAssembly.BlockSnapshot>();
            foreach (var b in blueprint.blocks)
            {
                int slot = 0; ModuleData module = null;
                // Resolve stable IDs to this installation's current catalog slots.
                for (int i = 1; i <= assembly.ModuleCount; i++)
                {
                    var candidate = assembly.GetModuleData(i);
                    if (candidate != null && candidate.CodeId == b.module) { slot = i; module = candidate; break; }
                }
                if (module == null || assembly.GetPrefab(slot) == null) throw new ArgumentException("현재 게임에 없는 모듈입니다: " + b.module);
                if (!unlocks.IsUnlocked(slot)) throw new ArgumentException("해금되지 않은 모듈이 포함된 코드입니다.");
                money += module.BuyPrice; credits += module.ModuleCreditCost;
                blocks.Add(new CubeAssembly.BlockSnapshot { slot = slot, prefab = assembly.GetPrefab(slot), cell = b.cell,
                    outputDirection = b.output, orientationNormal = b.normal, paidPurchaseCost = module.BuyPrice,
                    paidModuleCreditCost = module.ModuleCreditCost, hasColor = b.hasColor, color = b.color });
            }
            if (money + coreCost > int.MaxValue || credits > int.MaxValue) throw new ArgumentException("무기 제작비가 너무 큽니다.");
            dollars = (int)money; materials = (int)credits;
            return new CubeAssembly.LayoutSnapshot(blocks.ToArray(), coreCost + dollars);
        }
    }
}
