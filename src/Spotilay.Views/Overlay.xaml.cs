using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WinApi;
using Application = System.Windows.Application;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Control = System.Windows.Controls.Control;
using Cursors = System.Windows.Input.Cursors;
using Point = System.Windows.Point;

namespace Spotilay.Views
{
    public partial class Overlay : Window
    {
        private readonly Color _deepPurple = Color.FromRgb(103, 58, 183);
        private bool _isClickThrough = true;
        private bool _isDraggable;
        private readonly List<Control> _controls;
        private NotifyIcon _trayIcon;


        public Overlay()
        {
            InitializeComponent();
            Closing += OnWindowClosing;
            MouseLeftButtonDown += OnMouseLeftButtonDown;
            var contextMenu = CreateContextMenu();
            var tray = CreateTrayIcon(contextMenu);
            _trayIcon = tray;
            _trayIcon.Visible = true;
            _controls = new List<Control>
            {
                NextBtn, StopBtn, PrevBtn, Anchor
            };

            
            Show();
        }


        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var timer = new DispatcherTimer {Interval = TimeSpan.FromMilliseconds(150)};
            timer.Tick += MouseListenerEvent;
            timer.Start();
            DllExtern.setWindowExTransparent(new WindowInteropHelper(this).Handle);
        }
        
        private void MouseListenerEvent(object sender, EventArgs e)
        {
            if (_isDraggable)
                return;

            var compositionTarget = PresentationSource.FromVisual(this)?.CompositionTarget;
            if (compositionTarget == null) return;
            var transform = compositionTarget.TransformFromDevice;
            var toPoint = transform.Transform(GetMousePosition());
            var mouseInControl = false;

            foreach (var control in _controls)
            {

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    // Get control position relative to window
                    var windowPos = control.TransformToAncestor(this).Transform(new Point(0, 0));

                    var windowRectangle = new Rect
                    {
                        X = windowPos.X + Left,
                        Y = windowPos.Y + Top,
                        Width = control.Width,
                        Height = control.Height
                    };
                    // Add window position to get global control position

                    // Set control width/height

                    if (windowRectangle.Contains(toPoint))
                    {
                        mouseInControl = true;
                    }

                    if (mouseInControl && _isClickThrough)
                    {
                        _isClickThrough = false;
                        DllExtern.unsetWindowExTransparent(new WindowInteropHelper(this).Handle);
                    }
                    else if (!mouseInControl && !_isClickThrough)
                    {
                        _isClickThrough = true;
                        DllExtern.setWindowExTransparent(new WindowInteropHelper(this).Handle);
                    }
                }));
            }
        }


        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggable)
                DragMove();
        }

        private void OnWindowClosing(object sender, CancelEventArgs e)
        {
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            // var handle = new WindowInteropHelper(this).Handle;
        }

        private Point GetMousePosition()
        {
            var point = System.Windows.Forms.Control.MousePosition;
            return new Point(point.X, point.Y);
        }

        private void Anchor_OnClick(object sender, RoutedEventArgs e)
        {
            
            if (_isDraggable)
            {
                _isDraggable = false;
                Cursor = Cursors.Arrow;
                Anchor.Foreground = Brushes.DimGray;
                DllExtern.setWindowExTransparent(new WindowInteropHelper(this).Handle);
                return;
            }
            
            DllExtern.unsetWindowExTransparent(new WindowInteropHelper(this).Handle);
            Cursor = Cursors.Hand;
            _isDraggable = true;
            Anchor.Foreground = new SolidColorBrush(_deepPurple);
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

        private void Overlay_OnUnloaded(object sender, RoutedEventArgs e)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }
    }
}
