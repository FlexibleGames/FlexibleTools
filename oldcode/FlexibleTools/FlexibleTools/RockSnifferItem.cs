using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace FlexibleTools
{
    class RockSnifferItem : Item
    {        
        private ICoreClientAPI capi;
        private ICoreServerAPI sapi;
        private bool isRock = false;

        public override float OnBlockBreaking(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
        {
            Block block = player.Entity.World.BlockAccessor.GetBlock(blockSel.Position);
            // normal rock in game is rock-{type}
            if (block.FirstCodePart() == "rock")
            {
                isRock = true;
            }
            else isRock = false;

            return base.OnBlockBreaking(player, blockSel, itemslot, remainingResistance, dt, counter);
        }

        public override bool OnBlockBrokenWith(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, BlockSelection blockSel, float dropQuantityMultiplier = 1)
        {
            if (!isRock)
            {
                return base.OnBlockBrokenWith(world, byEntity, itemslot, blockSel, dropQuantityMultiplier);
            }
            // we're hitting rock! Time to Probe the Ground...
            if (byEntity is EntityPlayer)
            {
                IPlayer player = world.PlayerByUid((byEntity as EntityPlayer).PlayerUID);
                if (!player.Entity.World.Claims.TryAccess(player, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak)) // honor land claims
                {
                    itemslot.MarkDirty();
                    return false;
                }
                List<string> rockTypes = new List<string>();
                rockTypes = RockSniff(player, world, blockSel);
                if (rockTypes.Count == 0)
                {
                    if (api.Side == EnumAppSide.Client) capi.ShowChatMessage("Oddly, There are no rocks under you...");
                    return true;
                }
                if (api.Side == EnumAppSide.Client) capi.ShowChatMessage("Rock Found:");
                foreach (string rtype in rockTypes)
                {
                    if (api.Side == EnumAppSide.Client)
                    {
                        capi.ShowChatMessage(rtype);
                    }
                }
            }
            return base.OnBlockBrokenWith(world, byEntity, itemslot, blockSel, dropQuantityMultiplier); 
        }

        private List<string> RockSniff(IPlayer player, IWorldAccessor world, BlockSelection blockSel)
        {
            if (player == null) return null;
            if (world == null) return null;
            if (blockSel == null) return null;

            List<string> rockTypes = new List<string>();
            Block curBlock;
            BlockPos blockpos = new BlockPos(blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z);

            for (int offset = 1; offset < blockSel.Position.Y; offset++)
            {
                curBlock = world.BlockAccessor.GetBlock(blockpos.X, blockpos.Y - offset, blockpos.Z);
                if (curBlock.FirstCodePart() == "rock" || curBlock.FirstCodePart() == "lava" || curBlock.FirstCodePart() == "oil")
                {
                    string codePart = curBlock.FirstCodePart() == "rock" ? curBlock.LastCodePart() : curBlock.FirstCodePart();

                    if (!rockTypes.Contains(codePart))
                    {
                        rockTypes.Add(codePart);
                    }
                }
            }
            return rockTypes;
        }


        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            this.api = api;
            if (api.Side == EnumAppSide.Server)
            {
                sapi = api as ICoreServerAPI;
            }
            else
            {
                capi = api as ICoreClientAPI;
            }
        }
    }
}
