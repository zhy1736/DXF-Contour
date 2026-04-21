using netDxf;
using System;
using System.Collections.Generic;
using System.Text;

namespace dxf内外轮廓
{
    /// <summary>
    /// 通过在交点处打断曲线来修正相交和重叠几何。
    /// </summary>
    internal class 几何修正器
    {
        public List<曲线> 曲线组 = new List<曲线>();
        public List<(Vector3 min, Vector3 max)> 包围盒组 = new List<(Vector3 min, Vector3 max)>();

        public 几何修正器(List<曲线> 曲线组)
        {
            this.曲线组 = 曲线组;
            重算包围盒();
            Console.WriteLine("打断交点...");
            在交点处打断();
            Console.WriteLine("端点吸附...");
            端点吸附();
            Console.WriteLine("过滤重叠...");
            过滤重叠();
        }

        void 重算包围盒()
        {
            包围盒组.Clear();
            foreach (var 曲线 in 曲线组)
                包围盒组.Add(获取包围盒(曲线));
        }

        /// <summary>
        /// 计算所有曲线对的几何交点（线-线、线-弧、弧-弧），在交点处打断曲线。
        /// 两阶段算法:
        ///   阶段 1 用均匀网格空间索引,O(n × 候选数) 收集每条曲线上的所有内部交点;
        ///   阶段 2 对每条曲线按参数排序其交点,一次性顺序切分,避免 Insert/重启循环。
        /// 同时覆盖 T 型和 X 型交叉。
        /// </summary>
        void 在交点处打断()
        {
            int 原始数 = 曲线组.Count;
            if (原始数 == 0) return;

            // 阶段 1: 构建网格并收集每条曲线上的内部交点
            var 格子索引 = new Dictionary<(int, int), List<int>>();
            var 曲线所在格子 = new List<(int, int)>[原始数];
            for (int k = 0; k < 原始数; k++)
                曲线所在格子[k] = 注册到网格(格子索引, k, 包围盒组[k]);

            var 每曲线交点 = new List<Vector3>[原始数];
            for (int i = 0; i < 原始数; i++) 每曲线交点[i] = new List<Vector3>();

            var 候选集 = new HashSet<int>();
            for (int i = 0; i < 原始数; i++)
            {
                if (i % 1000 == 0 && i > 0)
                    Console.WriteLine($"在交点处打断 阶段1: i={i}, 总数={原始数}");

                候选集.Clear();
                foreach (var c in 曲线所在格子[i])
                {
                    if (!格子索引.TryGetValue(c, out var lst)) continue;
                    foreach (var j in lst)
                        if (j > i) 候选集.Add(j);
                }

                foreach (var j in 候选集)
                {
                    if (!包围盒相交(包围盒组[i], 包围盒组[j])) continue;
                    var 交点组 = 求交点(曲线组[i], 曲线组[j]);
                    foreach (var 交点 in 交点组)
                    {
                        if (是内部点(曲线组[i], 交点)) 每曲线交点[i].Add(交点);
                        if (是内部点(曲线组[j], 交点)) 每曲线交点[j].Add(交点);
                    }
                }
            }

            // 阶段 2: 对每条曲线按参数排序并顺序切分
            var 新曲线组 = new List<曲线>(原始数);
            for (int i = 0; i < 原始数; i++)
            {
                var 原曲线 = 曲线组[i];
                if (每曲线交点[i].Count == 0)
                {
                    新曲线组.Add(原曲线);
                    continue;
                }

                var 去重点 = 去重交点(每曲线交点[i]);
                var 排序点 = 按参数排序(原曲线, 去重点);

                var 当前 = 原曲线;
                foreach (var p in 排序点)
                {
                    if (!是内部点(当前, p)) continue; // 可能被前一次切分吃掉
                    var (第一段, 第二段) = 打断曲线(当前, p);
                    if (第一段 != null && 第二段 != null)
                    {
                        新曲线组.Add(第一段);
                        当前 = 第二段;
                    }
                }
                新曲线组.Add(当前);
            }

            曲线组.Clear();
            曲线组.AddRange(新曲线组);
            重算包围盒();
            Console.WriteLine($"在交点处打断完成: {原始数} -> {曲线组.Count}");
        }

