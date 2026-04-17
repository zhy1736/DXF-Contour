using netDxf;
using netDxf.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace dxf内外轮廓
{
    /// <summary>
    /// Identifies closed loops from an unstructured list of curves using a winged-edge topology.
    /// </summary>
    internal class LoopFinder
    {
        public List<Curve> Curves = new List<Curve>();
        public List<List<Curve>> Loops = new List<List<Curve>>();
        public LoopFinder(List<Curve> Curves)
        {
            this.Curves = Curves;
            int n = Curves.Count;

            // 1. 端点聚类为顶点
            var vertices = new List<Vector3>();
            var vStart = new int[n];
            var vEnd = new int[n];
            for (int i = 0; i < n; i++)
            {
                vStart[i] = FindOrAddVertex(vertices, Curves[i].StartPoint, Settings.Tolerance);
                vEnd[i] = FindOrAddVertex(vertices, Curves[i].EndPoint, Settings.Tolerance);
            }

            // 2. 创建半边 (2*i=正向, 2*i+1=反向)
            int heCount = 2 * n;
            var heFrom = new int[heCount];
            var heTo = new int[heCount];
            var heAngle = new double[heCount];
            var heValid = new bool[heCount];

            for (int i = 0; i < n; i++)
            {
                if (vStart[i] == vEnd[i]) continue; // 跳过自环

                heFrom[2 * i] = vStart[i];
                heTo[2 * i] = vEnd[i];
                heValid[2 * i] = true;

                heFrom[2 * i + 1] = vEnd[i];
                heTo[2 * i + 1] = vStart[i];
                heValid[2 * i + 1] = true;
            }

            // 3. 精确切线 + 曲率法计算半边出射角度
            //    对圆弧直接计算切线方向（半径的垂线），保证精确
            //    当两条圆弧在同一顶点相切（切线角相同）时，用有符号曲率区分：
            //    曲率 = +1/R（CCW方向）或 -1/R（CW方向），0 = 直线
            var heCurvature = new double[heCount];
            for (int h = 0; h < heCount; h++)
            {
                if (!heValid[h]) continue;
                var V = vertices[heFrom[h]];
                var c = Curves[h / 2];
                bool forward = (h % 2 == 0);

                if (Math.Abs(c.Bulge) > 1e-10)
                {
                    var (center, R, _, _) = c.ArcGeometry();
                    double rx = V.X - center.X, ry = V.Y - center.Y;
                    // 半边在此顶点处是否沿CCW方向行进
                    bool ccwAtVertex = (forward && c.Bulge > 0) || (!forward && c.Bulge < 0);
                    double tx, ty;
                    if (ccwAtVertex)
                    {
                        tx = -ry; ty = rx;           // CCW切线 = 半径逆时针旋转90°
                        heCurvature[h] = 1.0 / R;    // 正曲率
                    }
                    else
                    {
                        tx = ry; ty = -rx;            // CW切线 = 半径顺时针旋转90°
                        heCurvature[h] = -1.0 / R;   // 负曲率
                    }
                    heAngle[h] = Math.Atan2(ty, tx);
                }
                else
                {
                    // 直线：朝向另一端点
                    var other = forward ? c.EndPoint : c.StartPoint;
                    heAngle[h] = Math.Atan2(other.Y - V.Y, other.X - V.X);
                    heCurvature[h] = 0;
                }
            }

            // 按起始顶点分组，按角度排序
            var outEdges = new Dictionary<int, List<int>>();
            for (int h = 0; h < heCount; h++)
            {
                if (!heValid[h]) continue;
                if (!outEdges.TryGetValue(heFrom[h], out var list))
                {
                    list = new List<int>();
                    outEdges[heFrom[h]] = list;
                }
                list.Add(h);
            }
            foreach (var list in outEdges.Values)
                list.Sort((a, b) =>
                {
                    double da = heAngle[a] - heAngle[b];
                    if (Math.Abs(da) > 1e-9)
                        return da > 0 ? 1 : -1;
                    // 切线角相同时按曲率升序：曲率越大越偏左(CCW)排后面
                    return heCurvature[a].CompareTo(heCurvature[b]);
                });

            // 4. 计算每条半边的"下一条"半边
            // 半边 h(u→v) 的 next：在顶点 v 的出边列表中找到 twin(h)，取其前一条（CCW排序中）
            var heNext = new int[heCount];
            var hasNext = new bool[heCount];
            for (int h = 0; h < heCount; h++)
            {
                if (!heValid[h]) continue;
                int twin = h ^ 1;
                int v = heTo[h];
                if (!outEdges.TryGetValue(v, out var edges)) continue;
                int idx = edges.IndexOf(twin);
                if (idx < 0) continue;
                heNext[h] = edges[(idx - 1 + edges.Count) % edges.Count];
                hasNext[h] = true;
            }

            // 5. 追踪面 & 过滤
            var used = new bool[heCount];

            for (int start = 0; start < heCount; start++)
            {
                if (!heValid[start] || !hasNext[start] || used[start]) continue;

                var faceCurves = new List<Curve>();
                
                int h = start;
                bool ok = true;
                do
                {
                    if (used[h]) { ok = false; break; }
                    used[h] = true;
                    var src = Curves[h / 2];
                    var clone = src.CloneWithEndpoints(
                        vertices[heFrom[h]], vertices[heTo[h]]);
                    faceCurves.Add(clone);
                    
                    h = heNext[h];
                    if (faceCurves.Count > heCount) { ok = false; break; }
                } while (h != start);

                if (!ok || faceCurves.Count < 2) continue;

                // 有符号面积：正=InnerContour(CCW)，负=OuterContour(CW)，过滤外轮廓和退化面
                // 使用弧线采样点计算更精确的面积
                double area = 0;
                var sampledPts = new List<Vector3>();
                foreach (var fc in faceCurves)
                    sampledPts.AddRange(fc.GetOrderedPoints());
                for (int i = 0; i < sampledPts.Count; i++)
                {
                    var p1 = sampledPts[i];
                    var p2 = sampledPts[(i + 1) % sampledPts.Count];
                    area += p1.X * p2.Y - p2.X * p1.Y;
                }
                area /= 2.0;
                // 过滤退化面（面积接近零），保留正面积（InnerContour）和负面积（OuterContour/孔）
                if (Math.Abs(area) > 0.1) Loops.Add(faceCurves);
            }
        }



        int FindOrAddVertex(List<Vector3> vertices, Vector3 pt, double tol)
        {
            for (int i = 0; i < vertices.Count; i++)
                if (Vector3.Distance(pt, vertices[i]) < tol) return i;
            vertices.Add(pt);
            return vertices.Count - 1;
        }



    }
}
