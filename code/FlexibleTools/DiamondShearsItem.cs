using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Cairo;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace FlexibleTools
{
    public class DiamondShearsItem : Item
    {
        /// <summary>
        /// A lot of repeated code between this and the DiamondScythe, still learning the modding process so
        /// repetition helps.
        /// </summary>
        public SkillItem[] toolModes;
        public ICoreAPI coreapi;
        public ICoreClientAPI capi;
        public ICoreServerAPI sapi;

        private string[] allowedPrefixes;
        private string[] disallowedSuffixes;
        private SkillItem[] skillItems;

        private int breakQuantity = 27; // default radius of 1 = 3x3x3 = 27 total blocks could be broken.
        private int breakradius = 1;

        public int MultiBreakQuantity
        {
            get { return breakQuantity; }
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            this.allowedPrefixes = this.Attributes["codePrefixes"].AsArray<string>(null, null);
            this.disallowedSuffixes = this.Attributes["disallowedSuffixes"].AsArray<string>(null, null);
            this.skillItems = new SkillItem[]
            {
                new SkillItem
                {
                    Code = new AssetLocation("radius one"),
                    Name = "3x3"
                }.WithLetterIcon(api as ICoreClientAPI, "3"),
                new SkillItem
                {
                    Code = new AssetLocation("radius two"),
                    Name = "5x5"
                }.WithLetterIcon(api as ICoreClientAPI, "5"),
                new SkillItem
                {
                    Code = new AssetLocation("radius three"),
                    Name = "7x7"
                }.WithLetterIcon(api as ICoreClientAPI, "7"),
                new SkillItem
                {
                    Code = new AssetLocation("radius four"),
                    Name = "9x9"
                }.WithLetterIcon(api as ICoreClientAPI, "9")
            };
        }

        public bool CanMultiBreak(Block block)
        {
            return block.BlockMaterial == EnumBlockMaterial.Leaves;
        }

        private void DamageNearbyBlocks(IPlayer player, BlockSelection blockSel, float damage, int leftDurability)
        {
            Block block = player.Entity.World.BlockAccessor.GetBlock(blockSel.Position);
            if (!this.CanMultiBreak(block))
            {
                return;
            }

            Vec3d hitPos = blockSel.Position.ToVec3d().Add(blockSel.HitPosition);
            IEnumerable<BlockPos> enumerable = from x in this.GetNearbyBreakables(player.Entity.World, blockSel.Position, hitPos, breakradius)
                                               orderby x.Value
                                               select x.Key;
            int num = Math.Min(this.MultiBreakQuantity, leftDurability);
            foreach (BlockPos pos in enumerable)
            {
                if (num == 0)
                {
                    break;
                }
                BlockFacing opposite = BlockFacing.FromNormal(player.Entity.ServerPos.GetViewVector()).Opposite;
                if (player.Entity.World.Claims.TryAccess(player, pos, EnumBlockAccessFlags.BuildOrBreak))
                {
                    player.Entity.World.BlockAccessor.DamageBlock(pos, opposite, damage);
                    num--;
                }
            }
        }

        public override float OnBlockBreaking(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
        {
            float num = base.OnBlockBreaking(player, blockSel, itemslot, remainingResistance, dt, counter);
            int @int = itemslot.Itemstack.Attributes.GetInt("durability", this.Durability);
            this.DamageNearbyBlocks(player, blockSel, remainingResistance - num, @int);
            return num;
        }

        public override bool OnBlockBrokenWith(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, BlockSelection blockSel, float dropQuantityMultiplier = 1f)
        {
            Block block = world.BlockAccessor.GetBlock(blockSel.Position);
            if (!(byEntity is EntityPlayer) || itemslot.Itemstack == null)
            {
                return true;
            }
            IPlayer player = world.PlayerByUid((byEntity as EntityPlayer).PlayerUID);
            this.breakMultiBlock(blockSel.Position, player);
            if (!this.CanMultiBreak(block))
            {
                return true;
            }
            Vec3d hitPos = blockSel.Position.ToVec3d().Add(blockSel.HitPosition);
            IEnumerable<KeyValuePair<BlockPos, float>> enumerable = from x in this.GetNearbyBreakables(world, blockSel.Position, hitPos, breakradius)
                                                                    orderby x.Value
                                                                    select x;
            itemslot.Itemstack.Attributes.GetInt("durability", this.Durability);
            int num = 0;
            foreach (KeyValuePair<BlockPos, float> keyValuePair in enumerable)
            {
                if (player.Entity.World.Claims.TryAccess(player, keyValuePair.Key, EnumBlockAccessFlags.BuildOrBreak))
                {
                    this.breakMultiBlock(keyValuePair.Key, player);
                    this.DamageItem(world, byEntity, itemslot, 1);
                    num++;
                    if (num >= this.MultiBreakQuantity)
                    {
                        break;
                    }
                    if (itemslot.Itemstack == null)
                    {
                        break;
                    }
                }
            }
            return true;
        }

        private OrderedDictionary<BlockPos, float> GetNearbyBreakables(IWorldAccessor world, BlockPos pos, Vec3d hitPos, int radius)
        {
            OrderedDictionary<BlockPos, float> orderedDictionary = new OrderedDictionary<BlockPos, float>();
            for (int i = -radius; i <= radius; i++)
            {
                for (int j = -radius; j <= radius; j++)
                {
                    for (int k = -radius; k <= radius; k++)
                    {
                        BlockPos blockPos = pos.AddCopy(i, j, k);
                        if (this.CanMultiBreak(world.BlockAccessor.GetBlock(blockPos)))
                        {
                            orderedDictionary.Add(blockPos, hitPos.SquareDistanceTo((double)blockPos.X + 0.5,
                                (double)blockPos.Y + 0.5, (double)blockPos.Z + 0.5));
                        }
                    }
                }
            }
            return orderedDictionary;
        }
        protected void breakMultiBlock(BlockPos pos, IPlayer plr)
        {
            this.api.World.BlockAccessor.BreakBlock(pos, plr, 1f);
            this.api.World.BlockAccessor.MarkBlockDirty(pos);
        }
        public override int GetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection)
        {
            return slot.Itemstack.Attributes.GetInt("toolMode", 0);
        }
        public override SkillItem[] GetToolModes(ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel)
        {
            return this.skillItems;
        }
        /// <summary>
        /// This is where the meat of the multibreak is set. Hopefully the 9x9 won't cause lag.
        /// </summary>
        /// <param name="slot">Slot selected</param>
        /// <param name="byPlayer">Player</param>
        /// <param name="blockSelection">Block targeted</param>
        /// <param name="toolMode">Tool mode clicked</param>
        public override void SetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection, int toolMode)
        {
            slot.Itemstack.Attributes.SetInt("toolMode", toolMode);
            switch (toolMode)
            {
                case 0: this.breakQuantity = 27; break;
                case 1: this.breakQuantity = 125; break;
                case 2: this.breakQuantity = 343; break;
                case 3: this.breakQuantity = 729; break; // that is a LOT of leaves 9x9x9 area
                default: this.breakQuantity = 9; break;
            }
            this.breakradius = toolMode + 1;
        }
        public override void OnUnloaded(ICoreAPI api)
        {
            int num = 0;
            while (this.skillItems != null && num < this.skillItems.Length)
            {
                SkillItem skillItem = this.skillItems[num];
                if (skillItem != null)
                {
                    skillItem.Dispose();
                }
                num++;
            }
        }
    }
}