        /// <summary>
        /// 将曲线按其包围盒注册到均匀网格,返回所占格子列表。
        /// 包围盒跨度过大的曲线落入单一兜底桶,避免爆格。
        /// </summary>
        List<(int, int)> 注册到网格(Dictionary<(int, int), List<int>> 格子索引, int 索引, (Vector3 min, Vector3 max) 盒)
        {
            var 结果 = new List<(int, int)>();
            double s = 设置.打断网格尺寸;
            int xMin = (int)Math.Floor(盒.min.X / s);
            int xMax = (int)Math.Floor(盒.max.X / s);
            int yMin = (int)Math.Floor(盒.min.Y / s);
            int yMax = (int)Math.Floor(盒.max.Y / s);
            if (xMax - xMin > 设置.打断网格最大单边格数 || yMax - yMin > 设置.打断网格最大单边格数)
            {
                var 兜底 = (int.MinValue, int.MinValue);
                结果.Add(兜底);
                if (!格子索引.TryGetValue(兜底, out var lst)) { lst = new List<int>(); 格子索引[兜底] = lst; }
                lst.Add(索引);
                return 结果;
            }
            for (int y = yMin; y <= yMax; y++)
                for (int x = xMin; x <= xMax; x++)
                {
                    var key = (x, y);
                    结果.Add(key);
                    if (!格子索引.TryGetValue(key, out var lst)) { lst = new List<int>(); 格子索引[key] = lst; }
                    lst.Add(索引);
                }
            return 结果;
        }

        /// <summary>按曲线参数(线段 t / 圆弧扫掠角)从起点到终点升序排列交点。</summary>
        List<Vector3> 按参数排序(曲线 c, List<Vector3> 点组)
        {
            if (Math.Abs(c.凸度) < 设置.几何零容差)
            {
                double dx = c.终点.X - c.起点.X, dy = c.终点.Y - c.起点.Y;
                double 长度平方 = dx * dx + dy * dy;
                if (长度平方 < 设置.几何零容差) return 点组;
                var 结果 = new List<Vector3>(点组);
                结果.Sort((a, b) =>
                {
                    double ta = ((a.X - c.起点.X) * dx + (a.Y - c.起点.Y) * dy) / 长度平方;
                    double tb = ((b.X - c.起点.X) * dx + (b.Y - c.起点.Y) * dy) / 长度平方;
                    return ta.CompareTo(tb);
                });
                return 结果;
            }
            else
            {
                var (圆心, _, _, _) = c.获取圆弧几何();
                double 起始角 = Math.Atan2(c.起点.Y - 圆心.Y, c.起点.X - 圆心.X);
                double 方向符号 = c.凸度 > 0 ? 1 : -1;
                double 扫掠(Vector3 p)
                {
                    double 点角 = Math.Atan2(p.Y - 圆心.Y, p.X - 圆心.X);
                    double s = 方向符号 * (点角 - 起始角);
                    while (s < 0) s += 2 * Math.PI;
                    return s;
                }
                var 结果 = new List<Vector3>(点组);
                结果.Sort((a, b) => 扫掠(a).CompareTo(扫掠(b)));
                return 结果;
            }
        }

        /// <summary>按 是内部点容差 合并相近交点。</summary>
        List<Vector3> 去重交点(List<Vector3> 点组)
        {
            var 结果 = new List<Vector3>();
            foreach (var p in 点组)
            {
                bool 重复 = false;
                foreach (var q in 结果)
                    if (Vector3.Distance(p, q) < 设置.是内部点容差) { 重复 = true; break; }
                if (!重复) 结果.Add(p);
            }
            return 结果;
        }

