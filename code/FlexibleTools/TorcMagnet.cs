using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;
using System.Text;

namespace FlexibleTools
{
    public class TorcMagnetItem : ItemWearable
    {
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            bool enabled = (bool)inSlot?.Itemstack?.Attributes?.GetBool("enabled");
            dsc.AppendLine(enabled ? "Enabled" : "Disabled");
        }

        public override string GetHeldItemName(ItemStack itemStack)
        {
            if (itemStack != null)
            {
                string basename = base.GetHeldItemName(itemStack);
                bool enabled = itemStack.Attributes.GetBool("enabled");
                basename += " " + (enabled ? "Enabled":"Disabled");
                return basename;
            }
            return base.GetHeldItemName(itemStack);
        }
    }

    /// <summary>
    /// Handles the magnetic behavior while the magnet is worn.
    /// </summary>
    public class TorcMagnetBehavior : EntityBehavior
    {
        private FlexibleToolsConfig config;
        public TorcMagnetBehavior(Entity entity) : base(entity)
        {
            if (entity != null && entity.Api.Side == EnumAppSide.Server)
            {
                config = this.entity.Api.ModLoader.GetModSystem<FlexibleToolsMod>(true)?.ToolsConfig;
            }
        }

        public override void OnGameTick(float deltaTime)
        {
            // be VERY careful in here...
            if (this.entity.State != EnumEntityState.Active || !this.entity.Alive)
            {
                return;
            }
            EntityPlayer entityPlayer = this.entity as EntityPlayer;
            IPlayer player = (entityPlayer != null) ? entityPlayer.Player : null;
            IServerPlayer serverPlayer = player as IServerPlayer;
            if (serverPlayer != null && serverPlayer.ItemCollectMode == 1)
            {
                EntityAgent entityAgent = this.entity as EntityAgent;
                if (entityAgent != null && !entityAgent.Controls.Sneak)
                {
                    return;
                }
            }
            if (this.entity.IsActivityRunning("invulnerable"))
            {
                this.waitTicks = 3;
                return;
            }
            int num = this.waitTicks;
            this.waitTicks = num - 1;
            if  (num > 0)
            {
                return;
            }
            if (player != null && player.WorldData.CurrentGameMode == EnumGameMode.Spectator)
            {
                return;
            }
            IInventory ownInventory = player.InventoryManager.GetOwnInventory("character");            
            if (ownInventory[(int)EnumCharacterDressType.Neck].Itemstack == null
                || ownInventory[(int)EnumCharacterDressType.Neck].Itemstack.Item.FirstCodePart() != "torcmagnet")
            {
                return;
            }
            ItemStack magnet = ownInventory[(int)EnumCharacterDressType.Neck].Itemstack;
            bool enabled = magnet.Attributes.GetAsBool("enabled");
            
            if (!enabled) return;
            Entity[] entitiesAround;
            this.tmp.Set(this.entity.ServerPos.X, this.entity.ServerPos.Y + (double)this.entity.SelectionBox.Y1 + (double)(this.entity.SelectionBox.Y2 / 2f), this.entity.ServerPos.Z);
            try
            {
                entitiesAround = this.entity.World.GetEntitiesAround(this.tmp, config.MagnetPickupRadius, config.MagnetPickupRadius, new ActionConsumable<Entity>(this.entityMatcher));
            }
            catch (Exception e)
            {
                this.entity.Api.Logger.Error("TorcMagnet Error: " + e);
                return;
            }
            if (entitiesAround.Length == 0)
            {
                this.unconsumedDeltaTime = 0f;
                return;
            }
            deltaTime = Math.Min(1f, deltaTime + this.unconsumedDeltaTime);
            while(deltaTime - 1f / this.itemsPerSecond > 0f)
            {
                Entity entity = null;
                int i;
                for (i = 0; i < entitiesAround.Length; i++)
                {
                    // finally we can 'pull' items toward the player...
                    if (entitiesAround[i] != null)
                    {
                        entity = entitiesAround[i];                        
                    }
                    if (entity != null)
                    {                        
                        entity.ServerPos.SetPos(entityPlayer.Pos);
                    }
                    if (i > this.itemsPerSecond) break;
                }
                deltaTime -= 1f / this.itemsPerSecond;
            }
            this.unconsumedDeltaTime = deltaTime;
            //base.OnGameTick(deltaTime);
        }

        private bool entityMatcher(Entity foundEntity)
        {            
            if (foundEntity is EntityItem)
            {
                if (config.MagnetBlackList.Count > 0)
                {
                    if (config.MagnetBlackList.Contains((foundEntity as EntityItem).Itemstack.Collectible.Code.Path))
                    {
                        return false;
                    }
                }
                if (foundEntity == null || entity == null) return false;
                return foundEntity.CanCollect(this.entity);
            }

            return false;
        }

        public override string PropertyName()
        {
            return "vacuumitems";
        }
        private int waitTicks;
        private Vec3d tmp = new Vec3d();
        private float radius = 5f;
        private float unconsumedDeltaTime;
        private float itemsPerSecond = 23f; // this is limited because the player is limited on how many things it can pick up per second.
    }
}
