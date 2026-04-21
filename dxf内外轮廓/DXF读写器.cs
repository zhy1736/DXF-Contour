using System;
using System.Collections.Generic;
using netDxf;
using netDxf.Entities;

namespace dxf内外轮廓
{
    internal class DXF读写器
    {
        public static string[] 选择文件()
        {
            OpenFileDialog 打开文件对话框 = new OpenFileDialog();
            打开文件对话框.Filter = "DXF 文件 (*.dxf)|*.dxf|所有文件 (*.*)|*.*";
            打开文件对话框.Multiselect = true;
            if (打开文件对话框.ShowDialog() == DialogResult.OK)
                return 打开文件对话框.FileNames;
            return Array.Empty<string>();
        }

        public static IEnumerable<EntityObject> 读取(string 文件路径)
        {
            var 实体组 = new List<EntityObject>();
            DxfDocument? 图纸 = null;
            try
            {
                图纸 = DxfDocument.Load(文件路径);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"读取 DXF 失败: {文件路径}: {ex.Message}");
                return 实体组;
            }
            if (图纸 != null)
            {
                实体组.AddRange(图纸.Entities.Lines);
                实体组.AddRange(图纸.Entities.Arcs);
                实体组.AddRange(图纸.Entities.Circles);
                实体组.AddRange(图纸.Entities.Ellipses);
                实体组.AddRange(图纸.Entities.Polylines2D);
                实体组.AddRange(图纸.Entities.Polylines3D);
                实体组.AddRange(图纸.Entities.Splines);
            }
            return 实体组;
        }

        public static void 写入(string 文件路径, string 源文件路径, Polyline2D? 内轮廓, Polyline2D? 外轮廓,
            IEnumerable<曲线>? 修正后曲线组 = null)
        {
            DxfDocument? 图纸 = null;
            try
            {
                图纸 = DxfDocument.Load(源文件路径);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载源 DXF 失败 ({源文件路径}): {ex.Message},将创建新文档。");
            }
            if (图纸 == null) 图纸 = new DxfDocument();

            // 原有线改为白色
            foreach (var 实体 in 图纸.Entities.Lines) 实体.Color = AciColor.Default;
            foreach (var 实体 in 图纸.Entities.Arcs) 实体.Color = AciColor.Default;
            foreach (var 实体 in 图纸.Entities.Circles) 实体.Color = AciColor.Default;
            foreach (var 实体 in 图纸.Entities.Ellipses) 实体.Color = AciColor.Default;
            foreach (var 实体 in 图纸.Entities.Polylines2D) 实体.Color = AciColor.Default;
            foreach (var 实体 in 图纸.Entities.Polylines3D) 实体.Color = AciColor.Default;
            foreach (var 实体 in 图纸.Entities.Splines) 实体.Color = AciColor.Default;

            // 添加修正后的网图：蓝色（直线/圆弧）
            if (修正后曲线组 != null)
            {
                foreach (var 曲线 in 修正后曲线组)
                {
                    EntityObject? 实体 = 创建修正曲线实体(曲线);
                    if (实体 == null) continue;
                    实体.Color = AciColor.Blue;
                    图纸.Entities.Add(实体);
                }
            }

            // 添加轮廓：内轮廓黄色，外轮廓红色
            if (内轮廓 != null)
            {
                内轮廓.Color = AciColor.Yellow;
                图纸.Entities.Add(内轮廓);
            }
            if (外轮廓 != null)
            {
                外轮廓.Color = AciColor.Red;
                图纸.Entities.Add(外轮廓);
            }

            调整视口显示全部内容(图纸, 内轮廓, 外轮廓);
            try
            {
                图纸.Save(文件路径);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存 DXF 失败 ({文件路径}): {ex.Message}");
                throw;
            }
        }

        static EntityObject? 创建修正曲线实体(曲线 曲线)
        {
            if (Vector3.Distance(曲线.起点, 曲线.终点) < 设置.几何零容差 && Math.Abs(曲线.凸度) < 设置.几何零容差)
                return null;
            if (Math.Abs(曲线.凸度) < 设置.几何零容差)
            {
                return new Line(
                    new Vector2(曲线.起点.X, 曲线.起点.Y),
                    new Vector2(曲线.终点.X, 曲线.终点.Y));
            }
            var (圆心, 半径, 起始角, 终止角) = 曲线.获取圆弧几何();
            if (半径 < 设置.几何零容差) return null;
            return new Arc(new Vector2(圆心.X, 圆心.Y), 半径, 起始角, 终止角);
        }

