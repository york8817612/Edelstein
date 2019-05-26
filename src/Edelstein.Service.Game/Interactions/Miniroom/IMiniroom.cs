using System.Threading.Tasks;
using Edelstein.Network.Packets;
using Edelstein.Service.Game.Fields.Objects.User;

namespace Edelstein.Service.Game.Interactions.Miniroom
{
    public interface IMiniroom
    {
        MiniroomType Type { get; }

        Task<MiniroomEnterResult> Enter(FieldUser user);
        Task Leave(FieldUser user, MiniroomLeaveResult leaveResult = MiniroomLeaveResult.UserRequest);

        Task BroadcastPacket(IPacket packet);
        Task BroadcastPacket(FieldUser source, IPacket packet);
    }
}