namespace dxf内外轮廓;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    { 
        if (args.Length > 0 && args[0] == "--analyze" && args.Length > 1)
        {
            AnalyzeResultDxf(args[1]);
            return;
        }
        if (args.Length > 0 && System.IO.File.Exists(args[0]))
        {
            RunProcess(args[0]);
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new Form1());
    }

    static void RunProcess(string file)
    {
        Console.WriteLine($"读取文件: {System.IO.Path.GetFileName(file)}");
        var Curves = DXFIO.Read(file);
        Console.WriteLine($"实体数量: {Curves.Count()}");
        EntityConverter zh = new EntityConverter(Curves);
        Console.WriteLine($"转换后曲线: {zh.Curves.Count}");
        GeometryFixer xz = new GeometryFixer(zh.Curves);
        Console.WriteLine($"修正后曲线: {xz.Curves.Count}");
        // 输出所有修正后曲线的详细信息
        for (int i = 0; i < xz.Curves.Count; i++)
        {
            var c = xz.Curves[i];
            Console.WriteLine($"  [{i}] S=({c.StartPoint.X:F4},{c.StartPoint.Y:F4}) E=({c.EndPoint.X:F4},{c.EndPoint.Y:F4}) B={c.Bulge:F6} L={c.Length:F4}");
        }
        LoopFinder huan = new LoopFinder(xz.Curves);
        Console.WriteLine($"环数量: {huan.Loops.Count}");
        ContourAnalyzer nw = new ContourAnalyzer(huan.Loops);
        Console.WriteLine($"面积: {nw.AreaDescriptions}");
        Console.WriteLine($"OuterContour: {(nw.OuterContour != null ? nw.OuterContour.Vertexes.Count + "顶点" : "无")}");
        Console.WriteLine($"InnerContour: {(nw.InnerContour != null ? nw.InnerContour.Vertexes.Count + "顶点" : "无")}");
        string outDir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(file)!, "轮廓");
        System.IO.Directory.CreateDirectory(outDir);
        string outPath = System.IO.Path.Combine(outDir, System.IO.Path.GetFileName(file));
        DXFIO.Write(outPath, file, nw.InnerContour, nw.OuterContour);
        Console.WriteLine($"输出: {outPath}");
    }

    static void AnalyzeResultDxf(string file)
    {
        Console.WriteLine($"分析结果文件: {file}");
        var dxf = netDxf.DxfDocument.Load(file);
        if (dxf == null) { Console.WriteLine("无法加载"); return; }
        Console.WriteLine($"Lines: {dxf.Entities.Lines.Count()}");
        Console.WriteLine($"Arcs: {dxf.Entities.Arcs.Count()}");
        Console.WriteLine($"Circles: {dxf.Entities.Circles.Count()}");
        Console.WriteLine($"Polylines2D: {dxf.Entities.Polylines2D.Count()}");
        Console.WriteLine($"Splines: {dxf.Entities.Splines.Count()}");
        foreach (var pl in dxf.Entities.Polylines2D)
        {
            string colorName = pl.Color.Index switch { 2 => "Yellow", 1 => "Red", 7 => "White", _ => pl.Color.Index.ToString() };
            Console.WriteLine($"  Polyline2D color={colorName} verts={pl.Vertexes.Count} closed={pl.IsClosed}");
            for (int i = 0; i < pl.Vertexes.Count; i++)
            {
                var v = pl.Vertexes[i];
                Console.WriteLine($"    [{i}] ({v.Position.X:F4},{v.Position.Y:F4}) bulge={v.Bulge:F6}");
            }
        }
    }
}