        static void 调整视口显示全部内容(DxfDocument 图纸, Polyline2D? 内轮廓, Polyline2D? 外轮廓)
        {
            if (!尝试获取图形范围(图纸, 内轮廓, 外轮廓, out double 最小X, out double 最小Y, out double 最大X, out double 最大Y))
                return;

            double 宽度 = Math.Max(最大X - 最小X, 1.0);
            double 高度 = Math.Max(最大Y - 最小Y, 1.0);
            double 边距 = Math.Max(Math.Max(宽度, 高度) * 0.05, 设置.容差 * 10);
            double 视图宽度 = 宽度 + 边距 * 2;
            double 视图高度 = 高度 + 边距 * 2;

            图纸.Viewport.ViewCenter = new Vector2((最小X + 最大X) * 0.5, (最小Y + 最大Y) * 0.5);
            图纸.Viewport.ViewHeight = 视图高度;
            图纸.Viewport.ViewAspectRatio = 视图宽度 / 视图高度;
        }

        static bool 尝试获取图形范围(DxfDocument 图纸, Polyline2D? 内轮廓, Polyline2D? 外轮廓,
            out double 最小X, out double 最小Y, out double 最大X, out double 最大Y)
        {
            最小X = double.PositiveInfinity;
            最小Y = double.PositiveInfinity;
            最大X = double.NegativeInfinity;
            最大Y = double.NegativeInfinity;

            foreach (var 实体 in 图纸.Entities.All)
                纳入实体范围(实体, ref 最小X, ref 最小Y, ref 最大X, ref 最大Y);

            if (内轮廓 != null)
                纳入二维多段线范围(内轮廓, ref 最小X, ref 最小Y, ref 最大X, ref 最大Y);

            if (外轮廓 != null)
                纳入二维多段线范围(外轮廓, ref 最小X, ref 最小Y, ref 最大X, ref 最大Y);

            return !double.IsInfinity(最小X) && !double.IsInfinity(最小Y) && !double.IsInfinity(最大X) && !double.IsInfinity(最大Y);
        }

        static void 纳入实体范围(EntityObject 实体, ref double 最小X, ref double 最小Y, ref double 最大X, ref double 最大Y)
        {
            switch (实体)
            {
                case Line 直线:
                    纳入点范围(直线.StartPoint, ref 最小X, ref 最小Y, ref 最大X, ref 最大Y);
                    纳入点范围(直线.EndPoint, ref 最小X, ref 最小Y, ref 最大X, ref 最大Y);
                    break;
                case Arc 圆弧:
                    纳入点集范围(采样圆弧(圆弧, 72), ref 最小X, ref 最小Y, ref 最大X, ref 最大Y);
                    break;
                case Circle 圆:
                    纳入点范围(new Vector3(圆.Center.X - 圆.Radius, 圆.Center.Y - 圆.Radius, 0), ref 最小X, ref 最小Y, ref 最大X, ref 最大Y);
                    纳入点范围(new Vector3(圆.Center.X + 圆.Radius, 圆.Center.Y + 圆.Radius, 0), ref 最小X, ref 最小Y, ref 最大X, ref 最大Y);
                    break;
                case Ellipse 椭圆:
                    纳入点集范围(采样椭圆(椭圆, 144), ref 最小X, ref 最小Y, ref 最大X, ref 最大Y);
                    break;
                case Polyline2D 二维多段线:
                    纳入二维多段线范围(二维多段线, ref 最小X, ref 最小Y, ref 最大X, ref 最大Y);
                    break;
                case Polyline3D 三维多段线:
                    foreach (var 顶点 in 三维多段线.Vertexes)
                        纳入点范围(顶点, ref 最小X, ref 最小Y, ref 最大X, ref 最大Y);
                    break;
                case Spline 样条:
                    var 采样点组 = 样条.PolygonalVertexes(512);
                    if (采样点组 != null)
                        纳入点集范围(采样点组, ref 最小X, ref 最小Y, ref 最大X, ref 最大Y);
                    else if (样条.ControlPoints != null)
                        纳入点集范围(样条.ControlPoints, ref 最小X, ref 最小Y, ref 最大X, ref 最大Y);
                    break;
            }
        }

