using HidLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Th850Library
{
    public class Th850Device : IDisposable
    {
        private IHidDevice _hidDevice { get; set; }

        /// <summary>
        /// 受信用の非管理バッファ。デバイス1台につき一度だけ確保し Dispose() で解放する。
        /// 呼び出しごとに確保・解放すると、タイムアウトで放置された ReadFile が
        /// 解放済みの領域に書き込む恐れがあるため、寿命をデバイスに合わせている。
        /// </summary>
        private IntPtr _receiveBuffer = IntPtr.Zero;
        private int _receiveBufferLength;
        private bool _disposed;

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
        /// <param name="optionData"></param>
        /// <returns></returns>
        bool Send(Th850Cmd cmd, byte[] optionData)
        {
            try
            {
                // optionDataは14byteになるまでゼロパディング
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
            var nonManagedBuffer = EnsureReceiveBuffer(bytesToRead);
            var bytesRead = 0u;
            try
            {
                while (totalBytesRead < 3 || totalBytesRead < (totalBuffer[1] << 8 | totalBuffer[2]))
                {
                    using (var cts = new CancellationTokenSource(100))
                    {
                        var task = Task.Run(() => ReadFile(_hidDevice.ReadHandle, nonManagedBuffer, (uint)bytesToRead, out bytesRead, ref overlapped));
                        task.Wait(cts.Token);
                    }
                    Marshal.Copy(nonManagedBuffer, buffer, 0, (int)bytesRead);
                    if (buffer[0] != 0) return null;
                    Array.Copy(buffer, 2, totalBuffer, totalBytesRead, buffer[1]);
                    totalBytesRead += buffer[1];
                }
            }
            catch (Exception)
            {
                return null;
            }

            // データが完成したか
            var sum = (byte)totalBuffer.Take(totalBytesRead - 1).Sum(m => m);
            if (sum != totalBuffer[totalBytesRead - 1]) return null;
            else return totalBuffer.Take(totalBytesRead).ToArray();
        }

        /// <summary>
        /// 受信バッファを必要な長さで確保する（確保済みで足りていればそれを使う）
        /// </summary>
        IntPtr EnsureReceiveBuffer(int length)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(Th850Device));

            if (_receiveBuffer != IntPtr.Zero && _receiveBufferLength >= length) return _receiveBuffer;

            if (_receiveBuffer != IntPtr.Zero) Marshal.FreeHGlobal(_receiveBuffer);
            _receiveBuffer = Marshal.AllocHGlobal(length);
            _receiveBufferLength = length;
            return _receiveBuffer;
        }

        /// <summary>
        /// 受信バッファとHIDデバイスを解放する
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_receiveBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_receiveBuffer);
                _receiveBuffer = IntPtr.Zero;
                _receiveBufferLength = 0;
            }

            (_hidDevice as IDisposable)?.Dispose();
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
