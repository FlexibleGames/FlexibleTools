using System;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace FlexibleTools
{
    public class FlexibleToolsMod : ModSystem
    {
        ICoreAPI api;
        ICoreServerAPI sapi;
        ICoreClientAPI capi;       

        IClientNetworkChannel clientChannel;
        IServerNetworkChannel serverChannel;
        
        public static string ConfigFileName = "flexibletoolsconfig.json";

        private FlexibleToolsConfig _toolsconfig;
        public FlexibleToolsConfig ToolsConfig
        {   
            get
            {
                return _toolsconfig;
            }
        }


        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            this.api = api;

            _toolsconfig = ReadConfig(api);

            this.api.RegisterItemClass("DiamondScytheItem", typeof(DiamondScytheItem));
            this.api.RegisterItemClass("DiamondShearsItem", typeof(DiamondShearsItem));
            this.api.RegisterItemClass("ItemWorldEater", typeof(WorldEaterItem));
            this.api.RegisterItemClass("RockSnifferItem", typeof(RockSnifferItem));
            this.api.RegisterItemClass("TorcMagnetItem", typeof(TorcMagnetItem));
            this.api.RegisterEntityBehaviorClass("vacuumitems", typeof(TorcMagnetBehavior));
            
            // the only thing you can't disable is the diamond pick
            api.World.Config.SetBool("FlexibleTools_Scythe_Enable", ToolsConfig.EnableScythe);
            api.World.Config.SetBool("FlexibleTools_Shears_Enable", ToolsConfig.EnableShears);

            api.World.Config.SetBool("FlexibleTools_Magnet_Enable", ToolsConfig.EnableMagnet);
            api.World.Config.SetBool("FlexibleTools_Sniffer_Enable", ToolsConfig.EnableSniffer);
            api.World.Config.SetBool("FlexibleTools_Eater_Enable", ToolsConfig.EnableEater);
        }        

        public static FlexibleToolsConfig ReadConfig(ICoreAPI api)
        {
            FlexibleToolsConfig config;
            try
            {
                config = api.LoadModConfig<FlexibleToolsConfig>(ConfigFileName);
                if (config == null)
                {
                    // config doesn't exist, make it.
                    config = new FlexibleToolsConfig();
                    config.MagnetBlackList.Add("gear-temporal");
                    api.StoreModConfig<FlexibleToolsConfig>(config, ConfigFileName);
                    return config;
                }
                else
                {
                    api.StoreModConfig<FlexibleToolsConfig>(new FlexibleToolsConfig(config), ConfigFileName);
                    return api.LoadModConfig<FlexibleToolsConfig>(ConfigFileName);
                }
            }
            catch (Exception)
            {
                api.Logger.Warning("FlexibleTools: Config Exception, possibly has a typo. Rebuilding...");
                config = new FlexibleToolsConfig();
                config.MagnetBlackList.Add("gear-temporal");
                api.StoreModConfig<FlexibleToolsConfig>(config, ConfigFileName);
                return config;
            }
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            sapi = api;            

            serverChannel = sapi.Network.RegisterChannel("flexibletools").RegisterMessageType(typeof(bool))
                .SetMessageHandler<bool>(new NetworkClientMessageHandler<bool>(OnMagnetUpdate));

            sapi.Event.PlayerNowPlaying += AddMagnetBehavior;
        }
        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            capi = api;
            capi.Input.RegisterHotKey("fgmagnet", Lang.Get("gui-toggle-hotkey"), GlKeys.Y, HotkeyType.CharacterControls, false, false, false);
            capi.Input.SetHotKeyHandler("fgmagnet", OnToggleMagnet);

            clientChannel = capi.Network.RegisterChannel("flexibletools").RegisterMessageType(typeof(bool));
        }

        private void OnMagnetUpdate(IServerPlayer fromPlayer, bool packet)
        {
            ItemStack magnet = fromPlayer.InventoryManager.GetOwnInventory("character")[(int)EnumCharacterDressType.Neck].Itemstack;
            if (magnet != null)
            {
                bool enabled = magnet.Attributes.GetAsBool("enabled");
                magnet.Attributes.SetBool("enabled", !enabled);
                fromPlayer.BroadcastPlayerData(true);
            }
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
        private bool OnToggleMagnet(KeyCombination t1)
        {
            IInventory ownInventory = capi.World.Player.InventoryManager.GetOwnInventory("character");
            if (!ownInventory[(int)EnumCharacterDressType.Neck].Empty 
                && ownInventory[(int)EnumCharacterDressType.Neck].Itemstack.Item.FirstCodePart() == "torcmagnet")
            {
                ItemStack magnet = capi.World.Player.InventoryManager.GetOwnInventory("character")[(int)EnumCharacterDressType.Neck].Itemstack;
                bool enabled = magnet.Attributes.GetAsBool("enabled");
                clientChannel.SendPacket<bool>(!enabled);
                capi.World.PlaySoundAt(new AssetLocation("game:sounds/effect/latch"), capi.World.Player, null, false, 8, 1);
            }
            return true;
        }
    }
}