        (Vector3 min, Vector3 max) 获取包围盒(曲线 曲线)
        {
            if (Math.Abs(曲线.凸度) < 设置.几何零容差)
            {
                double minX = Math.Min(曲线.起点.X, 曲线.终点.X);
                double minY = Math.Min(曲线.起点.Y, 曲线.终点.Y);
                double maxX = Math.Max(曲线.起点.X, 曲线.终点.X);
                double maxY = Math.Max(曲线.起点.Y, 曲线.终点.Y);
                return (new Vector3(minX, minY, 0), new Vector3(maxX, maxY, 0));
            }
            else
            {
                var (圆心, 半径, _, _) = 曲线.获取圆弧几何();
                return (new Vector3(圆心.X - 半径, 圆心.Y - 半径, 0), new Vector3(圆心.X + 半径, 圆心.Y + 半径, 0));
            }
        }

        bool 包围盒相交((Vector3 min, Vector3 max) 包围盒A, (Vector3 min, Vector3 max) 包围盒B)
        {
            double 容差 = 设置.容差;
            if (包围盒A.max.X + 容差 < 包围盒B.min.X || 包围盒A.min.X - 容差 > 包围盒B.max.X) return false;
            if (包围盒A.max.Y + 容差 < 包围盒B.min.Y || 包围盒A.min.Y - 容差 > 包围盒B.max.Y) return false;
            return true;
        }

        List<Vector3> 求交点(曲线 a, 曲线 b)
        {
            var 结果 = new List<Vector3>();
            bool a是直线 = Math.Abs(a.凸度) < 设置.几何零容差;
            bool b是直线 = Math.Abs(b.凸度) < 设置.几何零容差;
            if (a是直线 && b是直线) 求线线交点(a, b, 结果);
            else if (a是直线) 求线弧交点(a, b, 结果);
            else if (b是直线) 求线弧交点(b, a, 结果);
            else 求弧弧交点(a, b, 结果);
            return 结果;
        }

        void 求线线交点(曲线 a, 曲线 b, List<Vector3> 结果)
        {
            double x1 = a.起点.X, y1 = a.起点.Y;
            double x2 = a.终点.X, y2 = a.终点.Y;
            double x3 = b.起点.X, y3 = b.起点.Y;
            double x4 = b.终点.X, y4 = b.终点.Y;
            double 分母 = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
            if (Math.Abs(分母) < 设置.几何零容差) return; // 平行或共线
            double t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / 分母;
            double u = -((x1 - x2) * (y1 - y3) - (y1 - y2) * (x1 - x3)) / 分母;
            if (t >= -设置.参数容差 && t <= 1 + 设置.参数容差 && u >= -设置.参数容差 && u <= 1 + 设置.参数容差)
            {
                t = Math.Max(0, Math.Min(1, t));
                结果.Add(new Vector3(x1 + t * (x2 - x1), y1 + t * (y2 - y1), 0));
            }
        }

