using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace FlexibleTools
{
    public class VeinMineBehavior : CollectibleBehavior
    {
        int _veinBlockLimit = 1;
        public VeinMineBehavior(CollectibleObject collObj) : base(collObj)
        {
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
            _veinBlockLimit = properties["veinblocklimit"].AsInt(1);
        }

        public override bool OnBlockBrokenWith(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, BlockSelection blockSel, float dropQuantityMultiplier, ref EnumHandling bhHandling)
        {
            bool sneaking = false;
            if (byEntity is EntityPlayer player)
            {
                sneaking = player.ServerControls.Sneak;

                BlockEntity betarget = world.BlockAccessor.GetBlockEntity(blockSel.Position);
                if (betarget != null) sneaking = false;
                // if target has a block entity, then do not vein mine at all.
            }
            if (_veinBlockLimit > 1 && sneaking) AttemptVeinMine(world, byEntity, itemslot, blockSel, dropQuantityMultiplier);
            else return base.OnBlockBrokenWith(world, byEntity, itemslot, blockSel, dropQuantityMultiplier, ref bhHandling);
            return true;
        }


        private void AttemptVeinMine(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, BlockSelection blockSel, float dropQuantityMultiplier = 1)
        {
            if (world.Side != EnumAppSide.Server) return;
            // debug sanity checks

            Block block = world.BlockAccessor.GetBlock(blockSel.Position);
            string blockminedcode = block.Id == 0 ? "air" : block.Code.Path;

            IPlayer player = null;

            List<ItemStack> itemsToDrop = new List<ItemStack>(); // <String Code, Int Count>

            if (byEntity is EntityPlayer)
            {
                player = world.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            }
            if (player == null)
            {
                world.Logger.VerboseDebug("FlexibleTools VeinMine Behavior: isnt EntityPlayer; Player is null!");
                return;
            }
            ItemStack[] blockDrops;
            bool istree = false;
            string treeGroupCode = string.Empty;
            int savedLimit = _veinBlockLimit - 1; // 1 less as the block broken to trigger this should count.
            string blockToMatch = world.BlockAccessor.GetBlock(blockSel.Position).Code.Path; //.FirstCodePart();

            if (blockToMatch.Contains("log"))
            {
                istree = true;
                treeGroupCode = block.Attributes["treeFellingGroupCode"].AsString(null); // the type of wood, oak, birch, etc.
                // leaves are 0oak, 1oak, 2oak, etc, for reasons passing understanding
                if (treeGroupCode != null)
                   savedLimit *= 2;
                else
                    istree = false;
            }

            List<BlockPos> blocksToMine = new List<BlockPos>();
            List<BlockPos> blocksToCheck = new List<BlockPos>();

            blocksToCheck.Add(blockSel.Position);
            blocksToMine.Add(blockSel.Position);

            while (blocksToCheck.Count > 0 && blocksToMine.Count < savedLimit)
            {
                List<BlockPos> blockstoadd = new List<BlockPos>();

                foreach (BlockPos pos in blocksToCheck)
                {
                    BlockPos start = pos.AddCopy(-1, -1, -1);
                    BlockPos end = pos.AddCopy(1, 1, 1);
                    world.BlockAccessor.WalkBlocks(start, end, delegate (Block dblock, int x, int y, int z)
                    {
                        if (dblock.BlockId != 0)
                        {
                            BlockPos bcheck = new BlockPos(x, y, z, 0);
                            if (istree)
                            {
                                // we're cutting a tree down, check the treeFellingGroupCode 
                                // this should now get only logs...
                                if (dblock.Attributes != null && dblock.Attributes["treeFellingGroupCode"].Exists
                                    && dblock.Attributes["treeFellingGroupCode"].AsString().Contains(treeGroupCode)
                                    /*&& !dblock.Code.Path.Contains("leaves")*/)
                                {
                                    if (!blocksToMine.Contains(bcheck) && blocksToMine.Count < savedLimit)
                                    {
                                        blockstoadd.Add(bcheck);
                                        blocksToMine.Add(bcheck);
                                    }
                                }
                            }
                            else if (dblock.Code.Path.Contains(blockToMatch))
                            {
                                if (!blocksToMine.Contains(bcheck) && blocksToMine.Count < savedLimit)
                                {
                                    blockstoadd.Add(bcheck);
                                    blocksToMine.Add(bcheck);
                                }
                            }
                        }
                    }, false);
                }
                blocksToCheck.Clear();
                if (blockstoadd.Count > 0 && blocksToMine.Count < savedLimit) { blocksToCheck.AddRange(blockstoadd); }
                blockstoadd.Clear();
            }
            blocksToCheck.Clear();            

            if (blocksToMine.Count > 0)
            {
                foreach (BlockPos blockPos in blocksToMine)
                {
                    blockDrops = world.BlockAccessor.GetBlock(blockPos).GetDrops(world, blockPos, player, dropQuantityMultiplier);
                    if (blockDrops == null)
                    {
                        continue;
                    }
                    if (blockDrops.Length > 0)
                    {
                        foreach (ItemStack itemStack in blockDrops)
                        {
                            itemsToDrop.Add(itemStack);
                        }
                    }
                    world.BlockAccessor.SetBlock(0, blockPos); // we've got the drops, set to AIR
                    world.BlockAccessor.MarkBlockDirty(blockPos);
                    world.BlockAccessor.TriggerNeighbourBlockUpdate(blockPos);
                    //itemslot.Itemstack.Collectible.DamageItem(world, byEntity, itemslot, 1); // damage the pick like you mined the block
                    player.InventoryManager.ActiveHotbarSlot.Itemstack.Collectible.DamageItem(world, player.Entity, player.InventoryManager.ActiveHotbarSlot, 1);
                    if (player.InventoryManager.ActiveHotbarSlot.Itemstack == null ||
                        (player.InventoryManager.ActiveHotbarSlot.Itemstack.Item != null &&
                        player.InventoryManager.ActiveHotbarSlot.Itemstack.Item.GetBehavior<VeinMineBehavior>() == null))
                    {
                        break; // if the tool broke, get out of the loop as the object is now null.
                    }
                }
                foreach (ItemStack stack in itemsToDrop)
                {
                    // for all the drops, drop them on the ground at the players feet.
                    try
                    {
                        world.SpawnItemEntity(stack, blockSel.Position.ToVec3d()); // blockSel.Position.ToVec3d());
                    }
                    catch (Exception e)
                    {
                        world.Logger.Warning($"Exception while trying to process drops! {e}");
                        world.Logger.Warning($"Stacktrace: {e.StackTrace}");
                    }
                }
                blocksToMine.Clear();
                itemsToDrop.Clear();
            }
        }
    }
}
