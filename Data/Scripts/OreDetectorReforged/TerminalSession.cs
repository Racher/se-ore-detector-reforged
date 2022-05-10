using System;
using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Gui;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace OreDetectorReforged
{


	[MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
	class TerminalSession : MySessionComponentBase
	{
		const ushort msgHandlerId = 54986;
		static readonly Guid guid = new Guid("88d1eb02574adb88cf18b124f7bbea10");

		static T GetLocal<T>(IMyEntity entity)
		{
			try
			{
				return MyAPIGateway.Utilities.SerializeFromBinary<T>(GetLocalBytes(entity));
			}
			catch
			{
				return default(T);
			}
		}

		public static T GetLocalOrNew<T>(IMyEntity entity) where T : new()
		{
			var r = GetLocal<T>(entity);
			return r != null ? r : new T();
		}

		public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
		{
			MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(msgHandlerId, HandleMsg);
			MyEntities.OnEntityCreate += InitBlock;
		}

		protected override void UnloadData()
		{
			MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(msgHandlerId, HandleMsg);
			MyEntities.OnEntityCreate -= InitBlock;
		}

		static byte[] GetLocalBytes(IMyEntity entity)
		{
			string s = null;
			entity?.Storage?.TryGetValue(guid, out s);
			return s == null ? null : Convert.FromBase64String(s);
		}

		static void SetLocalBytes(IMyEntity entity, byte[] v)
		{
			var s = Convert.ToBase64String(v);
			if (entity.Storage == null)
				entity.Storage = new MyModStorageComponent();
			entity.Storage[guid] = s;
		}

		static byte[] PrependData(long eid, byte[] payload)
		{
			var data = BitConverter.GetBytes(eid);
			Array.Resize(ref data, 8 + payload.Length);
			Array.Copy(payload, 0, data, 8, payload.Length);
			return data;
		}

		static void HandleMsg(ushort msgId, byte[] data, ulong sender, bool reliable)
		{
			var e = MyAPIGateway.Entities.GetEntityById(BitConverter.ToInt64(data, 0));
			if (e == null)
				return;
			else if (data.Length == 8)
			{
				var payload = GetLocalBytes(e);
				if (payload != null)
					MyAPIGateway.Multiplayer.SendMessageTo(msgHandlerId, PrependData(e.EntityId, payload), sender);
			}
			else
			{
				var payload = new byte[data.Length - 8];
				Array.Copy(data, 8, payload, 0, payload.Length);
				SetLocalBytes(e, payload);
			}
		}

		static void SetAndSendMessageToOthers<T>(IMyEntity e, T v)
		{
			SetLocalBytes(e, MyAPIGateway.Utilities.SerializeToBinary(v));
			MyAPIGateway.Multiplayer.SendMessageToOthers(msgHandlerId, PrependData(e.EntityId, MyAPIGateway.Utilities.SerializeToBinary(v)));
		}

		static void InitBlock(IMyEntity entity)
		{
			if ((entity as IMyOreDetector) != null)
			{
				InitIMyOreDetector?.Invoke();
				DownloadStorage(entity);
			}
			if ((entity as IMyProgrammableBlock) != null)
				InitIMyProgrammableBlock?.Invoke();
		}

		static void DownloadStorage(IMyEntity entity)
		{
			if (!MyAPIGateway.Multiplayer.IsServer)
				MyAPIGateway.Multiplayer.SendMessageToServer(msgHandlerId, BitConverter.GetBytes(entity.EntityId));
		}

		static Action InitIMyOreDetector = () =>
		{
			InitIMyOreDetector = null;
			{
				const string id = "Reforged: Separator";
				var p = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyOreDetector>(id);
				MyAPIGateway.TerminalControls.AddControl<IMyOreDetector>(p);
			}
			{
				const string id = "Reforged: GPSColor";
				var p = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlColor, IMyOreDetector>(id);
				p.Title = MyStringId.GetOrCompute(id);
				p.Getter = (e) => GetLocalOrNew<DetectorBlockStorage>(e).color;
				p.Setter = (e, v) => SetAndSendMessageToOthers(e, new DetectorBlockStorage(GetLocalOrNew<DetectorBlockStorage>(e)) { color = v });
				p.Tooltip = MyStringId.GetOrCompute("The same color for all mod generated ore gps");
				MyAPIGateway.TerminalControls.AddControl<IMyOreDetector>(p);
			}
			{
				const string id = "Reforged: Range";
				var p = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyOreDetector>(id);
				p.Title = MyStringId.GetOrCompute(id);
				p.Getter = (e) => GetLocalOrNew<DetectorBlockStorage>(e).range;
				p.Setter = (e, v) => SetAndSendMessageToOthers(e, new DetectorBlockStorage(GetLocalOrNew<DetectorBlockStorage>(e)) { range = v });
				p.SetLogLimits(1, 1e9f);
				p.Writer = (e, sb) =>
				{
					sb.Append(GetLocalOrNew<DetectorBlockStorage>(e).range);
					sb.Append(" m");
				};
				MyAPIGateway.TerminalControls.AddControl<IMyOreDetector>(p);
			}
			{
				const string id = "Reforged: Count";
				var p = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyOreDetector>(id);
				p.Title = MyStringId.GetOrCompute(id);
				p.Getter = (e) => GetLocalOrNew<DetectorBlockStorage>(e).count;
				p.Setter = (e, v) => SetAndSendMessageToOthers(e, new DetectorBlockStorage(GetLocalOrNew<DetectorBlockStorage>(e)) { count = (int)v });
				p.SetLogLimits(1, 100);
				p.Writer = (e, sb) =>
				{
					sb.Append(GetLocalOrNew<DetectorBlockStorage>(e).count);
					sb.Append(" each");
				};
				p.Tooltip = MyStringId.GetOrCompute("Show nearest N of each mined ore");
				MyAPIGateway.TerminalControls.AddControl<IMyOreDetector>(p);
			}
			{
				const string id = "Reforged: RefreshRate";
				var p = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyOreDetector>(id);
				p.Title = MyStringId.GetOrCompute(id);
				p.Getter = (e) => 1f / GetLocalOrNew<DetectorBlockStorage>(e).period;
				p.Setter = (e, v) => SetAndSendMessageToOthers(e, new DetectorBlockStorage(GetLocalOrNew<DetectorBlockStorage>(e)) { period = (int)Math.Ceiling(1 / v) });
				p.SetLogLimits(0.001f, 1);
				p.Writer = (e, sb) =>
				{
					sb.Append("1/");
					sb.Append(GetLocalOrNew<DetectorBlockStorage>(e).period);
					sb.Append(" update");
				};
				p.Tooltip = MyStringId.GetOrCompute("Search and update gps every Nth update");
				MyAPIGateway.TerminalControls.AddControl<IMyOreDetector>(p);
			}
			{
				string[] oreTypeNames;
				MyDefinitionManager.Static.GetOreTypeNames(out oreTypeNames);
				const string id = "Reforged: Whitelist";
				var p = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, IMyOreDetector>(id);
				p.Title = MyStringId.GetOrCompute(id);
				p.VisibleRowsCount = 10;
				p.Multiselect = true;
				p.ListContent = (e, ls, ss) =>
				{
					var valid = PlanetMatHelper.GetGeneratedOres() | DetectorPageNotPlanet.generatedOres.Get();
					var selected = GetLocalOrNew<DetectorBlockStorage>(e).whitelist;
					for (var o = 0; o < oreTypeNames.Length; ++o)
					{
						if ((valid & 1ul << o) == 0)
							continue;
						var l = new MyTerminalControlListBoxItem(MyStringId.GetOrCompute(oreTypeNames[o]), new MyStringId(), o);
						ls.Add(l);
						if ((selected & (1ul << o)) != 0)
							ss.Add(l);
					}
				};
				p.ItemSelected = (e, ss) =>
				{
					var v = GetLocalOrNew<DetectorBlockStorage>(e);
					v.whitelist = 0;
					foreach (var l in ss)
						v.whitelist |= 1ul << (int)l.UserData;
					SetAndSendMessageToOthers(e, v);
				};
				MyAPIGateway.TerminalControls.AddControl<IMyOreDetector>(p);
			}
			{
				const string id = "Ores";
				var p = MyAPIGateway.TerminalControls.CreateProperty<Dictionary<string, Vector3D>, IMyOreDetector>(id);
				p.Getter = (e) =>
				{
					var comp = e.Components.Get<LegacyOresComponent>();
					if (comp == null)
						e.Components.Add(comp = new LegacyOresComponent());
					string[] oreTypeNames;
					MyDefinitionManager.Static.GetOreTypeNames(out oreTypeNames);
					var valid = PlanetMatHelper.GetGeneratedOres() | DetectorPageNotPlanet.generatedOres.Get();
					var ores = comp.ores;
					for (var o = 0; o < oreTypeNames.Length; ++o)
					{
						if ((valid & 1ul << o) == 0)
							continue;
						var ore = oreTypeNames[o];
						DetectorServer.Add(new SearchTask(new BoundingSphereD(e.GetPosition(), 3e4), ore, 1, (vs) =>
						{
							if (vs.Count > 0)
								ores[ore] = vs[0];
						}));
					}
					return new Dictionary<string, Vector3D>(comp.ores);
				};
				MyAPIGateway.TerminalControls.AddControl<IMyOreDetector>(p);
			}
		};

		class LegacyOresComponent : MyComponentBase
		{
			public readonly Dictionary<string, Vector3D> ores = new Dictionary<string, Vector3D>();
		}

		static Action InitIMyProgrammableBlock = () =>
		{
			InitIMyProgrammableBlock = null;
			{
				const string id = "ReforgedDetectN";
				var p = MyAPIGateway.TerminalControls.CreateProperty<ValueTuple<BoundingSphereD, string, int, Action<List<Vector3D>>>, IMyProgrammableBlock>(id);
				p.Setter = (e, v) => DetectorServer.Add(new SearchTask(v.Item1, v.Item2, v.Item3, (vs) =>
				{
					try
					{
						if (!e.Closed)
							v.Item4(vs);
					}
					catch { }
				}));
				MyAPIGateway.TerminalControls.AddControl<IMyProgrammableBlock>(p);
			}
		};
	}
}
