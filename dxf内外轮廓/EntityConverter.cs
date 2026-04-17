using netDxf;
using netDxf.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace dxf内外轮廓
{
    /// <summary>
    /// Converts generic DXF entity objects (such as Polylines, Arcs, Splines) into abstract Curves.
    /// </summary>
    internal class EntityConverter
    {
        public List<Curve> Curves = new List<Curve>();
        public EntityConverter(IEnumerable<EntityObject> ents)
        {
            foreach (var e in ents)
            {
                Curves.AddRange(CreateCurves(e));
            }
        }
        List<Curve> CreateCurves(EntityObject dxf)
        {
            var result = new List<Curve>();
            if (dxf is Line l)
            {
                result.Add(new Curve(l.StartPoint, l.EndPoint));
            }
            else if (dxf is Arc a)
            {
                Arc arc = a;
                if (a.Normal.Z < 0)
                    arc = new Arc(a.Center, a.Radius, 180 - a.EndAngle, 180 - a.StartAngle);
                result.Add(ArcToCurve(arc));
            }
            else if (dxf is Polyline2D p && p.Vertexes.Count >= 2)
            {
                拆分多段线(p, result);
            }
            else if (dxf is Spline s)
            {
                if (s.Degree == 1)
                {
                    var pts = s.ControlPoints;
                    if (pts != null && pts.Count() >= 2)
                        result.Add(new Curve(pts.First(), pts.Last()));
                }
                else
                {
                    var pts = s.PolygonalVertexes(100);
                    if (pts != null && pts.Count >= 2)
                        result.AddRange(拟合圆弧(pts));
                }
            }
            else if (dxf is Polyline3D p3d && p3d.Vertexes.Count >= 2)
            {
                var vts = p3d.Vertexes;
                for (int i = 0; i < vts.Count - 1; i++)
                    result.Add(new Curve(vts[i], vts[i + 1]));
            }
            else if (dxf is Circle cir)
            {
                result.Add(ArcToCurve(new Arc(cir.Center, cir.Radius, 0, 180)));
                result.Add(ArcToCurve(new Arc(cir.Center, cir.Radius, 180, 360)));
            }
            else if (dxf is Ellipse ell)
            {
                var pts = SampleEllipse(ell, 36);
                if (pts.Count >= 2)
                    result.AddRange(拟合圆弧(pts));
            }
            return result;
        }
        void 拆分多段线(Polyline2D p, List<Curve> result)
        {
            int segCount = p.IsClosed ? p.Vertexes.Count : p.Vertexes.Count - 1;
            for (int i = 0; i < segCount; i++)
            {
                var v0 = p.Vertexes[i];
                var v1 = p.Vertexes[(i + 1) % p.Vertexes.Count];
                var sp = new Vector3(v0.Position.X, v0.Position.Y, 0);
                var ep = new Vector3(v1.Position.X, v1.Position.Y, 0);
                if (Vector3.Distance(sp, ep) < 1e-10) continue;
                result.Add(new Curve(sp, ep, v0.Bulge));
            }
        }

        List<Vector3> SampleEllipse(Ellipse ell, int segments)
        {
            double sa = ell.StartAngle * Math.PI / 180.0;
            double ea = ell.EndAngle * Math.PI / 180.0;
            if (Math.Abs(ea - sa) < 1e-6) ea = sa + 2 * Math.PI;
            if (ea < sa) ea += 2 * Math.PI;
            double cosRot = Math.Cos(ell.Rotation * Math.PI / 180.0);
            double sinRot = Math.Sin(ell.Rotation * Math.PI / 180.0);
            double rx = ell.MajorAxis * 0.5, ry = ell.MinorAxis * 0.5;
            var pts = new List<Vector3>();
            for (int i = 0; i <= segments; i++)
            {
                double t = sa + (ea - sa) * i / segments;
                double lx = rx * Math.Cos(t), ly = ry * Math.Sin(t);
                pts.Add(new Vector3(ell.Center.X + lx * cosRot - ly * sinRot,
                    ell.Center.Y + lx * sinRot + ly * cosRot, 0));
            }
            return pts;
        }
        Curve ArcToCurve(Arc arc)
        {
            double startRad = arc.StartAngle * Math.PI / 180.0;
            double endRad = arc.EndAngle * Math.PI / 180.0;
            double r = arc.Radius;
            var c = arc.Center;
            var sp = new Vector3(c.X + r * Math.Cos(startRad), c.Y + r * Math.Sin(startRad), 0);
            var ep = new Vector3(c.X + r * Math.Cos(endRad), c.Y + r * Math.Sin(endRad), 0);
            double sweep = endRad - startRad;
            if (sweep < 0) sweep += 2 * Math.PI;
            double bulge = Math.Tan(sweep / 4.0);
            return new Curve(sp, ep, bulge);
        }
        List<Curve> 拟合圆弧(List<Vector3> pts, double arcTol = 0.05)
        {
            var result = new List<Curve>();
            拟合圆弧递归(pts, 0, pts.Count - 1, arcTol, result);
            return result;
        }
        void 拟合圆弧递归(List<Vector3> pts,
          int iStart, int iEnd, double tol, List<Curve> result)
        {
            if (iEnd - iStart < 1) return;
            var sp = pts[iStart];
            var ep = pts[iEnd];

            // 仅2个点 → 直线
            if (iEnd - iStart == 1)
            {
                if (Vector3.Distance(sp, ep) < 1e-10) return;
                result.Add(new Curve(sp, ep));
                return;
            }

            // 三点定圆：起点、中点、终点
            int iMid = (iStart + iEnd) / 2;
            var mp = pts[iMid];
            var center = 三点定圆心(sp, mp, ep);

            if (center.HasValue)
            {
                double radius = Vector3.Distance(center.Value, sp);
                double maxErr = 0;
                for (int i = iStart; i <= iEnd; i++)
                {
                    double err = Math.Abs(Vector3.Distance(pts[i], center.Value) - radius);
                    if (err > maxErr) maxErr = err;
                }
                if (maxErr < tol)
                {
                    double startAng = Math.Atan2(sp.Y - center.Value.Y, sp.X - center.Value.X);
                    double endAng = Math.Atan2(ep.Y - center.Value.Y, ep.X - center.Value.X);
                    double midAng = Math.Atan2(mp.Y - center.Value.Y, mp.X - center.Value.X);

                    double sweep = NormalizeAngle(endAng - startAng);
                    double midSweep = NormalizeAngle(midAng - startAng);
                    if (midSweep > sweep)
                        sweep -= 2 * Math.PI;

                    // 用sweep直接计算bulge: tan(sweep/4)
                    double bulge = Math.Tan(sweep / 4.0);
                    result.Add(new Curve(sp, ep, bulge));
                    return;
                }
            }

            // 拟合失败，二分递归
            拟合圆弧递归(pts, iStart, iMid, tol, result);
            拟合圆弧递归(pts, iMid, iEnd, tol, result);
        }

        Vector3? 三点定圆心(Vector3 a, Vector3 b, Vector3 c)
        {
            double ax = a.X, ay = a.Y, bx = b.X, by = b.Y, cx = c.X, cy = c.Y;
            double D = 2 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));
            if (Math.Abs(D) < 1e-10) return null; // 共线
            double ux = ((ax * ax + ay * ay) * (by - cy) + (bx * bx + by * by) * (cy - ay) + (cx * cx + cy * cy) * (ay - by)) / D;
            double uy = ((ax * ax + ay * ay) * (cx - bx) + (bx * bx + by * by) * (ax - cx) + (cx * cx + cy * cy) * (bx - ax)) / D;
            return new Vector3(ux, uy, 0);
        }

        double NormalizeAngle(double a)
        {
            while (a < 0) a += 2 * Math.PI;
            while (a >= 2 * Math.PI) a -= 2 * Math.PI;
            return a;
        }

    }
}
