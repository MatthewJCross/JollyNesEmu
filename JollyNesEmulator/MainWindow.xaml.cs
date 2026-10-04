using JollyNesEmulator.Controls;
using JollyNesEmulator.Emulator;
using JollyNesEmulator.Video;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using static JollyNesEmulator.Emulator.NesController;

namespace JollyNesEmulator
{
    public partial class MainWindow : Window
    {
        private readonly NesMachine _machine;
        private readonly NesVideoRenderer _videoRenderer;
        private readonly NesControllerInput _controllerInput;

        private readonly Stopwatch _fpsTimer = new Stopwatch();

        private Thread? _emulationThread;
        private readonly ManualResetEventSlim _emulationWakeEvent = new ManualResetEventSlim(false);
        private volatile bool _emulationRunning;
        private volatile bool _isPaused;

        private string? _romPath;
        private long _frameCount;
        private int _framesThisSecond;

        public MainWindow()
        {
            InitializeComponent();

            _machine = new NesMachine();
            _controllerInput = new NesControllerInput(_machine.Bus.Controller1!);

            _videoRenderer = new NesVideoRenderer();
            NesScreen.Source = _videoRenderer.Bitmap;

            UpdateUi();
        }

        private void LoadRomButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "Load NES ROM";
            dialog.Filter = "NES ROM Files (*.nes)|*.nes|All Files (*.*)|*.*";
            dialog.CheckFileExists = true;
            dialog.Multiselect = false;

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                if (!File.Exists(dialog.FileName))
                    return;

                StopEmulation();

                _machine.LoadRom(dialog.FileName);

                _romPath = dialog.FileName;
                _isPaused = false;
                _frameCount = 0;
                _framesThisSecond = 0;

                _fpsTimer.Restart();

                _videoRenderer.Update(_machine.Ppu);
                _machine.Ppu.ClearFrameReady();

                RomNameText.Text = Path.GetFileName(_romPath);
                StatusText.Text = "Running";
                CpuStatusText.Text = "CPU: Running";
                FpsText.Text = "FPS: 0";
                FrameText.Text = "Frame: 0";

                UpdateUi();

                StartEmulation();
            }
            catch (Exception ex)
            {
                _isPaused = true;

                MessageBox.Show(this, ex.Message, "Unable to Load ROM", MessageBoxButton.OK, MessageBoxImage.Error);

                StatusText.Text = "Load failed";
                CpuStatusText.Text = "CPU: Stopped";

                UpdateUi();
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_machine.IsRomLoaded)
            {
                StatusText.Text = "No ROM loaded";
                return;
            }

