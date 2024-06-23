using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.IO.Ports;
using Timer = System.Timers.Timer; // Spécifiez l'espace de noms complet pour éviter les ambiguïtés

namespace Buttonbox_Software.Views
{
    public sealed partial class MainPage : Page
    {
        private Timer portCheckTimer;

        public MainPage()
        {
            this.InitializeComponent();
            LoadAvailablePorts();

            portCheckTimer = new Timer(2000); // Check every 2 seconds
            portCheckTimer.Elapsed += OnPortCheckTimerElapsed;
            portCheckTimer.Start();
        }

        private void OnPortCheckTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                LoadAvailablePorts();
            });
        }

        private void LoadAvailablePorts()
        {
            var ports = SerialPort.GetPortNames();
            var selectedPort = (string)PortComboBox.SelectedItem;

            PortComboBox.Items.Clear();
            foreach (var port in ports)
            {
                PortComboBox.Items.Add(port);
            }

            if (selectedPort != null && ports.Contains(selectedPort))
            {
                PortComboBox.SelectedItem = selectedPort;
            }
            else if (PortComboBox.Items.Count > 0)
            {
                PortComboBox.SelectedIndex = 0;
            }
        }

        private async void CompileButton_Click(object sender, RoutedEventArgs e)
        {
            string code = CodeTextBox.Text;
            string boardType = ((ComboBoxItem)BoardComboBox.SelectedItem)?.Content.ToString();
            if (boardType != null)
            {
                string sketchPath = await SaveSketchAsync(code);
                await CompileCode(sketchPath, boardType);
            }
        }

        private async void UploadButton_Click(object sender, RoutedEventArgs e)
        {
            string boardType = ((ComboBoxItem)BoardComboBox.SelectedItem)?.Content.ToString();
            string selectedPort = (string)PortComboBox.SelectedItem;
            if (boardType != null && selectedPort != null)
            {
                string sketchPath = Path.Combine(GetProjectDirectory(), "Sketches", "UserSketch");
                await UploadCode(sketchPath, boardType, selectedPort);
            }
        }

        private void ClearLogsButton_Click(object sender, RoutedEventArgs e)
        {
            LogsTextBox.Text = string.Empty;
        }

        private async Task<string> SaveSketchAsync(string code)
        {
            string sketchDir = Path.Combine(GetProjectDirectory(), "Sketches", "UserSketch");
            Directory.CreateDirectory(sketchDir);

            string sketchPath = Path.Combine(sketchDir, "UserSketch.ino");
            await File.WriteAllTextAsync(sketchPath, code);

            return sketchPath;
        }

        private async Task CompileCode(string sketchPath, string boardType)
        {
            string arduinoCliPath = Path.Combine(GetProjectDirectory(), "Utils", "arduino-cli.exe");
            string compileCommand = $"{arduinoCliPath} compile --fqbn {boardType} {sketchPath}";

            await RunCommand(compileCommand);
        }

        private async Task UploadCode(string sketchPath, string boardType, string port)
        {
            string arduinoCliPath = Path.Combine(GetProjectDirectory(), "Utils", "arduino-cli.exe");
            string uploadCommand = $"{arduinoCliPath} upload -p {port} --fqbn {boardType} {sketchPath}";

            await RunCommand(uploadCommand);
        }

        private async Task RunCommand(string command)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/C {command}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8, // Ensure UTF-8 encoding
                    StandardErrorEncoding = Encoding.UTF8 // Ensure UTF-8 encoding
                }
            };

            process.OutputDataReceived += (sender, args) => Log(CleanAnsi(args.Data));
            process.ErrorDataReceived += (sender, args) => Log(CleanAnsi(args.Data));

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();
        }

        private void Log(string message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    LogsTextBox.Text += $"{message}\n";
                    ScrollToBottom();
                });
            }
        }

        private void ScrollToBottom()
        {
            LogsScrollViewer.ChangeView(null, LogsScrollViewer.ScrollableHeight, null);
        }

        private string CleanAnsi(string input)
        {
            if (input == null) return string.Empty;
            return Regex.Replace(input, @"\x1B\[[0-9;]*[mK]", string.Empty);
        }

        private string GetProjectDirectory()
        {
            string currentDir = AppContext.BaseDirectory;
            DirectoryInfo dirInfo = new DirectoryInfo(currentDir);

            while (dirInfo != null && !File.Exists(Path.Combine(dirInfo.FullName, "Buttonbox-Software.csproj")))
            {
                dirInfo = dirInfo.Parent;
            }

            if (dirInfo == null)
            {
                throw new DirectoryNotFoundException("Could not find the project directory.");
            }

            return dirInfo.FullName;
        }
    }
}
