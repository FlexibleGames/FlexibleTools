using System;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Common;

namespace FlexibleTools
{
    public class FlexibleToolsMod : ModSystem
    {
        ICoreAPI api;
        ICoreServerAPI sapi;
        ICoreClientAPI capi;

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            this.api = api;
            this.api.RegisterItemClass("DiamondScytheItem", typeof(DiamondScytheItem));
            this.api.RegisterItemClass("DiamondShearsItem", typeof(DiamondShearsItem));
            this.api.RegisterItemClass("ItemWorldEater", typeof(WorldEaterItem));
            this.api.RegisterItemClass("RockSnifferItem", typeof(RockSnifferItem));
            this.api.RegisterItemClass("TorcMagnetItem", typeof(TorcMagnetItem));
            this.api.RegisterEntityBehaviorClass("vacuumitems", typeof(TorcMagnetBehavior));
        }
        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            sapi = api;
            sapi.Event.PlayerNowPlaying += AddMagnetBehavior;
        }

        /// <summary>
        /// Called ServerSide when a player joins and is ready to play... 
        /// Adds TorcMagnetBehavior to the player entity that joins.
        /// Magnet only works when item is in the neck slot.
        /// </summary>
        /// <param name="byPlayer"></param>
        private void AddMagnetBehavior(IServerPlayer byPlayer)
        {
            TorcMagnetBehavior tmb = new TorcMagnetBehavior(byPlayer.Entity);
            byPlayer.Entity.AddBehavior(tmb);
        }
        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            capi = api;             
        }        
    }
}
