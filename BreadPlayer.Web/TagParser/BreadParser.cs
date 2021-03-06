﻿using System.Collections.Generic;

namespace BreadPlayer.Web.TagParser
{
    public class BreadParser
    {
        private List<string> _tldList = new List<string>();
        private List<string> _seperatorList = new List<string> {" - "};
        private List<char> _garbageCharList = new List<char>();
        public int Compare2Strings(string a, string b)
        {
            List<string> list = new List<string> {"eminem", "justin", "justin bieber", "the way I am eminem", "nothing like us justin bieber" };
            foreach(var item in list)
            {
                int similarity = YetiLevenshteinDistance.YetiLevenshtein("justin bieber", item);
                int similarity3 = YetiLevenshteinDistance.YetiLevenshtein("bieber justin", item);
                string represent = a + ": " + similarity + " || " + b + ": " + similarity3;
                //return similarity + similarity3;
            }
            return 0;
        }
    }
}