        static void 纳入二维多段线范围(Polyline2D 多段线, ref double 最小X, ref double 最小Y, ref double 最大X, ref double 最大Y)
        {
            if (多段线.Vertexes.Count == 0)
                return;

            int 段数 = 多段线.IsClosed ? 多段线.Vertexes.Count : 多段线.Vertexes.Count - 1;
            for (int i = 0; i < 段数; i++)
            {
                var 起点顶点 = 多段线.Vertexes[i];
                var 终点顶点 = 多段线.Vertexes[(i + 1) % 多段线.Vertexes.Count];
                var 起点 = new Vector3(起点顶点.Position.X, 起点顶点.Position.Y, 0);
                var 终点 = new Vector3(终点顶点.Position.X, 终点顶点.Position.Y, 0);
                if (Vector3.Distance(起点, 终点) < 设置.几何零容差)
                    continue;

                var 曲线 = new 曲线(起点, 终点, 起点顶点.Bulge);
                纳入点集范围(曲线.获取有序点列(), ref 最小X, ref 最小Y, ref 最大X, ref 最大Y);
                纳入点范围(终点, ref 最小X, ref 最小Y, ref 最大X, ref 最大Y);
            }
        }

        static List<Vector3> 采样圆弧(Arc 圆弧, int 分段数)
        {
            double 起始角 = 圆弧.StartAngle * Math.PI / 180.0;
            double 终止角 = 圆弧.EndAngle * Math.PI / 180.0;
            if (终止角 < 起始角)
                终止角 += 2 * Math.PI;

            var 点组 = new List<Vector3>();
            for (int i = 0; i <= 分段数; i++)
            {
                double 当前角 = 起始角 + (终止角 - 起始角) * i / 分段数;
                点组.Add(new Vector3(
                    圆弧.Center.X + 圆弧.Radius * Math.Cos(当前角),
                    圆弧.Center.Y + 圆弧.Radius * Math.Sin(当前角),
                    0));
            }
            return 点组;
        }

        static List<Vector3> 采样椭圆(Ellipse 椭圆, int 分段数)
        {
            double 起始角 = 椭圆.StartAngle * Math.PI / 180.0;
            double 终止角 = 椭圆.EndAngle * Math.PI / 180.0;
            if (Math.Abs(终止角 - 起始角) < 设置.参数容差)
                终止角 = 起始角 + 2 * Math.PI;
            if (终止角 < 起始角)
                终止角 += 2 * Math.PI;

            double 旋转余弦 = Math.Cos(椭圆.Rotation * Math.PI / 180.0);
            double 旋转正弦 = Math.Sin(椭圆.Rotation * Math.PI / 180.0);
            double 长半轴 = 椭圆.MajorAxis * 0.5;
            double 短半轴 = 椭圆.MinorAxis * 0.5;

            var 点组 = new List<Vector3>();
            for (int i = 0; i <= 分段数; i++)
            {
                double 参数角 = 起始角 + (终止角 - 起始角) * i / 分段数;
                double 局部X = 长半轴 * Math.Cos(参数角);
                double 局部Y = 短半轴 * Math.Sin(参数角);
                点组.Add(new Vector3(
                    椭圆.Center.X + 局部X * 旋转余弦 - 局部Y * 旋转正弦,
                    椭圆.Center.Y + 局部X * 旋转正弦 + 局部Y * 旋转余弦,
                    0));
            }
            return 点组;
        }

        static void 纳入点集范围(IEnumerable<Vector3> 点组, ref double 最小X, ref double 最小Y, ref double 最大X, ref double 最大Y)
        {
            foreach (var 点 in 点组)
                纳入点范围(点, ref 最小X, ref 最小Y, ref 最大X, ref 最大Y);
        }

        static void 纳入点范围(Vector3 点, ref double 最小X, ref double 最小Y, ref double 最大X, ref double 最大Y)
        {
            if (点.X < 最小X) 最小X = 点.X;
            if (点.Y < 最小Y) 最小Y = 点.Y;
            if (点.X > 最大X) 最大X = 点.X;
            if (点.Y > 最大Y) 最大Y = 点.Y;
        }
    }
}
