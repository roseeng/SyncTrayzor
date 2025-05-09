﻿using SyncTrayzor.Services.Config;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using NLog;

namespace SyncTrayzor.Xaml
{
    public class WindowPlacementBehaviour : DetachingBehaviour<Window>
    {
        private const int taskbarHeight = 40; // Max height
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public WindowPlacement Placement
        {
            get => (WindowPlacement)GetValue(PlacementProperty);
            set => SetValue(PlacementProperty, value);
        }

        public static readonly DependencyProperty PlacementProperty =
            DependencyProperty.Register("Placement", typeof(WindowPlacement), typeof(WindowPlacementBehaviour), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        protected override void AttachHandlers()
        {
            AssociatedObject.SourceInitialized += SourceInitialized;
            AssociatedObject.Closing += Closing;

            SetDefaultSize();
        }

        protected override void DetachHandlers()
        {
            AssociatedObject.SourceInitialized -= SourceInitialized;
            AssociatedObject.Closing -= Closing;
        }

        private void SetDefaultSize()
        {
            // Beware, we may be duplicating functionality in NoSizeBelowScreenBehaviour

            AssociatedObject.Height = Math.Min(AssociatedObject.Height, SystemParameters.VirtualScreenHeight - taskbarHeight);
            AssociatedObject.Width = Math.Min(AssociatedObject.Width, SystemParameters.VirtualScreenWidth - taskbarHeight);
        }

        private void SourceInitialized(object sender, EventArgs e)
        {
            if (Placement == null)
                return;

            var nativePlacement = new WINDOWPLACEMENT()
            {
                length = Marshal.SizeOf(typeof(WINDOWPLACEMENT)),
                flags = 0,
                showCmd = Placement.IsMaximised ? SW_SHOWMAXIMIZED : SW_SHOWNORMAL,
                maxPosition = new POINT(Placement.MaxPosition.X, Placement.MaxPosition.Y),
                minPosition = new POINT(Placement.MinPosition.X, Placement.MinPosition.Y),
                normalPosition = new RECT(
                    Placement.NormalPosition.Left,
                    Placement.NormalPosition.Top,
                    Placement.NormalPosition.Right,
                    Placement.NormalPosition.Bottom),
            };

            if (!NativeMethods.SetWindowPlacement(new WindowInteropHelper(AssociatedObject).Handle, ref nativePlacement))
            {
                logger.Warn("Call to SetWindowPlacement failed", new Win32Exception(Marshal.GetLastWin32Error()));
            }

            // Not 100% sure why this is needed, but if the window is minimzed before being closed to tray,
            // then is restored, we can end up in a state where we aren't active
            AssociatedObject.Activate();
        }

        private void Closing(object sender, CancelEventArgs e)
        {
            WindowPlacement placement = null;

            if (NativeMethods.GetWindowPlacement(new WindowInteropHelper(AssociatedObject).Handle, out var nativePlacement))
            {
                placement = new WindowPlacement()
                {
                    IsMaximised = (nativePlacement.flags & SW_SHOWMAXIMIZED) > 0,
                    MaxPosition = new System.Drawing.Point(nativePlacement.maxPosition.X, nativePlacement.maxPosition.Y),
                    MinPosition = new System.Drawing.Point(nativePlacement.minPosition.X, nativePlacement.minPosition.Y),
                    NormalPosition = System.Drawing.Rectangle.FromLTRB(
                        nativePlacement.normalPosition.Left,
                        nativePlacement.normalPosition.Top,
                        nativePlacement.normalPosition.Right,
                        nativePlacement.normalPosition.Bottom
                    ),
                };
            }
            else
            {
                logger.Warn("Call to SetWindowPlacement failed", new Win32Exception(Marshal.GetLastWin32Error()));
            }

            if (placement != null && !placement.Equals(Placement))
                Placement = placement;
        }

        private const int SW_SHOWNORMAL = 1;
        private const int SW_SHOWMAXIMIZED = 3;

        // RECT structure required by WINDOWPLACEMENT structure
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public RECT(int left, int top, int right, int bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }
        }

        // POINT structure required by WINDOWPLACEMENT structure
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;

            public POINT(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        // WINDOWPLACEMENT stores the position, size, and state of a window
        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public POINT minPosition;
            public POINT maxPosition;
            public RECT normalPosition;
        }

        private class NativeMethods
        {
            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool SetWindowPlacement(IntPtr hWnd, [In] ref WINDOWPLACEMENT lpwndpl);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool GetWindowPlacement(IntPtr hWnd, out WINDOWPLACEMENT lpwndpl);
        }
    }
}
