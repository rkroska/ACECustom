using System;
using System.Collections.Generic;
using ACE.Database;
using ACE.DatLoader;
using ACE.DatLoader.FileTypes;

namespace CheckWeenie
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var portalDb = new PortalDatDatabase("c:\\ACE\\Dats\\client_portal.dat", keepOpen: false);
                PortalDatDatabase highResDb = null;
                try { highResDb = new PortalDatDatabase("c:\\ACE\\Dats\\client_highres.dat", keepOpen: false); } catch { }

                Console.WriteLine("--- DAT AUDIT: GUROGS ---");
                
                // Search for Gurog setup IDs in DATs or common setup IDs
                // Let's scan SetupModels for Gurogs or check known setup IDs
                // Typical Gurog setups are around 0x02000300 - 0x02000400 or higher (e.g. 0x020008xx, 0x02001xxx)
                
                List<uint> gurogSetups = new List<uint>();
                
                // Let's search all setup models for Gurog textures/references or search weenies in DB if available
                for (uint id = 0x02000000; id <= 0x02002500; id++)
                {
                    var setup = portalDb.ReadFromDat<SetupModel>(id);
                    if (setup != null)
                    {
                        // Check first GfxObj
                        if (setup.Parts.Count > 0)
                        {
                            var gfx = portalDb.ReadFromDat<GfxObj>(setup.Parts[0]);
                            if (gfx != null && gfx.Surfaces.Count > 0)
                            {
                                var surf = portalDb.ReadFromDat<Surface>(gfx.Surfaces[0]);
                                if (surf != null && surf.OrigTextureId != 0)
                                {
                                    // Check texture ID or surface texture
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
