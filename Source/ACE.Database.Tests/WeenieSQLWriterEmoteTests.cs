using System.Collections.Generic;
using System.IO;
using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.Database.Models.World;
using ACE.Database.SQLFormatters.World;
using ACE.Entity.Enum;

namespace ACE.Database.Tests
{
    /// <summary>
    /// Verifies that WeenieSQLWriter.CreateSQLINSERTStatement emits the correct
    /// damage_type value for null, named-enum, and bitmask-composite inputs.
    /// </summary>
    [TestClass]
    public class WeenieSQLWriterEmoteTests
    {
        private static string GenerateSql(int? damageType)
        {
            var writer = new WeenieSQLWriter();  // WeenieNames intentionally left null

            var emoteRow = new WeeniePropertiesEmote
            {
                Category    = (uint)EmoteCategory.ReceiveDamage,
                Probability = 1.0f,
                DamageType  = damageType
            };

            using var ms = new MemoryStream();
            using var sw = new StreamWriter(ms, Encoding.UTF8, leaveOpen: true);
            writer.CreateSQLINSERTStatement(1u, new List<WeeniePropertiesEmote> { emoteRow }, sw);
            sw.Flush();
            return Encoding.UTF8.GetString(ms.ToArray());
        }

        [TestMethod]
        public void NullDamageType_OutputContainsNULL()
        {
            var sql = GenerateSql(null);

            // FixNullFields should have replaced the trailing empty field with NULL.
            StringAssert.Contains(sql, "NULL)");

            // The damage_type column header must be present.
            StringAssert.Contains(sql, "`damage_type`");
        }

        [TestMethod]
        public void SlashDamageType_OutputContainsValueAndComment()
        {
            var sql = GenerateSql((int)DamageType.Slash);

            // Slash = 0x1 = 1; Enum.GetName returns "Slash" for this named value.
            StringAssert.Contains(sql, "1 /* Slash */");
        }

        [TestMethod]
        public void FireDamageType_OutputContainsValueAndComment()
        {
            var sql = GenerateSql((int)DamageType.Fire);

            // Fire = 0x10 = 16
            StringAssert.Contains(sql, "16 /* Fire */");
        }

        [TestMethod]
        public void BitmaskDamageType_OutputContainsValueWithoutComment()
        {
            // Slash | Pierce = 0x1 | 0x2 = 3; not a named single value,
            // so Enum.GetName returns null and no comment is emitted.
            int bitmask = (int)(DamageType.Slash | DamageType.Pierce);
            var sql = GenerateSql(bitmask);

            StringAssert.Contains(sql, "3)");

            // A spurious comment like "/* 3 */" or "/*  */" must not appear.
            Assert.IsFalse(sql.Contains("/* 3 */"),
                "Bitmask value should not emit an empty or spurious comment.");
        }

        [TestMethod]
        public void PhysicalHelperBitmask_OutputContainsValueAndComment()
        {
            // DamageType.Physical = Slash | Pierce | Bludgeon = 7; explicitly named in the enum.
            var sql = GenerateSql((int)DamageType.Physical);

            StringAssert.Contains(sql, "7 /* Physical */");
        }

        [TestMethod]
        public void DamageTypeColumn_AlwaysPresentInInsertHeader()
        {
            // Regression: the column header must appear regardless of damage_type value.
            Assert.IsTrue(GenerateSql(null).Contains("`damage_type`"),
                "INSERT header missing `damage_type` for null value.");
            Assert.IsTrue(GenerateSql((int)DamageType.Slash).Contains("`damage_type`"),
                "INSERT header missing `damage_type` for Slash.");
        }
    }
}
