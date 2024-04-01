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
using System.Text;

namespace FlexibleTools
{
    public class TorcMagnetItem : Item
    {
        public EnumCharacterDressType DressType { get; private set; }
        public bool IsArmor = false;        

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            string value = this.Attributes["clothscategory"].AsString(null);
            EnumCharacterDressType dresstype = EnumCharacterDressType.Unknown;
            Enum.TryParse<EnumCharacterDressType>(value, true, out dresstype);
            this.DressType = dresstype;
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            JsonObject attributes = itemstack.Collectible.Attributes;
            if (attributes == null)
                return;

            Dictionary<string, MultiTextureMeshRef> orCreate = ObjectCacheUtil.GetOrCreate<Dictionary<string, MultiTextureMeshRef>>(capi, "armorMeshRefs", () => new Dictionary<string, MultiTextureMeshRef>());
            string key = "armorModelRef-" + itemstack.Collectible.Code.ToString();
            if (!orCreate.TryGetValue(key, out renderinfo.ModelRef))
            {
                ITexPositionSource texSource = capi.Tesselator.GetTextureSource(itemstack.Item, false);
                MeshData mesh = this.genMeshRef(capi, itemstack, renderinfo);
                renderinfo.ModelRef = (orCreate[key] = ((mesh == null) ? renderinfo.ModelRef : capi.Render.UploadMultiTextureMesh(mesh)));
            }

            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
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

        private MeshData genMeshRef(ICoreClientAPI capi, ItemStack itemstack, ItemRenderInfo renderinfo)
        {
            //MeshRef modelRef = renderinfo.ModelRef;
            JsonObject attributes = itemstack.Collectible.Attributes;
            EntityProperties entityType = capi.World.GetEntityType(new AssetLocation("player"));
            Shape loadedShape = entityType.Client.LoadedShape;
            AssetLocation @base = entityType.Client.Shape.Base;
            Shape shape = new Shape
            {
                Elements = loadedShape.CloneElements(),
                Animations = loadedShape.Animations,
                AnimationsByCrc32 = loadedShape.AnimationsByCrc32,
                AttachmentPointsByCode = loadedShape.AttachmentPointsByCode,
                JointsById = loadedShape.JointsById,
                TextureWidth = loadedShape.TextureWidth,
                TextureHeight = loadedShape.TextureHeight,
                Textures = null
            };
            CompositeShape compositeShape = (!attributes["attachShape"].Exists) ? ((itemstack.Class == EnumItemClass.Item) ? itemstack.Item.Shape : itemstack.Block.Shape) : attributes["attachShape"].AsObject<CompositeShape>(null, itemstack.Collectible.Code.Domain);
            if (compositeShape == null)
            {
                capi.World.Logger.Warning("Entity armor {0} {1} does not define a shape through either the shape property or the attachShape Attribute. Armor pieces will be invisible.", new object[]
                {
                    itemstack.Class,
                    itemstack.Collectible.Code
                });
                return null;
            }
            AssetLocation assetLocation = compositeShape.Base.CopyWithPath("shapes/" + compositeShape.Base.Path + ".json");
            IAsset asset = capi.Assets.TryGet(assetLocation, true);
            if (asset == null)
            {
                capi.World.Logger.Warning("Entity wearable shape {0} defined in {1} {2} not found, was supposed to be at {3}. Armor piece will be invisible.", new object[]
                {
                    compositeShape.Base,
                    itemstack.Class,
                    itemstack.Collectible.Code,
                    assetLocation
                });
                return null;
            }
            Shape shape2;
            try
            {
                shape2 = asset.ToObject<Shape>(null);
            }
            catch (Exception ex)
            {
                capi.World.Logger.Warning("Exception thrown when trying to load entity armor shape {0} defined in {1} {2}. Armor piece will be invisible. Exception: {3}", new object[]
                {
                    compositeShape.Base,
                    itemstack.Class,
                    itemstack.Collectible.Code,
                    ex
                });
                return null;
            }
            shape.Textures = shape2.Textures;
            if (shape2.Textures.Count > 0 && shape2.TextureSizes.Count == 0)
            {
                foreach (KeyValuePair<string, AssetLocation> keyValuePair in shape2.Textures)
                {
                    shape2.TextureSizes.Add(keyValuePair.Key, new int[]
                    {
                        shape2.TextureWidth,
                        shape2.TextureHeight
                    });
                }
            }
            foreach (KeyValuePair<string, int[]> keyValuePair2 in shape2.TextureSizes)
            {
                shape.TextureSizes[keyValuePair2.Key] = keyValuePair2.Value;
            }
            foreach (ShapeElement shapeElement in shape2.Elements)
            {
                if (shapeElement.StepParentName != null)
                {
                    ShapeElement elementByName = shape.GetElementByName(shapeElement.StepParentName, StringComparison.InvariantCultureIgnoreCase);
                    if (elementByName == null)
                    {
                        capi.World.Logger.Warning("Entity wearable shape {0} defined in {1} {2} requires step parent element with name {3}, but no such element was found in shape {3}. Will not be visible.", new object[]
                        {
                            compositeShape.Base,
                            itemstack.Class,
                            itemstack.Collectible.Code,
                            shapeElement.StepParentName,
                            @base
                        });
                    }
                    else if (elementByName.Children == null)
                    {
                        elementByName.Children = new ShapeElement[]
                        {
                            shapeElement
                        };
                    }
                    else
                    {
                        elementByName.Children = elementByName.Children.Append(shapeElement);
                    }
                }
                else
                {
                    capi.World.Logger.Warning("Entity wearable shape element {0} in shape {1} defined in {2} {3} did not define a step parent element. Will not be visible.", new object[]
                    {
                        shapeElement.Name,
                        compositeShape.Base,
                        itemstack.Class,
                        itemstack.Collectible.Code
                    });
                }
            }
            ITexPositionSource textureSource = capi.Tesselator.GetTextureSource(itemstack.Item, false);
            MeshData data;
            capi.Tesselator.TesselateShapeWithJointIds("entity", shape, out data, textureSource, new Vec3f(), null, null);
            return data;
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

            this.tmp.Set(this.entity.ServerPos.X, this.entity.ServerPos.Y + (double)this.entity.SelectionBox.Y1 + (double)(this.entity.SelectionBox.Y2 / 2f), this.entity.ServerPos.Z);
            Entity[] entitiesAround = this.entity.World.GetEntitiesAround(this.tmp, config.MagnetPickupRadius, config.MagnetPickupRadius, new ActionConsumable<Entity>(this.entityMatcher));
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
