using System;
using System.Linq;
using System.Text;

namespace Th850Library
{
    /// <summary>
    /// TH-850 IDリード応答を保持するクラス
    /// </summary>
    public class Th850Id
    {
        public string DeviceId { get; }
        public DateTime DateTime { get; }
        public double FirmwareVersion { get; }
        public string SubId { get; }
        public bool IsValid { get; }

        public Th850Id(byte[] data)
        {
            IsValid = false;
            if (data == null || data.Length != 35) return;
            try
            {
                if (data[0] != 0x06) return;

                if (data[1] != 0x00) return;
                if (data[2] != 0x23) return;

                if (data[3] != 0x54) return;
                if (data[4] != 0x48) return;
                if (data[5] != 0x2d) return;
                if (data[6] != 0x38) return;
                if (data[7] != 0x35) return;
                if (data[8] != 0x30) return;
                if (data[9] != 0x00) return;
                if (data[10] != 0x00) return;
                if (data[11] != 0x00) return;
                if (data[12] != 0x00) return;

                DeviceId = Bcd.ToString(data.Skip(13).Take(5).ToArray());

                DateTime = new DateTime(2000 + data[18], data[19], data[20], data[21], data[22], data[23]);

                FirmwareVersion = int.Parse(Bcd.ToString(data.Skip(24).Take(2).ToArray()))/100.0;

                SubId = Encoding.ASCII.GetString(data.Skip(26).Take(8).ToArray()).TrimEnd('\u0000');

                var sum = data.Take(34).Sum(m=>m);
                if ((sum & 0xff) != data[34]) return;

                IsValid = true;
            }
            catch (Exception) { }
        }
    }
}
