using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.IO;


namespace NetworkAnalyzer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            InterfacesList.ItemsSource = interfaces;

            LoadHistory();
        }

        private void InterfacesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (InterfacesList.SelectedItem is NetworkInterface ni)
            {
                var ipProps = ni.GetIPProperties();
                var unicast = ipProps.UnicastAddresses.FirstOrDefault();
                var ip = unicast?.Address?.ToString() ?? "Нет данных";
                var mask = unicast?.IPv4Mask?.ToString() ?? "Нет данных";
                var mac = ni.GetPhysicalAddress().ToString();
                var status = ni.OperationalStatus;
                var speed = ni.Speed;
                var type = ni.NetworkInterfaceType;

                InterfaceInfoTextBlock.Text =
                    $"Название: {ni.Name}\n" +
                    $"Тип: {type}\n" +
                    $"Статус: {status}\n" +
                    $"MAC: {mac}\n" +
                    $"IP: {ip}\n" +
                    $"Маска: {mask}\n" +
                    $"Скорость: {speed / 1_000_000} Мбит/с";
            }
        }

        private void AnalyzeUrl_Click(object sender, RoutedEventArgs e)
        {
            var input = UrlInput.Text;

            if (Uri.TryCreate(input, UriKind.Absolute, out Uri uri))
            {
                SaveUrlToHistory(input);
                UrlInfoTextBlock.Text =
                    $"Схема (протокол): {uri.Scheme}\n" +
                    $"Хост: {uri.Host}\n" +
                    $"Порт: {(uri.IsDefaultPort ? "(по умолчанию)" : uri.Port.ToString())}\n" +
                    $"Путь: {uri.AbsolutePath}\n" +
                    $"Параметры запроса: {uri.Query}\n" +
                    $"Фрагмент: {uri.Fragment}";
            }
            else
            {
                UrlInfoTextBlock.Text = "Некорректный URL.";
            }
        }
        private async void PingHost_Click(object sender, RoutedEventArgs e)
        {
            string input = UrlInput.Text;

            if (Uri.TryCreate(input, UriKind.Absolute, out Uri uri))
            {
                string host = uri.Host;

                try
                {
                    Ping ping = new Ping();
                    PingReply reply = await ping.SendPingAsync(host, 3000);

                    if (reply.Status == IPStatus.Success)
                    {
                        PingResultTextBlock.Text = $"Хост {host} доступен.\n" +
                                                   $"Адрес: {reply.Address}\n" +
                                                   $"Время ответа: {reply.RoundtripTime} мс";
                    }
                    else
                    {
                        PingResultTextBlock.Text = $"Хост {host} недоступен. Статус: {reply.Status}";
                    }
                }
                catch (Exception ex)
                {
                    PingResultTextBlock.Text = $"Ошибка при Ping: {ex.Message}";
                }
            }
            else
            {
                PingResultTextBlock.Text = "Некорректный URL для Ping.";
            }
        }
        private async void DnsInfo_Click(object sender, RoutedEventArgs e)
        {
            string input = UrlInput.Text;

            if (Uri.TryCreate(input, UriKind.Absolute, out Uri uri))
            {
                string host = uri.Host;

                try
                {
                    IPHostEntry entry = await Dns.GetHostEntryAsync(host);
                    string result = $"DNS-информация для {host}:\n";

                    if (entry.Aliases.Length > 0)
                    {
                        result += $"Алиасы:\n - {string.Join("\n - ", entry.Aliases)}\n";
                    }

                    if (entry.AddressList.Length > 0)
                    {
                        result += "IP-адреса:\n";
                        foreach (var addr in entry.AddressList)
                        {
                            result += $" - {addr} ({addr.AddressFamily})\n";
                        }
                    }

                    DnsInfoTextBlock.Text = result;
                }
                catch (Exception ex)
                {
                    DnsInfoTextBlock.Text = $"Ошибка при получении DNS-информации: {ex.Message}";
                }
            }
            else
            {
                DnsInfoTextBlock.Text = "Некорректный URL.";
            }
        }

        private void CheckAddressType_Click(object sender, RoutedEventArgs e)
        {
            string input = UrlInput.Text;

            if (Uri.TryCreate(input, UriKind.Absolute, out Uri uri))
            {
                string host = uri.Host;

                try
                {
                    IPAddress[] addresses = Dns.GetHostAddresses(host);
                    if (addresses.Length == 0)
                    {
                        AddressTypeTextBlock.Text = "Не удалось определить IP-адрес.";
                        return;
                    }

                    string result = $"Типы IP-адресов для {host}:\n";

                    foreach (var ip in addresses)
                    {
                        if (IPAddress.IsLoopback(ip))
                        {
                            result += $"{ip} — Loopback\n";
                        }
                        else if (ip.AddressFamily == AddressFamily.InterNetwork && IsPrivateIPv4(ip))
                        {
                            result += $"{ip} — Локальный (частный)\n";
                        }
                        else
                        {
                            result += $"{ip} — Публичный\n";
                        }
                    }

                    AddressTypeTextBlock.Text = result;
                }
                catch (Exception ex)
                {
                    AddressTypeTextBlock.Text = $"Ошибка: {ex.Message}";
                }
            }
            else
            {
                AddressTypeTextBlock.Text = "Некорректный URL.";
            }
        }

        private bool IsPrivateIPv4(IPAddress ip)
        {
            byte[] bytes = ip.GetAddressBytes();
            return
                bytes[0] == 10 || // 10.0.0.0 – 10.255.255.255
                (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) || // 172.16.0.0 – 172.31.255.255
                (bytes[0] == 192 && bytes[1] == 168); // 192.168.0.0 – 192.168.255.255
        }

        private readonly string historyFilePath = "history.txt";

        private void SaveUrlToHistory(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            // Убираем повторы
            var urls = File.Exists(historyFilePath)
                ? new HashSet<string>(File.ReadAllLines(historyFilePath))
                : new HashSet<string>();

            if (urls.Add(url))
            {
                File.WriteAllLines(historyFilePath, urls);
                LoadHistory(); // обновить ListBox
            }
        }

        private void LoadHistory()
        {
            if (File.Exists(historyFilePath))
            {
                HistoryListBox.ItemsSource = File.ReadAllLines(historyFilePath);
            }
        }

        private void HistoryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (HistoryListBox.SelectedItem is string selectedUrl)
            {
                UrlInput.Text = selectedUrl;
            }
        }




    }

}