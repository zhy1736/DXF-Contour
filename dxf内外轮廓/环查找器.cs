using netDxf;
using netDxf.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace dxf内外轮廓
{
    /// <summary>
    /// 使用半边拓扑从无序曲线中识别闭合环。
    /// </summary>
    internal class 环查找器
    {
        public List<曲线> 曲线组 = new List<曲线>();
        public List<List<曲线>> 环组 = new List<List<曲线>>();
        public 环查找器(List<曲线> 曲线组)
        {
            this.曲线组 = 图修正后曲线组(曲线组);
            int 曲线数量 = this.曲线组.Count;

            // 1. 端点聚类为顶点(网格哈希加速)
            var 顶点查找 = new 顶点网格(设置.建图点重合容差);
            var 起点顶点索引 = new int[曲线数量];
            var 终点顶点索引 = new int[曲线数量];
            for (int i = 0; i < 曲线数量; i++)
            {
                起点顶点索引[i] = 顶点查找.查找或加入(this.曲线组[i].起点);
                终点顶点索引[i] = 顶点查找.查找或加入(this.曲线组[i].终点);
            }
            var 顶点组 = 顶点查找.顶点组;

            // 2. 创建半边 (2*i=正向, 2*i+1=反向)
            int 半边数量 = 2 * 曲线数量;
            var 半边起点 = new int[半边数量];
            var 半边终点 = new int[半边数量];
            var 半边角度 = new double[半边数量];
            var 半边有效 = new bool[半边数量];

            for (int i = 0; i < 曲线数量; i++)
            {
                if (起点顶点索引[i] == 终点顶点索引[i]) continue; // 跳过自环

                半边起点[2 * i] = 起点顶点索引[i];
                半边终点[2 * i] = 终点顶点索引[i];
                半边有效[2 * i] = true;

                半边起点[2 * i + 1] = 终点顶点索引[i];
                半边终点[2 * i + 1] = 起点顶点索引[i];
                半边有效[2 * i + 1] = true;
            }

            // 3. 精确切线法计算半边出射角度
            //    对圆弧直接计算切线方向（半径的垂线），保证精确
            for (int h = 0; h < 半边数量; h++)
            {
                if (!半边有效[h]) continue;
                var 顶点 = 顶点组[半边起点[h]];
                var 曲线 = this.曲线组[h / 2];
                bool 正向 = (h % 2 == 0);

                if (Math.Abs(曲线.凸度) > 设置.几何零容差)
                {
                    var (圆心, _, _, _) = 曲线.获取圆弧几何();
                    double 半径向量X = 顶点.X - 圆心.X, 半径向量Y = 顶点.Y - 圆心.Y;
                    // 半边在此顶点处是否沿CCW方向行进
                    bool 顶点处逆时针 = (正向 && 曲线.凸度 > 0) || (!正向 && 曲线.凸度 < 0);
                    double 切线X, 切线Y;
                    if (顶点处逆时针)
                    { 切线X = -半径向量Y; 切线Y = 半径向量X; }          // 逆时针切线 = 半径逆时针旋转 90°
                    else
                    { 切线X = 半径向量Y; 切线Y = -半径向量X; }           // 顺时针切线 = 半径顺时针旋转 90°
                    半边角度[h] = Math.Atan2(切线Y, 切线X);
                }
                else
                {
                    // 直线：朝向另一端点
                    var 另一端点 = 正向 ? 曲线.终点 : 曲线.起点;
                    半边角度[h] = Math.Atan2(另一端点.Y - 顶点.Y, 另一端点.X - 顶点.X);
                }

                半边角度[h] = 归一化排序角(半边角度[h]);
            }

            // 相切时用"顶点沿半边方向走一段长度 L 的点"相对顶点的方向角打破平局。
            // 对直线它退化为原切线角(不变);对圆弧因弯曲方向不同,相同 L 下偏离方向可区分。
            double 计算偏移方向角(int h, double L)
            {
                var 顶点 = 顶点组[半边起点[h]];
                var 曲线 = this.曲线组[h / 2];
                bool 正向 = (h % 2 == 0);
                double 长度 = 曲线.长度;
                double 实际L = Math.Min(L, 长度 * 0.5);
                if (实际L < 设置.几何零容差) 实际L = 长度 * 0.5;

                double 方向X, 方向Y;
                if (Math.Abs(曲线.凸度) > 设置.几何零容差)
                {
                    var (圆心, 半径, _, _) = 曲线.获取圆弧几何();
                    double θV = Math.Atan2(顶点.Y - 圆心.Y, 顶点.X - 圆心.X);
                    bool 顶点处逆时针 = (正向 && 曲线.凸度 > 0) || (!正向 && 曲线.凸度 < 0);
                    double Δθ = 实际L / 半径;
                    if (!顶点处逆时针) Δθ = -Δθ;
                    double θP = θV + Δθ;
                    double PX = 圆心.X + 半径 * Math.Cos(θP);
                    double PY = 圆心.Y + 半径 * Math.Sin(θP);
                    方向X = PX - 顶点.X; 方向Y = PY - 顶点.Y;
                }
                else
                {
                    var 另一端 = 正向 ? 曲线.终点 : 曲线.起点;
                    方向X = 另一端.X - 顶点.X; 方向Y = 另一端.Y - 顶点.Y;
                }
                return 归一化排序角(Math.Atan2(方向Y, 方向X));
            }

            // 按起始顶点分组，按角度排序
            var 顶点出边 = new Dictionary<int, List<int>>();
            for (int h = 0; h < 半边数量; h++)
            {
                if (!半边有效[h]) continue;
                if (!顶点出边.TryGetValue(半边起点[h], out var 列表))
                {
                    列表 = new List<int>();
                    顶点出边[半边起点[h]] = 列表;
                }
                列表.Add(h);
            }
            foreach (var 列表 in 顶点出边.Values)
                列表.Sort((a, b) =>
                {
                    double 角度差 = 半边角度[a] - 半边角度[b];
                    if (Math.Abs(角度差) > 设置.角度容差)
                        return 角度差 > 0 ? 1 : -1;
                    // 切线角相同(相切):改用偏移点向量比较。L = 两段长度较小者的一半,
                    // 保证两者都不会越过各自终点,且足够大以摆脱浮点噪声。
                    var 曲a = this.曲线组[a / 2];
                    var 曲b = this.曲线组[b / 2];
                    double L = Math.Min(曲a.长度, 曲b.长度) * 0.5;
                    double 偏a = 计算偏移方向角(a, L);
                    double 偏b = 计算偏移方向角(b, L);
                    double 偏差 = 偏a - 偏b;
                    if (Math.Abs(偏差) > 设置.角度容差)
                        return 偏差 > 0 ? 1 : -1;
                    return 0;
                });

            // 4. 为每条半边预计算"候选下一条"有序列表
            //    到达顶点 v 后,默认选择是出边列表中对边的前一条(最紧右转,半边拓扑惯例)。
            //    若该选择会与当前环路径中已用半边冲突,则回退尝试下一候选(角度更偏左的方向),
            //    以此类推,直到找到不冲突的候选或耗尽全部候选触发更上层回退。
            //    列表顺序: 索引-1, 索引-2, ..., 索引+1 (环形,跳过对边本身)
            var 候选表 = new List<int>?[半边数量];
            for (int h = 0; h < 半边数量; h++)
            {
                if (!半边有效[h]) continue;
                int 对边 = h ^ 1;
                int 顶点 = 半边终点[h];
                if (!顶点出边.TryGetValue(顶点, out var 出边列表)) continue;
                int 索引 = 出边列表.IndexOf(对边);
                if (索引 < 0) continue;
                int 出边数 = 出边列表.Count;
                var 候选 = new List<int>(Math.Max(0, 出边数 - 1));
                for (int k = 1; k < 出边数; k++)
                    候选.Add(出边列表[(索引 - k + 出边数) % 出边数]);
                候选表[h] = 候选;
            }

            // 5. 追踪全部环(带回退)
            //    路径中遇到已使用(当前环内)的半边必为错误,弹回上一步改选下一候选。
            var 已使用 = new bool[半边数量];

            for (int 起始半边 = 0; 起始半边 < 半边数量; 起始半边++)
            {
                if (!半边有效[起始半边] || 候选表[起始半边] == null || 已使用[起始半边]) continue;

                var 路径 = new List<int> { 起始半边 };
                var 位置栈 = new List<int> { 0 };
                var 在路径中 = new HashSet<int> { 起始半边 };

                int 迭代上限 = Math.Max(1024, 半边数量 * 8);
                int 迭代计数 = 0;
                bool 成功 = false;

                while (路径.Count > 0)
                {
                    if (++迭代计数 > 迭代上限)
                    {
                        Console.WriteLine($"环追踪迭代超限(起始半边={起始半边}),放弃此起点");
                        break;
                    }

                    int 当前 = 路径[路径.Count - 1];
                    var 候选 = 候选表[当前];
                    bool 推进了 = false;

                    if (候选 != null)
                    {
                        int pos = 位置栈[位置栈.Count - 1];
                        while (pos < 候选.Count)
                        {
                            int 下一 = 候选[pos];
                            pos++;
                            if (已使用[下一]) continue;   // 跨环:属于已成环的半边,换下一角度
                            if (下一 == 起始半边)         // 闭合到起点,成环
                            {
                                位置栈[位置栈.Count - 1] = pos;
                                成功 = true;
                                break;
                            }
                            if (在路径中.Contains(下一)) continue; // 当前环内重复,换下一角度
                            // 接受此候选,下探一层
                            位置栈[位置栈.Count - 1] = pos;
                            路径.Add(下一);
                            在路径中.Add(下一);
                            位置栈.Add(0);
                            推进了 = true;
                            break;
                        }
                        if (成功) break;
                    }

                    if (!推进了)
                    {
                        // 本层候选耗尽,回退到上一层让其改选下一候选
                        int 弹出 = 路径[路径.Count - 1];
                        路径.RemoveAt(路径.Count - 1);
                        位置栈.RemoveAt(位置栈.Count - 1);
                        在路径中.Remove(弹出);
                    }
                }

                if (!成功 || 路径.Count < 2) continue;

                var 面曲线组 = new List<曲线>(路径.Count);
                foreach (int h in 路径)
                {
                    已使用[h] = true;
                    var 原曲线 = this.曲线组[h / 2];
                    var 克隆曲线 = 原曲线.按端点克隆(
                        顶点组[半边起点[h]], 顶点组[半边终点[h]]);
                    面曲线组.Add(克隆曲线);
                }
                环组.Add(面曲线组);
            }
        }

        List<曲线> 图修正后曲线组(List<曲线> 输入曲线组)
        {
            var 顶点组 = new List<Vector3>();
            var 边组 = 构建图边组(输入曲线组, 顶点组);

            过滤重叠边(边组);

            过滤无环连通分量(边组, 顶点组.Count);

            int 悬挂上限 = Math.Max(16, 边组.Count * 2);
            int 悬挂迭代 = 0;
            while (剥离悬挂边(边组, 顶点组.Count))
            {
                if (++悬挂迭代 > 悬挂上限) { Console.WriteLine("剥离悬挂边迭代超限,提前退出"); break; }
            }

            int 压缩上限 = Math.Max(16, 顶点组.Count * 4);
            int 压缩迭代 = 0;
            while (压缩二度点(边组, 顶点组))
            {
                if (++压缩迭代 > 压缩上限) { Console.WriteLine("压缩二度点迭代超限,提前退出"); break; }
                悬挂迭代 = 0;
                while (剥离悬挂边(边组, 顶点组.Count))
                {
                    if (++悬挂迭代 > 悬挂上限) { Console.WriteLine("剥离悬挂边迭代超限,提前退出"); break; }
                }
            }

            return 提取有效曲线组(边组);
        }

        double 归一化排序角(double 角度)
        {
            while (角度 < 0)
                角度 += 2 * Math.PI;
            while (角度 >= 2 * Math.PI)
                角度 -= 2 * Math.PI;
            return 角度;
        }

        List<图边> 构建图边组(List<曲线> 输入曲线组, List<Vector3> 顶点组)
        {
            var 边组 = new List<图边>();
            var 顶点查找 = new 顶点网格(设置.建图点重合容差);
            foreach (var 曲线 in 输入曲线组)
            {
                int 起点索引 = 顶点查找.查找或加入(曲线.起点);
                int 终点索引 = 顶点查找.查找或加入(曲线.终点);
                if (起点索引 == 终点索引)
                    continue;

                边组.Add(new 图边(起点索引, 终点索引, 曲线));
            }
            顶点组.Clear();
            顶点组.AddRange(顶点查找.顶点组);
            return 边组;
        }

        void 过滤无环连通分量(List<图边> 边组, int 顶点数量)
        {
            var 顶点到边 = 构建顶点到边(边组, 顶点数量);
            var 已访问顶点 = new bool[顶点数量];
            var 边已计数 = new bool[边组.Count];

            for (int 起点 = 0; 起点 < 顶点数量; 起点++)
            {
                if (已访问顶点[起点] || 顶点到边[起点].Count == 0)
                    continue;

                var 待访问顶点 = new Queue<int>();
                var 分量顶点 = new List<int>();
                var 分量边 = new List<int>();
                待访问顶点.Enqueue(起点);
                已访问顶点[起点] = true;

                while (待访问顶点.Count > 0)
                {
                    int 当前顶点 = 待访问顶点.Dequeue();
                    分量顶点.Add(当前顶点);

                    foreach (int 边索引 in 顶点到边[当前顶点])
                    {
                        if (!边组[边索引].有效)
                            continue;

                        if (!边已计数[边索引])
                        {
                            边已计数[边索引] = true;
                            分量边.Add(边索引);
                        }

                        int 相邻顶点 = 边组[边索引].起点索引 == 当前顶点 ? 边组[边索引].终点索引 : 边组[边索引].起点索引;
                        if (已访问顶点[相邻顶点])
                            continue;

                        已访问顶点[相邻顶点] = true;
                        待访问顶点.Enqueue(相邻顶点);
                    }
                }

                if (分量边.Count < 分量顶点.Count)
                {
                    foreach (int 边索引 in 分量边)
                        边组[边索引].有效 = false;
                }
            }
        }

        bool 剥离悬挂边(List<图边> 边组, int 顶点数量)
        {
            var 度数 = 统计顶点度数(边组, 顶点数量);
            bool 已修改 = false;

            for (int i = 0; i < 边组.Count; i++)
            {
                if (!边组[i].有效)
                    continue;

                if (度数[边组[i].起点索引] <= 1 || 度数[边组[i].终点索引] <= 1)
                {
                    边组[i].有效 = false;
                    已修改 = true;
                }
            }

            return 已修改;
        }

        bool 压缩二度点(List<图边> 边组, List<Vector3> 顶点组)
        {
            var 顶点到边 = 构建顶点到边(边组, 顶点组.Count);

            for (int 顶点索引 = 0; 顶点索引 < 顶点到边.Count; 顶点索引++)
            {
                if (顶点到边[顶点索引].Count != 2)
                    continue;

                int 第一条边索引 = 顶点到边[顶点索引][0];
                int 第二条边索引 = 顶点到边[顶点索引][1];
                var 第一条边 = 边组[第一条边索引];
                var 第二条边 = 边组[第二条边索引];
                if (!第一条边.有效 || !第二条边.有效)
                    continue;

                int 第一另一端 = 第一条边.起点索引 == 顶点索引 ? 第一条边.终点索引 : 第一条边.起点索引;
                int 第二另一端 = 第二条边.起点索引 == 顶点索引 ? 第二条边.终点索引 : 第二条边.起点索引;
                if (第一另一端 == 第二另一端)
                    continue;

                if (!尝试合并二度点(第一条边.曲线, 第二条边.曲线, 顶点组[顶点索引], 顶点组[第一另一端], 顶点组[第二另一端], out var 合并后曲线))
                    continue;

                第一条边.有效 = false;
                第二条边.有效 = false;
                边组.Add(new 图边(第一另一端, 第二另一端, 合并后曲线!));
                return true;
            }

            return false;
        }

        bool 尝试合并二度点(曲线 第一段, 曲线 第二段, Vector3 公共顶点, Vector3 第一另一端, Vector3 第二另一端, out 曲线? 合并后曲线)
        {
            合并后曲线 = null;

            bool 第一段是直线 = Math.Abs(第一段.凸度) < 设置.几何零容差;
            bool 第二段是直线 = Math.Abs(第二段.凸度) < 设置.几何零容差;
            if (!第一段是直线 || !第二段是直线)
                return false;

            double 第一向量X = 第一另一端.X - 公共顶点.X;
            double 第一向量Y = 第一另一端.Y - 公共顶点.Y;
            double 第二向量X = 第二另一端.X - 公共顶点.X;
            double 第二向量Y = 第二另一端.Y - 公共顶点.Y;
            double 第一长度 = Math.Sqrt(第一向量X * 第一向量X + 第一向量Y * 第一向量Y);
            double 第二长度 = Math.Sqrt(第二向量X * 第二向量X + 第二向量Y * 第二向量Y);
            if (第一长度 < 设置.几何零容差 || 第二长度 < 设置.几何零容差)
                return false;

            double 叉积 = 第一向量X * 第二向量Y - 第一向量Y * 第二向量X;
            double 归一化叉积 = Math.Abs(叉积) / (第一长度 * 第二长度);
            double 点积 = (第一向量X * 第二向量X + 第一向量Y * 第二向量Y) / (第一长度 * 第二长度);
            if (归一化叉积 > 设置.二度点合并叉积容差 || 点积 > 设置.二度点反向点积阈值)
                return false;

            合并后曲线 = new 曲线(第一另一端, 第二另一端);
            return true;
        }

        List<List<int>> 构建顶点到边(List<图边> 边组, int 顶点数量)
        {
            var 顶点到边 = new List<List<int>>(顶点数量);
            for (int i = 0; i < 顶点数量; i++)
                顶点到边.Add(new List<int>());

            for (int i = 0; i < 边组.Count; i++)
            {
                if (!边组[i].有效)
                    continue;

                顶点到边[边组[i].起点索引].Add(i);
                顶点到边[边组[i].终点索引].Add(i);
            }

            return 顶点到边;
        }

        int[] 统计顶点度数(List<图边> 边组, int 顶点数量)
        {
            var 度数 = new int[顶点数量];
            foreach (var 边 in 边组)
            {
                if (!边.有效)
                    continue;

                度数[边.起点索引]++;
                度数[边.终点索引]++;
            }
            return 度数;
        }

        List<曲线> 提取有效曲线组(List<图边> 边组)
        {
            var 结果 = new List<曲线>();
            foreach (var 边 in 边组)
            {
                if (边.有效)
                    结果.Add(边.曲线);
            }
            return 结果;
        }

        /// <summary>
        /// 图建立后按拓扑过滤重叠边:相同(起点索引,终点索引)对且同类几何(直线/同向同心弧)仅保留一条。
        /// </summary>
        void 过滤重叠边(List<图边> 边组)
        {
            // 按无序顶点对分组
            var 分组 = new Dictionary<(int, int), List<int>>();
            for (int i = 0; i < 边组.Count; i++)
            {
                if (!边组[i].有效) continue;
                int 小 = Math.Min(边组[i].起点索引, 边组[i].终点索引);
                int 大 = Math.Max(边组[i].起点索引, 边组[i].终点索引);
                if (!分组.TryGetValue((小, 大), out var 列表))
                {
                    列表 = new List<int>();
                    分组[(小, 大)] = 列表;
                }
                列表.Add(i);
            }

            double 容差 = 设置.过滤重叠容差;
            foreach (var 列表 in 分组.Values)
            {
                if (列表.Count < 2) continue;
                for (int a = 0; a < 列表.Count; a++)
                {
                    int ia = 列表[a];
                    if (!边组[ia].有效) continue;
                    for (int b = a + 1; b < 列表.Count; b++)
                    {
                        int ib = 列表[b];
                        if (!边组[ib].有效) continue;
                        if (曲线重合(边组[ia].曲线, 边组[ib].曲线, 容差))
                            边组[ib].有效 = false;
                    }
                }
            }
        }

        static bool 曲线重合(曲线 a, 曲线 b, double 容差)
        {
            bool a是直线 = Math.Abs(a.凸度) < 设置.几何零容差;
            bool b是直线 = Math.Abs(b.凸度) < 设置.几何零容差;
            if (a是直线 != b是直线) return false;
            if (a是直线) return true; // 同顶点对+都是直线 => 重合
            // 圆弧:比较弧中点
            return Vector3.Distance(弧中点(a), 弧中点(b)) < 容差;
        }

        static Vector3 弧中点(曲线 曲线)
        {
            var (圆心, 半径, 起始角度, 终止角度) = 曲线.获取圆弧几何();
            double 中间弧度 = (起始角度 + 终止角度) / 2.0 * Math.PI / 180.0;
            return new Vector3(圆心.X + 半径 * Math.Cos(中间弧度), 圆心.Y + 半径 * Math.Sin(中间弧度), 0);
        }



        sealed class 图边
        {
            public int 起点索引;
            public int 终点索引;
            public 曲线 曲线;
            public bool 有效 = true;

            public 图边(int 起点索引, int 终点索引, 曲线 曲线)
            {
                this.起点索引 = 起点索引;
                this.终点索引 = 终点索引;
                this.曲线 = 曲线;
            }
        }



    }
}
