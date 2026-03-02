using System.Collections.Generic;

namespace ACE.Server.Entity
{
    public static class LandblockCollections
    {
        public static readonly HashSet<ushort> ThaelarynIslandLandblocks = new()
        {
            // North Tip
            0xF66C, 0xF76C, 0xF86C, 
            
            // Upper Body
            0xF66B, 0xF76B, 0xF86B,
            0xF76A, 0xF86A, 0xF96A,
            0xF669, 0xF769, 0xF869, 0xF969,
            0xF668, 0xF768, 0xF868, 0xF968,
            // Middle / Widening
            0xF467, 0xF567, 0xF667, 0xF767, 0xF867, 0xF967,
            0xF666, 0xF766, 0xF866, 0xF966,
            // Lower Body
            0xF565, 0xF665, 0xF765, 0xF865, 0xF965,
            0xF564, 0xF664, 0xF764, 0xF864, 0xF964,
            0xF563, 0xF663, 0xF763, 0xF863, 0xF963,
            0xF462, 0xF562, 0xF662, 0xF762, 0xF862, 0xF962,
            
            // South Tip
            0xF361, 0xF461, 0xF561, 0xF661, 0xF761
        };

        public static readonly HashSet<ushort> ValleyOfDeathLandblocks = new()
        {
            // Row 0x4A
            0x284A, 0x294A, 0x2A4A, 0x2B4A, 0x2C4A, 0x2D4A, 0x2E4A, 0x2F4A, 0x304A, 0x314A, 0x324A,
            // Row 0x4B
            0x274B, 0x284B, 0x294B, 0x2A4B, 0x2B4B, 0x2C4B, 0x2D4B, 0x2E4B, 0x2F4B, 0x304B, 0x314B, 0x324B,
            // Row 0x4C
            0x284C, 0x294C, 0x2A4C, 0x2B4C, 0x2C4C, 0x2D4C, 0x2E4C, 0x2F4C, 0x304C, 0x314C, 0x324C,
            // Row 0x4D
            0x254D, 0x264D, 0x274D, 0x284D, 0x294D, 0x2A4D, 0x2B4D, 0x2C4D, 0x2D4D, 0x2E4D, 0x2F4D, 0x304D, 0x314D, 0x324D,
            // Row 0x4E
            0x254E, 0x274E, 0x284E, 0x294E, 0x2A4E, 0x2B4E, 0x2C4E, 0x2D4E, 0x2E4E, 0x2F4E, 0x304E, 0x314E, 0x324E,
            // Row 0x4F
            0x254F, 0x264F, 0x284F, 0x294F, 0x2A4F, 0x2B4F, 0x2C4F, 0x2D4F, 0x2E4F, 0x2F4F, 0x304F, 0x314F, 0x324F,
            // Row 0x50
            0x2550, 0x2650, 0x2850, 0x2950, 0x2A50, 0x2B50, 0x2C50, 0x2D50, 0x2E50, 0x2F50, 0x3050, 0x3150, 0x3250,
            // Row 0x51
            0x2651, 0x2851, 0x2951, 0x2A51, 0x2B51, 0x2C51, 0x2D51, 0x2E51, 0x2F51, 0x3051, 0x3151, 0x3251,
            // Row 0x52
            0x2752, 0x2852, 0x2952, 0x2A52, 0x2B52, 0x2C52, 0x2D52, 0x2E52, 0x2F52, 0x3052, 0x3152, 0x3252,
            // Row 0x53
            0x2753, 0x2853, 0x2953, 0x2A53, 0x2B53, 0x2C53, 0x2D53, 0x2E53, 0x2F53, 0x3053, 0x3153, 0x3253, 0x3353,
            // Row 0x54
            0x2754, 0x2854, 0x2954, 0x2A54, 0x2B54, 0x2C54, 0x2D54, 0x2E54, 0x2F54, 0x3054, 0x3154, 0x3254,
            // Row 0x55
            0x2755, 0x2855, 0x2955, 0x2A55, 0x2B55, 0x2C55, 0x2D55, 0x2E55, 0x2F55, 0x3055, 0x3155, 0x3255, 0x3355,
            // Row 0x56
            0x2856, 0x2956, 0x2A56, 0x2B56, 0x2C56, 0x2D56, 0x2E56, 0x2F56, 0x3056, 0x3156, 0x3256,
            // Row 0x57
            0x2857, 0x2957, 0x2A57, 0x2B57, 0x2C57, 0x2D57, 0x2E57, 0x2F57, 0x3057, 0x3157, 0x3257,
        };

