using System;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;

namespace OreDetectorReforged
{
    class ListenerOnEntityCreate : IDisposable
    {
        private readonly Action<MyEntity> action;

        public ListenerOnEntityCreate(Action<MyEntity> action)
        {
            this.action = action;
            MyEntities.OnEntityCreate += action;
        }

        public void Dispose()
        {
            MyEntities.OnEntityCreate -= action;
        }
    }

    class ListenerMultiplayer : IDisposable
    {
        private readonly ushort id;
        private readonly Action<ushort, byte[], ulong, bool> action;

        public ListenerMultiplayer(ushort id, Action<ushort, byte[], ulong, bool> action)
        {
            this.action = action;
            this.id = id;
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(id, action);
        }

        public void Dispose()
        {
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(id, action);
        }
    }

    class ListenerMod : IDisposable
    {
        private readonly long id;
        private readonly Action<object> action;

        public ListenerMod(long id, Action<object> action)
        {
            this.id = id;
            this.action = action;
            MyAPIGateway.Utilities.RegisterMessageHandler(id, action);
        }

        public void Dispose()
        {
            MyAPIGateway.Utilities.UnregisterMessageHandler(id, action);
        }
    }
}
