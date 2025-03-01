﻿using Imgeneus.Network.PacketProcessor;

namespace Imgeneus.Network.Packets.Game
{
    public record RaidInvitePacket : IPacketDeserializer
    {
        public uint CharacterId { get; private set; }

        public void Deserialize(ImgeneusPacket packetStream)
        {
            CharacterId = packetStream.Read<uint>();
        }
    }
}
