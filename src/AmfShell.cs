using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AmfQuickLook.Core;

namespace AmfQuickLook.Shell
{
    public enum WtsAlphaType
    {
        Unknown = 0,
        Rgb = 1,
        Argb = 2
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [ComVisible(true)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("b7d14566-0509-4cce-a71f-0a554233bd9b")]
    public interface IInitializeWithFile
    {
        void Initialize([MarshalAs(UnmanagedType.LPWStr)] string pszFilePath, uint grfMode);
    }

    [ComVisible(true)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("e357fccd-a995-4576-b01f-234630154e96")]
    public interface IThumbnailProvider
    {
        void GetThumbnail(uint cx, out IntPtr hBitmap, out WtsAlphaType bitmapType);
    }

    [ComVisible(true)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("8895b1c6-b41f-4c1c-a562-0d564250836f")]
    public interface IPreviewHandler
    {
        void SetWindow(IntPtr hwnd, ref Rect rect);
        void SetRect(ref Rect rect);
        void DoPreview();
        void Unload();
        void SetFocus();
        void QueryFocus(out IntPtr hwnd);
        [PreserveSig]
        int TranslateAccelerator(IntPtr msg);
    }

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("AmfQuickLook.ThumbnailProvider")]
    [Guid("84F9DD9B-6C88-45D0-86E4-2D3D9447113D")]
    public sealed class ThumbnailProvider : IInitializeWithFile, IThumbnailProvider
    {
        private string path;

        public void Initialize(string pszFilePath, uint grfMode)
        {
            path = pszFilePath;
        }

        public void GetThumbnail(uint cx, out IntPtr hBitmap, out WtsAlphaType bitmapType)
        {
            int size = Math.Max(64, Math.Min(1024, (int)cx));
            var doc = AmfParser.Parse(path, 90000);
            using (var bitmap = AmfRenderer.RenderBitmap(doc, size, size, new RenderOptions { ShowStats = false }))
            {
                hBitmap = bitmap.GetHbitmap(Color.Transparent);
            }
            bitmapType = WtsAlphaType.Argb;
        }
    }

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("AmfQuickLook.PreviewHandler")]
    [Guid("FD3E123C-D4F0-4BC7-97D4-142C4FB4F035")]
    public sealed class PreviewHandler : IInitializeWithFile, IPreviewHandler
    {
        private string path;
        private IntPtr parent;
        private Rect bounds;
        private PreviewControl control;

        public void Initialize(string pszFilePath, uint grfMode)
        {
            path = pszFilePath;
        }

        public void SetWindow(IntPtr hwnd, ref Rect rect)
        {
            parent = hwnd;
            bounds = rect;
            if (control != null) AttachControl();
        }

        public void SetRect(ref Rect rect)
        {
            bounds = rect;
            if (control != null)
            {
                MoveWindow(control.Handle, bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top, true);
            }
        }

        public void DoPreview()
        {
            if (control == null)
            {
                control = new PreviewControl();
                control.LoadAmf(path);
            }
            AttachControl();
            control.Show();
        }

        public void Unload()
        {
            if (control != null)
            {
                control.Dispose();
                control = null;
            }
        }

        public void SetFocus()
        {
            if (control != null) control.Focus();
        }

        public void QueryFocus(out IntPtr hwnd)
        {
            hwnd = control == null ? IntPtr.Zero : control.Handle;
        }

        public int TranslateAccelerator(IntPtr msg)
        {
            return 1;
        }

        private void AttachControl()
        {
            if (control == null || parent == IntPtr.Zero) return;
            SetParent(control.Handle, parent);
            MoveWindow(control.Handle, bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top, true);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll")]
        private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int width, int height, bool repaint);
    }

    internal sealed class PreviewControl : UserControl
    {
        private AmfDocument document;
        private string error;

        public PreviewControl()
        {
            DoubleBuffered = true;
            BackColor = Color.White;
        }

        public void LoadAmf(string file)
        {
            try
            {
                document = AmfParser.Parse(file, 120000);
                error = null;
            }
            catch (Exception ex)
            {
                document = null;
                error = ex.Message;
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (document == null)
            {
                e.Graphics.Clear(Color.White);
                using (var font = new Font("Segoe UI", 10f))
                using (var brush = new SolidBrush(Color.FromArgb(80, 90, 105)))
                {
                    e.Graphics.DrawString(error ?? "No AMF preview", font, brush, new RectangleF(16, 16, Width - 32, Height - 32));
                }
                return;
            }

            using (var bitmap = AmfRenderer.RenderBitmap(document, Math.Max(64, Width), Math.Max(64, Height), new RenderOptions()))
            {
                e.Graphics.DrawImageUnscaled(bitmap, 0, 0);
            }
        }
    }
}
