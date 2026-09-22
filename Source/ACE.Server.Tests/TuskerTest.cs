using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using MySql.Data.MySqlClient;
using System.Collections.Generic;

namespace ACE.Server.Tests
{
    [TestClass]
    public class TuskerTest
    {
        [TestMethod]
        [Ignore("Ad-hoc local investigation script requiring local DATs and live DB")]
        public void InvestigateTuskerProtector()
        {
            var datPath = System.Environment.GetEnvironmentVariable("ACE_DAT_DIR") ?? @"C:\ACE\Dats\";
            if (!Directory.Exists(datPath)) return;
            DatManager.Initialize(datPath, false, false, false);
            
            var connectionString = System.Environment.GetEnvironmentVariable("ACE_DB_CONN") ?? "Server=127.0.0.1;Port=3306;Database=ace_world;Uid=root;Pwd=;";
            uint clothingBaseId = 0;
            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                using (var cmd = new MySqlCommand("SELECT type, value FROM weenie_properties_d_i_d WHERE object_Id = 36967;", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        uint val = reader.GetUInt32(1);
                        int prop = reader.GetInt32(0);
                        Console.WriteLine($"DID Property Type: {prop}, Value: 0x{val:X8}");
                        if (prop == 16) clothingBaseId = val; // ClothingBase is property 16 (0x10)
                    }
                }
                
                using (var cmd = new MySqlCommand("SELECT type, value FROM weenie_properties_int WHERE object_Id = 36967;", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Console.WriteLine($"Int Property Type: {reader.GetInt32(0)}, Value: {reader.GetInt32(1)}");
                    }
                }
                
                using (var cmd = new MySqlCommand("SELECT type, value FROM weenie_properties_float WHERE object_Id = 36967;", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        Console.WriteLine($"Float Property Type: {reader.GetInt32(0)}, Value: {reader.GetFloat(1)}");
                    }
                }
            }

            var setup = DatManager.PortalDat.ReadFromDat<SetupModel>(0x02000E79);
            Console.WriteLine($"Setup: 0x02000E79");
            // SetupModel doesn't have PaletteBase natively exposed in some ACE versions
            
            if (clothingBaseId == 0) clothingBaseId = 0x10000227;
            var ctable = DatManager.PortalDat.ReadFromDat<ClothingTable>(clothingBaseId); 
            if (ctable != null) {
                Console.WriteLine($"ClothingBase: 0x{clothingBaseId:X8}");
                foreach(var effect in ctable.ClothingSubPalEffects) {
                    Console.WriteLine($"Template: {effect.Key}, Icon: {effect.Value.Icon}");
                }
            }
            Assert.Fail("Investigation complete. Check standard output for details.");
        }
    }
}
