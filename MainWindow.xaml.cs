﻿using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Linq;
using System.Windows.Media;
using System.Globalization;
using System.Windows.Data;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;
using System.Threading.Tasks;
using System.IO;
using Size = System.Drawing.Size;
using Point = System.Drawing.Point;
using System.Windows.Threading;
using System.Windows.Input;
using System.Threading;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using NAudio.CoreAudioApi;
using System.Data;
using Windows.Devices.Sms;
using System.Windows.Controls;
using Application = System.Windows.Application;
using NAudio.Wave;
using AudioSwitcher.AudioApi.CoreAudio;
using System.Text;
using MessageBox = System.Windows.MessageBox;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Button = System.Windows.Controls.Button;
using System.Windows.Controls.Primitives;
using Windows.UI.Xaml.Controls;
using Grid = System.Windows.Controls.Grid;
using ComboBox = System.Windows.Forms.ComboBox;
using System.Windows.Media.Effects;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using System.Windows.Interop;
using System.Reflection;
using Border = System.Windows.Controls.Border;
using Color = System.Windows.Media.Color;
using NAudio.Utils;

namespace WindowSelector
{
    public class WindowItem
    {
        public string Name { get; set; }
        public Process Process { get; set; }
        public IntPtr WindowHandle { get; set; }
        public BitmapImage Preview { get; set; } // Store the preview image
    }

    public class WindowSinker
    {
        #region Properties

        private const UInt32 SWP_NOSIZE = 0x0001;
        private const UInt32 SWP_NOMOVE = 0x0002;
        private const UInt32 SWP_NOACTIVATE = 0x0010;
        private const UInt32 SWP_NOZORDER = 0x0004;
        private const int WM_ACTIVATEAPP = 0x001C;
        private const int WM_ACTIVATE = 0x0006;
        private const int WM_SETFOCUS = 0x0007;
        private const int WM_WINDOWPOSCHANGING = 0x0046;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(1);

        private Window Window = null;

        #endregion Properties

        #region WindowSinker

        public WindowSinker(Window Window)
        {
            this.Window = Window;
        }

        #endregion WindowSinker

        #region Methods

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr DeferWindowPos(IntPtr hWinPosInfo, IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr BeginDeferWindowPos(int nNumWindows);

        [DllImport("user32.dll")]
        private static extern bool EndDeferWindowPos(IntPtr hWinPosInfo);

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var Handle = (new WindowInteropHelper(Window)).Handle;

            var Source = HwndSource.FromHwnd(Handle);
            Source.RemoveHook(new HwndSourceHook(WndProc));
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var Hwnd = new WindowInteropHelper(Window).Handle;
            SetWindowPos(Hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);

            var Handle = (new WindowInteropHelper(Window)).Handle;

            var Source = HwndSource.FromHwnd(Handle);
            Source.AddHook(new HwndSourceHook(WndProc));
        }

        private IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_SETFOCUS)
            {
                hWnd = new WindowInteropHelper(Window).Handle;
                SetWindowPos(hWnd, (IntPtr)(-1), 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE);
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Sink()
        {
            Window.Loaded += OnLoaded;
            Window.Closing += OnClosing;
        }

        public void Unsink()
        {
            Window.Loaded -= OnLoaded;
            Window.Closing -= OnClosing;
        }

        #endregion Methods
    }

    public static class WindowExtensions
    {
        #region Always On Bottom

        public static readonly DependencyProperty SinkerProperty = DependencyProperty.RegisterAttached("Sinker", typeof(WindowSinker), typeof(WindowExtensions), new UIPropertyMetadata(null));

        public static WindowSinker GetSinker(DependencyObject obj)
        {
            return (WindowSinker)obj.GetValue(SinkerProperty);
        }

        public static void SetSinker(DependencyObject obj, WindowSinker value)
        {
            obj.SetValue(SinkerProperty, value);
        }

        public static readonly DependencyProperty AlwaysOnBottomProperty = DependencyProperty.RegisterAttached("AlwaysOnBottom", typeof(bool), typeof(WindowExtensions), new UIPropertyMetadata(false, OnAlwaysOnBottomChanged));

        public static bool GetAlwaysOnBottom(DependencyObject obj)
        {
            return (bool)obj.GetValue(AlwaysOnBottomProperty);
        }

        public static void SetAlwaysOnBottom(DependencyObject obj, bool value)
        {
            obj.SetValue(AlwaysOnBottomProperty, value);
        }

        private static void OnAlwaysOnBottomChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var Window = sender as Window;
            if (Window != null)
            {
                if ((bool)e.NewValue)
                {
                    var Sinker = new WindowSinker(Window);
                    Sinker.Sink();
                    SetSinker(Window, Sinker);
                }
                else
                {
                    var Sinker = GetSinker(Window);
                    Sinker.Unsink();
                    SetSinker(Window, null);
                }
            }
        }