        void 求线弧交点(曲线 直线, 曲线 圆弧, List<Vector3> 结果)
        {
            var (圆心, 半径, _, _) = 圆弧.获取圆弧几何();
            double x1 = 直线.起点.X, y1 = 直线.起点.Y;
            double dx = 直线.终点.X - x1, dy = 直线.终点.Y - y1;
            double 线长平方 = dx * dx + dy * dy;
            if (线长平方 < 设置.几何零容差 * 设置.几何零容差) return;
            double fx = x1 - 圆心.X, fy = y1 - 圆心.Y;
            double b = 2 * (fx * dx + fy * dy);
            double c = fx * fx + fy * fy - 半径 * 半径;
            double 判别式 = b * b - 4 * 线长平方 * c;
            // 相切容差:圆心到直线的垂距与半径之差在 弧上点半径容差 内视为相切。
            // 对应判别式容差 ≈ 4 * 线长平方 * (2R * 弧上点半径容差)。
            double 判别式容差 = 8 * 线长平方 * 半径 * 设置.弧上点半径容差;
            if (判别式 < -判别式容差) return;

            if (判别式 <= 判别式容差)
            {
                // 相切:取圆心在直线上的投影作为切点
                double t = -b / (2 * 线长平方);
                if (t < -设置.参数容差 || t > 1 + 设置.参数容差) return;
                t = Math.Max(0, Math.Min(1, t));
                var 切点 = new Vector3(x1 + t * dx, y1 + t * dy, 0);
                if (点在弧上(圆弧, 切点))
                    结果.Add(切点);
                return;
            }

            double 判别式平方根 = Math.Sqrt(判别式);
            for (int 符号 = -1; 符号 <= 1; 符号 += 2)
            {
                double t = (-b + 符号 * 判别式平方根) / (2 * 线长平方);
                if (t < -设置.参数容差 || t > 1 + 设置.参数容差) continue;
                t = Math.Max(0, Math.Min(1, t));
                var 交点 = new Vector3(x1 + t * dx, y1 + t * dy, 0);
                if (点在弧上(圆弧, 交点))
                    结果.Add(交点);
            }
        }

        void 求弧弧交点(曲线 a, 曲线 b, List<Vector3> 结果)
        {
            var (圆心1, 半径1, _, _) = a.获取圆弧几何();
            var (圆心2, 半径2, _, _) = b.获取圆弧几何();
            double dx = 圆心2.X - 圆心1.X, dy = 圆心2.Y - 圆心1.Y;
            double d = Math.Sqrt(dx * dx + dy * dy);
            // 相对容差:半径差异很大或很小时绝对容差会失效
            double 切线相对容差 = Math.Max(半径1, 半径2) * 1e-4 + 设置.几何零容差;
            if (d > 半径1 + 半径2 + 切线相对容差 || d < Math.Abs(半径1 - 半径2) - 切线相对容差 || d < 设置.几何零容差) return;
            double aa = (半径1 * 半径1 - 半径2 * 半径2 + d * d) / (2 * d);
            double h2 = 半径1 * 半径1 - aa * aa;
            if (h2 < 0) h2 = 0;
            double h = Math.Sqrt(h2);
            double mx = 圆心1.X + aa * dx / d, my = 圆心1.Y + aa * dy / d;
            if (h < 设置.几何零容差)
            {
                var 交点 = new Vector3(mx, my, 0);
                if (点在弧上(a, 交点) && 点在弧上(b, 交点))
                    结果.Add(交点);
            }
            else
            {
                var p1 = new Vector3(mx + h * dy / d, my - h * dx / d, 0);
                var p2 = new Vector3(mx - h * dy / d, my + h * dx / d, 0);
                if (点在弧上(a, p1) && 点在弧上(b, p1)) 结果.Add(p1);
                if (点在弧上(a, p2) && 点在弧上(b, p2)) 结果.Add(p2);
            }
        }

        bool 点在弧上(曲线 圆弧, Vector3 点)
        {
            var (圆心, 半径, _, _) = 圆弧.获取圆弧几何();
            if (Math.Abs(Vector3.Distance(点, 圆心) - 半径) > 设置.弧上点半径容差) return false;
            double 起始角 = Math.Atan2(圆弧.起点.Y - 圆心.Y, 圆弧.起点.X - 圆心.X);
            double 点角 = Math.Atan2(点.Y - 圆心.Y, 点.X - 圆心.X);
            double 总扫掠角 = 4.0 * Math.Atan(Math.Abs(圆弧.凸度));
            double 扫掠角;
            if (圆弧.凸度 > 0)
            { 扫掠角 = 点角 - 起始角; while (扫掠角 < -设置.角度容差) 扫掠角 += 2 * Math.PI; }
            else
            { 扫掠角 = 起始角 - 点角; while (扫掠角 < -设置.角度容差) 扫掠角 += 2 * Math.PI; }
            return 扫掠角 <= 总扫掠角 + 设置.参数容差;
        }

