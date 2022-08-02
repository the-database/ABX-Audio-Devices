using CSCore;
using CSCore.Codecs;
using CSCore.CoreAudioAPI;
using CSCore.SoundOut;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Websocket.Client;
using Websocket.Client.Exceptions;

namespace ABX_Audio_Devices
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private readonly MusicPlayer _musicPlayerA = new MusicPlayer();
        private readonly MusicPlayer _musicPlayerB = new MusicPlayer();

        public MainWindow()
        {
            InitializeComponent();

            System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += dispatcherTimer_Tick;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();

            RefreshAudioDevices(lstAudioDevicesA);
            RefreshAudioDevices(lstAudioDevicesB);

            txtAudioFilePath.Text = Settings.Default.AudioFile;
            txtIpAddress.Text = Settings.Default.IpAddress;

            if (!string.IsNullOrEmpty(txtIpAddress.Text))
            {
                Connect();
            }

            lstAudioDevicesA.SelectedIndex = lstAudioDevicesA.Items.IndexOf(new AudioDeviceListItem() { DeviceId = Settings.Default.DeviceA });
            lstAudioDevicesB.SelectedIndex = lstAudioDevicesB.Items.IndexOf(new AudioDeviceListItem() { DeviceId = Settings.Default.DeviceB });

            UpdateButtons();
        }

        private void dispatcherTimer_Tick(object? sender, EventArgs e)
        {
            lblPlayADebug.Content = _musicPlayerA._tryPlay + " " + _musicPlayerA.PlaybackState;
            lblPlayBDebug.Content = _musicPlayerB._tryPlay + " " + _musicPlayerB.PlaybackState;

            
            if (_musicPlayerA._tryPlay && _musicPlayerA.PlaybackState == PlaybackState.Stopped)
            {
                PlayClick(lstAudioDevicesA, lstInputsA, _musicPlayerA);
            } else if (_musicPlayerB._tryPlay && _musicPlayerB.PlaybackState == PlaybackState.Stopped) {
                PlayClick(lstAudioDevicesB, lstInputsB, _musicPlayerB);
            }
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {

            RefreshAudioDevices(lstAudioDevicesA);
            RefreshAudioDevices(lstAudioDevicesB);
            UpdateButtons();
        }

        private void btnPlayA_Click(object sender, RoutedEventArgs e)
        {
            lblPlayAStatus.Content = "";
            lblPlayBStatus.Content = "";

            try
            {
                PlayClick(lstAudioDevicesA, lstInputsA, _musicPlayerA);
            } 
            catch (Exception ex)
            {
                lblPlayAStatus.Content = ex.Message;
            }
        }
        private void btnPlayB_Click(object sender, RoutedEventArgs e)
        {
            lblPlayAStatus.Content = "";
            lblPlayBStatus.Content = "";

            try
            {
                PlayClick(lstAudioDevicesB, lstInputsB, _musicPlayerB);
            } 
            catch (Exception ex)
            {
                lblPlayBStatus.Content = ex.Message;
            }
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            _musicPlayerA.Stop();
            _musicPlayerB.Stop();
            UpdateButtons();
        }

        private void btnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                //txtEditor.Text = File.ReadAllText(openFileDialog.FileName);
                txtAudioFilePath.Text = openFileDialog.FileName;
                Settings.Default.AudioFile = txtAudioFilePath.Text.Trim();
                Settings.Default.Save();
            }

            UpdateButtons();
        }

        private void PlayClick(ComboBox devices, ComboBox inputs, MusicPlayer musicPlayer)
        {
            _musicPlayerA.Stop();
            _musicPlayerB.Stop();
            SetInput(((InputListItem)inputs.SelectedItem).InputCode);

            using (var enumerator = new MMDeviceEnumerator())
            using (var collection = enumerator.EnumAudioEndpoints(DataFlow.All, DeviceState.Active))
            // ^^^ take a look on parameters here, you can filter devices with it
            {
                foreach (var device in collection)
                {
                    if (device.DeviceID == ((AudioDeviceListItem)devices.SelectedItem).DeviceId)
                    {
                        musicPlayer.Open(txtAudioFilePath.Text, device);
                        musicPlayer.Play();
                        break;
                    }

                    device.Dispose();
                }
            }

            UpdateButtons();
        }

        private void RefreshAudioDevices(ComboBox lstAudioDevices)
        {
            var selectedItem = lstAudioDevices.SelectedItem as AudioDeviceListItem;
            var selectedIndex = -1;
            lstAudioDevices.Items.Clear();

            using var enumerator = new MMDeviceEnumerator();
            using var collection = enumerator.EnumAudioEndpoints(DataFlow.All, DeviceState.Active);
            foreach (var device in collection)
            {
                var newItem = new AudioDeviceListItem
                {
                    DeviceId = device.DeviceID,
                    FriendlyName = device.FriendlyName
                };

                lstAudioDevices.Items.Add(newItem);

                if (newItem.Equals(selectedItem))
                {
                    selectedIndex = lstAudioDevices.Items.Count - 1;
                }

                device.Dispose();
            }

            lstAudioDevices.SelectedIndex = selectedIndex;
        }

        private void RefreshInputs(ComboBox lstInputs, Dictionary<string,Mso.Input> inputs)
        {
            if (inputs == null)
            {
                return;
            }

            lstInputs.Items.Clear();

            foreach(var input in inputs)
            {
                lstInputs.Items.Add(new InputListItem()
                {
                    InputCode = input.Key,
                    DefaultName = InputName(input.Key),
                    Label = input.Value.label
                });
            }
        }

        private class AudioDeviceListItem
        {
            public string? DeviceId { get; set; }
            public string? FriendlyName { get; set; }

            public override string ToString()
            {
                return $"{FriendlyName} ({DeviceId})";
            }

            public override bool Equals(object? obj)
            {
                if (obj is AudioDeviceListItem other)
                {
                    return other.DeviceId == DeviceId;
                }

                return false;
            }
        }

        private class InputListItem
        {
            public string? InputCode { get; set; }
            public string? DefaultName { get; set; }
            public string? Label { get; set; }

            public override string ToString()
            {
                return (DefaultName.Equals(Label, StringComparison.OrdinalIgnoreCase) ? DefaultName : $"{Label} ({DefaultName})") ?? "";
            }

            public override bool Equals(object? obj)
            {
                if (obj is InputListItem other)
                {
                    return other.InputCode == InputCode;
                }

                return false;
            }
        }

        private void UpdateButtons()
        {
            btnPlayA.IsEnabled = lstAudioDevicesA.SelectedItem != null && lstInputsA.SelectedItem != null && txtAudioFilePath.Text.Length > 0;
            btnPlayB.IsEnabled = lstAudioDevicesB.SelectedItem != null && lstInputsB.SelectedItem != null && txtAudioFilePath.Text.Length > 0;
        }

        private void lstAudioDevicesA_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems?.Count > 0)
            {
                Settings.Default.DeviceA = ((AudioDeviceListItem)e.AddedItems[0]).DeviceId;
                Settings.Default.Save();
            }
            
            UpdateButtons();
        }

        private void lstInputsA_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                Settings.Default.InputA = ((InputListItem)e.AddedItems[0]).InputCode;
                Settings.Default.Save();
            }
            
            UpdateButtons();
        }

        private void lstAudioDevicesB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                Settings.Default.DeviceB = ((AudioDeviceListItem)e.AddedItems[0]).DeviceId;
                Settings.Default.Save();
            }
            UpdateButtons();
        }

        private void lstInputsB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                Settings.Default.InputB = ((InputListItem)e.AddedItems[0]).InputCode;
                Settings.Default.Save();
            }
            UpdateButtons();
        }

        private Uri clientUri => new Uri($"ws://{txtIpAddress.Text.Trim()}:80/ws/controller");
        private WebsocketClient client;

        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            Connect();
        }

        private async void Connect()
        {
            //var exitEvent = new ManualResetEvent(false);
            lblWebsocketStatus.Content = "";

            if (client != null)
            {
                client.Dispose();
            }
            
            client = new WebsocketClient(clientUri);
            Settings.Default.IpAddress = txtIpAddress.Text.Trim();
            Settings.Default.Save();
            

            client.ReconnectTimeout = TimeSpan.FromSeconds(30);
            //client.ReconnectionHappened.Subscribe(info =>
            //    Log.Information($"Reconnection happened, type: {info.Type}"));

            client.MessageReceived.Subscribe(msg =>
            {
                //Log.Information($"Message received: {msg}");
                Trace.WriteLine($"Message received: {msg}");

                var msoResponse = ParseMso(msg.Text);

                if (msoResponse?["Verb"] == "mso")
                {
                    var inputs = ((Mso)msoResponse["Arg"]).inputs;

                    Dispatcher.Invoke(() =>
                    {
                        RefreshInputs(lstInputsA, inputs);
                        RefreshInputs(lstInputsB, inputs);

                        lstInputsA.SelectedIndex = lstInputsA.Items.IndexOf(new InputListItem() { InputCode = Settings.Default.InputA });
                        lstInputsB.SelectedIndex = lstInputsB.Items.IndexOf(new InputListItem() { InputCode = Settings.Default.InputB });
                    });
                }
            });

            try
            {
                await client.StartOrFail();
                await Task.Run(() => client.Send("getmso"));
            } catch (WebsocketException ex)
            {
                lblWebsocketStatus.Content = ex.Message;
            }
        }

        private void SetInput(string inputCode)
        {
            Task.Run(() => {
                client.Send($@"changemso [{{""op"": ""replace"", ""path"": ""/input"", ""value"": ""{inputCode}""}}]");
            });
        }

        private Dictionary<string,dynamic> ParseMso(string cmd)
        {
            var i = cmd.IndexOf(" ");
            var verb = cmd.Substring(0, i);
            if (i > 0 && verb == "mso")
            {
                return new Dictionary<string, dynamic>() {
                    {"Verb", verb },
                    {"Arg", JsonSerializer.Deserialize<Mso>(cmd.Substring(i+1)) }
                };
            }

            return new Dictionary<string, dynamic>()
            {
                {"Verb", verb }
            };
        }

        private string InputName(string inputCode)
        {
            if (inputCode == null) return "";
            if ((inputCode[0] == 'h') && (inputCode.Length == 2)) return "HDMI " + inputCode[1];
            if ((inputCode[0] == 'a') && (inputCode.Length == 2)) return "Analog " + inputCode[1];
            if ((inputCode[0] == 's') && (inputCode.Length == 6)) return "COAX " + inputCode[5];
            if ((inputCode[0] == 'o') && (inputCode.Length == 8)) return "Optical " + inputCode[7];

            switch (inputCode)
            {
                case "tv":
                    return "TV";
                case "roon":
                    return "Roon";
                case "aes":
                    return "AES/EBU";
                case "b":
                    return "Bluetooth";
                case "fm":
                    return "FM";
                case "usb":
                    return "USB Audio";
                default:
                    return inputCode.ToUpper();
            }
        }
    }
}
