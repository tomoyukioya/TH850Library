
using HidLibrary;
using System.Collections.Generic;
using System.Linq;

namespace Th850Library
{
    public class Th850Devices
    {
        public static readonly int Th850VenderId = 0x190e;
        public static readonly int Th850ProductId = 0x0007;

        public static IEnumerable<Th850Device> Enumerate()
        {
            return HidDevices.Enumerate(Th850VenderId, new int[] { Th850ProductId }).Select(n=>new Th850Device(n));
        }
    }
}
