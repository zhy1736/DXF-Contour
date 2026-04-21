namespace dxf内外轮廓;

static class 程序
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--analyze" && args.Length > 1)
        {
            分析结果Dxf(args[1]);
            return;
        }
        if (args.Length > 0 && System.IO.File.Exists(args[0]))
        {
            执行处理(args[0]);
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new DXF找轮廓());
    }

    static void 执行处理(string 文件路径)
    {
        Console.WriteLine($"读取文件: {System.IO.Path.GetFileName(文件路径)}");
        var 实体组 = DXF读写器.读取(文件路径);
        Console.WriteLine($"实体数量: {实体组.Count()}");
        实体转换器 转换器 = new 实体转换器(实体组);
        Console.WriteLine($"转换后曲线: {转换器.曲线组.Count}");

        double 最大包围盒尺寸 = 0, 最大半径 = 0;
        int 大曲线数量 = 0;

        foreach (var 曲线 in 转换器.曲线组)
        {
            double 半径 = 0;
            if (Math.Abs(曲线.凸度) > 设置.几何零容差)
            {
                var (_, 当前半径, _, _) = 曲线.获取圆弧几何();
                半径 = 当前半径;
                最大半径 = Math.Max(最大半径, 半径);
            }

            var 采样点组 = 曲线.获取有序点列();
            采样点组.Add(曲线.终点);

            if (采样点组.Count > 0)
            {
                double minX = 采样点组[0].X, maxX = 采样点组[0].X;
                double minY = 采样点组[0].Y, maxY = 采样点组[0].Y;
                foreach (var 点 in 采样点组)
                {
                    if (点.X < minX) minX = 点.X;
                    if (点.X > maxX) maxX = 点.X;
                    if (点.Y < minY) minY = 点.Y;
                    if (点.Y > maxY) maxY = 点.Y;
                }

                double 宽度 = maxX - minX;
                double 高度 = maxY - minY;
                double 包围盒尺寸 = Math.Max(宽度, 高度);
                最大包围盒尺寸 = Math.Max(最大包围盒尺寸, 包围盒尺寸);

                if (半径 > 100 || 包围盒尺寸 > 100) 大曲线数量++;
            }
        }

        Console.WriteLine($"最大包围盒尺寸 = {最大包围盒尺寸:F4}");
        Console.WriteLine($"最大半径 = {最大半径:F4}");
        Console.WriteLine($"尺寸大于 100 的曲线数量 = {大曲线数量}");

        几何修正器 修正器 = new 几何修正器(转换器.曲线组);
        Console.WriteLine($"修正后曲线: {修正器.曲线组.Count}");
        for (int i = 0; i < 修正器.曲线组.Count; i++)
        {
            var 曲线 = 修正器.曲线组[i];
            Console.WriteLine($"  [{i}] 起点=({曲线.起点.X:F4},{曲线.起点.Y:F4}) 终点=({曲线.终点.X:F4},{曲线.终点.Y:F4}) 凸度={曲线.凸度:F6} 长度={曲线.长度:F4}");
        }
        环查找器 环查找 = new 环查找器(修正器.曲线组);
        Console.WriteLine($"环数量: {环查找.环组.Count}");
        for (int ri = 0; ri < 环查找.环组.Count; ri++)
        {
            var 环 = 环查找.环组[ri];
            double 环面积 = 0;
            for (int k = 0; k < 环.Count; k++)
                环面积 += 环[k].起点.X * 环[(k + 1) % 环.Count].起点.Y - 环[(k + 1) % 环.Count].起点.X * 环[k].起点.Y;
            环面积 *= 0.5;
            Console.WriteLine($"  环[{ri}] 边数={环.Count} 面积≈{环面积:F2}");
            for (int k = 0; k < 环.Count; k++)
                Console.WriteLine($"    [{k}] ({环[k].起点.X:F4},{环[k].起点.Y:F4})->({环[k].终点.X:F4},{环[k].终点.Y:F4}) 凸度={环[k].凸度:F4}");
        }
        轮廓分析器 分析器 = new 轮廓分析器(环查找.环组);
        Console.WriteLine($"面积: {分析器.面积描述}");
        Console.WriteLine($"外轮廓: {(分析器.外轮廓 != null ? 分析器.外轮廓.Vertexes.Count + "顶点" : "无")}");
        Console.WriteLine($"内轮廓: {(分析器.内轮廓 != null ? 分析器.内轮廓.Vertexes.Count + "顶点" : "无")}");
        string 输出目录 = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(文件路径)!, "轮廓");
        System.IO.Directory.CreateDirectory(输出目录);
        string 输出路径 = System.IO.Path.Combine(输出目录, System.IO.Path.GetFileName(文件路径));
        DXF读写器.写入(输出路径, 文件路径, 分析器.内轮廓, 分析器.外轮廓, 修正器.曲线组);
        Console.WriteLine($"输出: {输出路径}");
    }

    static void 分析结果Dxf(string 文件路径)
    {
        Console.WriteLine($"分析结果文件: {文件路径}");
        var 图纸 = netDxf.DxfDocument.Load(文件路径);
        if (图纸 == null) { Console.WriteLine("无法加载"); return; }
        Console.WriteLine($"直线数量: {图纸.Entities.Lines.Count()}");
        Console.WriteLine($"圆弧数量: {图纸.Entities.Arcs.Count()}");
        Console.WriteLine($"圆数量: {图纸.Entities.Circles.Count()}");
        Console.WriteLine($"二维多段线数量: {图纸.Entities.Polylines2D.Count()}");
        Console.WriteLine($"样条数量: {图纸.Entities.Splines.Count()}");
        foreach (var 多段线 in 图纸.Entities.Polylines2D)
        {
            string 颜色名称 = 多段线.Color.Index switch { 2 => "黄色", 1 => "红色", 7 => "白色", _ => 多段线.Color.Index.ToString() };
            Console.WriteLine($"  二维多段线 颜色={颜色名称} 顶点数={多段线.Vertexes.Count} 闭合={多段线.IsClosed}");
            for (int i = 0; i < 多段线.Vertexes.Count; i++)
            {
                var 顶点 = 多段线.Vertexes[i];
                Console.WriteLine($"    [{i}] ({顶点.Position.X:F4},{顶点.Position.Y:F4}) 凸度={顶点.Bulge:F6}");
            }
        }
    }
}
