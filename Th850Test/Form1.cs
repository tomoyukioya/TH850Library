using Th850Library;

namespace Th850Test
{
    public partial class Form1 : Form
    {
        private readonly CancellationTokenSource _cts = new();

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Task.Run(() => Worker());
        }

        private async Task Worker()
        {
            Th850Device? currentDevice = null;
            Th850Id? currentId = null;
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    try
                    {
                        var devices = Th850Devices.Enumerate().ToList();
                        if (devices.Count == 0)
                        {
                            // デバイスが見つからなかった
                            if (currentDevice != null)
                            {
                                // deviceが抜かれた
                                Invoke(new Action(() => ClearData()));
                                currentDevice.Dispose();
                                currentDevice = null;
                                currentId = null;
                            }
                        }
                        else
                        {
                            // デバイスが見つかった
                            var detectedDevice = devices[0];
                            var detectedId = detectedDevice.ReadId();
                            if (detectedId != null && (currentId == null || detectedId.DeviceId != currentId.DeviceId))
                            {
                                // 新たなデバイスが見つかった
                                currentDevice?.Dispose();
                                currentDevice = detectedDevice;
                                currentId = detectedId;
                                var data = detectedDevice.ReadData();
                                if (data != null)
                                    Invoke(new Action(() => ShowData(currentId, data)));
                            }

                            // 引き継がなかったデバイスは解放する
                            foreach (var device in devices)
                                if (!ReferenceEquals(device, currentDevice)) device.Dispose();
                        }

                        // 待機
                        await Task.Delay(500, _cts.Token);
                    }
                    catch (Exception)
                    {
                        if (_cts.IsCancellationRequested) return;
                    }
                }
            }
            finally
            {
                currentDevice?.Dispose();
            }
        }

        /// <summary>
        /// TreeView消去
        /// </summary>
        private void ClearData()
        {
            treeView.Nodes.Clear();
        }

        /// <summary>
        /// TreeViewにデータを表示
        /// </summary>
        /// <param name="id"></param>
        /// <param name="data"></param>
        private void ShowData(Th850Id id, Th850Data data)
        {
            treeView.Nodes.Clear();

            // id
            var idNode = new TreeNode("ID");
            idNode.Nodes.Add($"Id: {id.DeviceId}");
            idNode.Nodes.Add($"DateTime: {id.DateTime}");
            idNode.Nodes.Add($"Ver: {id.FirmwareVersion}");
            idNode.Nodes.Add($"Sub ID: {id.SubId}");
            treeView.Nodes.Add(idNode);

            // data
            var dataNode = new TreeNode("DATA");
            dataNode.Nodes.Add($"Weight: {data.Weight}");
            dataNode.Nodes.Add($"Stride: {data.Stride}");
            foreach (var dayly in data.DailyWorkouts)
            {
                var daylyNode = new TreeNode(dayly.Date.ToLongDateString());
                daylyNode.Nodes.Add($"Step: {dayly.Step}");
                daylyNode.Nodes.Add($"Pw: {dayly.Pw}");
                daylyNode.Nodes.Add($"Distance: {dayly.Distance}");
                daylyNode.Nodes.Add($"Calorie: {dayly.Calorie}");
                daylyNode.Nodes.Add($"Ex: {dayly.Ex}");
                daylyNode.Nodes.Add($"Fat: {dayly.Fat}");
                var hourlyRoot = new TreeNode("Hour");
                foreach (var hourly in dayly.HourlyWorkouts)
                    hourlyRoot.Nodes.Add(new TreeNode($"{hourly.Hour.ToString()}: [Step: {hourly.Step}, Pw: {hourly.Pw}, Ex: {hourly.Ex}]"));
                daylyNode.Nodes.Add(hourlyRoot);
                dataNode.Nodes.Add(daylyNode);
            }
            treeView.Nodes.Add(dataNode);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            _cts.Cancel();
        }
    }
}
