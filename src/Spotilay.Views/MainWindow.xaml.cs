using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Forms.PropertyGridInternal;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinApi;
using Application = System.Windows.Application;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace Spotilay.Views
{
    public partial class MainWindow
    {
        private NotifyIcon _trayIcon;

        public MainWindow()
        {
            InitializeComponent();
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = -1000;
            Top = -1000;
            Visibility = Visibility.Hidden;
            Hide();
            ShowInTaskbar = false;
            var contextMenu = CreateContextMenu();
            var tray = CreateTrayIcon(contextMenu);
            _trayIcon = tray;
            _trayIcon.Visible = true;
            SourceInitialized += OnSourceInitialized;
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            Hide();
            HideMinimizeAndMaximizeButtons(this);
        }

        private ContextMenuStrip CreateContextMenu()
        {
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Exit", null, (sender, args) =>
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
                Application.Current.Shutdown();
            });
            return contextMenu;
        }

        private NotifyIcon CreateTrayIcon(ContextMenuStrip contextMenu)
        {
            if (contextMenu == null) throw new NullReferenceException(nameof(contextMenu));
            
            var trayIcon = new Icon(GetType(), "Resources.favicon.ico");
            var trayNotifyIcon = new NotifyIcon
            {
                ContextMenuStrip = contextMenu,
                Visible = false,
                Text = "Spotilay",
                Icon = trayIcon
            };
            Icon = ToImageSource(trayIcon);
            trayNotifyIcon.DoubleClick +=
                (sender, args) =>
                {
                    Left = 0;
                    Top = 0;
                    Topmost = true;
                    Show();
                    WindowState = WindowState.Normal;
                };

            return trayNotifyIcon;
        }
        private static ImageSource ToImageSource(Icon icon)
        {            
            var bitmap = icon.ToBitmap();
            var hBitmap = bitmap.GetHbitmap();

            ImageSource wpfBitmap = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            if (!DllExtern.deleteObject(hBitmap))
            {
                throw new Win32Exception();
            }

            return wpfBitmap;
        }

        private static void HideMinimizeAndMaximizeButtons(Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            var style = DllExtern.getWindowLong(hwnd, DllExtern.gwlStyle);
            var value = style & ~DllExtern.wsMaximizebox & ~DllExtern.wsMinimizebox;
            DllExtern.setWindowLong(hwnd, DllExtern.gwlStyle, value);
        }
        
        
        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState != WindowState.Minimized) return;
            _trayIcon.Visible = true;
            Hide();
        }

        // Minimize to system tray when application is closed.
        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
            if (_trayIcon != null)
                _trayIcon.Visible = true;
                
            base.OnClosing(e);
        }
        
        
        private void UiElement_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (WindowState == WindowState.Maximized)
                {
                    WindowState = WindowState.Normal;
                    if (Application.Current.MainWindow != null)
                        Application.Current.MainWindow.Top = 3;
                }
                DragMove();
            }
        }

        private void ShowMe()
        {
            if (WindowState == WindowState.Minimized) {
                WindowState = WindowState.Normal;
            }

            var top = Topmost;
            Topmost = true;
            Topmost = top;
        }

        private void MainWindow_OnUnloaded(object sender, RoutedEventArgs e)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }
    }
}
