using System;
using System.Net;
using System.Net.Sockets;

namespace FreeBudsController
{
    /// <summary>
    /// Custom EndPoint for Bluetooth RFCOMM (AF_BTH = 32) on Windows.
    /// </summary>
    public class BluetoothEndPoint : EndPoint
    {
        private const AddressFamily BluetoothAddressFamily = (AddressFamily)32;
        private readonly ulong _address;
        private readonly int _port;

        public BluetoothEndPoint(ulong address, int port)
        {
            _address = address;
            _port = port;
        }

        public override AddressFamily AddressFamily => BluetoothAddressFamily;

        public override SocketAddress Serialize()
        {
            // Windows SOCKADDR_BTH is 30 bytes:
            // USHORT addressFamily (2 bytes)
            // BTH_ADDR btAddr (8 bytes)
            // GUID serviceClassId (16 bytes)
            // ULONG port (4 bytes)
            var sa = new SocketAddress(BluetoothAddressFamily, 30);
            byte[] addrBytes = BitConverter.GetBytes(_address);
            for (int i = 0; i < 8; i++)
            {
                sa[2 + i] = addrBytes[i];
            }

            byte[] portBytes = BitConverter.GetBytes(_port);
            for (int i = 0; i < 4; i++)
            {
                sa[26 + i] = portBytes[i];
            }

            return sa;
        }

        public static ulong ParseMacAddress(string mac)
        {
            if (string.IsNullOrWhiteSpace(mac)) return 0;
            string clean = mac.Replace(":", "").Replace("-", "").Trim();
            if (clean.Length != 12) return 0;

            ulong result = 0;
            for (int i = 0; i < 12; i += 2)
            {
                result = (result << 8) | Convert.ToByte(clean.Substring(i, 2), 16);
            }
            return result;
        }
    }
}