        bool 是内部点(曲线 曲线, Vector3 点)
        {
            if (Vector3.Distance(点, 曲线.起点) < 设置.是内部点容差) return false;
            if (Vector3.Distance(点, 曲线.终点) < 设置.是内部点容差) return false;
            if (Math.Abs(曲线.凸度) < 设置.几何零容差)
            {
                var 线段向量 = 曲线.终点 - 曲线.起点;
                double 线段长度 = Math.Sqrt(线段向量.X * 线段向量.X + 线段向量.Y * 线段向量.Y);
                if (线段长度 < 设置.几何零容差) return false;
                var 偏移向量 = 点 - 曲线.起点;
                double 投影长度 = (偏移向量.X * 线段向量.X + 偏移向量.Y * 线段向量.Y) / 线段长度;
                double 垂足X = 偏移向量.X - 投影长度 * 线段向量.X / 线段长度, 垂足Y = 偏移向量.Y - 投影长度 * 线段向量.Y / 线段长度;
                return Math.Sqrt(垂足X * 垂足X + 垂足Y * 垂足Y) < 设置.是内部点容差 && 投影长度 > 设置.是内部点容差 && 投影长度 < 线段长度 - 设置.是内部点容差;
            }
            else
            {
                var (圆心, 半径, _, _) = 曲线.获取圆弧几何();
                if (Math.Abs(Vector3.Distance(点, 圆心) - 半径) > 设置.弧上点半径容差) return false;
                double 起始角 = Math.Atan2(曲线.起点.Y - 圆心.Y, 曲线.起点.X - 圆心.X);
                double 点角 = Math.Atan2(点.Y - 圆心.Y, 点.X - 圆心.X);
                double 总扫掠角 = 4.0 * Math.Atan(Math.Abs(曲线.凸度));
                double 扫掠角;
                if (曲线.凸度 > 0)
                { 扫掠角 = 点角 - 起始角; while (扫掠角 < -设置.角度容差) 扫掠角 += 2 * Math.PI; }
                else
                { 扫掠角 = 起始角 - 点角; while (扫掠角 < -设置.角度容差) 扫掠角 += 2 * Math.PI; }
                return 扫掠角 > 设置.参数容差 && 扫掠角 < 总扫掠角 - 设置.参数容差;
            }
        }

        (曲线?, 曲线?) 打断曲线(曲线 曲线, Vector3 点)
        {
            if (Vector3.Distance(曲线.起点, 点) < 设置.打断端点避让容差 || Vector3.Distance(曲线.终点, 点) < 设置.打断端点避让容差)
                return (null, null);

            if (曲线.长度 < 设置.最小可打断曲线长度) return (null, null);

            if (Math.Abs(曲线.凸度) < 设置.几何零容差)
                return (new 曲线(曲线.起点, 点), new 曲线(点, 曲线.终点));
            var (圆心, _, _, _) = 曲线.获取圆弧几何();
            double 起始角 = Math.Atan2(曲线.起点.Y - 圆心.Y, 曲线.起点.X - 圆心.X);
            double 中点角 = Math.Atan2(点.Y - 圆心.Y, 点.X - 圆心.X);
            double 总扫掠角 = 4.0 * Math.Atan(Math.Abs(曲线.凸度));
            double 第一段扫掠角;
            if (曲线.凸度 > 0)
            { 第一段扫掠角 = 中点角 - 起始角; while (第一段扫掠角 < 0) 第一段扫掠角 += 2 * Math.PI; }
            else
            { 第一段扫掠角 = 起始角 - 中点角; while (第一段扫掠角 < 0) 第一段扫掠角 += 2 * Math.PI; }
            double 第二段扫掠角 = 总扫掠角 - 第一段扫掠角;
            if (第一段扫掠角 < 设置.角度容差 || 第二段扫掠角 < 设置.角度容差) return (null, null);
            double 方向符号 = 曲线.凸度 > 0 ? 1 : -1;
            return (new 曲线(曲线.起点, 点, 方向符号 * Math.Tan(第一段扫掠角 / 4.0)),
                    new 曲线(点, 曲线.终点, 方向符号 * Math.Tan(第二段扫掠角 / 4.0)));
        }

