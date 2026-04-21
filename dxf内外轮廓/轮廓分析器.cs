
using netDxf;
using netDxf.Entities;
using System;
using System.Collections.Generic;
using System.Linq;



namespace dxf内外轮廓
{
    /// <summary>
    /// 分析拓扑环并根据面积识别有效的内外轮廓。
    /// </summary>
    internal class 轮廓分析器
    {
        public Polyline2D? 外轮廓 = null;
        public Polyline2D? 内轮廓 = null;
        public string 面积描述 = "";
        public 轮廓分析器(List<List<曲线>> 环组)
        {
            var 全部轮廓 = new List<(Polyline2D 多段线, double 面积, List<Vector3> 点组)>();
            foreach (var 环 in 环组)
            {
                var 采样点 = 获取环采样点(环);
                double 面积 = 计算面积(采样点);
                var 多段线 = 曲线转Polyline2D(环);
                if (多段线 != null) 全部轮廓.Add((多段线, 面积, 采样点));
            }
            // 第 5 步：统一在这里过滤小面积环。
            var 已排序轮廓 = 全部轮廓.Where(p => Math.Abs(p.面积) > 设置.最小环面积).OrderByDescending(p => Math.Abs(p.面积)).ToList();
            面积描述 = string.Join(",", 已排序轮廓.Select(p => p.面积.ToString("F2")));

            if (已排序轮廓.Count == 0) return;

            var 最大环 = 已排序轮廓[0];
            外轮廓 = 最大环.多段线;

            var 图形中心点 = 计算图形中心点(已排序轮廓.SelectMany(p => p.点组));
            var 内轮廓候选组 = 已排序轮廓
                .Where(p => !ReferenceEquals(p.多段线, 外轮廓))
                .Where(p => Math.Abs(Math.Abs(p.面积) - Math.Abs(最大环.面积)) >= 设置.最小环面积)
                .ToList();

            var 包含中心点的环 = 内轮廓候选组
                .Where(p => 点在环内(图形中心点, p.点组))
                .OrderBy(p => Math.Abs(p.面积))
                .ToList();

            if (包含中心点的环.Count > 0)
                内轮廓 = 包含中心点的环[0].多段线;
        }

        Polyline2D? 曲线转Polyline2D(List<曲线> 有序曲线组)
        {
            if (有序曲线组.Count == 0) return null;
            var 顶点组 = new List<Polyline2DVertex>();
            foreach (var 曲线 in 有序曲线组)
            {
                顶点组.Add(new Polyline2DVertex(曲线.起点.X, 曲线.起点.Y) { Bulge = 曲线.凸度 });
            }
            if (顶点组.Count < 2) return null;

            // 合并连续共线的直线段（凸度为 0 的相邻顶点方向相同时移除中间点）
            for (int i = 顶点组.Count - 2; i >= 1; i--)
            {
                if (Math.Abs(顶点组[i - 1].Bulge) > 设置.几何零容差) continue;
                if (Math.Abs(顶点组[i].Bulge) > 设置.几何零容差) continue;
                var p0 = 顶点组[i - 1].Position;
                var p1 = 顶点组[i].Position;
                var p2 = 顶点组[(i + 1) % 顶点组.Count].Position;
                double dx1 = p1.X - p0.X, dy1 = p1.Y - p0.Y;
                double dx2 = p2.X - p1.X, dy2 = p2.Y - p1.Y;
                double cross = dx1 * dy2 - dy1 * dx2;
                double dot = dx1 * dx2 + dy1 * dy2;
                if (Math.Abs(cross) < 设置.共线判断容差 && dot > 0)
                    顶点组.RemoveAt(i);
            }
            // 检查首尾跨越处
            if (顶点组.Count >= 3 &&
                Math.Abs(顶点组[顶点组.Count - 1].Bulge) < 设置.几何零容差 &&
                Math.Abs(顶点组[0].Bulge) < 设置.几何零容差)
            {
                var pLast = 顶点组[顶点组.Count - 1].Position;
                var p0 = 顶点组[0].Position;
                var p1 = 顶点组[1].Position;
                double dx1 = p0.X - pLast.X, dy1 = p0.Y - pLast.Y;
                double dx2 = p1.X - p0.X, dy2 = p1.Y - p0.Y;
                double cross = dx1 * dy2 - dy1 * dx2;
                double dot = dx1 * dx2 + dy1 * dy2;
                if (Math.Abs(cross) < 设置.共线判断容差 && dot > 0)
                    顶点组.RemoveAt(0);
            }
            return 顶点组.Count >= 2 ? new Polyline2D(顶点组, true) : null;
        }

        List<Vector3> 获取环采样点(List<曲线> 环)
        {
            var 采样点 = new List<Vector3>();
            foreach (var 曲线 in 环)
                采样点.AddRange(曲线.获取有序点列());
            return 采样点;
        }

        double 计算面积(List<Vector3> pts)
        {
            if (pts.Count < 3) return 0;
            double area = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                var p1 = pts[i];
                var p2 = pts[(i + 1) % pts.Count];
                area += p1.X * p2.Y - p2.X * p1.Y;
            }
            return area / 2.0;
        }

        Vector3 计算图形中心点(IEnumerable<Vector3> 点组)
        {
            var 采样点组 = 点组.ToList();
            if (采样点组.Count == 0) return Vector3.Zero;

            double minX = 采样点组[0].X, maxX = 采样点组[0].X;
            double minY = 采样点组[0].Y, maxY = 采样点组[0].Y;
            foreach (var pt in 采样点组)
            {
                if (pt.X < minX) minX = pt.X;
                if (pt.X > maxX) maxX = pt.X;
                if (pt.Y < minY) minY = pt.Y;
                if (pt.Y > maxY) maxY = pt.Y;
            }

            return new Vector3((minX + maxX) / 2.0, (minY + maxY) / 2.0, 0);
        }

        bool 点在环内(Vector3 点, List<Vector3> 多边形)
        {
            if (多边形.Count < 3) return false;

            bool 在内部 = false;
            for (int i = 0, j = 多边形.Count - 1; i < 多边形.Count; j = i++)
            {
                var 当前点 = 多边形[i];
                var 前一点 = 多边形[j];
                // 标准射线穿越法:外层条件已排除水平边(分母必非零),无需加魔数防护
                if ((当前点.Y > 点.Y) != (前一点.Y > 点.Y))
                {
                    double 分母 = 前一点.Y - 当前点.Y;
                    if (Math.Abs(分母) < 设置.几何零容差) continue;
                    double 交点X = (前一点.X - 当前点.X) * (点.Y - 当前点.Y) / 分母 + 当前点.X;
                    if (点.X < 交点X)
                        在内部 = !在内部;
                }
            }

            return 在内部;
        }

    }
}
