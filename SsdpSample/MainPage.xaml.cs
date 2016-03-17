using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace SsdpSample
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void SendButton_OnClick(object sender, RoutedEventArgs e)
        {
            SendButton.IsEnabled = false;
            var msg = SendMessageBox.Text;
            try
            {
                var results = await SendSSDP(msg, CancellationToken.None).ConfigureAwait(true);
                ResultsPanel.ItemsSource = results.Length > 0
                    ? results
                    : new[] {"No results."};
            }
            catch (Exception ex)
            {
                var dlg = new MessageDialog(ex.ToString(), ex.GetType().ToString());
                await dlg.ShowAsync();
            }
            finally
            {
                SendButton.IsEnabled = true;
            }
        }

        private async Task<string[]> SendSSDP(string service, CancellationToken token)
        {
            var multicastIp = new HostName("239.255.255.250");
            var waitForReceiver = new ManualResetEventSlim(false);
            var results = new List<string>();

            using (var socket = new DatagramSocket())
            {
                socket.MessageReceived += (s, e) =>
                {
                    using (var reader = e.GetDataReader())
                    {
                        var ret = reader.ReadString(reader.UnconsumedBufferLength);
                        if (ret.StartsWith("M-SEARCH")) return;
                        if (results.Contains(ret)) return;

                        results.Add(ret);
                        if (results.Count > 3)
                            waitForReceiver.Set();
                    }
                };
                await socket.BindServiceNameAsync("1900");
                socket.JoinMulticastGroup(multicastIp);

                var request = string.Concat(
                    "M-SEARCH * HTTP/1.1\r\n", "HOST: 239.255.255.250:1900\r\n",
                    "MAN: \"ssdp: discover\"\r\n", "MX: 2\r\n", $"ST: {service}\r\n\r\n");
                var reqbytes = Encoding.UTF8.GetBytes(request).AsBuffer();
                using (var stream = await socket.GetOutputStreamAsync(multicastIp, "1900"))
                {
                    await stream.WriteAsync(reqbytes);
                }
                waitForReceiver.Wait(5000, token);
            }
            return results.ToArray();
        }
    }
}