            try
            {
                StopEmulation();

                _machine.Reset();

                _frameCount = 0;
                _framesThisSecond = 0;
                _isPaused = false;

                _fpsTimer.Restart();

                _videoRenderer.Update(_machine.Ppu);
                _machine.Ppu.ClearFrameReady();

                StatusText.Text = "Running";
                CpuStatusText.Text = "CPU: Running";
                FpsText.Text = "FPS: 0";
                FrameText.Text = "Frame: 0";

                UpdateUi();

                StartEmulation();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Unable to Reset", MessageBoxButton.OK, MessageBoxImage.Error);

                StatusText.Text = "Reset failed";
                CpuStatusText.Text = "CPU: Stopped";

                UpdateUi();
            }
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_machine.IsRomLoaded)
            {
                StatusText.Text = "No ROM loaded";
                return;
            }

            if (_isPaused)
            {
                _isPaused = false;
                _fpsTimer.Restart();
                _framesThisSecond = 0;

                StatusText.Text = "Running";
                CpuStatusText.Text = "CPU: Running";

                _emulationWakeEvent.Set();
            }
            else
            {
                _isPaused = true;

                StatusText.Text = "Paused";
                CpuStatusText.Text = "CPU: Paused";
            }

            UpdateUi();
        }

        private void StartEmulation()
        {
            if (_emulationRunning)
                return;

            _emulationRunning = true;
            _isPaused = false;

            _emulationThread = new Thread(EmulationLoop);
            _emulationThread.IsBackground = true;
            _emulationThread.Name = "NES Emulation Thread";
            _emulationThread.Priority = ThreadPriority.AboveNormal;
            _emulationThread.Start();
        }

        private void StopEmulation()
        {
            _emulationRunning = false;
            _emulationWakeEvent.Set();

            if (_emulationThread != null && _emulationThread.IsAlive)
                _emulationThread.Join(1000);

            _emulationThread = null;
        }

        private void EmulationLoop()
        {
            Stopwatch frameTimer = Stopwatch.StartNew();
            long targetFrameTicks = Stopwatch.Frequency / 200;

            while (_emulationRunning)
            {
                if (_isPaused || !_machine.IsRomLoaded)
                {
                    _emulationWakeEvent.Wait(100);
                    _emulationWakeEvent.Reset();
                    frameTimer.Restart();
                    continue;
                }

                try
                {
                    _machine.RunFrame();

                    long frameTicks = frameTimer.ElapsedTicks;
                    long remainingTicks = targetFrameTicks - frameTicks;

                    //if (remainingTicks > 0)
                    //{
                    //    int sleepMilliseconds = (int)((remainingTicks * 1000) / Stopwatch.Frequency);

                    //    if (sleepMilliseconds > 0)
                    //        Thread.Sleep(sleepMilliseconds);
                    //}

                    frameTimer.Restart();

                    _frameCount++;
                    _framesThisSecond++;

                    long frame = _frameCount;
                    int fpsFrames = _framesThisSecond;

                    Dispatcher.BeginInvoke(() =>
                    {
                        if (!_emulationRunning)
                            return;

                        _videoRenderer.Update(_machine.Ppu);
                        _machine.Ppu.ClearFrameReady();

                        UpdateFrameStatistics(frame, fpsFrames);

                        //_machine.Ppu.PrintPaletteRequests();
                        //_machine.Ppu.DumpNametable(0x2000, 64);
                        if (_machine.Cpu.IsHalted)
                        {
                            _isPaused = true;
                            StatusText.Text = "CPU halted";
                            CpuStatusText.Text = "CPU: Halted";
                            UpdateUi();
                        }
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        _isPaused = true;
                        StatusText.Text = "Emulation error";
                        CpuStatusText.Text = "CPU: Error";
                        MessageBox.Show(this, ex.ToString(), "Emulation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        UpdateUi();
                    });

                    _emulationRunning = false;
                }
            }
        }

        private void UpdateFrameStatistics(long frameCount, int framesThisSecond)
        {
            FrameText.Text = $"Frame: {frameCount}";
            CpuStatusText.Text = $"CPU: PC=${_machine.Cpu.PC:X4}  A=${_machine.Cpu.A:X2}  X=${_machine.Cpu.X:X2}  Y=${_machine.Cpu.Y:X2}";

            if (_fpsTimer.ElapsedMilliseconds >= 1000)
            {
                FpsText.Text = $"FPS: {framesThisSecond}";
                _framesThisSecond = 0;
                _fpsTimer.Restart();
            }
        }

        private void UpdateUi()
        {
            bool loaded = _machine.IsRomLoaded;

            LoadRomButton.IsEnabled = true;
            ResetButton.IsEnabled = loaded;
            PauseButton.IsEnabled = loaded;
            PauseButton.Content = _isPaused ? "Resume" : "Pause";
        }

        protected override void OnClosed(EventArgs e)
        {
            StopEmulation();
            _emulationWakeEvent.Dispose();
            base.OnClosed(e);
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.IsRepeat)
                return;

            _controllerInput.KeyDown(e.Key);

            if (e.Key is Key.Z or Key.X or Key.Enter or Key.RightShift or Key.Up or Key.Down or Key.Left or Key.Right)
                e.Handled = true;
        }

        private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            _controllerInput.KeyUp(e.Key);

            if (e.Key is Key.Z or Key.X or Key.Enter or Key.RightShift or Key.Up or Key.Down or Key.Left or Key.Right)
                e.Handled = true;
        }
    }
}

