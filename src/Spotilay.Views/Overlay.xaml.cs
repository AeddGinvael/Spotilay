using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using WinApi;

namespace Spotilay.Views
{
    public partial class Overlay : Window
    {
        private readonly Color _deepPurple = Color.FromRgb(103, 58, 183);
        private bool _isClickThrough = true;
        private bool _isDraggable;
        private readonly List<Control> _controls;

        public Overlay()
        {
            Closing += OnWindowClosing;
            MouseLeftButtonDown += OnMouseLeftButtonDown;
            InitializeComponent();
            _controls =  new List<Control>
            {
                NextBtn, StopBtn, PrevBtn, Anchor
            };
            _controls.GetEnumerator();

        }
        
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;
            var timer = new DispatcherTimer {Interval = TimeSpan.FromMilliseconds(150)};
            timer.Tick += MouseListenerEvent;
            timer.Start();
            DllExtern.setWindowExTransparent(hwnd);
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
                        var hwnd = new WindowInteropHelper(this).Handle;
                        DllExtern.unsetWindowExTransparent(hwnd);
                    }
                    else if (!mouseInControl && !_isClickThrough)
                    {
                        _isClickThrough = true;
                        var hwnd = new WindowInteropHelper(this).Handle;
                        DllExtern.setWindowExTransparent(hwnd);
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
            var data = Common.createData(Left, Top);
            Common.saveCache(data);
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            // var data = Common.loadConfig();
            // Left = data.Left;
            // Top = data.Top;

            var handle = new WindowInteropHelper(Application.Current.MainWindow).Handle;
            DllExtern.sendWpfWindowBack(handle);
        }

        private Point GetMousePosition()
        {
            var point = System.Windows.Forms.Control.MousePosition;
            return new Point(point.X, point.Y);
        }

        private void Anchor_OnClick(object sender, RoutedEventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            
            if (_isDraggable)
            {
                _isDraggable = false;
                Cursor = Cursors.Arrow;
                Anchor.Foreground = Brushes.DimGray;
                DllExtern.setWindowExTransparent(hwnd);
                return;
            }
            
            DllExtern.unsetWindowExTransparent(hwnd);
            Cursor = Cursors.Hand;
            _isDraggable = true;
            Anchor.Foreground = new SolidColorBrush(_deepPurple);
        }
    }
}
