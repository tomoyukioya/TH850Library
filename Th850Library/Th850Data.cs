using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Th850Library
{
    /// <summary>
    /// TH-850データリード応答を保持するクラス
    /// </summary>
    public class Th850Data
    {
        /// <summary>
        /// デバイスID
        /// </summary>
        public string DeviceId { get; }

        /// <summary>
        /// 日時
        /// </summary>
        public DateTime DateTime { get; }

        /// <summary>
        /// 体重
        /// </summary>
        public int Weight { get; }

        /// <summary>
        /// 歩幅
        /// </summary>
        public int Stride { get; }

        /// <summary>
        /// 当日の歩数等～14日前の歩数等
        /// </summary>
        public List<DailyWorkout> DailyWorkouts { get; }

        /// <summary>
        /// デーが有効かどうか
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="data"></param>
        public Th850Data(byte[] data)
        {
            IsValid = false;
            DailyWorkouts = new List<DailyWorkout>();

            try
            {
                if (data[0] != 0x06) return;

                var len = data[1] * 0x100 + data[2];

                DeviceId = Bcd.ToString(data.Skip(3).Take(5).ToArray());
                DateTime = new DateTime(2000 + data[8], data[9], data[10], data[11], data[12], data[13]);
                Weight = data[14] * 0x100 + data[15];
                Stride = data[16] * 0x100 + data[17];

                var ptr = 18;
                for (int i = 0; ; i++)
                {
                    if (len < ptr + 139) break;
                    var dailyWorkout = new DailyWorkout(data.Skip(18 + i * 139).Take(139).ToArray());
                    if (dailyWorkout.IsValid)
                        DailyWorkouts.Add(dailyWorkout);
                    ptr += 139;
                }

                var sum = data.Take(len - 1).Sum(m => m);
                if ((sum & 0xff) != data[len - 1]) return;

                IsValid = true;
            }
            catch (Exception) { }
        }
    }

    /// <summary>
    /// 一日当たりのワークアウト
    /// </summary>
    public class DailyWorkout
    {
        /// <summary>
        /// 日時
        /// </summary>
        public DateTime Date { get; }

        /// <summary>
        /// 一日の歩数
        /// </summary>
        public int Step { get; }

        /// <summary>
        /// 一日のPW
        /// </summary>
        public int Pw { get; }

        /// <summary>
        /// 一日の距離(km)
        /// </summary>
        public double Distance { get; }

        /// <summary>
        /// 一日の消費カロリー(KCal)
        /// </summary>
        public double Calorie { get; }

        /// <summary>
        /// 一日のエクササイズ
        /// </summary>
        public double Ex { get; }

        /// <summary>
        /// 一日の体脂肪燃焼量(g)
        /// </summary>
        public double Fat { get; }

        /// <summary>
        /// 0時から23時までの一時間ごとのワークアウト
        /// </summary>
        public List<HourlyWorkout> HourlyWorkouts { get; }

        /// <summary>
        /// データが有効かどうか
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="data"></param>
        public DailyWorkout(byte[] data)
        {
            IsValid = false;
            HourlyWorkouts = new List<HourlyWorkout>();
            try
            {
                Date = new DateTime(2000 + data[0], data[1], data[2]);
                Step = data[3] * 0x10000 + data[4] * 0x100 + data[5];
                Pw = data[6] * 0x10000 + data[7] * 0x100 + data[8];
                Distance = (data[9] * 0x10000 + data[10] * 0x100 + data[11]) / 100.0;
                Calorie = (data[12] * 0x10000 + data[13] * 0x100 + data[14]) / 10.0;
                Ex = (data[15] * 0x100 + data[16]) / 10.0;
                Fat = (data[17] * 0x100 + data[18]) / 10.0;

                for (int i = 0; i <= 23; i++)
                {
                    var hourlyWorkout = new HourlyWorkout(data.Skip(19 + i * 5).Take(5).ToArray(), i);
                    HourlyWorkouts.Add(hourlyWorkout);
                }

                IsValid = true;
            }
            catch (Exception) { }
        }
    }

    /// <summary>
    /// 一時間ごとのワークアウト
    /// </summary>
    public class HourlyWorkout
    {
        /// <summary>
        /// 時刻
        /// </summary>
        public int Hour { get; }

        /// <summary>
        /// 一時間当たりの歩数
        /// </summary>
        public int Step { get; }

        /// <summary>
        /// 一時間当たりのPW
        /// </summary>
        public double Pw { get; }

        /// <summary>
        /// 一時間当たりのエクササイズ
        /// </summary>
        public double Ex { get; }

        public HourlyWorkout(byte[] data, int hour)
        {
            Hour = hour;
            Step = data[0] * 0x100 + data[1];
            Pw = data[2] * 0x100 + data[3];
            Ex = data[4] / 10.0;
        }
    }
}
