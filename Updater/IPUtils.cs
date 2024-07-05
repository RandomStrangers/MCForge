using System;
using System.Net;
using System.Net.Sockets;
namespace MCForgeUpdater
{
    public static class IPUtil
    {
        public static bool IsPrivate(IPAddress ip)
        {
            if (IPAddress.IsLoopback(ip)) return true;
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                byte[] addr = ip.GetAddressBytes();
                if (addr[0] == 172 && (addr[1] >= 16 && addr[1] <= 31)) return true;
                if (addr[0] == 192 && addr[1] == 168) return true;
                if (addr[0] == 10) return true;
            }
            return ip.IsIPv6LinkLocal;
        }
        public static bool IsIPv4Mapped(IPAddress ip)
        {
            if (ip.AddressFamily != AddressFamily.InterNetworkV6) return false;
            byte[] addr = ip.GetAddressBytes();
            for (int i = 0; i < 10; i++)
            {
                if (addr[i] != 0) return false;
            }
            return addr[10] == 0xFF && addr[11] == 0xFF;
        }
        public static IPAddress MapToIPV4(IPAddress ip)
        {
            byte[] addr = ip.GetAddressBytes();
            byte[] ipv4 = new byte[4];
            Buffer.BlockCopy(addr, 12, ipv4, 0, 4);
            return new IPAddress(ipv4);
        }
    }
    public static class SocketUtil
    {
        public static IPAddress GetIP(Socket s)
        {
            IPAddress addr = ((IPEndPoint)s.RemoteEndPoint).Address;
            if (IPUtil.IsIPv4Mapped(addr)) addr = IPUtil.MapToIPV4(addr);
            return addr;
        }
    }
}