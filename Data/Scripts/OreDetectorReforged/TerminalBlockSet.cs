using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.ModAPI;
using Sandbox.Game.Entities;
using System.Linq;
using System;
using VRage.Utils;

namespace OreDetectorReforged
{
    class TerminalBlockSet<T> : MySessionComponentBase where T : class, IMyTerminalBlock
    {
        static readonly object m_addLock = new object();
        static readonly object m_delLock = new object();

        public static IEnumerable<T> Get => UpdateAndGet();

        static readonly HashSet<T> es = new HashSet<T>();
        static readonly HashSet<T> esAdd = new HashSet<T>();
        static readonly HashSet<T> esDel = new HashSet<T>();

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

        static IEnumerable<T> UpdateAndGet()
        {
            lock(m_addLock)
            {
              es.UnionWith(esAdd);
              esAdd.Clear();
            }

            lock(m_delLock)
            {
              es.RemoveWhere(x => esDel.Contains(x));
              esDel.Clear();
            }
            return es.Where(e => !e.Closed);
        }

        static void OnEntityCreate(IMyEntity e)
        {
            if (e is T)
                lock (m_addLock)
                {
                    esAdd.Add(e as T);
                }
        }

        static void OnEntityAdd(IMyEntity e)
        {
            if (e is T)
                lock (m_addLock)
                {
                    esAdd.Add(e as T);
                }
        }

        static void OnEntityRemove(IMyEntity e)
        {
            if (e is T)
                lock (m_delLock)
                {
                    esDel.Remove(e as T);
                }
        }

        static void OnEntityDelete(IMyEntity e)
        {
            if (e is T)
                lock (m_delLock)
                {
                    esDel.Remove(e as T);
                }
        }
    }
}
