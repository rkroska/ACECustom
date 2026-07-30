using System;
using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity.Enum.Properties;
using ACE.Database;

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
                
                uint setupId = 0x02001BCE;
                var setup = portalDb.ReadFromDat<SetupModel>(setupId);
                if (setup != null)
                {
                    Console.WriteLine("\n--- Setup 0x" + setupId.ToString("X8") + " ---");
                    foreach (var part in setup.Parts)
                    {
                        var gfxObj = portalDb.ReadFromDat<GfxObj>(part);
                        if (gfxObj != null)
                        {
                            Console.WriteLine($"GfxObj 0x{part:X8}: {gfxObj.Surfaces.Count} surfaces");
                            foreach (var surfId in gfxObj.Surfaces)
                            {
                                var surf = portalDb.ReadFromDat<Surface>(surfId);
                                if (surf != null)
                                {
                                    uint tex = surf.OrigTextureId;
                                    Console.WriteLine($"  Surface 0x{surfId:X8} -> OrigTex 0x{tex:X8} Type: {surf.Type}");
                                    
                                    if ((tex & 0xFF000000) == 0x05000000)
                                    {
                                        var surfTex = portalDb.ReadFromDat<SurfaceTexture>(tex);
                                        if (surfTex != null && surfTex.Textures.Count > 0)
                                        {
                                            Console.WriteLine($"    SurfaceTexture 0x{tex:X8} resolves to Texture 0x{surfTex.Textures[0]:X8}");
                                            tex = surfTex.Textures[0];
                                        }
                                        else if (highResDb != null)
                                        {
                                            surfTex = highResDb.ReadFromDat<SurfaceTexture>(tex);
                                            if (surfTex != null && surfTex.Textures.Count > 0)
                                            {
                                                Console.WriteLine($"    [HighRes] SurfaceTexture 0x{tex:X8} resolves to Texture 0x{surfTex.Textures[0]:X8}");
                                                tex = surfTex.Textures[0];
                                            }
                                        }
                                    }

                                    var texture = portalDb.ReadFromDat<Texture>(tex);
                                    if (texture == null && highResDb != null)
                                        texture = highResDb.ReadFromDat<Texture>(tex);
                                        
                                    if (texture != null)
                                    {
                                        Console.WriteLine($"    Texture 0x{tex:X8} Format: {texture.Format}, Width: {texture.Width}, Height: {texture.Height}");
                                    }
                                    else if (tex != 0)
                                    {
                                        Console.WriteLine($"    Texture 0x{tex:X8} NOT FOUND.");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.ToString());
            }
        }
    }
}