        /// <summary>
        /// A mapping of apartment landblocks => apartment complex names
        /// </summary>
        public static readonly Dictionary<ushort, string> ApartmentBlocks = new Dictionary<ushort, string>()
        {
            // currently used for apartment deeds
            { 0x5360, "Sanctum Residential Halls - Alvan Court" },
            { 0x5361, "Sanctum Residential Halls - Caerna Dwellings" },
            { 0x5362, "Sanctum Residential Halls - Illsin Veranda" },
            { 0x5363, "Sanctum Residential Halls - Marin Court" },
            { 0x5364, "Sanctum Residential Halls - Ruadnar Court" },
            { 0x5365, "Sanctum Residential Halls - Senmai Court" },
            { 0x5366, "Sanctum Residential Halls - Sigil Veranda" },
            { 0x5367, "Sanctum Residential Halls - Sorveya Court" },
            { 0x5368, "Sanctum Residential Halls - Sylvan Dwellings" },
            { 0x5369, "Sanctum Residential Halls - Treyval Veranda" },
            { 0x7200, "Atrium Residential Halls - Winthur Gate" },
            { 0x7300, "Atrium Residential Halls - Larkspur Gardens" },
            { 0x7400, "Atrium Residential Halls - Mellas Court" },
            { 0x7500, "Atrium Residential Halls - Vesper Gate" },
            { 0x7600, "Atrium Residential Halls - Gajin Dwellings" },
            { 0x7700, "Atrium Residential Halls - Valorya Gate" },
            { 0x7800, "Atrium Residential Halls - Heartland Yard" },
            { 0x7900, "Atrium Residential Halls - Ivory Gate" },
            { 0x7A00, "Atrium Residential Halls - Alphas Court" },
            { 0x7B00, "Atrium Residential Halls - Hasina Gardens" },
            { 0x7C00, "Oriel Residential Halls - Sorac Gate" },
            { 0x7D00, "Oriel Residential Halls - Maru Veranda" },
            { 0x7E00, "Oriel Residential Halls - Forsythian Gardens" },
            { 0x7F00, "Oriel Residential Halls - Vindalan Dwellings" },
            { 0x8000, "Oriel Residential Halls - Syrah Dwellings" },
            { 0x8100, "Oriel Residential Halls - Allain Court" },
            { 0x8200, "Oriel Residential Halls - White Lotus Gate" },
            { 0x8300, "Oriel Residential Halls - Autumn Moon Gardens" },
            { 0x8400, "Oriel Residential Halls - Trellyn Gardens" },
            { 0x8500, "Oriel Residential Halls - Endara Gate" },
            { 0x8600, "Haven Residential Halls - Celcynd Grotto" },
            { 0x8700, "Haven Residential Halls - Trothyr Hollow" },
            { 0x8800, "Haven Residential Halls - Jojii Gardens" },
            { 0x8900, "Haven Residential Halls - Cedraic Court" },
            { 0x8A00, "Haven Residential Halls - Ben Ten Lodge" },
            { 0x8B00, "Haven Residential Halls - Dulok Court" },
            { 0x8C00, "Haven Residential Halls - Crescent Moon Veranda" },
            { 0x8D00, "Haven Residential Halls - Jade Gate" },
            { 0x8E00, "Haven Residential Halls - Ispar Yard" },
            { 0x8F00, "Haven Residential Halls - Xao Wu Gardens" },
            { 0x9000, "Victory Residential Halls - Accord Veranda" },
            { 0x9100, "Victory Residential Halls - Candeth Court" },
            { 0x9200, "Victory Residential Halls - Celdiseth Court" },
            { 0x9300, "Victory Residential Halls - Festivus Court" },
            { 0x9400, "Victory Residential Halls - Hibiscus Gardens" },
            { 0x9500, "Victory Residential Halls - Meditation Gardens" },
            { 0x9600, "Victory Residential Halls - Setera Gardens" },
            { 0x9700, "Victory Residential Halls - Spirit Gate" },
            { 0x9800, "Victory Residential Halls - Triumphal Gardens" },
            { 0x9900, "Victory Residential Halls - Wilamil Court" },
        };
    }
}
