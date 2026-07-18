using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace AmfQuickLook.Core
{
    public struct Vec3
    {
        public double X;
        public double Y;
        public double Z;

        public Vec3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    public struct Triangle
    {
        public int V1;
        public int V2;
        public int V3;

        public Triangle(int v1, int v2, int v3)
        {
            V1 = v1;
            V2 = v2;
            V3 = v3;
        }
    }

    public sealed class Bounds3
    {
        public Vec3 Min = new Vec3(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
        public Vec3 Max = new Vec3(double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity);

        public bool IsEmpty
        {
            get { return double.IsInfinity(Min.X) || double.IsInfinity(Max.X); }
        }

        public Vec3 Center
        {
            get { return new Vec3((Min.X + Max.X) / 2.0, (Min.Y + Max.Y) / 2.0, (Min.Z + Max.Z) / 2.0); }
        }

        public double Span
        {
            get
            {
                if (IsEmpty) return 1.0;
                return Math.Max(0.000001, Math.Max(Max.X - Min.X, Math.Max(Max.Y - Min.Y, Max.Z - Min.Z)));
            }
        }

        public void Include(Vec3 v)
        {
            Min = new Vec3(Math.Min(Min.X, v.X), Math.Min(Min.Y, v.Y), Math.Min(Min.Z, v.Z));
            Max = new Vec3(Math.Max(Max.X, v.X), Math.Max(Max.Y, v.Y), Math.Max(Max.Z, v.Z));
        }
    }

    public sealed class MeshObject
    {
        public string Id = "";
        public Color Color = Color.FromArgb(100, 149, 237);
        public readonly List<Vec3> Vertices = new List<Vec3>();
        public readonly List<Triangle> Triangles = new List<Triangle>();
        public int SkippedTriangles;
    }

    public sealed class AmfDocument
    {
        public string Path = "";
        public string Unit = "";
        public readonly List<MeshObject> Objects = new List<MeshObject>();
        public readonly Bounds3 Bounds = new Bounds3();
        public int VertexCount;
        public int TriangleCount;
        public int RenderTriangleCount;
        public bool RenderLimited;
    }

    public sealed class RenderOptions
    {
        public double RotationX = -0.55;
        public double RotationY = 0.70;
        public double Zoom = 0.86;
        public double PanX;
        public double PanY;
        public bool Wireframe;
        public bool ShowStats = true;
    }

    public static class AmfParser
    {
        private static readonly Color[] Palette = new[]
        {
            Color.FromArgb(221, 83, 75),
            Color.FromArgb(231, 181, 73),
            Color.FromArgb(88, 170, 111),
            Color.FromArgb(69, 132, 201),
            Color.FromArgb(166, 108, 191),
            Color.FromArgb(80, 160, 170)
        };

        public static AmfDocument Parse(string path, int renderTriangleLimit)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("AMF path is empty.", "path");
            if (!File.Exists(path)) throw new FileNotFoundException("AMF file not found.", path);
            if (renderTriangleLimit < 1) renderTriangleLimit = 1;

            var doc = new AmfDocument { Path = path };
            MeshObject current = null;
            int objectIndex = 0;

            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                IgnoreComments = true,
                IgnoreWhitespace = true
            };

            using (var reader = XmlReader.Create(path, settings))
            {
                while (true)
                {
                    if (reader.ReadState == ReadState.Initial && !reader.Read()) break;
                    bool consumed = false;

                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        var name = reader.LocalName;
                        if (name == "amf")
                        {
                            doc.Unit = reader.GetAttribute("unit") ?? "";
                        }
                        else if (name == "object")
                        {
                            current = new MeshObject
                            {
                                Id = reader.GetAttribute("id") ?? "",
                                Color = Palette[objectIndex % Palette.Length]
                            };
                            objectIndex++;
                        }
                        else if (current != null && name == "color")
                        {
                            current.Color = ReadColor(reader, current.Color);
                            consumed = true;
                        }
                        else if (current != null && name == "vertex")
                        {
                            var vertex = ReadVertex(reader);
                            current.Vertices.Add(vertex);
                            doc.Bounds.Include(vertex);
                            doc.VertexCount++;
                            consumed = true;
                        }
                        else if (current != null && name == "triangle")
                        {
                            var triangle = ReadTriangle(reader);
                            doc.TriangleCount++;
                            if (current.Triangles.Count < renderTriangleLimit)
                            {
                                current.Triangles.Add(triangle);
                                doc.RenderTriangleCount++;
                            }
                            else
                            {
                                current.SkippedTriangles++;
                                doc.RenderLimited = true;
                            }
                            consumed = true;
                        }
                    }
                    else if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "object" && current != null)
                    {
                        doc.Objects.Add(current);
                        current = null;
                    }

                    if (!consumed && !reader.Read()) break;
                }
            }

            if (current != null) doc.Objects.Add(current);
            return doc;
        }

        private static Vec3 ReadVertex(XmlReader reader)
        {
            var element = XElement.Parse(reader.ReadOuterXml());
            double x = 0, y = 0, z = 0;
            foreach (var node in element.Descendants())
            {
                if (node.Name.LocalName == "x") x = ParseDouble(node.Value);
                else if (node.Name.LocalName == "y") y = ParseDouble(node.Value);
                else if (node.Name.LocalName == "z") z = ParseDouble(node.Value);
            }
            return new Vec3(x, y, z);
        }

        private static Triangle ReadTriangle(XmlReader reader)
        {
            var element = XElement.Parse(reader.ReadOuterXml());
            int v1 = 0, v2 = 0, v3 = 0;
            foreach (var node in element.Descendants())
            {
                if (node.Name.LocalName == "v1") v1 = ParseInt(node.Value);
                else if (node.Name.LocalName == "v2") v2 = ParseInt(node.Value);
                else if (node.Name.LocalName == "v3") v3 = ParseInt(node.Value);
            }
            return new Triangle(v1, v2, v3);
        }

        private static Color ReadColor(XmlReader reader, Color fallback)
        {
            var element = XElement.Parse(reader.ReadOuterXml());
            double r = fallback.R / 255.0;
            double g = fallback.G / 255.0;
            double b = fallback.B / 255.0;
            double a = 1.0;

            foreach (var node in element.Descendants())
            {
                if (node.Name.LocalName == "r") r = ParseDouble(node.Value);
                else if (node.Name.LocalName == "g") g = ParseDouble(node.Value);
                else if (node.Name.LocalName == "b") b = ParseDouble(node.Value);
                else if (node.Name.LocalName == "a") a = ParseDouble(node.Value);
            }

            return Color.FromArgb(ToByte(a), ToByte(r), ToByte(g), ToByte(b));
        }

        private static int ReadIntElement(XmlReader reader)
        {
            return int.Parse(reader.ReadElementContentAsString(), CultureInfo.InvariantCulture);
        }

        private static double ReadDoubleElement(XmlReader reader)
        {
            return double.Parse(reader.ReadElementContentAsString(), CultureInfo.InvariantCulture);
        }

        private static int ParseInt(string text)
        {
            return int.Parse(text, CultureInfo.InvariantCulture);
        }

        private static double ParseDouble(string text)
        {
            return double.Parse(text, CultureInfo.InvariantCulture);
        }

        private static int ToByte(double value)
        {
            if (value <= 1.0) value *= 255.0;
            if (value < 0) value = 0;
            if (value > 255) value = 255;
            return (int)Math.Round(value);
        }
    }

    public static class AmfRenderer
    {
        public static Bitmap RenderBitmap(AmfDocument doc, int width, int height, RenderOptions options)
        {
            if (options == null) options = new RenderOptions();
            width = Math.Max(64, width);
            height = Math.Max(64, height);

            var bitmap = new Bitmap(width, height);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.FromArgb(248, 249, 251));

                using (var border = new Pen(Color.FromArgb(215, 220, 228)))
                {
                    g.DrawRectangle(border, 0, 0, width - 1, height - 1);
                }

                if (doc == null || doc.Objects.Count == 0 || doc.Bounds.IsEmpty)
                {
                    DrawCenteredText(g, "No AMF mesh", width, height);
                    return bitmap;
                }

                var faces = BuildFaces(doc, width, height, options);
                faces.Sort((a, b) => a.Depth.CompareTo(b.Depth));

                foreach (var face in faces)
                {
                    if (!options.Wireframe)
                    {
                        using (var brush = new SolidBrush(Color.FromArgb(175, face.Color)))
                        {
                            g.FillPolygon(brush, face.Points);
                        }
                    }

                    using (var pen = new Pen(Color.FromArgb(options.Wireframe ? 180 : 80, 28, 39, 53), options.Wireframe ? 1.2f : 0.7f))
                    {
                        g.DrawPolygon(pen, face.Points);
                    }
                }

                if (options.ShowStats)
                {
                    DrawStats(g, doc, width, height);
                }
            }

            return bitmap;
        }

        private static List<Face2D> BuildFaces(AmfDocument doc, int width, int height, RenderOptions options)
        {
            var faces = new List<Face2D>(Math.Min(doc.RenderTriangleCount, 50000));
            var center = doc.Bounds.Center;
            double span = doc.Bounds.Span;
            double scale = Math.Min(width, height) * 0.82 * options.Zoom / span;
            double ox = width / 2.0 + options.PanX;
            double oy = height / 2.0 + options.PanY;

            foreach (var obj in doc.Objects)
            {
                int count = obj.Triangles.Count;
                int step = Math.Max(1, count / 60000);
                for (int i = 0; i < count; i += step)
                {
                    var tri = obj.Triangles[i];
                    if (!IsValid(tri, obj.Vertices.Count)) continue;

                    double z1, z2, z3;
                    var p1 = Project(obj.Vertices[tri.V1], center, scale, ox, oy, options, out z1);
                    var p2 = Project(obj.Vertices[tri.V2], center, scale, ox, oy, options, out z2);
                    var p3 = Project(obj.Vertices[tri.V3], center, scale, ox, oy, options, out z3);

                    if (TriangleArea(p1, p2, p3) < 0.05) continue;
                    faces.Add(new Face2D
                    {
                        Points = new[] { p1, p2, p3 },
                        Depth = (z1 + z2 + z3) / 3.0,
                        Color = obj.Color
                    });
                }
            }

            return faces;
        }

        private static bool IsValid(Triangle tri, int vertexCount)
        {
            return tri.V1 >= 0 && tri.V2 >= 0 && tri.V3 >= 0 &&
                   tri.V1 < vertexCount && tri.V2 < vertexCount && tri.V3 < vertexCount;
        }

        private static PointF Project(Vec3 v, Vec3 center, double scale, double ox, double oy, RenderOptions options, out double depth)
        {
            double x = v.X - center.X;
            double y = v.Y - center.Y;
            double z = v.Z - center.Z;

            double cy = Math.Cos(options.RotationY);
            double sy = Math.Sin(options.RotationY);
            double x1 = x * cy + z * sy;
            double z1 = -x * sy + z * cy;

            double cx = Math.Cos(options.RotationX);
            double sx = Math.Sin(options.RotationX);
            double y2 = y * cx - z1 * sx;
            double z2 = y * sx + z1 * cx;

            depth = z2;
            return new PointF((float)(ox + x1 * scale), (float)(oy - y2 * scale));
        }

        private static double TriangleArea(PointF a, PointF b, PointF c)
        {
            return Math.Abs((a.X * (b.Y - c.Y) + b.X * (c.Y - a.Y) + c.X * (a.Y - b.Y)) / 2.0);
        }

        private static void DrawStats(Graphics g, AmfDocument doc, int width, int height)
        {
            string file = string.IsNullOrWhiteSpace(doc.Path) ? "AMF" : System.IO.Path.GetFileName(doc.Path);
            string line1 = file;
            string line2 = string.Format(CultureInfo.InvariantCulture, "{0:n0} vertices   {1:n0} triangles   {2} objects", doc.VertexCount, doc.TriangleCount, doc.Objects.Count);
            if (doc.RenderLimited) line2 += "   sampled";

            using (var font = new Font("Segoe UI", 9f))
            using (var brush = new SolidBrush(Color.FromArgb(220, 25, 35, 48)))
            using (var bg = new SolidBrush(Color.FromArgb(218, 255, 255, 255)))
            {
                var s1 = g.MeasureString(line1, font);
                var s2 = g.MeasureString(line2, font);
                float boxW = Math.Min(width - 16, Math.Max(s1.Width, s2.Width) + 14);
                g.FillRectangle(bg, 8, height - 52, boxW, 44);
                g.DrawString(line1, font, brush, 15, height - 48);
                g.DrawString(line2, font, brush, 15, height - 28);
            }
        }

        private static void DrawCenteredText(Graphics g, string text, int width, int height)
        {
            using (var font = new Font("Segoe UI", 12f))
            using (var brush = new SolidBrush(Color.FromArgb(80, 90, 105)))
            {
                var size = g.MeasureString(text, font);
                g.DrawString(text, font, brush, (width - size.Width) / 2f, (height - size.Height) / 2f);
            }
        }

        private sealed class Face2D
        {
            public PointF[] Points;
            public double Depth;
            public Color Color;
        }
    }
}
