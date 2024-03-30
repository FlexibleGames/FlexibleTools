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
    public class DiamondScytheItem : Item
    {
        public SkillItem[] toolModes;
        public ICoreAPI coreapi;
        public ICoreClientAPI capi;
        public ICoreServerAPI sapi;

        private string[] allowedPrefixes;
        private string[] disallowedSuffixes;
        private SkillItem[] skillItems;

        private int breakQuantity = 9;
        private int breakradius = 1;
        private bool doRemove = false;

        public int MultiBreakQuantity
        {
            get { return breakQuantity;  }
        }        

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            this.allowedPrefixes = this.Attributes["codePrefixes"].AsArray<string>(null, null);
            this.disallowedSuffixes = this.Attributes["disallowedSuffixes"].AsArray<string>(null, null);
            capi = api as ICoreClientAPI;
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
                }.WithLetterIcon(api as ICoreClientAPI, "9"),
                new SkillItem
                {
                    Code = new AssetLocation("trim grass"),
                    Name = Lang.Get("Trim grass", Array.Empty<object>())
                },
                new SkillItem
                {
                    Code = new AssetLocation("remove grass"),
                    Name = Lang.Get("Remove grass", Array.Empty<object>())
                },
            };
            if (capi != null)
            {
                skillItems[4].WithIcon(capi, capi.Gui.LoadSvgWithPadding(new AssetLocation("game:textures/icons/scythetrim.svg"), 48, 48, 5, new int?(-1)));
                skillItems[5].WithIcon(capi, capi.Gui.LoadSvgWithPadding(new AssetLocation("game:textures/icons/scytheremove.svg"), 48, 48, 5, new int?(-1)));
                skillItems[4].TexturePremultipliedAlpha = false;
                skillItems[5].TexturePremultipliedAlpha = false;
            }
        }

        public bool CanMultiBreak(Block block)
        {
            for (int i = 0; i < this.allowedPrefixes.Length; i++)
            {
                if (block.Code.Path.StartsWith(this.allowedPrefixes[i]))
                {
                    if (this.disallowedSuffixes != null)
                    {
                        for (int j = 0; j < this.disallowedSuffixes.Length; j++)
                        {
                            if (block.Code.Path.EndsWith(this.disallowedSuffixes[j]))
                            {
                                return false;
                            }
                        }
                    }
                    return true;
                }
            }
            return false;
        }
        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handling);
            if (blockSel == null)
            {
                return;
            }
            EntityPlayer entityPlayer = byEntity as EntityPlayer;
            IPlayer player = (entityPlayer != null) ? entityPlayer.Player : null;
            if (!byEntity.World.Claims.TryAccess(player, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {                
                return;
            }
            byEntity.Attributes.SetBool("didBreakBlocks", false);
            byEntity.Attributes.SetBool("didPlayScytheSound", false);
            handling = EnumHandHandling.PreventDefault;
        }

        public override bool OnHeldAttackStep(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
        {
            this.performActions(secondsPassed, byEntity, slot, blockSelection);
            return this.api.Side == EnumAppSide.Server || secondsPassed < 2f;
        }
        public override void OnHeldAttackStop(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
        {
            this.performActions(secondsPassed, byEntity, slot, blockSelection);
        }
        private void performActions(float secondsPassed, EntityAgent byEntity, ItemSlot slot, BlockSelection blockSelection)
        {
            if (blockSelection == null)
            {
                return;
            }            

            Block block = this.api.World.BlockAccessor.GetBlock(blockSelection.Position);
            EntityPlayer entityPlayer = byEntity as EntityPlayer;
            IPlayer player = (entityPlayer != null) ? entityPlayer.Player : null;
            bool flag = this.CanMultiBreak(this.api.World.BlockAccessor.GetBlock(blockSelection.Position));
            if (flag && secondsPassed > 0.75f && !byEntity.Attributes.GetBool("didPlayScytheSound", false))
            {
                this.api.World.PlaySoundAt(new AssetLocation("sounds/tool/scythe1"), byEntity, player, true, 16f, 1f);
                byEntity.Attributes.SetBool("didPlayScytheSound", true);
            }
            if (flag && secondsPassed > 1.05f && !byEntity.Attributes.GetBool("didBreakBlocks", false))
            {
                if (byEntity.World.Side == EnumAppSide.Server && byEntity.World.Claims.TryAccess(player, blockSelection.Position, EnumBlockAccessFlags.BuildOrBreak))
                {
                    this.doRemove = slot.Itemstack.Attributes.GetBool("doRemove", false);
                    this.OnBlockBrokenWith(byEntity.World, byEntity, slot, blockSelection, 1f);
                }
                byEntity.Attributes.SetBool("didBreakBlocks", true);
            }
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
            if (!this.doRemove)
            {
                Block block = api.World.BlockAccessor.GetBlock(pos);                

                if (block.Code.FirstCodePart().Contains("berrybush"))
                {
                    BlockEntityBerryBush bebb = api.World.BlockAccessor.GetBlockEntity<BlockEntityBerryBush>(pos);
                    if (bebb.IsRipe())
                    {
                        ItemStack berrydrops = null;
                        BlockBehaviorHarvestable bbh = api.World.BlockAccessor.GetBlock(pos).GetBehavior<BlockBehaviorHarvestable>();
                        if (bbh != null)
                        {
                            float droprate = 1f;
                            droprate *= plr.Entity.Stats.GetBlended("forageDropRate");
                            berrydrops = bbh.harvestedStack.GetNextItemStack(droprate);
                        }                         
                        if (berrydrops != null)
                        {
                            api.World.SpawnItemEntity(berrydrops, pos.ToVec3d(), null);
                        }
                        Block emptybush = api.World.GetBlock(block.CodeWithVariant("state", "empty"));
                        if (emptybush != null)
                        {
                            api.World.BlockAccessor.SetBlock(emptybush.BlockId, pos);
                            //api.World.BlockAccessor.MarkBlockDirty(pos);
                        }
                    }                    
                    bebb.Pruned = true;
                    bebb.LastPrunedTotalDays = api.World.Calendar.TotalDays;
                    bebb.MarkDirty(true, null);
                    api.World.BlockAccessor.MarkBlockDirty(pos);
                    return;
                }

                Block trimmedblock = api.World.GetBlock(block.CodeWithVariant("tallgrass", "eaten"));
                if (block == trimmedblock) return;

                if (trimmedblock != null)
                {
                    api.World.BlockAccessor.BreakBlock(pos, plr, 2f);
                    api.World.BlockAccessor.MarkBlockDirty(pos);
                    api.World.BlockAccessor.SetBlock(trimmedblock.BlockId, pos);
                    BlockEntityTransient be = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityTransient;
                    if (be != null)
                    {
                        be.ConvertToOverride = block.Code.ToShortString();
                    }
                    return;
                }
            }
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
            if (toolMode < 4) slot.Itemstack.Attributes.SetInt("toolMode", toolMode);
            else slot.Itemstack.Attributes.SetBool("doRemove", toolMode != 4);

            switch (toolMode)
            {
                case 0: this.breakQuantity = 9; break;
                case 1: this.breakQuantity = 25; break;
                case 2: this.breakQuantity = 49; break;
                case 3: this.breakQuantity = 81; break;
                case 4: this.doRemove = false; break;
                case 5: this.doRemove = true; break;
                default: this.breakQuantity = 9; doRemove = false; break;
            }
            this.breakradius = toolMode < 4 ? toolMode + 1 : breakradius;
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
