using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Edelstein.Network.Packets;
using Edelstein.Provider.Templates.Field;
using MoreLinq.Extensions;

namespace Edelstein.Service.Game.Fields
{
    public class Field : IField
    {
        public int ID => Template.ID;
        public FieldTemplate Template { get; }

        private readonly IDictionary<FieldObjType, IFieldPool> _pools;

        public Field(FieldTemplate template)
        {
            Template = template;
            _pools = new Dictionary<FieldObjType, IFieldPool>();
        }

        public Task Enter(IFieldObj obj) => Enter(obj, null);
        public Task Leave(IFieldObj obj) => Leave(obj, null);

        public IFieldObj GetObject(int id)
            => GetObjects().FirstOrDefault(o => o.ID == id);

        public T GetObject<T>(int id) where T : IFieldObj
            => GetObjects().OfType<T>().FirstOrDefault(o => o.ID == id);

        public IEnumerable<IFieldObj> GetObjects()
            => _pools.Values.SelectMany(p => p.GetObjects());

        public IEnumerable<T> GetObjects<T>() where T : IFieldObj
            => GetObjects().OfType<T>();

        public IFieldPool GetPool(FieldObjType type)
        {
            if (!_pools.ContainsKey(type))
                _pools[type] = new FieldObjPool();
            return _pools[type];
        }

        public Task Enter(IFieldObj obj, byte portal, Func<IPacket> getEnterPacket = null)
        {
            throw new NotImplementedException();
        }

        public Task Enter(IFieldObj obj, string portal, Func<IPacket> getEnterPacket = null)
        {
            throw new NotImplementedException();
        }

        public async Task Enter(IFieldObj obj, Func<IPacket> getEnterPacket = null)
        {
            var pool = GetPool(obj.Type);

            obj.Field?.Leave(obj);
            obj.Field = this;
            await pool.Enter(obj);

            if (obj is IFieldUser user)
            {
                var portal = Template.Portals.Values.FirstOrDefault(p => p.ID == user.Character.FieldPortal) ??
                             Template.Portals.Values.First(p => p.Type == FieldPortalType.Spawn);

                user.ID = user.Character.ID;
                user.Character.FieldID = ID;
                user.Position = portal.Position;
                user.Foothold = (short) (portal.Type != FieldPortalType.Spawn
                    ? Template.Footholds.Values
                        .Where(f => f.X1 <= portal.Position.X && f.X2 >= portal.Position.X)
                        .First(f => f.X1 < f.X2).ID
                    : 0);

                await user.SendPacket(user.GetSetFieldPacket());
                await BroadcastPacket(user, getEnterPacket?.Invoke() ?? user.GetEnterFieldPacket());

                if (!user.IsInstantiated) user.IsInstantiated = true;

                GetObjects()
                    .Where(o => o != user)
                    .ForEach(o => user.SendPacket(o.GetEnterFieldPacket()));
            }
            else await BroadcastPacket(getEnterPacket?.Invoke() ?? obj.GetEnterFieldPacket());

            UpdateControlledObjects();
        }

        public async Task Leave(IFieldObj obj, Func<IPacket> getLeavePacket = null)
        {
            if (obj is IFieldUser user)
            {
                user.Dispose();
                await BroadcastPacket(user, user.GetLeaveFieldPacket());
            }
            else await BroadcastPacket(getLeavePacket?.Invoke() ?? obj.GetLeaveFieldPacket());

            await GetPool(obj.Type).Leave(obj);
            UpdateControlledObjects();
        }

        public Task BroadcastPacket(IPacket packet)
            => BroadcastPacket(null, packet);

        public Task BroadcastPacket(IFieldObj source, IPacket packet)
            => Task.WhenAll(
                GetObjects<IFieldUser>()
                    .Where(u => u != source)
                    .Select(u => u.SendPacket(packet))
            );

        public void UpdateControlledObjects()
        {
            var controllers = GetObjects().OfType<IFieldUser>().Shuffle().ToList();
            var controlled = GetObjects().OfType<AbstractFieldControlledLife>().ToList();

            controlled
                .Where(
                    c => c.Controller == null ||
                         !controllers.Contains(c.Controller))
                .ForEach(c => c.SetController(controllers.FirstOrDefault()));
        }
    }
}