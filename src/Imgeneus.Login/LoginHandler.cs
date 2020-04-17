﻿using Imgeneus.Core.DependencyInjection;
using Imgeneus.Database;
using Imgeneus.Database.Entities;
using Imgeneus.Login.Packets;
using Imgeneus.Network;
using Imgeneus.Network.Data;
using Imgeneus.Network.Packets;
using Imgeneus.Network.Packets.Login;
using Org.BouncyCastle.Math;
using System;
using System.Linq;

namespace Imgeneus.Login
{
    public static class LoginHandler
    {

        [PacketHandler(PacketType.LOGIN_HANDSHAKE)]
        public static void OnLoginHandshake(LoginClient client, IPacketStream packet)
        {
            var encryptedBytes = packet.Buffer.Skip(5).ToArray(); // 128 bytes here

            // Again endian problem. Client sends big integer as small endian, but BigInteger in c# is big endian.
            // So what we need to do in this case: we should reverse array and add 0-byte as first byte.
            // From here: https://stackoverflow.com/questions/48372017/convert-byte-array-to-biginteger
            // Example: client sends [2, 20, 200] we trasform to [0, 200, 20, 2]
            byte[] rev = new byte[encryptedBytes.Length + 1];
            for (int i = 0, j = encryptedBytes.Length; j > 0; i++, j--)
                rev[j] = encryptedBytes[i];

            var encryptedNumber = new BigInteger(rev);
            var decryptedNumber = client.CryptoManager.DecryptRSA(encryptedNumber);

            client.CryptoManager.GenerateAESKeyAndIV(decryptedNumber);
        }

        [PacketHandler(PacketType.LOGIN_REQUEST)]
        public static void OnLoginRequest(LoginClient client, IPacketStream packet)
        {
            if (packet.Length != 52)
            {
                return;
            }

            var authenticationPacket = new AuthenticationPacket(packet);

            var result = Authentication(authenticationPacket.Username, authenticationPacket.Password);

            if (result != AuthenticationResult.SUCCESS)
            {
                LoginPacketFactory.AuthenticationFailed(client, result);
                return;
            }
            var loginServer = DependencyContainer.Instance.Resolve<ILoginServer>();

            using var database = DependencyContainer.Instance.Resolve<IDatabase>();
            DbUser dbUser = database.Users.First(x => x.Username.Equals(authenticationPacket.Username, StringComparison.OrdinalIgnoreCase));

            if (loginServer.IsClientConnected(dbUser.Id))
            {
                client.Disconnect();
                return;
            }

            dbUser.LastConnectionTime = DateTime.Now;
            database.Users.Update(dbUser);
            database.SaveChanges();

            client.SetClientUserID(dbUser.Id);

            LoginPacketFactory.AuthenticationFailed(client, result, dbUser);
        }

        [PacketHandler(PacketType.SELECT_SERVER)]
        public static void OnSelectServer(LoginClient client, IPacketStream packet)
        {
            if (packet.Length != 9)
            {
                return;
            }

            var selectServerPacket = new SelectServerPacket(packet);

            var server = DependencyContainer.Instance.Resolve<ILoginServer>();
            var worldInfo = server.GetWorldByID(selectServerPacket.WorldId);

            if (worldInfo == null)
            {
                LoginPacketFactory.SelectServerFailed(client, SelectServer.CannotConnect);
                return;
            }

            // For some reason, the current game.exe has version -1. Maybe this is somehow connected with decrypted packages?
            // In any case, for now client version check is disabled.
            if (worldInfo.BuildVersion != selectServerPacket.BuildClient && selectServerPacket.BuildClient != -1)
            {
                LoginPacketFactory.SelectServerFailed(client, SelectServer.VersionDoesntMatch);
                return;
            }

            if (worldInfo.ConnectedUsers >= worldInfo.MaxAllowedUsers)
            {
                LoginPacketFactory.SelectServerFailed(client, SelectServer.ServerSaturated);
                return;
            }

            LoginPacketFactory.SelectServerSuccess(client, worldInfo.Host);
        }

        [PacketHandler(PacketType.CLOSE_CONNECTION)]
        public static void OnCloseConnection(LoginClient client, IPacketStream packet)
        {
            client.Disconnect();
        }

        public static AuthenticationResult Authentication(string username, string password)
        {
            using var database = DependencyContainer.Instance.Resolve<IDatabase>();

            DbUser dbUser = database.Users.FirstOrDefault(x => x.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

            if (dbUser == null)
            {
                return AuthenticationResult.ACCOUNT_DONT_EXIST;
            }

            if (dbUser.IsDeleted)
            {
                return AuthenticationResult.ACCOUNT_IN_DELETE_PROCESS_1;
            }

            if (!dbUser.Password.Equals(password))
            {
                return AuthenticationResult.INVALID_PASSWORD;
            }

            return (AuthenticationResult)dbUser.Status;
        }
    }
}
