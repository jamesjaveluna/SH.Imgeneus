﻿using Imgeneus.Network.PacketProcessor;

namespace Imgeneus.Network.Packets.Game
{
    public record GuildGetEtinPacket : IPacketDeserializer
    {
        public void Deserialize(ImgeneusPacket packetStream)
        {
        }
    }
}
