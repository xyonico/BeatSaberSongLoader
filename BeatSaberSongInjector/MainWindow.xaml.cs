using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Configuration;
using System.Windows.Controls;
using System.Threading;
using System.Windows.Media;
using System.Timers;
using Timer = System.Timers.Timer;

namespace BSSI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool _beatSaberRunning;

        public MainWindow()
        {
            InitializeComponent();
            StartProcessTimer();
            KillDuplicateProcesses("BSSI");
            KillDuplicateProcesses("MonoJunkie");
        }

        private void StartProcessTimer()
        {
            var timer = new Timer(2000);
            timer.Elapsed += OnProcessTimerElapsed;
            timer.AutoReset = true;
            timer.Start();
            OnProcessTimerElapsed(null, null);
        }

        private void OnProcessTimerElapsed(object sender, ElapsedEventArgs e)
        {
            var pname = Process.GetProcessesByName("Beat Saber");
            if (pname.Length > 0)
            {
                if (_beatSaberRunning) return;
                OnBeatSaberStarted();
            }
            else
            {
                if (!_beatSaberRunning) return;
                OnBeatSaberExited();
            }
        }

        private void OnBeatSaberStarted()
        {
            Dispatcher.Invoke(() =>
            {
                _beatSaberRunning = true;
                InjectButton.IsEnabled = true;
                ProcessStatusLabel.Content = "Beat Saber found!";
                ProcessStatusLabel.Foreground = Brushes.Green;
            });
        }

        private void OnBeatSaberExited()
        {
            Dispatcher.Invoke(() =>
            {
                _beatSaberRunning = false;
                InjectButton.IsEnabled = false;
                ProcessStatusLabel.Content = "Beat Saber not detected...";
                ProcessStatusLabel.Foreground = Brushes.Red;
            });
        }

        private static void KillDuplicateProcesses(string processName)
        {
            var process = Process.GetProcessesByName(processName);
            var current = Process.GetCurrentProcess();
            foreach (var p in process)
            {
                if (p.Id != current.Id)
                {
                    p.Kill();
                }
            }
        }

        private void InjectButtonOnClick(object sender, RoutedEventArgs e)
        {
            InjectButton.IsEnabled = false;
            var sDll = ConfigurationManager.AppSettings["dll"];
            var sNamespace = ConfigurationManager.AppSettings["namespace"];
            var sClass = ConfigurationManager.AppSettings["class"];
            var sMethod = ConfigurationManager.AppSettings["method"];

            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "MonoJunkie/MonoJunkie.exe",
                    Arguments = string.Format("-dll \"{0}\" -namespace {1} -class {2} -method {3} -exe \"{4}\"", sDll, sNamespace, sClass, sMethod, "Beat Saber.exe"),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            if (proc.Start())
            {
                Redirect(proc.StandardOutput, proc.StandardError, ConsoleTextBlock);
            }

            proc.WaitForExit();
            InjectButton.IsEnabled = true;
        }

        private void Redirect(StreamReader input, StreamReader inputError, TextBlock output)
        {
            output.Text = string.Empty;
            new Thread(a =>
            {
                while (!input.EndOfStream)
                {
                    var line = input.ReadLine();
                    output.Dispatcher.Invoke(new Action(delegate
                    {
                        output.Text += line + Environment.NewLine;
                        ConsoleScroller.ScrollToBottom();
                    }));
                };
            }).Start();

            new Thread(a =>
            {
                while (!inputError.EndOfStream)
                {
                    var line = inputError.ReadLine();
                    output.Dispatcher.Invoke(new Action(delegate
                    {
                        output.Text += line + Environment.NewLine;
                        ConsoleScroller.ScrollToBottom();
                    }));
                };
            }).Start();
        }
    }
}
