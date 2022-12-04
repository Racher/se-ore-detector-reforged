using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.Gui;
using Sandbox.ModAPI;
using System;
using VRage.Game.Components;
using VRage.ModAPI;

namespace OreDetectorReforged
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    class Main : MySessionComponentBase
    {
        public const ushort storageMsgId = 54986;
        public const ushort configMsgId = 54987;
        public static readonly Guid guid = new Guid("88d1eb02574adb88cf18b124f7bbea10");

        public override void LoadData()
        {
            Config.LoadConfig();
            MyEntities.OnEntityCreate += OnEntityCreate;
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(storageMsgId, SyncModStorage.HandleMsg);
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(configMsgId, Config.HandleMsg);
        }

        protected override void UnloadData()
        {
            MyEntities.OnEntityCreate -= OnEntityCreate;
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(storageMsgId, SyncModStorage.HandleMsg);
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(configMsgId, Config.HandleMsg);
        }

        public override void UpdateAfterSimulation()
        {
            PlayerOreGps.UpdateGpss();
        }

        static void OnEntityCreate(IMyEntity entity)
        {
            SyncModStorage.DownloadStorage(entity);
            TerminalOreDetector.InitTerminalControls(entity);
            TerminalProgrammableBlock.InitTerminalControls(entity);
        }
    }
}