        void 过滤重叠()
        {
            // 只过滤容差内完全重合的曲线（端点一致 + 同类弧中点一致）
            double 容差 = 设置.过滤重叠容差;

            for (int i = 曲线组.Count - 1; i >= 0; i--)
            {
                var 当前曲线 = 曲线组[i];
                var 当前包围盒 = 包围盒组[i];
                for (int j = i - 1; j >= 0; j--)
                {
                    if (!包围盒相交(当前包围盒, 包围盒组[j])) continue;

                    if (是重合(当前曲线, 曲线组[j], 容差))
                    {
                        曲线组.RemoveAt(i);
                        包围盒组.RemoveAt(i);
                        break;
                    }
                }
            }

            重算包围盒();
        }

        void 端点吸附()
        {
            if (曲线组.Count == 0)
                return;

            double 吸附容差 = 设置.端点吸附容差;
            var 网格 = new 顶点网格(吸附容差);
            var 曲线端点索引组 = new (int 起点索引, int 终点索引)[曲线组.Count];

            for (int i = 0; i < 曲线组.Count; i++)
            {
                int 起点索引 = 网格.查找或加入(曲线组[i].起点);
                int 终点索引 = 网格.查找或加入(曲线组[i].终点);
                曲线端点索引组[i] = (起点索引, 终点索引);
            }

            var 顶点组 = 网格.顶点组;

            for (int i = 0; i < 曲线组.Count; i++)
            {
                var 原曲线 = 曲线组[i];
                var 起点 = 顶点组[曲线端点索引组[i].起点索引];
                var 终点 = 顶点组[曲线端点索引组[i].终点索引];
                if (Vector3.Distance(起点, 终点) < 设置.几何零容差)
                    continue;

                曲线组[i] = new 曲线(起点, 终点, 原曲线.凸度);
            }

            for (int i = 曲线组.Count - 1; i >= 0; i--)
            {
                if (Vector3.Distance(曲线组[i].起点, 曲线组[i].终点) < 设置.几何零容差)
                    曲线组.RemoveAt(i);
            }

            重算包围盒();
        }

        bool 是重合(曲线 a, 曲线 b, double 容差)
        {
            bool a是直线 = Math.Abs(a.凸度) < 设置.几何零容差;
            bool b是直线 = Math.Abs(b.凸度) < 设置.几何零容差;
            if (a是直线 != b是直线) return false;

            bool 同向重合 = Vector3.Distance(a.起点, b.起点) < 容差 &&
                        Vector3.Distance(a.终点, b.终点) < 容差;
            bool 反向重合 = Vector3.Distance(a.起点, b.终点) < 容差 &&
                       Vector3.Distance(a.终点, b.起点) < 容差;
            if (!同向重合 && !反向重合) return false;

            if (a是直线 && b是直线) return true;

            // 圆弧：端点一致后，比较中点确保是同一圆弧
            return Vector3.Distance(弧中点(a), 弧中点(b)) < 容差;
        }

        Vector3 弧中点(曲线 曲线)
        {
            if (Math.Abs(曲线.凸度) < 设置.几何零容差)
                return new Vector3((曲线.起点.X + 曲线.终点.X) / 2, (曲线.起点.Y + 曲线.终点.Y) / 2, 0);
            var (圆心, 半径, 起始角度, 终止角度) = 曲线.获取圆弧几何();
            double 中间角度 = (起始角度 + 终止角度) / 2.0;
            double 中间弧度 = 中间角度 * Math.PI / 180.0;
            return new Vector3(圆心.X + 半径 * Math.Cos(中间弧度), 圆心.Y + 半径 * Math.Sin(中间弧度), 0);
        }
    }
}
