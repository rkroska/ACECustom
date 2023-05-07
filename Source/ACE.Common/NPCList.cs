using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACE.Common
{
    public class NPCList
    {
        public static List<uint> GetNPCs()
        {
            var list = new List<uint>
            {
                24153,
                53281,
                87590,
                44207,
                27251,
                47172,
                35828,
                38215,
                51956,
                31648,
                24577,
                87733,
                5177,
                31645,
                8370,
                41520,
                5365,
                24224,
                20203,
                44895,
                28475,
                31655,
                8404,
                12203,
                30479,
                31931,
                35825,
                41526,
                28688,
                30629,
                9495,
                9498,
                9501,
                20911,
                80152,
                22934,
                41946,
                42023,
                32073,
                41515,
                9227,
                72120,
                43028,
                28705,
                31950,
                35109,
                70344,
                70345,
                70343,
                72677,
                12686,
                43741,
                25711,
                34062,
                44886,
                31951,
                24001,
                9493,
                40488,
                70055,
                44892,
                43183,
                39288,
                21489,
                33546,
                23348,
                42112,
                23524,
                36923,
                27266,
                29858,
                27262,
                14571,
                27263,
                11372,
                28472,
                11406,
                29860,
                36866,
                29861,
                27267,
                27268,
                39054,
                24055,
                46683,
                27265,
                11348,
                24053,
                29859,
                52015,
                11330,
                22890,
                29862,
                24054,
                32107,
                14578,
                11410,
                51378,
                27280,
                22936,
                23040,
                14575,
                34253,
                24214,
                14577,
                27264,
                11940,
                24052,
                42136,
                11349,
                24576,
                24576,
                25672,
                9467,
                41177,
                42731,
                37074,
                71059,
                11322,
                31959,
                28258,
                70294,
                15774,
                7240,
                33194,
                12238,
                80437,
                9131,
                10842,
                33966,
                5047,
                44288,
                44893,
                42723,
                19354,
                32836,
                2225,
                24215,
                24216,
                46776,
                71351,
                27899,
                22829,
                52286,
                34824,
                40098,
                14469,
                31973,
                53280,
                4794,
                5693,
                28684,
                22642,
                31958,
                34821,
                9132,
                32561,
                15809,
                70708,
                72976,
                44891,
                30277,
                24212,
                80431,
                48937,
                31702,
                37602,
                32070,
                32395,
                28515,
                32396,
                32394,
                39420,
                38151,
                42519,
                38978,
                38993,
                41522,
                40895,
                31974,
                4795,
                44950,
                31841,
                28836,
                25788,
                35343,
                80434,
                8766,
                8765,
                8767,
                19713,
                31650,
                28717,
                31934,
                34938,
                34442,
                47799,
                28476,
                51897,
                34037,
                9133,
                80427,
                42616,
                22075,
                19464,
                46681,
                35839,
                35606,
                31949,
                11633,
                23523,
                11636,
                11627,
                11635,
                11634,
                42037,
                11632,
                11631,
                11630,
                11629,
                11628,
                14470,
                35904,
                35177,
                45864,
                49639,
                42131,
                25485,
                42716,
                30627,
                14471,
                5063,
                25585,
                41561,
                25715,
                41546,
                14415,
                44260,
                31952,
                37166,
                6848,
                31941,
                25483,
                9545,
                46559,
                72670,
                48716,
                31932,
                43062,
                35905,
                23349,
                40772,
                6026,
                40461,
                80428,
                2224,
                31690,
                10737,
                8266,
                5636,
                36608,
                34259,
                53272,
                34849,
                53306,
                71409,
                34841,
                31689,
                4201,
                28685,
                33970,
                42720,
                9056,
                12262,
                32992,
                31429,
                80151,
                44897,
                22818,
                72770,
                28701,
                25793,
                33937,
                31957,
                9544,
                49584,
                41518,
                25794,
                30272,
                35562,
                32084,
                31651,
                5048,
                28690,
                43030,
                80153,
                35909,
                19461,
                10879,
                30265,
                47052,
                87443,
                7403,
                38035,
                5694,
                52181,
                31365,
                43526,
                8229,
                41186,
                5020,
                31652,
                7560,
                43941,
                27948,
                72918,
                41311,
                44894,
                72768,
                28694,
                31296,
                32595,
                28692,
                28697,
                28698,
                28695,
                28693,
                28696,
                72650,
                31972,
                72238,
                5033,
                72744,
                72742,
                24493,
                37441,
                33675,
                30439,
                30438,
                22088,
                34834,
                9226,
                51958,
                31431,
                42977,
                41521,
                7402,
                32693,
                38462,
                24873,
                41178,
                44190,
                51681,
                25721,
                51959,
                72356,
                8126,
                8124,
                8125,
                33935,
                9496,
                9499,
                9502,
                44896,
                44209,
                30385,
                8654,
                32820,
                80092,
                15810,
                34914,
                28857,
                28516,
                6889,
                28700,
                28699,
                14923,
                14921,
                14922,
                31644,
                26536,
                43495,
                46682,
                4202,
                31953,
                34729,
                36535,
                37444,
                37445,
                25717,
                29769,
                29771,
                29770,
                29767,
                29766,
                36536,
                37440,
                29772,
                36534,
                29768,
                37442,
                36533,
                35344,
                37043,
                24432,
                43247,
                27306,
                19462,
                45194,
                9676,
                41519,
                5837,
                33969,
                9308,
                80155,
                44989,
                44300,
                6025,
                25832,
                46684,
                80436,
                32991,
                25568,
                42722,
                32628,
                34305,
                32673,
                33187,
                44104,
                35962,
                22644,
                10978,
                11344,
                31709,
                10922,
                11343,
                11371,
                41227,
                25600,
                30979,
                51861,
                28969,
                20912,
                43900,
                31842,
                38045,
                19460,
                25486,
                25826,
                35572,
                33877,
                72522,
                42957,
                34504,
                43740,
                29468,
                28689,
                32801,
                25828,
                30270,
                80429,
                51654,
                5119,
                19463,
                29229,
                43861,
                24858,
                31364,
                25683,
                37145,
                9546,
                25951,
                80432,
                8403,
                12204,
                48814,
                48815,
                35462,
                33460,
                37607,
                31940,
                5120,
                31659,
                28970,
                35772,
                5152,
                41562,
                31284,
                51962,
                37599,
                27317,
                37610,
                12239,
                20913,
                30276,
                5838,
                12240,
                20914,
                31692,
                40460,
                40097,
                43404,
                15860,
                34063,
                47020,
                5836,
                6770,
                42601,
                53271,
                41524,
                71053,
                46775,
                28971,
                20915,
                33293,
                31960,
                25712,
                22461,
                46735,
                37603,
                46338,
                24213,
                28687,
                51379,
                51960,
                34823,
                34825,
                34951,
                34826,
                5695,
                30271,
                38078,
                43988,
                46294,
                46297,
                46296,
                46295,
                5644,
                38211,
                33875,
                33542,
                33939,
                31840,
                9527,
                23039,
                43057,
                37146,
                4122,
                20916,
                9134,
                6771,
                51961,
                30278,
                8155,
                28477,
                15811,
                43403,
                32841,
                46678,
                32843,
                32842,
                41569,
                41567,
                48921,
                11809,
                41568,
                48920,
                32650,
                46686,
                41570,
                27812,
                27806,
                72283,
                30981,
                14574,
                11347,
                52248,
                38206,
                32715,
                36236,
                52281,
                32360,
                43061,
                28704,
                5046,
                48853,
                42984,
                46680,
                9406,
                88247,
                43406,
                80430,
                5064,
                5195,
                30274,
                5178,
                32835,
                32074,
                28681,
                30073,
                32813,
                43064,
                5839,
                8490,
                24068,
                5874,
                33936,
                36231,
                31728,
                24327,
                34010,
                5179,
                31654,
                51862,
                37443,
                46445,
                31642,
                31839,
                30436,
                34441,
                42116,
                34016,
                80421,
                80422,
                87664,
                9407,
                9149,
                31954,
                5366,
                22640,
                43753,
                10866,
                33015,
                32684,
                33013,
                30435,
                42618,
                11345,
                32052,
                37400,
                32122,
                32051,
                42982,
                35308,
                42987,
                45754,
                45685,
                9494,
                7773,
                35907,
                25827,
                4796,
                42931,
                42930,
                6890,
                34822,
                31647,
                46424,
                38995,
                9492,
                43410,
                51987,
                70351,
                72970,
                42144,
                45853,
                34383,
                43398,
                24242,
                24243,
                5180,
                42721,
                28683,
                28682,
                40807,
                41516,
                47191,
                48907,
                5197,
                44101,
                32072,
                43029,
                34851,
                25790,
                36720,
                37089,
                31649,
                88224,
                25791,
                5153,
                41208,
                38207,
                51377,
                44208,
                5193,
                23997,
                29505,
                8362,
                43530,
                45865,
                8491,
                35776,
                34365,
                40103,
                2223,
                31653,
                35477,
                32066,
                16912,
                24574,
                30267,
                80433,
                33614,
                33616,
                33615,
                33596,
                35853,
                8129,
                43063,
                43059,
                31930,
                71352,
                32069,
                28679,
                72649,
                24244,
                46356,
                72156,
                29094,
                34467,
                41179,
                25673,
                71074,
                32217,
                37598,
                41525,
                30434,
                51923,
                28706,
                39983,
                51864,
                41523,
                24245,
                5024,
                14410,
                30389,
                20918,
                28856,
                51955,
                9307,
                25388,
                35605,
                34842,
                51866,
                31316,
                28680,
                32108,
                40897,
                30273,
                51789,
                33277,
                24246,
                24160,
                25789,
                30269,
                32110,
                38034,
                28686,
                32067,
                32510,
                27689,
                32618,
                28414,
                40247,
                5137,
                44890,
                8614,
                31658,
                32596,
                31036,
                72058,
                72151,
                31656,
                36609,
                30437,
                9309,
                43911,
                32075,
                25896,
                31641,
                5138,
                31643,
                31691,
                31376,
                5154,
                35908,
                34286,
                48730,
                28703,
                34956,
                24249,
                24251,
                24250,
                25569,
                39746,
                29051,
                28819,
                35603,
                52294,
                33839,
                25493,
                35573,
                46679,
                42027,
                36926,
                32493,
                42030,
                40799,
                43405,
                42029,
                45201,
                44637,
                30826,
                72974,
                46806,
                11346,
                34499,
                31328,
                40922,
                5121,
                34820,
                9497,
                9500,
                9503,
                33968,
                46889,
                11811,
                30275,
                31970,
                6356,
                70707,
                44262,
                44999,
                43804,
                42734,
                42360,
                29488,
                44263,
                44261,
                43847,
                42361,
                14413,
                41571,
                14414,
                52289,
                41545,
                43060,
                72648,
                20204,
                52310,
                33876,
                36232,
                72501,
                72978,
                43145,
                31663,
                31430,
                14472,
                42134,
                71056,
                87020,
                14412,
                22893,
                30266,
                53451,
                31602,
                32109,
                30387,
                30386,
                53283,
                35903,
                51863,
                15812,
                31657,
                33244,
                25584,
                38820,
                33938,
                9135,
                87705,
                87704,
                87706,
                72624,
                40322,
                87707,
                30509,
                44301,
                5763,
                5065,
                24247,
                10923,
                11637,
                51888,
                25792,
                43058,
                72914,
                23350,
                35209,
                3607,
                22074,
                35906,
                51860,
                20925,
                22935,
                41548,
                44299,
                30696,
                32068,
                28473,
                40099,
                35826,
                32362,
                34036,
                34950,
                22894,
                27117,
                34145,
                8402,
                12205,
                32834,
                32065,
                22749,
                33673,
                45851,
                11810,
                26457,
                6873,
                70995,
                24575,
                24575,
                14473,
                25314,
                33837,
                32071,
                80439,
                80439,
                33746,
                52139,
                33499,
                19127,
                71078,
                32064,
                37600,
                24859,
                87730,
                72179,
                5122,
                31971,
                24069,
                51865,
                12127,
                22076,
                3924,
                5035,
                32780,
                25682,
                31509,
                31308,
                31646,
                9228,
                14573,
                14572,
                25974,
                28094,
                5898,
                51957,
                41852,
                6847,
                7241,
                24573,
                42981,
                33967,
                36233,
                8441,
                38077
            };
            return list;
        }
    }
}