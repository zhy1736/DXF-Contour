using netDxf;
using System;
using System.Collections.Generic;
using System.Text;

namespace dxf内外轮廓
{
    internal class 曲线
    {
        public readonly Vector3 起点, 终点;
        public readonly double 凸度; // 0=直线, 非0=圆弧凸度

        public 曲线(Vector3 起点, Vector3 终点, double 凸度 = 0)
        {
            this.起点 = 起点; this.终点 = 终点; this.凸度 = 凸度;
        }

        public double 长度
        {
            get
            {
                double 弦长 = Vector3.Distance(起点, 终点);
                if (Math.Abs(凸度) < 设置.几何零容差)
                    return 弦长;
                if (弦长 < 设置.几何零容差)
                    return 0; // 退化:起终点重合的伪圆弧
                double 扫掠角 = 4.0 * Math.Atan(Math.Abs(凸度));
                double 半径 = 弦长 / (2.0 * Math.Sin(扫掠角 / 2.0));
                return 半径 * 扫掠角;
            }
        }

        /// <summary>
        /// 从Bulge反算圆弧的圆心、半径、起始角、终止角（CCW），仅Bulge≠0时有效
        /// </summary>
        public (Vector3 center, double radius, double startDeg, double endDeg) 获取圆弧几何()
        {
            double 横向差 = 终点.X - 起点.X, 纵向差 = 终点.Y - 起点.Y;
            double 弦长 = Math.Sqrt(横向差 * 横向差 + 纵向差 * 纵向差);
            if (弦长 < 设置.几何零容差 || Math.Abs(凸度) < 设置.几何零容差)
                return (起点, 0, 0, 0); // 退化几何,调用方应避免依赖返回值
            double 弓高 = Math.Abs(凸度) * 弦长 / 2;
            double 半径 = (弦长 * 弦长 / 4 + 弓高 * 弓高) / (2 * 弓高);
            double 中点X = (起点.X + 终点.X) / 2, 中点Y = (起点.Y + 终点.Y) / 2;
            double 法线X = -纵向差 / 弦长, 法线Y = 横向差 / 弦长;
            double 偏移距离 = 半径 - 弓高;
            double 方向符号 = 凸度 > 0 ? 1 : -1;
            double 圆心X = 中点X + 方向符号 * 偏移距离 * 法线X, 圆心Y = 中点Y + 方向符号 * 偏移距离 * 法线Y;
            var 圆心 = new Vector3(圆心X, 圆心Y, 0);
            double 起始角 = Math.Atan2(起点.Y - 圆心Y, 起点.X - 圆心X) * 180 / Math.PI;
            double 终止角 = Math.Atan2(终点.Y - 圆心Y, 终点.X - 圆心X) * 180 / Math.PI;
            if (起始角 < 0) 起始角 += 360;
            if (终止角 < 0) 终止角 += 360;
            // CCW: endAng > startAng
            if (凸度 > 0 && 终止角 < 起始角) 终止角 += 360;
            if (凸度 < 0) { if (起始角 < 终止角) 起始角 += 360; double 临时角 = 起始角; 起始角 = 终止角; 终止角 = 临时角; }
            return (圆心, 半径, 起始角, 终止角);
        }

        public 曲线 按端点克隆(Vector3 起点, Vector3 终点)
        {
            double 新凸度 = 凸度;
            // 端点互换时凸度取反(使用端点吸附容差判断,避免误判)
            if (Math.Abs(凸度) > 设置.几何零容差 &&
                Vector3.Distance(起点, this.终点) < 设置.端点吸附容差 &&
                Vector3.Distance(终点, this.起点) < 设置.端点吸附容差)
                新凸度 = -凸度;
            return new 曲线(起点, 终点, 新凸度);
        }

        /// <summary>返回沿StartPoint→EndPoint方向的采样点序列（不含EndPoint）</summary>
        public List<Vector3> 获取有序点列()
        {
            var 结果 = new List<Vector3>();
            if (Math.Abs(凸度) < 设置.几何零容差)
            {
                结果.Add(起点);
            }
            else
            {
                var (圆心, 半径, 起始角度, 终止角度) = 获取圆弧几何();
                // 判断StartPoint是在arc起点还是终点
                double 圆弧起点弧度 = 起始角度 * Math.PI / 180.0;
                var 圆弧起点 = new Vector3(圆心.X + 半径 * Math.Cos(圆弧起点弧度),
                    圆心.Y + 半径 * Math.Sin(圆弧起点弧度), 0);
                bool 正向 = Vector3.Distance(起点, 圆弧起点) < 设置.圆弧起点匹配容差;
                int 分段数 = Math.Max(8, (int)((终止角度 - 起始角度) / 5));
                分段数 = Math.Min(分段数, 180); // 上限防止极端扫掠角时过采样
                if (正向)
                    for (int i = 0; i < 分段数; i++)
                    {
                        double 当前角度 = (起始角度 + (终止角度 - 起始角度) * i / 分段数) * Math.PI / 180.0;
                        结果.Add(new Vector3(圆心.X + 半径 * Math.Cos(当前角度),
                            圆心.Y + 半径 * Math.Sin(当前角度), 0));
                    }
                else
                    for (int i = 分段数; i > 0; i--)
                    {
                        double 当前角度 = (起始角度 + (终止角度 - 起始角度) * i / 分段数) * Math.PI / 180.0;
                        结果.Add(new Vector3(圆心.X + 半径 * Math.Cos(当前角度),
                            圆心.Y + 半径 * Math.Sin(当前角度), 0));
                    }
            }
            return 结果;
        }
    }
}
