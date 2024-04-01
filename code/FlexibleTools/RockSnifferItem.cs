using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Config;

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
            if (block.FirstCodePart() == "rock" || block.FirstCodePart() == "crackedrock")
            {
                isRock = true;
            }
            else isRock = false;

            return base.OnBlockBreaking(player, blockSel, itemslot, remainingResistance, dt, counter);
        }

        public override bool OnBlockBrokenWith(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, BlockSelection blockSel, float dropQuantityMultiplier = 1)
        {
            if (api.Side == EnumAppSide.Client) return true;
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
                    return false;
                }
                List<string> rockTypes = new List<string>();
                rockTypes = RockSniff(player, world, blockSel);
                if (rockTypes.Count == 0)
                {
                    sapi.SendMessage(player, 0, "Oddly, There are no rocks under you...", EnumChatType.OwnMessage);                    
                    return true;
                }
                sapi.SendMessage(player, 0, "RockSniffer Found:", EnumChatType.OwnMessage);                
                foreach (string rtype in rockTypes)
                {
                    sapi.SendMessage(player, 0, rtype, EnumChatType.OwnMessage);
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
            BlockPos blockpos = new BlockPos(blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z, 0);

            bool reportfluid = config.RockSnifferReportsFluid;
            bool reportore = config.RockSnifferReportsOres;

            //reportore = true; // DEBUG ONLY

            for (int offset = 1; offset < blockSel.Position.Y; offset++)
            {
                BlockPos temppos = blockSel.Position.DownCopy(offset);

                if (reportfluid)
                {
                    curBlock = world.BlockAccessor.GetBlock(temppos, 3); // grab the fluid, if not present grab the rock.
                }
                else
                {
                    curBlock = world.BlockAccessor.GetBlock(temppos, 0);
                }
                if (reportore && curBlock.FirstCodePart() == "ore")
                {
                    string oretype = new ItemStack(curBlock, 1).GetName();
                    if (!rockTypes.Contains(oretype)) rockTypes.Add(oretype);
                }
                else
                {
                    if (reportfluid && curBlock.IsLiquid())
                    {                        
                        string fluidtype = curBlock.FirstCodePart();
                        fluidtype = char.ToUpper(fluidtype[0]) + fluidtype.Substring(1);

                        if (!rockTypes.Contains(fluidtype)) { rockTypes.Add(fluidtype); }

                        continue;
                    }
                    if (curBlock.FirstCodePart().Contains("rock") && !curBlock.FirstCodePart().Contains("cracked")) // it is rock
                    {
                        string rocktype = new ItemStack(curBlock, 1).GetName();
                        if (!rockTypes.Contains(rocktype)) rockTypes.Add(rocktype);
                    }
                }
            }
            return rockTypes;
        }

        private FlexibleToolsConfig config;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            this.api = api;            
            if (api.Side == EnumAppSide.Server)
            {
                sapi = api as ICoreServerAPI;
                config = api.ModLoader.GetModSystem<FlexibleToolsMod>(true).ToolsConfig;
            }
            else
            {
                capi = api as ICoreClientAPI;
            }
        }                
    }
}
