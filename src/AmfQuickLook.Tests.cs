using System;
using System.Drawing;
using System.IO;
using AmfQuickLook.Core;

internal static class AmfQuickLookTests
{
    private static readonly string SampleRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "tests", "fixtures"));

    private static int Main()
    {
        try
        {
            ParsesSingleCube();
            ParsesMultipleColoredObjects();
            RendersThumbnailPng();
            Console.WriteLine("All AMF QuickLook tests passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void ParsesSingleCube()
    {
        var doc = AmfParser.Parse(Path.Combine(SampleRoot, "cube_single.AMF"), 1000);
        AssertEqual("millimeter", doc.Unit, "unit");
        AssertEqual(1, doc.Objects.Count, "object count");
        AssertEqual(8, doc.VertexCount, "vertex count");
        AssertEqual(12, doc.TriangleCount, "triangle count");
        AssertNear(-30, doc.Bounds.Min.X, "min x");
        AssertNear(10, doc.Bounds.Max.Y, "max y");
        AssertNear(10, doc.Bounds.Max.Z, "max z");
    }

    private static void ParsesMultipleColoredObjects()
    {
        var doc = AmfParser.Parse(Path.Combine(SampleRoot, "cube_multi.AMF"), 1000);
        AssertEqual(3, doc.Objects.Count, "multi object count");
        AssertEqual(24, doc.VertexCount, "multi vertex count");
        AssertEqual(36, doc.TriangleCount, "multi triangle count");
        AssertNear(1, doc.Objects[0].Color.R / 255.0, "first object red");
        AssertNear(1, doc.Objects[1].Color.G / 255.0, "second object green channel");
        AssertNear(0, doc.Objects[2].Color.R / 255.0, "third object red channel");
    }

    private static void RendersThumbnailPng()
    {
        var output = Path.Combine(Path.GetTempPath(), "amf-quicklook-test-thumb.png");
        if (File.Exists(output)) File.Delete(output);

        var doc = AmfParser.Parse(Path.Combine(SampleRoot, "cube_single.AMF"), 1000);
        using (var bmp = AmfRenderer.RenderBitmap(doc, 256, 256, new RenderOptions()))
        {
            bmp.Save(output, System.Drawing.Imaging.ImageFormat.Png);
        }

        var info = new FileInfo(output);
        if (!info.Exists || info.Length < 1024)
        {
            throw new InvalidOperationException("thumbnail PNG was not generated correctly");
        }
    }

    private static void AssertEqual<T>(T expected, T actual, string label)
    {
        if (!object.Equals(expected, actual))
        {
            throw new InvalidOperationException(label + ": expected " + expected + ", got " + actual);
        }
    }

    private static void AssertNear(double expected, double actual, string label)
    {
        if (Math.Abs(expected - actual) > 0.0001)
        {
            throw new InvalidOperationException(label + ": expected " + expected + ", got " + actual);
        }
    }
}
