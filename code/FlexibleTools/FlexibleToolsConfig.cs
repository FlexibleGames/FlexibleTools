using System;
using System.Collections.Generic;

namespace FlexibleTools
{
    public class FlexibleToolsConfig
    {        
        public List<string> MagnetBlackList = new List<string>();
        public float MagnetPickupRadius = 5f;
        public bool RockSnifferReportsOres = false;
        public bool RockSnifferReportsFluid = true;
        public int WorldEaterVeinMineLimit = 512;

        public FlexibleToolsConfig()
        {

        }
    }
}
