using System;
using System.Collections.Generic;

namespace FreeBudsController
{
    public enum AncMode
    {
        Off = 0x00,
        NoiseCancelling = 0x01,
        Awareness = 0x02
    }

    public enum AncLevel
    {
        General = 0x00,
        Cozy = 0x01,
        Ultra = 0x02,
        Dynamic = 0x03
    }

    public class AncStatus
    {
        public AncMode Mode { get; set; }
        public AncLevel Level { get; set; }

        public string ModeName => Mode switch
        {
            AncMode.Off => "Off",
            AncMode.NoiseCancelling => "Noise Cancelling",
            AncMode.Awareness => "Awareness",
            _ => "Unknown"
        };

        public string LevelName => Level switch
        {
            AncLevel.General => "General",
            AncLevel.Cozy => "Cozy",
            AncLevel.Ultra => "Ultra",
            AncLevel.Dynamic => "Dynamic",
            _ => $"Level {(int)Level}"
        };

        public string DisplayText => Mode switch
        {
            AncMode.NoiseCancelling => $"Noise Cancelling ({LevelName})",
            _ => ModeName
        };
    }

    public class BatteryStatus
    {
        public int Left { get; set; } = -1;
        public int Right { get; set; } = -1;
        public int Case { get; set; } = -1;
    }

    public static class FreeBudsProtocol
    {
        public static readonly byte[] CmdSetAncMode = new byte[] { 0x2b, 0x04 };
        public static readonly byte[] CmdGetAncMode = new byte[] { 0x2b, 0x2a };
        public static readonly byte[] CmdGetBattery = new byte[] { 0x01, 0x08 };

        private static readonly ushort[] CrcTab = new ushort[] {
            0,4129,8258,12387,16516,20645,24774,28903,33032,37161,41290,45419,49548,53677,57806,61935,
            4657,528,12915,8786,21173,17044,29431,25302,37689,33560,45947,41818,54205,50076,62463,58334,
            9314,13379,1056,5121,25830,29895,17572,21637,42346,46411,34088,38153,58862,62927,50604,54669,
            13907,9842,5649,1584,30423,26358,22165,18100,46939,42874,38681,34616,63455,59390,55197,51132,
            18628,22757,26758,30887,2112,6241,10242,14371,51660,55789,59790,63919,35144,39273,43274,47403,
            23285,19156,31415,27286,6769,2640,14899,10770,56317,52188,64447,60318,39801,35672,47931,43802,
            27814,31879,19684,23749,11298,15363,3168,7233,60846,64911,52716,56781,44330,48395,36200,40265,
            32407,28342,24277,20212,15891,11826,7761,3696,65439,61374,57309,53244,48923,44858,40793,36728,
            37256,33193,45514,41451,53516,49453,61774,57711,4224,161,12482,8419,20484,16421,28742,24679,
            33721,37784,41979,46042,49981,54044,58239,62302,689,4752,8947,13010,16949,21012,25207,29270,
            46570,42443,38312,34185,62830,58703,54572,50445,13538,9411,5280,1153,29798,25671,21540,17413,
            42971,47098,34713,38840,59231,63358,50973,55100,9939,14066,1681,5808,26199,30326,17941,22068,
            55628,51565,63758,59695,39368,35305,47498,43435,22596,18533,30726,26663,6336,2273,14466,10403,
            52093,56156,60223,64286,35833,39896,43963,48026,19061,23124,27191,31254,2801,6864,10931,14994,
            64814,60687,56684,52557,48554,44427,40424,36297,31782,27655,23652,19525,15522,11395,7392,3265,
            61215,65342,53085,57212,44955,49082,36825,40952,28183,32310,20053,24180,11923,16050,3793,7920
        };

        public static byte[] CalculateCrc16(byte[] data)
        {
            ushort s = 0;
            foreach (byte b in data)
            {
                s = (ushort)(CrcTab[((s >> 8) ^ b) & 255] ^ (s << 8));
            }
            return new byte[] { (byte)(s >> 8), (byte)(s & 0xFF) };
        }

        public static byte[] BuildPacket(byte[] command, List<(byte Type, byte[] Value)> parameters)
        {
            var body = new List<byte>(command);
            foreach (var (type, value) in parameters)
            {
                body.Add(type);
                body.Add((byte)value.Length);
                body.AddRange(value);
            }

            int length = body.Count + 1;
            var packet = new List<byte>
            {
                0x5A,
                (byte)(length >> 8),
                (byte)(length & 0xFF),
                0x00
            };
            packet.AddRange(body);

            byte[] crc = CalculateCrc16(packet.ToArray());
            packet.AddRange(crc);

            return packet.ToArray();
        }

        public static byte[] BuildSetAncPacket(AncMode mode, AncLevel? level = null)
        {
            byte modeByte = (byte)mode;
            byte levelByte;

            if (mode == AncMode.Off)
            {
                levelByte = 0x00;
            }
            else if (mode == AncMode.Awareness)
            {
                levelByte = 0xFF;
            }
            else
            {
                levelByte = level.HasValue ? (byte)level.Value : (byte)0xFF;
            }

            var payload = new byte[] { modeByte, levelByte };
            return BuildPacket(CmdSetAncMode, new List<(byte, byte[])> { (1, payload) });
        }

        public static byte[] BuildGetAncPacket()
        {
            return BuildPacket(CmdGetAncMode, new List<(byte, byte[])> { (1, Array.Empty<byte>()), (2, Array.Empty<byte>()) });
        }

        public static byte[] BuildGetBatteryPacket()
        {
            return BuildPacket(CmdGetBattery, new List<(byte, byte[])> { (1, Array.Empty<byte>()), (2, Array.Empty<byte>()) });
        }

        public static AncStatus? ParseAncStatus(byte[] data)
        {
            // Look for 0x2b 0x2a sequence in response
            for (int i = 0; i <= data.Length - 6; i++)
            {
                if (data[i] == 0x2B && data[i + 1] == 0x2A)
                {
                    // data[i+2] = param 1, data[i+3] = length (2)
                    // data[i+4] = level, data[i+5] = mode
                    byte level = data[i + 4];
                    byte mode = data[i + 5];

                    return new AncStatus
                    {
                        Mode = (AncMode)mode,
                        Level = (AncLevel)level
                    };
                }
            }
            return null;
        }

        public static BatteryStatus? ParseBatteryStatus(byte[] data)
        {
            // Look for 0x01 0x27 or 0x01 0x08
            for (int i = 0; i <= data.Length - 10; i++)
            {
                if (data[i] == 0x01 && (data[i + 1] == 0x27 || data[i + 1] == 0x08))
                {
                    // Scan params for Type 2 (3 bytes: left, right, case)
                    int p = i + 2;
                    while (p + 2 < data.Length)
                    {
                        byte pType = data[p];
                        byte pLen = data[p + 1];
                        if (pType == 2 && pLen == 3 && p + 2 + 3 <= data.Length)
                        {
                            return new BatteryStatus
                            {
                                Left = data[p + 2],
                                Right = data[p + 3],
                                Case = data[p + 4]
                            };
                        }
                        p += 2 + pLen;
                    }
                }
            }
            return null;
        }
    }
}
