using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FlexibleTools
{
    public class FlexibleToolsConfig
    {
        public List<string> MagnetBlackList = new List<string>();
        public float MagnetPickupRadius = 5f;
        public bool RockSnifferReportsOres = false;
        public bool RockSnifferReportsFluid = true;
        public int WorldEaterVeinMineLimit = 512;
        
        public bool EnableScythe = true;
        public bool EnableShears = true;

        public bool EnableMagnet = true;
        public bool EnableSniffer = true;
        public bool EnableEater = true;

        public FlexibleToolsConfig()
        {

        }

        public FlexibleToolsConfig(FlexibleToolsConfig oldconfig)
        {
            this.MagnetBlackList.AddRange(oldconfig.MagnetBlackList);
            this.MagnetPickupRadius = oldconfig.MagnetPickupRadius;
            this.RockSnifferReportsOres = oldconfig.RockSnifferReportsOres;
            this.RockSnifferReportsFluid = oldconfig.RockSnifferReportsFluid;
            this.WorldEaterVeinMineLimit = oldconfig.WorldEaterVeinMineLimit;

            this.EnableScythe = oldconfig.EnableScythe;
            this.EnableShears = oldconfig.EnableShears;
            this.EnableMagnet = oldconfig.EnableMagnet;
            this.EnableSniffer = oldconfig.EnableSniffer;
            this.EnableEater = oldconfig.EnableEater;
        }
    }
}
