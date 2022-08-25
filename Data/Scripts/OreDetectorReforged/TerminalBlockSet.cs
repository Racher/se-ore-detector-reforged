using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.ModAPI;
using Sandbox.Game.Entities;

namespace OreDetectorReforged
{
    class TerminalBlockSet<T> : MySessionComponentBase where T : class, IMyTerminalBlock
    {
        public static IEnumerable<T> Get => es;

        static readonly HashSet<T> es = new HashSet<T>();

        public override void LoadData()
        {
            MyEntities.OnEntityCreate += OnEntityCreate;
            MyEntities.OnEntityDelete += OnEntityDelete;
        }

        protected override void UnloadData()
        {
            MyEntities.OnEntityCreate -= OnEntityCreate;
            MyEntities.OnEntityDelete -= OnEntityDelete;
        }

        static void OnEntityCreate(IMyEntity e)
        {
            if (e is T)
                es.Add(e as T);
        }

        static void OnEntityDelete(IMyEntity e)
        {
            if (e is T)
                es.Remove(e as T);
        }
    }
}
