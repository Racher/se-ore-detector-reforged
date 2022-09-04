using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.ModAPI;
using Sandbox.Game.Entities;
using System.Linq;
using System;
using VRage.Utils;
using System.Collections.Concurrent;

namespace OreDetectorReforged
{
    class TerminalBlockSet<T> : MySessionComponentBase where T : class, IMyTerminalBlock
    {
        public static IEnumerable<T> Get => es.Where(e => !e.Key.Closed).Select(e => e.Key);

        static readonly ConcurrentDictionary<T, byte> es = new ConcurrentDictionary<T, byte>();

        public override void LoadData()
        {
            MyEntities.OnEntityCreate += OnEntityCreate;
            MyEntities.OnEntityAdd += OnEntityAdd;
            MyEntities.OnEntityRemove += OnEntityRemove;
            MyEntities.OnEntityDelete += OnEntityDelete;
        }

        protected override void UnloadData()
        {
            MyEntities.OnEntityCreate -= OnEntityCreate;
            MyEntities.OnEntityAdd -= OnEntityAdd;
            MyEntities.OnEntityRemove -= OnEntityRemove;
            MyEntities.OnEntityDelete -= OnEntityDelete;
        }

        static void OnEntityCreate(IMyEntity e)
        {
            if (e is T)
                es.TryAdd(e as T, 0);
        }

        static void OnEntityAdd(IMyEntity e)
        {
            if (e is T)
                es.TryAdd(e as T, 0);
        }

        static void OnEntityRemove(IMyEntity e)
        {
            if (e is T)
                es.Remove(e as T);
        }

        static void OnEntityDelete(IMyEntity e)
        {
            if (e is T)
                es.Remove(e as T);
        }
    }
}