        #endregion Always On Bottom
    }

    public class LazyImageLoader : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IntPtr hwnd && hwnd != IntPtr.Zero)
            {
                Bitmap resizedBmp = null;
                try
                {
                    Bitmap bmp = MainWindow.PrintWindow(hwnd); // Capture the window image

                    double scaleFactor = 1.5;

                    // Calculate the maximum width based on the screen width and scale factor
                    int maxWidth = (int)(System.Windows.SystemParameters.PrimaryScreenWidth * scaleFactor);
                    int maxHeight = (int)(System.Windows.SystemParameters.PrimaryScreenHeight * scaleFactor);

                    double ratioX = (double)maxWidth / bmp.Width;
                    double ratioY = (double)maxHeight / bmp.Height;
                    double ratio = Math.Min(ratioX, ratioY);

                    int newWidth = (int)(bmp.Width * ratio);
                    int newHeight = (int)(bmp.Height * ratio);

                    resizedBmp = new Bitmap(bmp, newWidth, newHeight); // Use the resized bitmap

                    using (MemoryStream memory = new MemoryStream())
                    {
                        resizedBmp.Save(memory, System.Drawing.Imaging.ImageFormat.Jpeg); // Save as JPEG
                        memory.Position = 0;
                        BitmapImage bitmapimage = new BitmapImage();
                        bitmapimage.BeginInit();
                        bitmapimage.StreamSource = memory;
                        bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapimage.EndInit();
                        bitmapimage.Freeze(); // Important for use in another thread
                        return bitmapimage;
                    }
                }
                catch
                {
                    // Handle exceptions or return a default image
                    return DependencyProperty.UnsetValue;
                }
                finally
                {
                    resizedBmp?.Dispose(); // Ensure resizedBmp is disposed of correctly
                }
            }

            return DependencyProperty.UnsetValue; // Return an unset value if conversion is not possible
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class AudioDevice
    {
        public string Name { get; set; }
        public Guid Id { get; set; }
        public bool IsInput { get; set; } // True for input devices, false for output
    }

    // Convert window titles to uppercase function
    public class StringToUpperConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value.ToString().ToUpper();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class SimplifyDeviceNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var fullName = value as string;
            if (string.IsNullOrEmpty(fullName)) return "";

            // Example logic to trim after the first occurrence of '('
            var simplifiedName = fullName.Split('(')[0].Trim();
            return simplifiedName;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CompositeConverter : IValueConverter
    {
        public IValueConverter First { get; set; }
        public IValueConverter Second { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var firstResult = First.Convert(value, targetType, parameter, culture);
            return Second.Convert(firstResult, targetType, parameter, culture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public partial class MainWindow : Window
    {
        private readonly Controller controller;

        private const int ASFW_ANY = -1;

        private NotifyIcon trayIcon;
        private GamepadButtonFlags previousButtons = GamepadButtonFlags.None;
        private DispatcherTimer gamepadTimer;
        private Gamepad previousGamepadState;
        private bool aButtonPressed = false;

        private WindowSinker sinker;
        private DispatcherTimer topmostCheckTimer;

        public MainWindow()
        {
            sinker = new WindowSinker(this);
            sinker.Sink();
            InitializeComponent();
            InitializeMaterialDesign();
            InitializeGamepadPolling();
            _ = PopulateAudioDevicesAsync();
            AllowSetForegroundWindow(ASFW_ANY); // Allow any process to bring this window to foreground

            if (this.WindowState == WindowState.Minimized)
            {
                this.WindowState = WindowState.Normal;
            }
            this.Activate();
            BringApplicationToFront();

            refreshTimer = new DispatcherTimer();
            refreshTimer.Interval = TimeSpan.FromSeconds(1); // Adjust the interval as needed
            refreshTimer.Tick += (sender, e) => RefreshWindowTitlesIfNeeded();
            refreshTimer.Start();

            // Initialize knownWindowHandles to force the first refresh
            var initialWindows = GetOpenWindows();
            knownWindowHandles = new HashSet<IntPtr>(initialWindows.Select(w => w.MainWindowHandle));

            // Explicitly populate the window list at startup with the current windows
            RefreshWindowTitles(initialWindows); // Now correctly passing the initialWindows as the argument

            this.WindowState = WindowState.Maximized;
            this.Width = SystemParameters.PrimaryScreenWidth;
            this.Height = SystemParameters.PrimaryScreenHeight;

            this.ShowInTaskbar = false;
            trayIcon = new NotifyIcon();
            trayIcon.Icon = new System.Drawing.Icon("OS.ico");
            trayIcon.Visible = false;

            // Add the application icon to the system tray
            this.Loaded += MainWindow_Loaded;
            // Ensure the tray icon is removed when the application exits
            Application.Current.Exit += Current_Exit;

            System.Windows.Forms.ContextMenu trayMenu = new System.Windows.Forms.ContextMenu();
            System.Windows.Forms.MenuItem quitItem = new System.Windows.Forms.MenuItem("Quit");
            quitItem.Click += (sender, e) => System.Windows.Application.Current.Shutdown();
            trayMenu.MenuItems.Add(quitItem);
            trayIcon.ContextMenu = trayMenu;

            controller = new Controller(UserIndex.One);

            this.MouseDown += new MouseButtonEventHandler(MainWindow_MouseDown);
        }

        private DispatcherTimer refreshTimer;

        // Define the constants
        private const UInt32 SWP_NOSIZE = 0x0001;

        private const UInt32 SWP_NOMOVE = 0x0002;

        // Declare the external method
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var Hwnd = new WindowInteropHelper(this).Handle; // Use 'this' to refer to the current window instance
            SetWindowPos(Hwnd, (IntPtr)(-1), 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE);
        }

        private void BringApplicationToFront()
        {
            var windowHandle = new WindowInteropHelper(this).Handle;
            SetForegroundWindow(windowHandle);
            BringWindowToTop(windowHandle);
        }

        private DispatcherTimer focusCheckTimer;

        private void SetupFocusCheckTimer()
        {
            focusCheckTimer = new DispatcherTimer();
            focusCheckTimer.Interval = TimeSpan.FromMilliseconds(500); // Check every 500 milliseconds
            focusCheckTimer.Tick += FocusCheckTimer_Tick;
            focusCheckTimer.Start();
        }

        private void FocusCheckTimer_Tick(object sender, EventArgs e)
        {
            IntPtr foregroundWindow = GetForegroundWindow();
            IntPtr myWindowHandle = new WindowInteropHelper(this).Handle;
            if (foregroundWindow != myWindowHandle)
            {
                // Your window is not at the front
                MinimizeBlockingWindow(foregroundWindow);
            }
        }
        private void MinimizeBlockingWindow(IntPtr windowHandle)
        {
            // Minimize the window that's currently blocking ours
            ShowWindow(windowHandle, SW_MINIMIZE);
        }

        private void EnsureAlwaysOnTop()
        {
            // Temporarily disable TopMost to trigger internal window state update
            this.Topmost = false;
            this.Activate();
            this.Topmost = true;

            // Attempt to bring window to the foreground
            var hwnd = new WindowInteropHelper(this).Handle;
            SetForegroundWindow(hwnd);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            MakeWindowAlwaysOnTop(); // Ensure window is always on top when loaded
            SetupWindowMessageHook(); // Start monitoring window messages

            // Existing logic...
            if (!trayIcon.Visible)
            {
                trayIcon.Visible = true;
                HideAudioDevicesPopup();
                this.Hide(); // Hide main window initially, if that's the intended behavior
            }
        }

        private void Current_Exit(object sender, ExitEventArgs e)
        {
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }
        }

        private void InitializeGamepadPolling()
        {
            gamepadTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(10) // Needs more adjustments
            };
            gamepadTimer.Tick += GamepadPollingTick;
            gamepadTimer.Start();
        }

        private bool previousDpadUpPressed = false;
        private bool previousDpadDownPressed = false;

        private void GamepadPollingTick(object sender, EventArgs e)
        {
            if (!controller.IsConnected) return;

            var gamepadState = controller.GetState().Gamepad;
            bool l1Pressed = gamepadState.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder);
            bool r1Pressed = gamepadState.Buttons.HasFlag(GamepadButtonFlags.RightShoulder);
            bool dpadLeftPressed = gamepadState.Buttons.HasFlag(GamepadButtonFlags.DPadLeft);
            bool dpadRightPressed = gamepadState.Buttons.HasFlag(GamepadButtonFlags.DPadRight);
            bool yPressed = gamepadState.Buttons.HasFlag(GamepadButtonFlags.Y);
            bool xPressed = gamepadState.Buttons.HasFlag(GamepadButtonFlags.X);
            bool windowListboxUp = gamepadState.Buttons.HasFlag(GamepadButtonFlags.DPadUp);
            bool windowListboxDown = gamepadState.Buttons.HasFlag(GamepadButtonFlags.DPadDown);

            // Check if the current application is the active window
            IntPtr foregroundWindow = GetForegroundWindow();
            GetWindowThreadProcessId(foregroundWindow, out int foregroundProcId);
            Process foregroundProc = Process.GetProcessById(foregroundProcId);
            if (this.WindowState == WindowState.Minimized || !this.IsVisible)
            {
                // Restore window from tray if L1, R1, and DPad Left are pressed
                if (l1Pressed && r1Pressed && dpadLeftPressed)
                {
                    Dispatcher.Invoke(() =>
                    {
                        RestoreWindowFromTray();
                    });
                }
                // Ignore further gamepad inputs since the window is minimized or not visible
                return;
            }

            // Check if the application is minimized
            if (this.WindowState == WindowState.Minimized)
            {
                // Restore window from tray if L1, R1, and DPad Left are pressed
                if (gamepadState.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder) &&
                    gamepadState.Buttons.HasFlag(GamepadButtonFlags.RightShoulder) &&
                    gamepadState.Buttons.HasFlag(GamepadButtonFlags.DPadLeft))
                {
                    Dispatcher.Invoke(() =>
                    {
                        RestoreWindowFromTray();
                    });
                }
                else
                {
                    return; // Ignore other inputs if the application is minimized
                }
            }
            else if (AudioDevicesPopup.IsOpen)
            {
                // Prevent double dpad input when activating the popup
                if ((DateTime.Now - lastMenuOpenTime).TotalMilliseconds < 150)
                {
                    // Not enough time has passed, ignore the input
                    return;
                }

                // Directly select output or input tab using DPad left and right
                if (dpadLeftPressed)
                {
                    AudioDeviceTabs.SelectedIndex = 0; // Output tab selected
                }
                else if (dpadRightPressed)
                {
                    AudioDeviceTabs.SelectedIndex = 1; // Input tab selected
                }
                else
                {
                    // Handle input specifically for the selected device list (input or output)
                    var selectedTab = AudioDeviceTabs.SelectedItem as TabItem;
                    var AudioDeviceListBox = selectedTab.Content as System.Windows.Controls.ListBox;

                    // Ensure we only move the selection if the DPad was not previously pressed
                    if (windowListboxUp && !previousGamepadState.Buttons.HasFlag(GamepadButtonFlags.DPadUp))
                    {
                        // Prevent index from going below 0
                        if (AudioDeviceListBox.SelectedIndex > 0)
                        {
                            AudioDeviceListBox.SelectedIndex--;
                        }
                    }
                    else if (windowListboxDown && !previousGamepadState.Buttons.HasFlag(GamepadButtonFlags.DPadDown))
                    {
                        // Prevent index from exceeding the count of items
                        if (AudioDeviceListBox.SelectedIndex < AudioDeviceListBox.Items.Count - 1)
                        {
                            AudioDeviceListBox.SelectedIndex++;
                        }
                    }

                    // confirm audio device selection
                    if (gamepadState.Buttons.HasFlag(GamepadButtonFlags.A) && !previousGamepadState.Buttons.HasFlag(GamepadButtonFlags.A))
                    {
                        var selectedDevice = AudioDeviceListBox.SelectedItem as AudioDevice;

                        if (selectedDevice != null)
                        {
                            ChangeAudioDeviceToSelected(selectedDevice);
                            aButtonPressed = false;
                        }
                    }

                    // close audio devices popup
                    if (gamepadState.Buttons.HasFlag(GamepadButtonFlags.B) && !previousGamepadState.Buttons.HasFlag(GamepadButtonFlags.B))
                    {
                        HideAudioDevicesPopup();
                    }
                }
                // Save the current gamepad state for the next tick
                previousGamepadState = gamepadState;
            }
            else
            {
                // If the audio menu is not visible and the 'B' button is pressed, hide the main window to the tray.
                if (gamepadState.Buttons.HasFlag(GamepadButtonFlags.B) && !previousGamepadState.Buttons.HasFlag(GamepadButtonFlags.B))
                {
                    Dispatcher.Invoke(() =>
                    {
                        this.Hide(); // Hide the main window
                        trayIcon.Visible = true; // Make sure the tray icon is visible
                        HideAudioDevicesPopup();
                    });
                }

                if (gamepadState.Buttons.HasFlag(GamepadButtonFlags.A) && !previousGamepadState.Buttons.HasFlag(GamepadButtonFlags.A))
                {
                    aButtonPressed = true;

                    // Minimize the selected window first
                    if (WindowListBox.SelectedItem != null)
                    {
                        dynamic selectedItem = WindowListBox.SelectedItem;
                        var process = selectedItem.Process as Process;
                        if (process != null && process.MainWindowHandle != IntPtr.Zero)
                        {
                            // Assuming ShowWindow and SW_MINIMIZE are already defined appropriately
                            ShowWindow(process.MainWindowHandle, SW_MINIMIZE);
                        }
                    }

                    // Now proceed to hide the main window and show the tray icon
                    WindowListBox_SelectionChanged(WindowListBox, null); // Assuming this is still required for your logic
                    this.Hide();
                    trayIcon.Visible = true;
                    HideAudioDevicesPopup();

                    aButtonPressed = false;
                }

                if (yPressed && !previousButtons.HasFlag(GamepadButtonFlags.Y))
                {
                    // Minimize the selected window
                    if (WindowListBox.SelectedItem != null)
                    {
                        dynamic selectedItem = WindowListBox.SelectedItem;
                        var process = selectedItem.Process as Process;
                        if (process != null && process.MainWindowHandle != IntPtr.Zero)
                        {
                            ShowWindow(process.MainWindowHandle, SW_MINIMIZE);
                            this.Activate();
                            this.Focus();
                            SetForegroundWindow(new WindowInteropHelper(this).Handle);
                        }
                    }
                }

                if (xPressed && !previousButtons.HasFlag(GamepadButtonFlags.X))
                {
                    // More gracefully close the selected window
                    if (WindowListBox.SelectedItem != null)
                    {
                        dynamic selectedItem = WindowListBox.SelectedItem;
                        var process = selectedItem.Process as Process;
                        if (process != null)
                        {
                            if (process.MainWindowHandle != IntPtr.Zero)
                            {
                                PostMessage(process.MainWindowHandle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                            }
                            else
                            {
                                try
                                {
                                    process.Kill();
                                }
                                catch (Exception ex)
                                {
                                    // Handle the exception, log, etc
                                }
                            }
                        }
                    }
                }

                // DPad Up navigation logic
                if (windowListboxUp)
                {
                    if (!previousDpadUpPressed)
                    {
                        if (WindowListBox.SelectedIndex > 0)
                        {
                            WindowListBox.SelectedIndex--;
                        }
                        previousDpadUpPressed = true;
                    }
                }
                else
                {
                    previousDpadUpPressed = false;
                }

                // DPad Down navigation logic
                if (windowListboxDown)
                {
                    if (!previousDpadDownPressed)
                    {
                        if (WindowListBox.SelectedIndex < WindowListBox.Items.Count - 1)
                        {
                            WindowListBox.SelectedIndex++;
                        }
                        previousDpadDownPressed = true;
                    }
                }
                else
                {
                    previousDpadDownPressed = false;
                }

                if (dpadRightPressed && !previousButtons.HasFlag(GamepadButtonFlags.DPadRight))
                {
                    // show list of audio devices
                    ShowAudioDevicesPopup();

                    lastMenuOpenTime = DateTime.Now;
                }

                if (gamepadState.Buttons.HasFlag(GamepadButtonFlags.B) && !previousButtons.HasFlag(GamepadButtonFlags.B))
                {
                    if (!isAudioDeviceListVisible)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            // hide the audio devices popup
                            AudioDevicesPopup.IsOpen = false;
                        });
                    }
                    else
                    {
                        this.Hide();
                        trayIcon.Visible = true;
                        HideAudioDevicesPopup();
                    }
                }
            }

            if (gamepadState.Buttons.HasFlag(GamepadButtonFlags.B) && !previousGamepadState.Buttons.HasFlag(GamepadButtonFlags.B))
            { // if the tray icon is not visible and the 'B' button is pressed, hide the main window to the tray
                if (!trayIcon.Visible)
                {
                    Dispatcher.Invoke(() =>
                    {
                        this.Hide(); // Hide the main window
                        trayIcon.Visible = true; // Make sure the tray icon is visible
                        HideAudioDevicesPopup();
                    });
                }
            }
            previousGamepadState = gamepadState;
        }

        private DateTime lastMenuOpenTime = DateTime.MinValue;

        private void ShowAudioDevicesPopup()
        {
            // Ensure the popup is closed before adjusting its position, to avoid visual glitches.
            AudioDevicesPopup.IsOpen = false;

            // Calculate the desired position for the popup.
            var windowLocation = this.PointToScreen(new System.Windows.Point(0, 0));
            var windowHeight = this.ActualHeight;
            var windowWidth = this.ActualWidth;

            var popupHeight = PopupContent.ActualHeight;
            var popupWidth = PopupContent.ActualWidth;

            // Set the position to the bottom right of the window.
            AudioDevicesPopup.HorizontalOffset = windowLocation.X + windowWidth - popupWidth - 20;
            AudioDevicesPopup.VerticalOffset = windowLocation.Y + windowHeight - popupHeight - 20;

            // Now open the popup.
            AudioDevicesPopup.IsOpen = true;

            // Start the animation, if any.
            var popInStoryboard = FindResource("OpenAudioDevicePopupAnimation") as Storyboard;
            if (popInStoryboard != null)
            {
                Storyboard.SetTarget(popInStoryboard, PopupContent);
                popInStoryboard.Begin();
            }
            ApplyBlurEffectToMainWindowContent(true);
        }

        private void HideAudioDevicesPopup()
        {
            var popOutStoryboard = FindResource("CloseAudioDevicePopupAnimation") as Storyboard;

            if (popOutStoryboard != null)
            {
                popOutStoryboard.Completed += (s, e) =>
                {
                    AudioDevicesPopup.IsOpen = false;
                };
                Storyboard.SetTarget(popOutStoryboard, PopupContent);
                popOutStoryboard.Begin();
                ApplyBlurEffectToMainWindowContent(false);
            }
            else
            {
                AudioDevicesPopup.IsOpen = false;
            }
        }

        private void ApplyBlurEffectToMainWindowContent(bool apply)
        {
            if (apply)
            {
                var blur = new BlurEffect();
                RootPanel.Effect = blur;

                var animation = new DoubleAnimation
                {
                    From = 0,
                    To = 25, // Target blur radius
                    Duration = TimeSpan.FromSeconds(0.5),
                    FillBehavior = FillBehavior.Stop // Stops the animation at its final value
                };

                animation.Completed += (s, e) => blur.Radius = 25;
                blur.BeginAnimation(BlurEffect.RadiusProperty, animation);
            }
            else
            {
                if (RootPanel.Effect is BlurEffect blur)
                {
                    var animation = new DoubleAnimation
                    {
                        To = 0, // Animate back to no blur
                        Duration = TimeSpan.FromSeconds(0.5),
                    };

                    animation.Completed += (s, e) => RootPanel.Effect = null;
                    blur.BeginAnimation(BlurEffect.RadiusProperty, animation);
                }
            }
        }

        private void InitializeMaterialDesign()
        {
            // Create dummy objects to force the MaterialDesign assemblies to be loaded
            // from this assembly, which causes the MaterialDesign assemblies to be searched
            // relative to this assembly's path. Otherwise, the MaterialDesign assemblies
            // are searched relative to Eclipse's path, so they're not found.
            var card = new Card();
            var hue = new Hue("Dummy", Colors.Black, Colors.White);
        }

        private CustomPopupPlacement[] PopupCustomPlacementMethod(Size popupSize, Size targetSize, Point offset)
        {
            var screen = System.Windows.SystemParameters.WorkArea;
            var rightEdge = screen.Right;
            var popupX = rightEdge - popupSize.Width;
            var popupY = (screen.Height / 2) - (popupSize.Height / 2); // Center vertically

            return new CustomPopupPlacement[] { new CustomPopupPlacement(new System.Windows.Point(popupX, popupY), PopupPrimaryAxis.None) };
        }

        private async Task PopulateAudioDevicesAsync()
        {
            LoadingTextBlock.Visibility = Visibility.Visible;

            // Separate lists for input and output devices
            var outputDeviceList = new List<AudioDevice>();
            var inputDeviceList = new List<AudioDevice>();

            await Task.Run(() =>
            {
                var controller = new CoreAudioController();
                // Fetch output devices
                outputDeviceList = controller.GetPlaybackDevices(AudioSwitcher.AudioApi.DeviceState.Active)
                    .Select(d => new AudioDevice { Name = d.FullName, Id = d.Id, IsInput = false }).ToList();

                // Fetch input devices
                inputDeviceList = controller.GetCaptureDevices(AudioSwitcher.AudioApi.DeviceState.Active)
                    .Select(d => new AudioDevice { Name = d.FullName, Id = d.Id, IsInput = true }).ToList();
            });

            Dispatcher.Invoke(() =>
            {
                AudioOutputDeviceList.ItemsSource = outputDeviceList;
                AudioInputDeviceList.ItemsSource = inputDeviceList;
                shouldUpdateDevices = false; // Reset the flag
                LoadingTextBlock.Visibility = Visibility.Collapsed;
                AudioOutputDeviceList.Visibility = Visibility.Visible; // Make the list visible
            });
        }

        private async Task SetDefaultAudioDeviceAsync(Guid deviceId)
        {
            try
            {
                var controller = new AudioSwitcher.AudioApi.CoreAudio.CoreAudioController();
                var device = await controller.GetDeviceAsync(deviceId).ConfigureAwait(false);
                if (device != null)
                {
                    await device.SetAsDefaultAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                // Since this runs in a background thread, use Dispatcher.Invoke to show the message box
                Dispatcher.Invoke(() =>
                    MessageBox.Show($"Error setting default audio device: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error)
                );
            }
        }

        private DateTime lastRefreshTime = DateTime.MinValue;

        private void MainWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Check if the left mouse button was clicked
            if (e.ButtonState == MouseButtonState.Pressed && e.ChangedButton == MouseButton.Left)
            {
                aButtonPressed = true;
                WindowListBox_SelectionChanged(WindowListBox, null);
                this.Hide();
                trayIcon.Visible = true;
            }
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            this.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 0, 0, 0));
        }

        private bool isAudioDeviceListVisible = false;

        private HashSet<IntPtr> knownWindowHandles = new HashSet<IntPtr>();

        private void RefreshWindowTitlesIfNeeded()
        {
            var currentWindows = GetOpenWindows();
            var currentHandles = new HashSet<IntPtr>(currentWindows.Select(w => w.MainWindowHandle));

            // Check if the set of window handles has changed since the last check
            if (!currentHandles.SetEquals(knownWindowHandles))
            {
                RefreshWindowTitles(currentWindows); // Pass the current windows to avoid fetching them again
                knownWindowHandles = currentHandles; // Update the known handles
            }
        }

        private void RefreshWindowTitles(IEnumerable<Process> currentWindows)
        {
            int selectedIndex = WindowListBox.SelectedIndex;
            WindowListBox.ItemsSource = currentWindows.Select(p =>
            {
                var item = new WindowItem
                {
                    Name = GetFriendlyName(p.ProcessName).ToUpper(),
                    Process = p,
                    WindowHandle = p.MainWindowHandle // Store the handle directly
                };
                return item;
            }).ToList();
            WindowListBox.SelectedIndex = Math.Min(selectedIndex, WindowListBox.Items.Count - 1);
        }

        private DateTime lastNavigationTime = DateTime.MinValue;

        private List<CoreAudioDevice> audioDeviceCache = null;
        private bool shouldUpdateDevices = true;

        public List<MMDevice> GetAudioOutputDevices()
        {
            var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();
            return devices;
        }

        private async void ChangeAudioDeviceToSelected(AudioDevice selectedDevice)
        {
            if (selectedDevice == null) return;

            try
            {
                // Reset UI to initial state explicitly
                Dispatcher.Invoke(() =>
                {
                    // Ensure LoadingText is visible
                    LoadingText.Visibility = Visibility.Visible;
                    // Reset backgrounds to transparent
                    LoadingTextBackground.Background = new SolidColorBrush(Colors.Transparent);
                    CompletedTextBackground.Background = new SolidColorBrush(Colors.Transparent);
                });

                // Simulate the operation
                await Task.Run(() => SetDefaultAudioDeviceAsync(selectedDevice.Id));

                // Immediately after operation, prepare for showing CompletedText
                Dispatcher.Invoke(() =>
                {
                    // Hide LoadingText
                    LoadingText.Visibility = Visibility.Collapsed;
                    // Set background to green for visibility and make CompletedText visible and fully opaque
                    CompletedTextBackground.Background = new SolidColorBrush(Colors.Green);
                    CompletedText.Visibility = Visibility.Visible;
                    CompletedText.Opacity = 1;
                });

                // Immediately fade out after ensuring CompletedText is visible
                var fadeOutAnimation = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = new Duration(TimeSpan.FromSeconds(2))
                };

                // Apply fade-out animation to both CompletedText and its background
                Dispatcher.Invoke(() =>
                {
                    CompletedTextBackground.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation);
                    CompletedText.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation);
                });

                // Wait for the fade-out to complete before resetting UI
                await Task.Delay(TimeSpan.FromSeconds(2)); // This delay ensures the fade-out animation completes
                Dispatcher.Invoke(() =>
                {
                    // Hide and reset CompletedText for next operation
                    CompletedText.Visibility = Visibility.Collapsed;
                    CompletedText.Opacity = 0; // Ensure it's ready for next use
                                               // Reset background colors to transparent
                    CompletedTextBackground.Background = new SolidColorBrush(Colors.Transparent);
                    CompletedTextBackground.Opacity = 1; // Reset opacity to fully opaque for next use
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                    MessageBox.Show($"Error setting default audio device: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error));
            }
        }

        private async Task AnimateBackgroundColor(Border target, Color fromColor, Color toColor, TimeSpan duration)
        {
            var animation = new ColorAnimation
            {
                From = fromColor,
                To = toColor,
                Duration = duration
            };

            Dispatcher.Invoke(() =>
            {
                var brush = new SolidColorBrush(fromColor);
                target.Background = brush;
                brush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
            });

            await Task.Delay(duration);
        }

        private async Task FadeOutUIElement(UIElement element, TimeSpan duration)
        {
            var fadeOutAnimation = new DoubleAnimation
            {
                From = 1.0, // Start fully opaque
                To = 0.0, // End completely transparent
                Duration = duration
            };

            Storyboard storyboard = new Storyboard();
            storyboard.Children.Add(fadeOutAnimation);
            Storyboard.SetTarget(fadeOutAnimation, element);
            Storyboard.SetTargetProperty(fadeOutAnimation, new PropertyPath("Opacity"));
            storyboard.Begin();
        }

        //
        // Credit to the Dev of "Handheld Control Panel" for the following functions and structs. Thank you, kind sir!
        //

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, int nFlags);

        public static Bitmap PrintWindow(IntPtr hwnd)
        {
            RECT rc;
            GetWindowRect(hwnd, out rc);

            Bitmap bmp = new Bitmap(rc.Width, rc.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Graphics gfxBmp = Graphics.FromImage(bmp);
            IntPtr hdcBitmap = gfxBmp.GetHdc();

            PrintWindow(hwnd, hdcBitmap, 2);

            gfxBmp.ReleaseHdc(hdcBitmap);
            gfxBmp.Dispose();

            return bmp;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            private int _Left;
            private int _Top;
            private int _Right;
            private int _Bottom;

            public RECT(RECT Rectangle) : this(Rectangle.Left, Rectangle.Top, Rectangle.Right, Rectangle.Bottom)
            {
            }

            public RECT(int Left, int Top, int Right, int Bottom)
            {
                _Left = Left;
                _Top = Top;
                _Right = Right;
                _Bottom = Bottom;
            }

            public int X
            {
                get { return _Left; }
                set { _Left = value; }
            }

            public int Y
            {
                get { return _Top; }
                set { _Top = value; }
            }

            public int Left
            {
                get { return _Left; }
                set { _Left = value; }
            }

            public int Top
            {
                get { return _Top; }
                set { _Top = value; }
            }

            public int Right
            {
                get { return _Right; }
                set { _Right = value; }
            }

            public int Bottom
            {
                get { return _Bottom; }
                set { _Bottom = value; }
            }

            public int Height
            {
                get { return _Bottom - _Top; }
                set { _Bottom = value + _Top; }
            }

            public int Width
            {
                get { return _Right - _Left; }
                set { _Right = value + _Left; }
            }

            public Point Location
            {
                get { return new Point(Left, Top); }
                set
                {
                    _Left = value.X;
                    _Top = value.Y;
                }
            }

            public Size Size
            {
                get { return new Size(Width, Height); }
                set
                {
                    _Right = value.Width + _Left;
                    _Bottom = value.Height + _Top;
                }
            }

            public static implicit operator Rectangle(RECT Rectangle)
            {
                return new Rectangle(Rectangle.Left, Rectangle.Top, Rectangle.Width, Rectangle.Height);
            }

            public static implicit operator RECT(Rectangle Rectangle)
            {
                return new RECT(Rectangle.Left, Rectangle.Top, Rectangle.Right, Rectangle.Bottom);
            }

            public static bool operator ==(RECT Rectangle1, RECT Rectangle2)
            {
                return Rectangle1.Equals(Rectangle2);
            }

            public static bool operator !=(RECT Rectangle1, RECT Rectangle2)
            {
                return !Rectangle1.Equals(Rectangle2);
            }

            public override string ToString()
            {
                return "{Left: " + _Left + "; " + "Top: " + _Top + "; Right: " + _Right + "; Bottom: " + _Bottom + "}";
            }

            public override int GetHashCode()
            {
                return ToString().GetHashCode();
            }

            public bool Equals(RECT Rectangle)
            {
                return Rectangle.Left == _Left && Rectangle.Top == _Top && Rectangle.Right == _Right && Rectangle.Bottom == _Bottom;
            }

            public override bool Equals(object Object)
            {
                if (Object is RECT)
                {
                    return Equals((RECT)Object);
                }
                else if (Object is Rectangle)
                {
                    return Equals(new RECT((Rectangle)Object));
                }

                return false;
            }
        }

        //
        // Again, Thank you, HCP Dev :)
        //

        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        private void ShowTrayIcon(bool show)
        {
            if (trayIcon != null)
            {
                trayIcon.Visible = show;
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (trayIcon != null)
            {
                trayIcon.Dispose();
            }
            //e.Cancel = true; // Prevents the window from closing
            this.Hide();
            base.OnClosing(e);
            HideAudioDevicesPopup();
        }

        private void RestoreWindowFromTray()
        {
            ShowTrayIcon(false);
            this.Show();
            this.WindowState = WindowState.Maximized;

            // Ensure the window is brought to the foreground
            this.Topmost = false;
            this.Activate();
            this.Topmost = true;
            this.Focus();
            MakeWindowAlwaysOnTop(); // Use the method to ensure it's always on top

            // Set dimensions
            this.Left = 0;
            this.Top = 0;
            this.Width = SystemParameters.VirtualScreenWidth;
            this.Height = SystemParameters.VirtualScreenHeight;

            // Hide the popup, if it's open
            HideAudioDevicesPopup();

            // Allow the window to set itself as the foreground window
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            AllowSetForegroundWindow(ASFW_ANY);
            SetForegroundWindow(hwnd);

            // If you decide to keep the option to revert the topmost status, consider user preferences
            // Dispatcher.BeginInvoke(new Action(() =>
            // {
            //     this.Topmost = false;
            // }), DispatcherPriority.ApplicationIdle);
        }

        private void MakeWindowAlwaysOnTop()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            uint styles = (uint)SetWindowLongPtr(hwnd, GWL_EXSTYLE, (uint)WindowExStyles.WS_EX_TOPMOST).ToInt64();
        }

        private void SetupWindowMessageHook()
        {
            var hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            hwndSource.AddHook(new HwndSourceHook(WindowProc));
        }

        private const int WM_WINDOWPOSCHANGING = 0x0046;
        private bool needsToBeTopmost = false;

        private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_WINDOWPOSCHANGING)
            {
                // Flag that the window needs to be set back to always on top.
                needsToBeTopmost = true;
            }

            handled = false;
            return IntPtr.Zero;
        }

        private void ToggleAlwaysOnTop(bool enable)
        {
            if (enable)
            {
                MakeWindowAlwaysOnTop();
            }
            else
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE);
            }
        }

        private string GetFriendlyName(string processName)
        {
            var nameMap = new Dictionary<string, string>
        {
            { "spotify", "Spotify"  },
            { "steam", "Steam"  },
            { "steamwebhelper", "Steam"  },
            { "steamwebhelperupdater", "Steam Updater"  },
            { "steamclient", "Steam"  },
            { "devenv", "Visual Studio"  },
            { "eadesktop", "EA Launcher"  },
            { "playnite.fullscreenapp", "Playnite Fullscreen"  },
            { "playnite.desktopapp", "Playnite Desktop"  },
            { "ubc", "Ubisoft Connect"  },
            // Add more mappings as and when required
        };

            if (nameMap.TryGetValue(processName.ToLower(), out var friendlyName))
            {
                return friendlyName;
            }

            return processName;
        }

        private List<Process> GetOpenWindows()
        {
            var processes = new List<Process>();

            foreach (Process process in Process.GetProcesses())
            {
                if (!string.IsNullOrEmpty(process.MainWindowTitle))
                {
                    IntPtr windowHandle = process.MainWindowHandle;
                    if (IsAltTabWindow(windowHandle))
                    {
                        processes.Add(process);
                    }
                }
            }

            return processes;
        }

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;
        private const uint WS_VISIBLE = 0x10000000;
        private const uint WS_EX_TOOLWINDOW = 0x00000080;
        private const uint WS_EX_APPWINDOW = 0x00040000;

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size == 8)
                return GetWindowLongPtr64(hWnd, nIndex);
            else
                return GetWindowLongPtr32(hWnd, nIndex);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out bool pvAttribute, int cbAttribute);

        private const int DWMWA_CLOAKED = 14;
        private const uint GW_OWNER = 4;

        private bool IsAltTabWindow(IntPtr hWnd)
        {
            if (!IsWindowVisible(hWnd))
                return false;

            if (GetWindow(hWnd, GW_OWNER) != IntPtr.Zero)
                return false;

            if (GetWindowTextLength(hWnd) == 0)
                return false;

            // Check for cloaked windows (invisible Modern/Metro app windows)
            DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out bool isCloaked, Marshal.SizeOf(typeof(bool)));
            if (isCloaked)
                return false;

            var style = (uint)GetWindowLongPtr(hWnd, GWL_STYLE).ToInt64();
            var exStyle = (uint)GetWindowLongPtr(hWnd, GWL_EXSTYLE).ToInt64();

            bool isAppWindow = (exStyle & WS_EX_APPWINDOW) != 0;
            bool isToolWindow = (exStyle & WS_EX_TOOLWINDOW) != 0;

            return isAppWindow || (!isToolWindow && IsWindowVisible(hWnd));
        }

        #region Native Methods

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int processId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const uint WM_CLOSE = 0x0010;

        #endregion Native Methods

        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public POINT ptMinPosition;
            public POINT ptMaxPosition;
            public RECT rcNormalPosition;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        private const int SW_SHOWMINIMIZED = 2;
        private const int SW_RESTORE = 9;
        private const int SW_MAXIMIZE = 3;

        // Function to switch to a specific window
        private async void WindowListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (aButtonPressed)
            {
                if (WindowListBox.SelectedIndex >= 0 && WindowListBox.SelectedIndex < WindowListBox.Items.Count)
                {
                    var selectedItem = WindowListBox.SelectedItem as dynamic;
                    var process = selectedItem?.Process as Process;
                    if (process != null && process.MainWindowHandle != IntPtr.Zero)
                    {
                        WINDOWPLACEMENT placement = new WINDOWPLACEMENT();
                        GetWindowPlacement(process.MainWindowHandle, ref placement);

                        if (placement.showCmd == SW_SHOWMINIMIZED)
                        {
                            ShowWindow(process.MainWindowHandle, SW_RESTORE);
                            // Optional: Add a slight delay here if needed
                            await Task.Delay(100); // 100 milliseconds delay
                        }

                        ShowWindow(process.MainWindowHandle, SW_MAXIMIZE);
                        SetForegroundWindow(process.MainWindowHandle);
                    }
                }
                aButtonPressed = false;
            }
        }

        #region Native Methods

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AllowSetForegroundWindow(int dwProcessId);

        [DllImport("user32.dll")]
        public static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_MINIMIZE = 6;

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        public static extern int SetWindowLong32(IntPtr hWnd, int nIndex, uint dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        public static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, uint dwNewLong);

        public enum WindowExStyles : uint
        {
            WS_EX_TOPMOST = 0x00000008
        }

        public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, uint dwNewLong)
        {
            if (IntPtr.Size == 8)
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            else
                return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong));
        }

        #endregion Native Methods
    }
}