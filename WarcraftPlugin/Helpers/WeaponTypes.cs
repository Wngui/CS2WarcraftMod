using System.Collections.Generic;

namespace WarcraftPlugin.Helpers
{
    public static class WeaponTypes
    {
        public static readonly List<string> Shotguns =
            [
                "xm1014", "sawedoff", "nova", "mag7"
            ];

        public static readonly List<string> Snipers =
            [
                "sg553", "scar20", "aug", "ssg08", "awp", "g3sg1"
            ];

        public static readonly List<string> Rifles =
            [
                "ak47", "m4a1", "m4a1_silencer", "galilar", "famas"
            ];

        public static readonly List<string> SMGs =
            [
                "mp9", "mac10", "mp7", "ump45", "p90", "bizon"
            ];

        public static readonly List<string> Pistols =
            [
                "glock", "usp_silencer", "p2000", "dualberettas", "p250", "fiveseven", "tec9", "cz75a", "deagle", "revolver"
            ];

        public static readonly List<string> Heavy =
            [
                "m249", "negev"
            ];
    }
}
