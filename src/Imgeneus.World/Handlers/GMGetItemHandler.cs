﻿using Imgeneus.Database.Preload;
using Imgeneus.Network.Packets;
using Imgeneus.Network.Packets.Game;
using Imgeneus.World.Game.Inventory;
using Imgeneus.World.Game.Player;
using Imgeneus.World.Game.Session;
using Imgeneus.World.Packets;
using Sylver.HandlerInvoker.Attributes;
using System.Threading.Tasks;

namespace Imgeneus.World.Handlers
{
    [Handler]
    public class GMGetItemHandler : BaseHandler
    {
        private readonly IDatabasePreloader _databasePreloader;
        private readonly IInventoryManager _inventoryManager;

        public GMGetItemHandler(IGamePacketFactory packetFactory, IGameSession gameSession, IDatabasePreloader databasePreloader, IInventoryManager inventoryManager) : base(packetFactory, gameSession)
        {
            _databasePreloader = databasePreloader;
            _inventoryManager = inventoryManager;
        }

        [HandlerAction(PacketType.GM_COMMAND_GET_ITEM)]
        public async Task Handle(WorldClient client, GMGetItemPacket packet)
        {
            if (!_gameSession.IsAdmin)
                return;

            var itemCount = packet.Count;
            var ok = false;

            while (itemCount > 0)
            {
                var newItem = new Item(_databasePreloader, packet.Type, packet.TypeId, itemCount);

                var item = _inventoryManager.AddItem(newItem);
                if (item != null)
                {
                    ok = true;
                }

                itemCount -= newItem.Count;
            }

            if (ok)
                _packetFactory.SendGmCommandSuccess(client);
            else
                _packetFactory.SendGmCommandError(client, PacketType.GM_COMMAND_GET_ITEM);
        }
    }
}
