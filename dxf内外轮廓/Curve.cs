using netDxf;
using System;
using System.Collections.Generic;
using System.Text;

namespace dxf内外轮廓
{
    internal class Curve
    {
        public Vector3 StartPoint, EndPoint;
        public double Bulge; // 0=直线, 非0=圆弧凸度

        public Curve(Vector3 start, Vector3 end, double bulge = 0)
        {
            StartPoint = start; EndPoint = end; Bulge = bulge;
        }

        public double Length
        {
            get
            {
                if (Math.Abs(Bulge) < 1e-10)
                    return Vector3.Distance(StartPoint, EndPoint);
                double chord = Vector3.Distance(StartPoint, EndPoint);
                double sweep = 4.0 * Math.Atan(Math.Abs(Bulge));
                double radius = chord / (2.0 * Math.Sin(sweep / 2.0));
                return radius * sweep;
            }
        }

        /// <summary>
        /// 从Bulge反算圆弧的圆心、半径、起始角、终止角（CCW），仅Bulge≠0时有效
        /// </summary>
        public (Vector3 center, double radius, double startDeg, double endDeg) ArcGeometry()
        {
            double dx = EndPoint.X - StartPoint.X, dy = EndPoint.Y - StartPoint.Y;
            double chord = Math.Sqrt(dx * dx + dy * dy);
            double sagitta = Math.Abs(Bulge) * chord / 2;
            double radius = (chord * chord / 4 + sagitta * sagitta) / (2 * sagitta);
            double mx = (StartPoint.X + EndPoint.X) / 2, my = (StartPoint.Y + EndPoint.Y) / 2;
            double nx = -dy / chord, ny = dx / chord;
            double dist = radius - sagitta;
            double sign = Bulge > 0 ? 1 : -1;
            double cx = mx + sign * dist * nx, cy = my + sign * dist * ny;
            var center = new Vector3(cx, cy, 0);
            double startAng = Math.Atan2(StartPoint.Y - cy, StartPoint.X - cx) * 180 / Math.PI;
            double endAng = Math.Atan2(EndPoint.Y - cy, EndPoint.X - cx) * 180 / Math.PI;
            if (startAng < 0) startAng += 360;
            if (endAng < 0) endAng += 360;
            // CCW: endAng > startAng
            if (Bulge > 0 && endAng < startAng) endAng += 360;
            if (Bulge < 0) { if (startAng < endAng) startAng += 360; double tmp = startAng; startAng = endAng; endAng = tmp; }
            return (center, radius, startAng, endAng);
        }

        public Curve CloneWithEndpoints(Vector3 start, Vector3 end)
        {
            double bulge = Bulge;
            // 端点互换时凸度取反
            if (Math.Abs(Bulge) > 1e-10 &&
                Vector3.Distance(start, EndPoint) < 1e-4 &&
                Vector3.Distance(end, StartPoint) < 1e-4)
                bulge = -Bulge;
            return new Curve(start, end, bulge);
        }

        /// <summary>返回沿StartPoint→EndPoint方向的采样点序列（不含EndPoint）</summary>
        public List<Vector3> GetOrderedPoints()
        {
            var result = new List<Vector3>();
            if (Math.Abs(Bulge) < 1e-10)
            {
                result.Add(StartPoint);
            }
            else
            {
                var (center, radius, sDeg, eDeg) = ArcGeometry();
                // 判断StartPoint是在arc起点还是终点
                double arcStartRad = sDeg * Math.PI / 180.0;
                var arcSP = new Vector3(center.X + radius * Math.Cos(arcStartRad),
                    center.Y + radius * Math.Sin(arcStartRad), 0);
                bool forward = Vector3.Distance(StartPoint, arcSP) < 0.01;
                int segments = Math.Max(8, (int)((eDeg - sDeg) / 5));
                if (forward)
                    for (int i = 0; i < segments; i++)
                    {
                        double ang = (sDeg + (eDeg - sDeg) * i / segments) * Math.PI / 180.0;
                        result.Add(new Vector3(center.X + radius * Math.Cos(ang),
                            center.Y + radius * Math.Sin(ang), 0));
                    }
                else
                    for (int i = segments; i > 0; i--)
                    {
                        double ang = (sDeg + (eDeg - sDeg) * i / segments) * Math.PI / 180.0;
                        result.Add(new Vector3(center.X + radius * Math.Cos(ang),
                            center.Y + radius * Math.Sin(ang), 0));
                    }
            }
            return result;
        }
    }
}
