using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using AmfQuickLook.Core;

namespace AmfQuickLook
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length >= 3 && string.Equals(args[0], "--thumbnail", StringComparison.OrdinalIgnoreCase))
                {
                    int size = args.Length >= 4 ? Math.Max(64, int.Parse(args[3])) : 256;
                    var doc = AmfParser.Parse(args[1], 120000);
                    using (var bitmap = AmfRenderer.RenderBitmap(doc, size, size, new RenderOptions { ShowStats = false }))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[2])));
                        bitmap.Save(args[2], ImageFormat.Png);
                    }
                    return 0;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new ViewerForm(args.Length > 0 ? args[0] : null));
                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "AMF QuickLook", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
    }

    internal sealed class ViewerForm : Form
    {
        private readonly PreviewPanel preview = new PreviewPanel();
        private readonly ToolStripStatusLabel status = new ToolStripStatusLabel("Ready");
        private string currentPath;

        public ViewerForm(string initialPath)
        {
            Text = "AMF QuickLook";
            Width = 1100;
            Height = 780;
            MinimumSize = new Size(520, 360);
            AllowDrop = true;

            var strip = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, RenderMode = ToolStripRenderMode.System };
            var open = new ToolStripButton("Open");
            var reset = new ToolStripButton("Reset");
            var mode = new ToolStripButton("Wire") { CheckOnClick = true };
            var export = new ToolStripButton("PNG");

            open.Click += (s, e) => OpenViaDialog();
            reset.Click += (s, e) => preview.ResetView();
            mode.CheckedChanged += (s, e) => preview.Wireframe = mode.Checked;
            export.Click += (s, e) => ExportPng();

            strip.Items.Add(open);
            strip.Items.Add(reset);
            strip.Items.Add(mode);
            strip.Items.Add(export);

            var statusStrip = new StatusStrip();
            statusStrip.Items.Add(status);

            preview.Dock = DockStyle.Fill;
            preview.DocumentChanged += (s, e) => UpdateStatus();

            Controls.Add(preview);
            Controls.Add(statusStrip);
            Controls.Add(strip);
            strip.Dock = DockStyle.Top;
            statusStrip.Dock = DockStyle.Bottom;

            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;

            if (!string.IsNullOrWhiteSpace(initialPath))
            {
                LoadFile(initialPath);
            }
        }

        private void OpenViaDialog()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "AMF files (*.amf;*.AMF)|*.amf;*.AMF|All files (*.*)|*.*";
                dialog.Title = "Open AMF";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    LoadFile(dialog.FileName);
                }
            }
        }

        private void LoadFile(string path)
        {
            currentPath = path;
            Text = Path.GetFileName(path) + " - AMF QuickLook";
            status.Text = "Loading " + path;
            Refresh();

            try
            {
                var doc = AmfParser.Parse(path, 180000);
                preview.Document = doc;
                UpdateStatus();
            }
            catch (Exception ex)
            {
                preview.Document = null;
                status.Text = ex.Message;
                MessageBox.Show(this, ex.Message, "Could not open AMF", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateStatus()
        {
            var doc = preview.Document;
            if (doc == null)
            {
                status.Text = "Drag an AMF file here, or use Open.";
                return;
            }

            status.Text = string.Format("{0} | {1:n0} vertices | {2:n0} triangles | {3} objects | mouse: drag rotate, wheel zoom, right-drag pan",
                Path.GetFileName(doc.Path), doc.VertexCount, doc.TriangleCount, doc.Objects.Count);
        }

        private void ExportPng()
        {
            if (preview.Document == null) return;
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "PNG image (*.png)|*.png";
                dialog.FileName = Path.GetFileNameWithoutExtension(currentPath ?? "amf") + ".png";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    using (var bitmap = AmfRenderer.RenderBitmap(preview.Document, 1400, 1000, preview.Options))
                    {
                        bitmap.Save(dialog.FileName, ImageFormat.Png);
                    }
                }
            }
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && files.Length > 0) LoadFile(files[0]);
        }
    }

    internal sealed class PreviewPanel : Control
    {
        private AmfDocument document;
        private readonly RenderOptions options = new RenderOptions();
        private bool dragging;
        private bool panning;
        private Point lastMouse;

        public event EventHandler DocumentChanged;

        public PreviewPanel()
        {
            DoubleBuffered = true;
            BackColor = Color.White;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
        }

        public AmfDocument Document
        {
            get { return document; }
            set
            {
                document = value;
                ResetView();
                if (DocumentChanged != null) DocumentChanged(this, EventArgs.Empty);
            }
        }

        public RenderOptions Options
        {
            get { return options; }
        }

        public bool Wireframe
        {
            get { return options.Wireframe; }
            set { options.Wireframe = value; Invalidate(); }
        }

        public void ResetView()
        {
            options.RotationX = -0.55;
            options.RotationY = 0.70;
            options.Zoom = 0.86;
            options.PanX = 0;
            options.PanY = 0;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var bitmap = AmfRenderer.RenderBitmap(document, Math.Max(64, Width), Math.Max(64, Height), options))
            {
                e.Graphics.DrawImageUnscaled(bitmap, 0, 0);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            dragging = e.Button == MouseButtons.Left;
            panning = e.Button == MouseButtons.Right || e.Button == MouseButtons.Middle;
            lastMouse = e.Location;
            Capture = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!dragging && !panning) return;

            int dx = e.X - lastMouse.X;
            int dy = e.Y - lastMouse.Y;
            lastMouse = e.Location;

            if (dragging)
            {
                options.RotationY += dx * 0.01;
                options.RotationX += dy * 0.01;
            }
            else
            {
                options.PanX += dx;
                options.PanY += dy;
            }
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            dragging = false;
            panning = false;
            Capture = false;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            options.Zoom *= e.Delta > 0 ? 1.12 : 0.89;
            if (options.Zoom < 0.05) options.Zoom = 0.05;
            if (options.Zoom > 20) options.Zoom = 20;
            Invalidate();
        }
    }
}
