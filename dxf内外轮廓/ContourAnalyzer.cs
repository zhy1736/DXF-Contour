
using netDxf;
using netDxf.Entities;
using System;
using System.Collections.Generic;



namespace dxf内外轮廓
{
    /// <summary>
    /// Analyzes the topological loops to identify valid inner and outer contours based on signed area.
    /// </summary>
    internal class ContourAnalyzer
    {
        public Polyline2D? OuterContour = null;
        public Polyline2D? InnerContour = null;
        public string AreaDescriptions = "";
        public ContourAnalyzer(List<List<Curve>> Loops)
        {
            var all = new List<(Polyline2D polyline, double area)>();
            foreach (var loop in Loops)
            {
                double area = 计算环面积(loop);
                var pl = 曲线转Polyline2D(loop);
                if (pl != null) all.Add((pl, area));
            }
            // 过滤退化环（面积接近零），按绝对面积降序排列
            var sorted = all.Where(p => Math.Abs(p.area) > 0.1).OrderByDescending(p => Math.Abs(p.area)).ToList();
            AreaDescriptions = string.Join(",", sorted.Select(p => p.area.ToString("F2")));

            if (sorted.Count > 0) OuterContour = sorted[0].polyline;
            // 如果最大正面积与最大负面积绝对值相等，说明是同一个环的正反方向，跳过取下一个
            int nextIdx = 1;
            if (sorted.Count > 1 && Math.Abs(Math.Abs(sorted[0].area) - Math.Abs(sorted[1].area)) < 0.1)
                nextIdx = 2;
            if (sorted.Count > nextIdx) InnerContour = sorted[nextIdx].polyline;
        }
        Polyline2D? 曲线转Polyline2D(List<Curve> ordered)
        {
            if (ordered.Count == 0) return null;
            var vertices = new List<Polyline2DVertex>();
            foreach (var c in ordered)
            {
                vertices.Add(new Polyline2DVertex(c.StartPoint.X, c.StartPoint.Y) { Bulge = c.Bulge });
            }
            if (vertices.Count < 2) return null;

            // 合并连续共线的直线段（bulge=0的相邻顶点方向相同时移除中间点）
            for (int i = vertices.Count - 2; i >= 1; i--)
            {
                if (Math.Abs(vertices[i - 1].Bulge) > 1e-10) continue;
                if (Math.Abs(vertices[i].Bulge) > 1e-10) continue;
                var p0 = vertices[i - 1].Position;
                var p1 = vertices[i].Position;
                var p2 = vertices[(i + 1) % vertices.Count].Position;
                double dx1 = p1.X - p0.X, dy1 = p1.Y - p0.Y;
                double dx2 = p2.X - p1.X, dy2 = p2.Y - p1.Y;
                double cross = dx1 * dy2 - dy1 * dx2;
                double dot = dx1 * dx2 + dy1 * dy2;
                if (Math.Abs(cross) < 1e-6 && dot > 0)
                    vertices.RemoveAt(i);
            }
            // 检查首尾跨越处
            if (vertices.Count >= 3 &&
                Math.Abs(vertices[vertices.Count - 1].Bulge) < 1e-10 &&
                Math.Abs(vertices[0].Bulge) < 1e-10)
            {
                var pLast = vertices[vertices.Count - 1].Position;
                var p0 = vertices[0].Position;
                var p1 = vertices[1].Position;
                double dx1 = p0.X - pLast.X, dy1 = p0.Y - pLast.Y;
                double dx2 = p1.X - p0.X, dy2 = p1.Y - p0.Y;
                double cross = dx1 * dy2 - dy1 * dx2;
                double dot = dx1 * dx2 + dy1 * dy2;
                if (Math.Abs(cross) < 1e-6 && dot > 0)
                    vertices.RemoveAt(0);
            }
            return vertices.Count >= 2 ? new Polyline2D(vertices, true) : null;
        }

        double 计算环面积(List<Curve> loop)
        {
            var pts = new List<Vector3>();
            foreach (var c in loop)
                pts.AddRange(c.GetOrderedPoints());
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

    }
}
