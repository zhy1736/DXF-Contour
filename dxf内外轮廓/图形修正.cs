using netDxf;
using System;
using System.Collections.Generic;
using System.Text;

namespace dxf内外轮廓
{
    internal class 图形修正
    {
        public List<Curve> curs = new List<Curve>();
        public 图形修正(List<Curve> curs)
        {
            this.curs = curs;
            过滤重叠();       // 先过滤重叠（spline拟合产生的近似重复弧）
            在交点处打断();
            过滤重叠();       // 打断后可能产生新的重叠碎片，再次过滤
            过滤毛刺();
        }

        /// <summary>
        /// 计算所有曲线对的几何交点（线-线、线-弧、弧-弧），在交点处打断曲线。
        /// 同时处理 T 型交叉和 X 型交叉。
        /// </summary>
        void 在交点处打断()
        {
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int i = 0; i < curs.Count && !changed; i++)
                {
                    for (int j = i + 1; j < curs.Count && !changed; j++)
                    {
                        var pts = 求交点(curs[i], curs[j]);
                        foreach (var pt in pts)
                        {
                            if (是内部点(curs[i], pt))
                            {
                                var (c1, c2) = 打断曲线(curs[i], pt);
                                if (c1 != null && c2 != null)
                                {
                                    curs[i] = c1;
                                    curs.Insert(i + 1, c2);
                                    changed = true;
                                    break;
                                }
                            }
                        }
                        if (changed) break;
                        foreach (var pt in pts)
                        {
                            if (是内部点(curs[j], pt))
                            {
                                var (c1, c2) = 打断曲线(curs[j], pt);
                                if (c1 != null && c2 != null)
                                {
                                    curs[j] = c1;
                                    curs.Insert(j + 1, c2);
                                    changed = true;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        List<Vector3> 求交点(Curve a, Curve b)
        {
            var result = new List<Vector3>();
            bool aLine = Math.Abs(a.Bulge) < 1e-10;
            bool bLine = Math.Abs(b.Bulge) < 1e-10;
            if (aLine && bLine) 求线线交点(a, b, result);
            else if (aLine) 求线弧交点(a, b, result);
            else if (bLine) 求线弧交点(b, a, result);
            else 求弧弧交点(a, b, result);
            return result;
        }

        void 求线线交点(Curve a, Curve b, List<Vector3> result)
        {
            double x1 = a.StartPoint.X, y1 = a.StartPoint.Y;
            double x2 = a.EndPoint.X, y2 = a.EndPoint.Y;
            double x3 = b.StartPoint.X, y3 = b.StartPoint.Y;
            double x4 = b.EndPoint.X, y4 = b.EndPoint.Y;
            double denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
            if (Math.Abs(denom) < 1e-10) return; // 平行或共线
            double t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denom;
            double u = -((x1 - x2) * (y1 - y3) - (y1 - y2) * (x1 - x3)) / denom;
            if (t >= -1e-6 && t <= 1 + 1e-6 && u >= -1e-6 && u <= 1 + 1e-6)
            {
                t = Math.Max(0, Math.Min(1, t));
                result.Add(new Vector3(x1 + t * (x2 - x1), y1 + t * (y2 - y1), 0));
            }
        }

        void 求线弧交点(Curve line, Curve arc, List<Vector3> result)
        {
            var (center, R, _, _) = arc.ArcGeometry();
            double x1 = line.StartPoint.X, y1 = line.StartPoint.Y;
            double dx = line.EndPoint.X - x1, dy = line.EndPoint.Y - y1;
            double fx = x1 - center.X, fy = y1 - center.Y;
            double a = dx * dx + dy * dy;
            if (a < 1e-20) return;
            double b = 2 * (fx * dx + fy * dy);
            double c = fx * fx + fy * fy - R * R;
            double disc = b * b - 4 * a * c;
            if (disc < 0) return;
            double sqrtDisc = Math.Sqrt(Math.Max(0, disc));
            for (int sign = -1; sign <= 1; sign += 2)
            {
                double t = (-b + sign * sqrtDisc) / (2 * a);
                if (t < -1e-6 || t > 1 + 1e-6) continue;
                t = Math.Max(0, Math.Min(1, t));
                var pt = new Vector3(x1 + t * dx, y1 + t * dy, 0);
                if (点在弧上(arc, pt))
                    result.Add(pt);
            }
        }

        void 求弧弧交点(Curve a, Curve b, List<Vector3> result)
        {
            var (c1, r1, _, _) = a.ArcGeometry();
            var (c2, r2, _, _) = b.ArcGeometry();
            double dx = c2.X - c1.X, dy = c2.Y - c1.Y;
            double d = Math.Sqrt(dx * dx + dy * dy);
            if (d > r1 + r2 + 静态.Tolerance || d < Math.Abs(r1 - r2) - 静态.Tolerance || d < 1e-10) return;
            double aa = (r1 * r1 - r2 * r2 + d * d) / (2 * d);
            double h2 = r1 * r1 - aa * aa;
            if (h2 < 0) h2 = 0;
            double h = Math.Sqrt(h2);
            double mx = c1.X + aa * dx / d, my = c1.Y + aa * dy / d;
            if (h < 1e-10)
            {
                var pt = new Vector3(mx, my, 0);
                if (点在弧上(a, pt) && 点在弧上(b, pt))
                    result.Add(pt);
            }
            else
            {
                var p1 = new Vector3(mx + h * dy / d, my - h * dx / d, 0);
                var p2 = new Vector3(mx - h * dy / d, my + h * dx / d, 0);
                if (点在弧上(a, p1) && 点在弧上(b, p1)) result.Add(p1);
                if (点在弧上(a, p2) && 点在弧上(b, p2)) result.Add(p2);
            }
        }

        bool 点在弧上(Curve arc, Vector3 pt)
        {
            var (center, R, _, _) = arc.ArcGeometry();
            if (Math.Abs(Vector3.Distance(pt, center) - R) > 静态.Tolerance * 10) return false;
            double angS = Math.Atan2(arc.StartPoint.Y - center.Y, arc.StartPoint.X - center.X);
            double angPt = Math.Atan2(pt.Y - center.Y, pt.X - center.X);
            double fullSweep = 4.0 * Math.Atan(Math.Abs(arc.Bulge));
            double sweep;
            if (arc.Bulge > 0)
            { sweep = angPt - angS; while (sweep < -1e-9) sweep += 2 * Math.PI; }
            else
            { sweep = angS - angPt; while (sweep < -1e-9) sweep += 2 * Math.PI; }
            return sweep <= fullSweep + 1e-6;
        }

        bool 是内部点(Curve c, Vector3 pt)
        {
            if (Vector3.Distance(pt, c.StartPoint) < 静态.Tolerance) return false;
            if (Vector3.Distance(pt, c.EndPoint) < 静态.Tolerance) return false;
            if (Math.Abs(c.Bulge) < 1e-10)
            {
                var seg = c.EndPoint - c.StartPoint;
                double segLen = Math.Sqrt(seg.X * seg.X + seg.Y * seg.Y);
                if (segLen < 1e-10) return false;
                var d = pt - c.StartPoint;
                double proj = (d.X * seg.X + d.Y * seg.Y) / segLen;
                double perpX = d.X - proj * seg.X / segLen, perpY = d.Y - proj * seg.Y / segLen;
                return Math.Sqrt(perpX * perpX + perpY * perpY) < 静态.Tolerance && proj > 静态.Tolerance && proj < segLen - 静态.Tolerance;
            }
            else
            {
                var (center, R, _, _) = c.ArcGeometry();
                if (Math.Abs(Vector3.Distance(pt, center) - R) > 静态.Tolerance) return false;
                double angS = Math.Atan2(c.StartPoint.Y - center.Y, c.StartPoint.X - center.X);
                double angPt = Math.Atan2(pt.Y - center.Y, pt.X - center.X);
                double fullSweep = 4.0 * Math.Atan(Math.Abs(c.Bulge));
                double sweep;
                if (c.Bulge > 0)
                { sweep = angPt - angS; while (sweep < -1e-9) sweep += 2 * Math.PI; }
                else
                { sweep = angS - angPt; while (sweep < -1e-9) sweep += 2 * Math.PI; }
                return sweep > 1e-6 && sweep < fullSweep - 1e-6;
            }
        }

        (Curve?, Curve?) 打断曲线(Curve c, Vector3 pt)
        {
            if (Math.Abs(c.Bulge) < 1e-10)
                return (new Curve(c.StartPoint, pt), new Curve(pt, c.EndPoint));
            var (center, R, _, _) = c.ArcGeometry();
            double angS = Math.Atan2(c.StartPoint.Y - center.Y, c.StartPoint.X - center.X);
            double angM = Math.Atan2(pt.Y - center.Y, pt.X - center.X);
            double fullSweep = 4.0 * Math.Atan(Math.Abs(c.Bulge));
            double sweep1;
            if (c.Bulge > 0)
            { sweep1 = angM - angS; while (sweep1 < 0) sweep1 += 2 * Math.PI; }
            else
            { sweep1 = angS - angM; while (sweep1 < 0) sweep1 += 2 * Math.PI; }
            double sweep2 = fullSweep - sweep1;
            if (sweep1 < 1e-9 || sweep2 < 1e-9) return (null, null);
            double sign = c.Bulge > 0 ? 1 : -1;
            return (new Curve(c.StartPoint, pt, sign * Math.Tan(sweep1 / 4.0)),
                    new Curve(pt, c.EndPoint, sign * Math.Tan(sweep2 / 4.0)));
        }

        void 过滤毛刺()
        {
            bool removed = true;
            while (removed)
            {
                removed = false;
                for (int i = curs.Count - 1; i >= 0; i--)
                {
                    var a = curs[i];
                    bool sConn = false, eConn = false;
                    for (int j = 0; j < curs.Count; j++)
                    {
                        if (j == i) continue;
                        var b = curs[j];
                        if (!sConn && (Vector3.Distance(a.StartPoint, b.StartPoint) < 静态.Tolerance ||
                                       Vector3.Distance(a.StartPoint, b.EndPoint) < 静态.Tolerance))
                            sConn = true;
                        if (!eConn && (Vector3.Distance(a.EndPoint, b.StartPoint) < 静态.Tolerance ||
                                       Vector3.Distance(a.EndPoint, b.EndPoint) < 静态.Tolerance))
                            eConn = true;
                        if (sConn && eConn) break;
                    }
                    if (!sConn || !eConn)
                    {
                        curs.RemoveAt(i);
                        removed = true;
                    }
                }
            }
        }

        void 过滤重叠()
        {
            // spline拟合出的同一几何曲线的不同副本，端点可能有较大偏差(0.001~0.03)
            // 用两种策略：1.端点+中点匹配 2.同圆弧几何匹配（圆心+半径+角度重叠）
            double endTol = 静态.Tolerance * 10; // 端点容差 1e-3
            double geoTol = 0.05; // 几何容差（圆心/半径比较用），适应spline拟合误差

            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int i = curs.Count - 1; i >= 0; i--)
                {
                    var a = curs[i];
                    for (int j = i - 1; j >= 0; j--)
                    {
                        var b = curs[j];
                        if (是重叠(a, b, endTol, geoTol))
                        {
                            curs.RemoveAt(i); changed = true; break;
                        }
                    }
                    if (changed) break;
                }
            }
        }

        bool 是重叠(Curve a, Curve b, double endTol, double geoTol)
        {
            bool aLine = Math.Abs(a.Bulge) < 1e-10;
            bool bLine = Math.Abs(b.Bulge) < 1e-10;

            // 策略1：端点接近
            bool same = Vector3.Distance(a.StartPoint, b.StartPoint) < endTol &&
                        Vector3.Distance(a.EndPoint, b.EndPoint) < endTol;
            bool opp = Vector3.Distance(a.StartPoint, b.EndPoint) < endTol &&
                       Vector3.Distance(a.EndPoint, b.StartPoint) < endTol;
            if (same || opp)
            {
                if (aLine && bLine) return true;
                if (!aLine && !bLine)
                {
                    var midA = 弧中点(a);
                    var midB = 弧中点(b);
                    if (Vector3.Distance(midA, midB) < endTol) return true;
                }
                if (aLine != bLine && Math.Abs(a.Length - b.Length) < a.Length * 0.02 + endTol) return true;
            }

            // 策略2：两条圆弧在同一圆上且角度范围高度重叠
            if (!aLine && !bLine)
            {
                var (ca, ra, sa, ea) = a.ArcGeometry();
                var (cb, rb, sb, eb) = b.ArcGeometry();
                if (Math.Abs(ra - rb) < geoTol && Vector3.Distance(ca, cb) < geoTol)
                {
                    // 角度范围重叠检测
                    double sweepA = ea - sa, sweepB = eb - sb;
                    double overlapStart = Math.Max(sa, sb);
                    double overlapEnd = Math.Min(ea, eb);
                    double overlap = overlapEnd - overlapStart;
                    double minSweep = Math.Min(sweepA, sweepB);
                    if (minSweep > 0 && overlap / minSweep > 0.9) return true;
                }
            }

            // 策略3：直线共线且大部分重叠
            if (aLine && bLine)
            {
                double lenA = a.Length, lenB = b.Length;
                if (lenA < 1e-10 || lenB < 1e-10) return false;
                // 检查方向是否平行
                var dA = a.EndPoint - a.StartPoint;
                var dB = b.EndPoint - b.StartPoint;
                double cross = dA.X * dB.Y - dA.Y * dB.X;
                if (Math.Abs(cross) > geoTol * Math.Max(lenA, lenB)) return false;
                // a的端点到b线段的距离
                double distAS = 点到线段距离(a.StartPoint, b);
                double distAE = 点到线段距离(a.EndPoint, b);
                double distBS = 点到线段距离(b.StartPoint, a);
                double distBE = 点到线段距离(b.EndPoint, a);
                if (distAS < geoTol && distAE < geoTol) return true;
                if (distBS < geoTol && distBE < geoTol) return true;
            }

            return false;
        }

        double 点到线段距离(Vector3 pt, Curve line)
        {
            var seg = line.EndPoint - line.StartPoint;
            double segLen = Math.Sqrt(seg.X * seg.X + seg.Y * seg.Y);
            if (segLen < 1e-10) return Vector3.Distance(pt, line.StartPoint);
            var d = pt - line.StartPoint;
            double proj = (d.X * seg.X + d.Y * seg.Y) / segLen;
            proj = Math.Max(0, Math.Min(segLen, proj));
            double px = line.StartPoint.X + proj * seg.X / segLen - pt.X;
            double py = line.StartPoint.Y + proj * seg.Y / segLen - pt.Y;
            return Math.Sqrt(px * px + py * py);
        }

        Vector3 弧中点(Curve c)
        {
            if (Math.Abs(c.Bulge) < 1e-10)
                return new Vector3((c.StartPoint.X + c.EndPoint.X) / 2, (c.StartPoint.Y + c.EndPoint.Y) / 2, 0);
            var (center, radius, sDeg, eDeg) = c.ArcGeometry();
            double midDeg = (sDeg + eDeg) / 2.0;
            double midRad = midDeg * Math.PI / 180.0;
            return new Vector3(center.X + radius * Math.Cos(midRad), center.Y + radius * Math.Sin(midRad), 0);
        }
    }
}
