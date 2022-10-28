using HidLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Th850Library
{
    public class Th850Device
    {
        private IHidDevice _hidDevice { get; set; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="hidDevice"></param>
        public Th850Device(IHidDevice hidDevice)
        {
            _hidDevice = hidDevice;
        }

        /// <summary>
        /// ID読み出し
        /// </summary>
        /// <returns></returns>
        public Th850Id ReadId()
        {
            if (!Send(Th850Cmd.ReadId, null)) return null;
            return new Th850Id(Receive());
        }

        /// <summary>
        /// 歩数データ読み出し
        /// </summary>
        /// <returns></returns>
        public Th850Data ReadData()
        {
            if (!Send(Th850Cmd.ReadData, null)) return null;
            return new Th850Data(Receive());
        }

        /// <summary>
        /// TH850にコマンド送信
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        bool Send(Th850Cmd cmd, byte[] optionData)
        {
            try
            {
                // optionDataは12byteになるまでゼロパディング
                if (optionData == null) optionData = new byte[14];
                if (optionData.Length < 14) Array.Resize(ref optionData, 14);

                // コマンドバイト列構築
                var cmdbytes = new List<byte> { (byte)cmd, (byte)((optionData.Length + 4) >> 8), (byte)(optionData.Length + 4) };
                cmdbytes.AddRange(optionData);
                cmdbytes.Add((byte)cmdbytes.Sum(m => m));

                var bytesSent = 0;
                while (bytesSent < cmdbytes.Count)
                {
                    // _hidDevice.Capabilities.OutputReportByteLength長以下になるよう分割してWrite()
                    var cmdbytesSegment = cmdbytes.Skip(bytesSent).Take(_hidDevice.Capabilities.OutputReportByteLength - 3);
                    var dataToSend = new List<byte> { 0x00, (byte)cmdbytesSegment.Count() };
                    dataToSend.AddRange(cmdbytesSegment);
                    dataToSend.Add((byte)dataToSend.Sum(m => m));

                    if (!_hidDevice.Write(dataToSend.ToArray())) return false;

                    bytesSent += cmdbytesSegment.Count();
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// TH850からコマンド受信
        /// </summary>
        /// <returns></returns>
        byte[] Receive()
        {
            var totalBytesRead = 0;
            var totalBuffer = new byte[4000];
            var overlapped = new NativeOverlapped();
            var bytesToRead = _hidDevice.Capabilities.InputReportByteLength;
            var buffer = new byte[bytesToRead];
            var nonManagedBuffer = Marshal.AllocHGlobal(bytesToRead);
            var bytesRead = 0u;
            try
            {
                while (totalBytesRead < 3 || totalBytesRead < (totalBuffer[1]<<8 | totalBuffer[2]))
                {
                    using(var cts = new CancellationTokenSource(100))
                    {
                        var task = Task.Run(() => ReadFile(_hidDevice.ReadHandle, nonManagedBuffer, (uint)bytesToRead, out bytesRead, ref overlapped));
                        task.Wait(cts.Token);
                    }
                    Marshal.Copy(nonManagedBuffer, buffer, 0, (int)bytesRead);
                    if (buffer[0] != 0) return null;
                    Array.Copy(buffer, 2, totalBuffer, totalBytesRead, buffer[1]);
                    totalBytesRead += buffer[1];

                    //// デバイスからデータ読み込み
                    //Task<HidDeviceData> task;
                    //using(var cts = new CancellationTokenSource(1000))
                    //{
                    //    task = Task.Run(()=> _hidDevice.Read());
                    //    task.Wait(cts.Token);
                    //}
                    //if (task.Result.Data[0] != 0x00) return null;
                    //if (task.Result.Data[1] + 2 > task.Result.Data.Length) return null;

                    //string o = $"{task.Result.Status.ToString()}";
                    //foreach (var a in task.Result.Data) o+=$"{a:X2}";
                    //Debug.WriteLine(o);

                    //data.AddRange(task.Result.Data.Skip(2).Take(task.Result.Data[1]));

                    //// データが完成したか
                    //if (data.Count() > 0 && data[0] != 0x06) return null;
                    //if (data.Count() < 4) continue;
                    //var length = data[1] << 8 | data[2];
                    //if (length > data.Count()) continue;
                    //var sum = (byte)data.Take(data.Count() - 1).Sum(m => m);
                    //if (sum != data[data.Count() - 1]) return null;
                    //return data.ToArray();
                }
            }
            catch (Exception ex)
            {
                return null;
            }

            // データが完成したか
            var sum = (byte)totalBuffer.Take(totalBytesRead - 1).Sum(m => m);
            if (sum != totalBuffer[totalBytesRead - 1]) return null;
            else return totalBuffer.Take(totalBytesRead).ToArray();
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static internal extern bool ReadFile(IntPtr hFile, IntPtr lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, [In] ref System.Threading.NativeOverlapped lpOverlapped);

        enum Th850Cmd
        {
            ReadId = 0xa0,
            ReadData = 0xa1,
        }
    }
}
