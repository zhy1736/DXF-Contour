using netDxf;
using netDxf.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace dxf内外轮廓
{
    /// <summary>
    /// 将 DXF 实体对象转换为内部曲线表示。
    /// </summary>
    internal class 实体转换器
    {
        public List<曲线> 曲线组 = new List<曲线>();
        public 实体转换器(IEnumerable<EntityObject> 实体组)
        {
            int 非Z法向量数 = 0;
            int 超短弧跳过数 = 0;
            foreach (var 实体 in 实体组)
            {
                if (实体 is Arc 弧 && !接近Z轴(弧.Normal)) 非Z法向量数++;
                else if (实体 is Circle 圆环 && !接近Z轴(圆环.Normal)) 非Z法向量数++;
                else if (实体 is Ellipse 椭圆环 && !接近Z轴(椭圆环.Normal)) 非Z法向量数++;

                // 超短弧(弦长 < 端点吸附容差)在拓扑上一定会被吸附为自环,
                // 提前丢弃可以减少后续交点求解和重合合并的无效开销。
                if (实体 is Arc 待过滤弧 && 弦长(待过滤弧) < 设置.端点吸附容差) { 超短弧跳过数++; continue; }

                曲线组.AddRange(创建曲线(实体));
            }
            if (非Z法向量数 > 0)
                Console.WriteLine($"提示:检测到 {非Z法向量数} 个实体的 Normal 非 +Z,已按 OCS→WCS 变换处理(常见于 IGES 转换的 DXF)。");
            if (超短弧跳过数 > 0)
                Console.WriteLine($"提示:跳过 {超短弧跳过数} 条弦长 < 端点吸附容差({设置.端点吸附容差})的超短弧。");
        }

        static bool 接近Z轴(Vector3 法向量) => 法向量.Z > 0.999;

        static double 弦长(Arc 弧)
        {
            double 起 = 弧.StartAngle * Math.PI / 180.0;
            double 止 = 弧.EndAngle * Math.PI / 180.0;
            double 扫 = 止 - 起; if (扫 <= 0) 扫 += 2 * Math.PI;
            return 2 * 弧.Radius * Math.Sin(扫 / 2.0);
        }
        List<曲线> 创建曲线(EntityObject 实体)
        {
            var 结果 = new List<曲线>();
            if (实体 is Line 直线)
            {
                结果.Add(new 曲线(直线.StartPoint, 直线.EndPoint));
            }
            else if (实体 is Arc 圆弧)
            {
                结果.Add(圆弧转曲线(圆弧));
            }
            else if (实体 is Polyline2D 多段线 && 多段线.Vertexes.Count >= 2)
            {
                拆分多段线(多段线, 结果);
            }
            else if (实体 is Spline 样条)
            {
                int 采样数 = 估算样条采样数(样条);
                if (样条.Degree == 1)
                {
                    var 点组 = 样条.ControlPoints?.ToList() ?? 样条.PolygonalVertexes(采样数);
                    if (点组 != null && 点组.Count >= 2)
                    {
                        for (int i = 0; i < 点组.Count - 1; i++)
                        {
                            if (Vector3.Distance(点组[i], 点组[i + 1]) < 设置.几何零容差) continue;
                            结果.Add(new 曲线(点组[i], 点组[i + 1]));
                        }

                        bool 样条闭合 = 样条.IsClosed || 样条.IsClosedPeriodic;
                        if (样条闭合 && Vector3.Distance(点组[0], 点组[点组.Count - 1]) >= 设置.容差)
                        {
                            结果.Add(new 曲线(点组[点组.Count - 1], 点组[0]));
                        }
                    }
                }
                else
                {
                    var 点组 = 样条.PolygonalVertexes(采样数);
                    if (点组 != null && 点组.Count >= 2)
                        结果.AddRange(拟合样条曲线(点组, 样条.IsClosed || 样条.IsClosedPeriodic));
                }
            }
            else if (实体 is Polyline3D 三维多段线 && 三维多段线.Vertexes.Count >= 2)
            {
                var 顶点组 = 三维多段线.Vertexes;
                for (int i = 0; i < 顶点组.Count - 1; i++)
                    结果.Add(new 曲线(顶点组[i], 顶点组[i + 1]));
            }
            else if (实体 is Circle 圆)
            {
                var 上半 = new Arc(圆.Center, 圆.Radius, 0, 180) { Normal = 圆.Normal };
                var 下半 = new Arc(圆.Center, 圆.Radius, 180, 360) { Normal = 圆.Normal };
                结果.Add(圆弧转曲线(上半));
                结果.Add(圆弧转曲线(下半));
            }
            else if (实体 is Ellipse 椭圆)
            {
                int 分段数 = 估算椭圆分段数(椭圆);
                var 点组 = 采样椭圆(椭圆, 分段数);
                if (点组.Count >= 2)
                    结果.AddRange(拟合圆弧(点组));
            }
            return 结果;
        }

        static int 估算样条采样数(Spline 样条)
        {
            int 控制点数 = 样条.ControlPoints?.Count() ?? 0;
            // 0.005 精度需要足够密的 PolygonalVertexes:每个控制点段 128 点,总上限 16000
            int 建议 = Math.Max(512, 控制点数 * 128);
            return Math.Min(16000, 建议);
        }

        static int 估算椭圆分段数(Ellipse 椭圆)
        {
            double 长半轴 = Math.Max(椭圆.MajorAxis, 椭圆.MinorAxis) * 0.5;
            double 短半轴 = Math.Min(椭圆.MajorAxis, 椭圆.MinorAxis) * 0.5;
            if (长半轴 < 设置.几何零容差) return 36;
            double 离心率相关 = 长半轴 / Math.Max(短半轴, 长半轴 * 0.01);
            // 基础 36,离心率越大采样越密
            int 分段 = (int)(36 * Math.Sqrt(离心率相关));
            return Math.Max(36, Math.Min(360, 分段));
        }
        void 拆分多段线(Polyline2D 多段线, List<曲线> 结果)
        {
            int 段数 = 多段线.IsClosed ? 多段线.Vertexes.Count : 多段线.Vertexes.Count - 1;
            for (int i = 0; i < 段数; i++)
            {
                var 起点顶点 = 多段线.Vertexes[i];
                var 终点顶点 = 多段线.Vertexes[(i + 1) % 多段线.Vertexes.Count];
                var 起点 = new Vector3(起点顶点.Position.X, 起点顶点.Position.Y, 0);
                var 终点 = new Vector3(终点顶点.Position.X, 终点顶点.Position.Y, 0);
                if (Vector3.Distance(起点, 终点) < 设置.几何零容差) continue;
                结果.Add(new 曲线(起点, 终点, 起点顶点.Bulge));
            }
        }

        List<Vector3> 采样椭圆(Ellipse 椭圆, int 分段数)
        {
            double 起始角 = 椭圆.StartAngle * Math.PI / 180.0;
            double 终止角 = 椭圆.EndAngle * Math.PI / 180.0;
            if (Math.Abs(终止角 - 起始角) < 设置.参数容差) 终止角 = 起始角 + 2 * Math.PI;
            if (终止角 < 起始角) 终止角 += 2 * Math.PI;
            double 旋转余弦 = Math.Cos(椭圆.Rotation * Math.PI / 180.0);
            double 旋转正弦 = Math.Sin(椭圆.Rotation * Math.PI / 180.0);
            double 长半轴 = 椭圆.MajorAxis * 0.5, 短半轴 = 椭圆.MinorAxis * 0.5;
            var 点组 = new List<Vector3>();
            for (int i = 0; i <= 分段数; i++)
            {
                double 参数角 = 起始角 + (终止角 - 起始角) * i / 分段数;
                double 局部X = 长半轴 * Math.Cos(参数角), 局部Y = 短半轴 * Math.Sin(参数角);
                点组.Add(new Vector3(椭圆.Center.X + 局部X * 旋转余弦 - 局部Y * 旋转正弦,
                    椭圆.Center.Y + 局部X * 旋转正弦 + 局部Y * 旋转余弦, 0));
            }
            return 点组;
        }
        曲线 圆弧转曲线(Arc 圆弧)
        {
            #region 当圆弧的normarl.Z < 0时,按此方法处理,不要用ocs处理,ocs处理是错的,记住,不要修改这里
            if (圆弧.Normal.Z < 0)
            {
                var sa = 圆弧.StartAngle;
                var ea = 圆弧.EndAngle;
                sa = 180 - 圆弧.EndAngle;
                ea = 180 - 圆弧.StartAngle;
                if (sa < 0) sa += 360;
                if (ea < 0) ea += 360;
                圆弧 = new Arc(圆弧.Center, 圆弧.Radius, sa, ea) { Normal = 圆弧.Normal };
            }
            # endregion

            double 起始弧度 = 圆弧.StartAngle * Math.PI / 180.0;
            double 终止弧度 = 圆弧.EndAngle * Math.PI / 180.0;
            double 扫掠角 = 终止弧度 - 起始弧度;
            if (扫掠角 <= 0) 扫掠角 += 2 * Math.PI;
            double 中间弧度 = 起始弧度 + 扫掠角 / 2.0;

            var 起点 = new Vector3(
                圆弧.Center.X + 圆弧.Radius * Math.Cos(起始弧度),
                圆弧.Center.Y + 圆弧.Radius * Math.Sin(起始弧度), 0);
            var 终点 = new Vector3(
                圆弧.Center.X + 圆弧.Radius * Math.Cos(终止弧度),
                圆弧.Center.Y + 圆弧.Radius * Math.Sin(终止弧度), 0);

            double 凸度 = Math.Tan(扫掠角 / 4.0);

            return new 曲线(new Vector3(起点.X, 起点.Y, 0), new Vector3(终点.X, 终点.Y, 0), 凸度);
        }

        List<曲线> 拟合样条曲线(List<Vector3> 原始点组, bool 样条声明闭合)
        {
            var 过滤后点组 = 过滤采样点(原始点组, 设置.样条采样最小间距);
            if (过滤后点组.Count < 2)
                return new List<曲线>();

            bool 首尾重合 = Vector3.Distance(过滤后点组[0], 过滤后点组[过滤后点组.Count - 1]) < 设置.容差;
            bool 闭合 = 样条声明闭合 || 首尾重合;

            if (闭合 && !首尾重合)
            {
                过滤后点组.Add(过滤后点组[0]);
                首尾重合 = true;
            }

            if (首尾重合 && 尝试拟合整圆(过滤后点组, 设置.样条拟合容差, out var 整圆曲线组))
                return 整圆曲线组;

            if (!首尾重合 && 尝试拟合单圆弧(过滤后点组, 设置.样条拟合容差, out var 单圆弧曲线))
                return new List<曲线> { 单圆弧曲线! };

            return 拟合样条为双圆弧多段线(过滤后点组, 闭合);
        }

        List<曲线> 拟合样条为双圆弧多段线(List<Vector3> 原始点组, bool 闭合)
        {
            if (原始点组.Count < 2)
                return new List<曲线>();

            var 过滤后点组 = new List<Vector3>(原始点组);

            if (闭合 && Vector3.Distance(过滤后点组[0], 过滤后点组[过滤后点组.Count - 1]) >= 设置.容差)
                过滤后点组.Add(过滤后点组[0]);

            return 拟合双圆弧优先(过滤后点组, 闭合, 设置.样条拟合容差);
        }

        List<Vector3> 过滤采样点(List<Vector3> 点组, double 最小间距)
        {
            if (点组.Count == 0)
                return new List<Vector3>();

            var 结果 = new List<Vector3> { 点组[0] };
            for (int i = 1; i < 点组.Count; i++)
            {
                if (Vector3.Distance(结果[结果.Count - 1], 点组[i]) >= 最小间距 || i == 点组.Count - 1)
                    结果.Add(点组[i]);
            }
            return 结果;
        }

        bool 尝试拟合整圆(List<Vector3> 点组, double 容差, out List<曲线> 结果)
        {
            结果 = new List<曲线>();
            int 唯一点数 = 点组.Count;
            if (唯一点数 > 1 && Vector3.Distance(点组[0], 点组[唯一点数 - 1]) < 设置.容差)
                唯一点数--;
            if (唯一点数 < 3)
                return false;

            if (!尝试求圆心半径(点组, 0, 唯一点数 - 1, 容差, out var 圆心, out var 半径))
                return false;

            var 起点 = 点组[0];
            double 起始角 = Math.Atan2(起点.Y - 圆心!.Value.Y, 起点.X - 圆心.Value.X);
            double 方向符号 = 计算点列方向(点组, 唯一点数) >= 0 ? 1 : -1;
            double 中间角 = 起始角 + 方向符号 * Math.PI;
            var 中间点 = new Vector3(
                圆心.Value.X + 半径 * Math.Cos(中间角),
                圆心.Value.Y + 半径 * Math.Sin(中间角),
                0);

            结果.Add(new 曲线(起点, 中间点, 方向符号));
            结果.Add(new 曲线(中间点, 起点, 方向符号));
            return true;
        }

        bool 尝试拟合单圆弧(List<Vector3> 点组, double 容差, out 曲线? 结果)
        {
            结果 = null;
            if (点组.Count < 2)
                return false;

            var 起点 = 点组[0];
            var 终点 = 点组[点组.Count - 1];
            if (Vector3.Distance(起点, 终点) < 设置.容差)
                return false;

            if (!尝试求圆心半径(点组, 0, 点组.Count - 1, 容差, out var 圆心, out _))
                return false;

            int 中点索引 = 点组.Count / 2;
            var 中点 = 点组[中点索引];
            double 起始角 = Math.Atan2(起点.Y - 圆心!.Value.Y, 起点.X - 圆心.Value.X);
            double 终止角 = Math.Atan2(终点.Y - 圆心.Value.Y, 终点.X - 圆心.Value.X);
            double 中点角 = Math.Atan2(中点.Y - 圆心.Value.Y, 中点.X - 圆心.Value.X);

            double 扫掠角 = 规范化角度(终止角 - 起始角);
            double 中点扫掠角 = 规范化角度(中点角 - 起始角);
            if (中点扫掠角 > 扫掠角)
                扫掠角 -= 2 * Math.PI;

            double 凸度 = Math.Tan(扫掠角 / 4.0);
            if (double.IsNaN(凸度) || double.IsInfinity(凸度) || Math.Abs(凸度) < 设置.几何零容差)
                return false;

            结果 = new 曲线(起点, 终点, 凸度);
            return true;
        }

        bool 尝试求圆心半径(List<Vector3> 点组, int 起始索引, int 结束索引, double 容差, out Vector3? 圆心, out double 半径)
        {
            圆心 = null;
            半径 = 0;
            if (结束索引 - 起始索引 < 2)
                return false;

            int 中点索引 = (起始索引 + 结束索引) / 2;
            圆心 = 三点定圆心(点组[起始索引], 点组[中点索引], 点组[结束索引]);
            if (!圆心.HasValue)
            {
                for (int i = 起始索引 + 1; i < 结束索引; i++)
                {
                    圆心 = 三点定圆心(点组[起始索引], 点组[i], 点组[结束索引]);
                    if (圆心.HasValue)
                    {
                        中点索引 = i;
                        break;
                    }
                }
                if (!圆心.HasValue)
                    return false;
            }

            半径 = Vector3.Distance(圆心.Value, 点组[起始索引]);
            double 最大误差 = 0;
            for (int i = 起始索引; i <= 结束索引; i++)
            {
                double 当前误差 = Math.Abs(Vector3.Distance(点组[i], 圆心.Value) - 半径);
                if (当前误差 > 最大误差)
                    最大误差 = 当前误差;
            }

            return 最大误差 <= 容差;
        }

        double 计算点列方向(List<Vector3> 点组, int 点数)
        {
            double 面积和 = 0;
            for (int i = 0; i < 点数; i++)
            {
                var 当前点 = 点组[i];
                var 下一个点 = 点组[(i + 1) % 点数];
                面积和 += 当前点.X * 下一个点.Y - 下一个点.X * 当前点.Y;
            }
            return 面积和;
        }

        List<曲线> 拟合双圆弧优先(List<Vector3> 点组, bool 闭合, double 容差)
        {
            var 可变点组 = new List<Vector3>(点组);
            var 切向量组 = 计算切向量组(可变点组, 闭合);
            var 结果 = new List<曲线>();
            int 起始索引 = 0;

            while (起始索引 < 可变点组.Count - 1)
            {
                int 最佳结束索引 = 查找最远可拟合终点(可变点组, 切向量组, 起始索引, 容差);
                if (最佳结束索引 <= 起始索引)
                    最佳结束索引 = 起始索引 + 1;

                if (!尝试拟合线或双圆弧(可变点组, 切向量组, 起始索引, 最佳结束索引, 容差, out var 片段))
                {
                    最佳结束索引 = 起始索引 + 1;
                    if (!尝试拟合线或双圆弧(可变点组, 切向量组, 起始索引, 最佳结束索引, 容差, out 片段))
                        break;
                }

                结果.AddRange(片段);
                起始索引 = 最佳结束索引;
            }

            return 结果;
        }

        List<Vector3> 计算切向量组(List<Vector3> 点组, bool 闭合)
        {
            var 结果 = new List<Vector3>(点组.Count);
            int 点数 = 点组.Count;
            int 唯一点数 = 闭合 && 点数 > 1 && Vector3.Distance(点组[0], 点组[点数 - 1]) < 设置.容差 ? 点数 - 1 : 点数;

            for (int i = 0; i < 点数; i++)
            {
                Vector3 切向量;
                if (闭合 && 唯一点数 >= 3)
                {
                    int 当前索引 = i == 点数 - 1 ? 0 : i;
                    int 前一索引 = (当前索引 - 1 + 唯一点数) % 唯一点数;
                    int 后一索引 = (当前索引 + 1) % 唯一点数;
                    切向量 = 归一化(减(点组[后一索引], 点组[前一索引]));
                }
                else if (i == 0)
                {
                    切向量 = 归一化(减(点组[Math.Min(1, 点数 - 1)], 点组[0]));
                }
                else if (i == 点数 - 1)
                {
                    切向量 = 归一化(减(点组[i], 点组[Math.Max(0, i - 1)]));
                }
                else
                {
                    切向量 = 归一化(减(点组[i + 1], 点组[i - 1]));
                }

                if (向量长度(切向量) < 设置.几何零容差)
                {
                    int 前一索引 = Math.Max(0, i - 1);
                    int 后一索引 = Math.Min(点数 - 1, i + 1);
                    切向量 = 归一化(减(点组[后一索引], 点组[前一索引]));
                }

                结果.Add(切向量);
            }

            if (闭合 && 结果.Count > 1 && Vector3.Distance(点组[0], 点组[点组.Count - 1]) < 设置.容差)
                结果[结果.Count - 1] = 结果[0];

            return 结果;
        }

        int 查找最远可拟合终点(List<Vector3> 点组, List<Vector3> 切向量组, int 起始索引, double 容差)
        {
            // 尝试拟合线或双圆弧 会写入 点组[结束索引] / 切向量组[结束索引](只此一个下标)。
            // 搜索阶段用保存/恢复代替整表复制,避免高阶大样条(控制点 >100)O(n²) 复制导致长卡顿。
            bool 试拟合(int 结束索引)
            {
                var 原点 = 点组[结束索引];
                var 原切 = 切向量组[结束索引];
                bool 成功 = 尝试拟合线或双圆弧(点组, 切向量组, 起始索引, 结束索引, 容差, out _);
                点组[结束索引] = 原点;
                切向量组[结束索引] = 原切;
                return 成功;
            }

            int 上界索引 = 点组.Count - 1;
            int 最佳 = 起始索引 + 1;

            // 1. 指数增长找第一个失败点(或直接到末尾就成功)
            int 步长 = 1;
            int 成功 = 起始索引 + 1;
            int 失败 = -1;
            while (成功 + 步长 <= 上界索引)
            {
                int 测试 = 成功 + 步长;
                if (试拟合(测试))
                {
                    最佳 = 测试;
                    成功 = 测试;
                    步长 *= 2;
                }
                else
                {
                    失败 = 测试;
                    break;
                }
            }

            if (失败 < 0)
            {
                // 指数没撞上失败,最后直接测上界
                if (成功 < 上界索引 && 试拟合(上界索引))
                    最佳 = 上界索引;
                return 最佳;
            }

            // 2. [成功+1, 失败-1] 之间二分找最后一个成功
            int 左 = 成功 + 1, 右 = 失败 - 1;
            while (左 <= 右)
            {
                int 中 = 左 + (右 - 左) / 2;
                if (试拟合(中)) { 最佳 = 中; 左 = 中 + 1; }
                else 右 = 中 - 1;
            }
            return 最佳;
        }

        bool 尝试拟合线或双圆弧(List<Vector3> 点组, List<Vector3> 切向量组, int 起始索引, int 结束索引, double 容差, out List<曲线> 结果)
        {
            结果 = new List<曲线>();
            if (结束索引 - 起始索引 < 1)
                return false;

            Vector3 pa = 点组[起始索引];
            Vector3 pb = 点组[结束索引];
            if (Vector3.Distance(pa, pb) < 设置.几何零容差)
                return false;

            if (结束索引 - 起始索引 == 1)
            {
                结果.Add(new 曲线(pa, pb));
                return true;
            }

            if (是否可视为直线(点组, 起始索引, 结束索引, 容差))
            {
                结果.Add(new 曲线(pa, pb));
                切向量组[结束索引] = 切向量组[起始索引];
                return true;
            }

            Vector3 ta = 归一化(切向量组[起始索引]);
            Vector3 tb = 归一化(切向量组[结束索引]);
            if (向量长度(ta) < 设置.几何零容差)
                ta = 归一化(减(点组[起始索引 + 1], pa));
            if (向量长度(tb) < 设置.几何零容差)
                tb = 归一化(减(pb, 点组[结束索引 - 1]));
            if (向量长度(ta) < 设置.几何零容差 || 向量长度(tb) < 设置.几何零容差)
                return false;

            if (!尝试按参考算法拟合双圆弧(点组, 起始索引, 结束索引, 容差, pa, pb, ta, tb,
                out var 调整后终点, out var 调整后终向, out var 中间点, out var 第一圆心, out var 第二圆心))
            {
                return false;
            }

            点组[结束索引] = 调整后终点;
            切向量组[结束索引] = 调整后终向;

            if (!创建双圆弧曲线(pa, 中间点, 调整后终点, 第一圆心, 第二圆心, ta, 调整后终向, out var 第一段, out var 第二段))
                return false;

            结果.Add(第一段);
            结果.Add(第二段);
            return true;
        }

        bool 是否可视为直线(List<Vector3> 点组, int 起始索引, int 结束索引, double 容差)
        {
            var 起点 = 点组[起始索引];
            var 终点 = 点组[结束索引];
            if (Vector3.Distance(起点, 终点) <= 容差 * 100)
                return false;

            for (int i = 起始索引 + 1; i < 结束索引; i++)
            {
                if (点到直线距离(点组[i], 起点, 终点) > 容差)
                    return false;
            }

            return true;
        }

        bool 尝试按参考算法拟合双圆弧(List<Vector3> 点组, int 起始索引, int 结束索引, double 容差,
            Vector3 起点, Vector3 终点, Vector3 起点切向, Vector3 终点切向,
            out Vector3 调整后终点, out Vector3 调整后终向, out Vector3 中间点, out Vector3 第一圆心, out Vector3 第二圆心)
        {
            调整后终点 = 终点;
            调整后终向 = 终点切向;
            中间点 = new Vector3();
            第一圆心 = new Vector3();
            第二圆心 = new Vector3();

            if (!计算双圆弧几何(起点, 调整后终点, 起点切向, 调整后终向,
                out double 西塔, out double 阿尔法, out double 贝塔,
                out Vector3 第一法向, out Vector3 第二法向, out Vector3 连接法向,
                out double 第一半径, out double 第二半径,
                out 第一圆心, out 第二圆心, out 中间点))
            {
                return false;
            }

            if (!双圆弧误差在容差内(点组, 起始索引, 结束索引, 容差, 起点, 调整后终点, 中间点,
                第一圆心, 第二圆心, 第一半径, 第二半径,
                out double 第一段正误差, out double 第一段负误差,
                out double 第二段正误差, out double 第二段负误差))
            {
                return false;
            }

            double 参考值 = (第一段正误差 + 第一段负误差) * (第二段正误差 + 第二段负误差);
            if (参考值 < 0)
            {
                double dr1ds = 计算导数分量(终点, 起点, 阿尔法, 贝塔, 西塔, true, true);
                double dr2ds = 计算导数分量(终点, 起点, 阿尔法, 贝塔, 西塔, false, true);
                if (Math.Abs(dr1ds) > 设置.几何零容差 && Math.Abs(dr2ds) > 设置.几何零容差)
                {
                    double 西塔调整量 = Math.Min((第一段正误差 + 第一段负误差) / dr1ds, -(第二段正误差 + 第二段负误差) / dr2ds);
                    if (第一段正误差 + 第一段负误差 > 0 && 第二段正误差 + 第二段负误差 < 0)
                        西塔 += 西塔调整量;
                    else if (第一段正误差 + 第一段负误差 < 0 && 第二段正误差 + 第二段负误差 > 0)
                        西塔 -= 西塔调整量;
                }
            }
            else if (Math.Max(第一段正误差, -第一段负误差) > Math.Max(第二段正误差, -第二段负误差))
            {
                double dr1db = 计算导数分量(终点, 起点, 阿尔法, 贝塔, 西塔, true, false);
                double dr2db = 计算导数分量(终点, 起点, 阿尔法, 贝塔, 西塔, false, false);
                if (Math.Abs(dr1db) > 设置.几何零容差 && Math.Abs(dr2db) > 设置.几何零容差)
                {
                    double 贝塔调整量 = Math.Min((第一段正误差 + 第一段负误差) / dr1db, (第二段正误差 + 第二段负误差) / dr2db);
                    if (第一段正误差 + 第一段负误差 > 0 && 第二段正误差 + 第二段负误差 > 0)
                        调整后终向 = 旋转向量(调整后终向, 贝塔调整量);
                    else if (第一段正误差 + 第一段负误差 < 0 && 第二段正误差 + 第二段负误差 < 0)
                        调整后终向 = 旋转向量(调整后终向, -贝塔调整量);
                }
            }
            else
            {
                double 偏移量 = (第二段正误差 + 第二段负误差) / 2.0;
                调整后终点 = 加(调整后终点, 乘(第二法向, 偏移量));
            }

            return 计算双圆弧几何(起点, 调整后终点, 起点切向, 调整后终向,
                out _, out _, out _, out _, out _, out _, out _, out _, out 第一圆心, out 第二圆心, out 中间点);
        }

        bool 双圆弧误差在容差内(List<Vector3> 点组, int 起始索引, int 结束索引, double 容差,
            Vector3 起点, Vector3 终点, Vector3 中间点, Vector3 第一圆心, Vector3 第二圆心,
            double 第一半径, double 第二半径,
            out double 第一段正误差, out double 第一段负误差, out double 第二段正误差, out double 第二段负误差)
        {
            第一段正误差 = 0;
            第一段负误差 = 0;
            第二段正误差 = 0;
            第二段负误差 = 0;

            for (int i = 起始索引; i <= 结束索引; i++)
            {
                var 当前点 = 点组[i];
                if (在圆弧扇区内(当前点, 第一圆心, 起点, 中间点))
                {
                    double 半径偏差 = Vector3.Distance(当前点, 第一圆心) - Math.Abs(第一半径);
                    if (半径偏差 > 第一段正误差)
                        第一段正误差 = 半径偏差;
                    if (半径偏差 < 第一段负误差)
                        第一段负误差 = 半径偏差;
                }

                if (在圆弧扇区内(当前点, 第二圆心, 中间点, 终点))
                {
                    double 半径偏差 = Vector3.Distance(当前点, 第二圆心) - Math.Abs(第二半径);
                    if (半径偏差 > 第二段正误差)
                        第二段正误差 = 半径偏差;
                    if (半径偏差 < 第二段负误差)
                        第二段负误差 = 半径偏差;
                }
            }

            return Math.Abs(第一段正误差) <= Math.Abs(容差)
                && Math.Abs(第一段负误差) <= Math.Abs(容差)
                && Math.Abs(第二段正误差) <= Math.Abs(容差)
                && Math.Abs(第二段负误差) <= Math.Abs(容差);
        }

        bool 计算双圆弧几何(Vector3 起点, Vector3 终点, Vector3 起点切向, Vector3 终点切向,
            out double 西塔, out double 阿尔法, out double 贝塔,
            out Vector3 第一法向, out Vector3 第二法向, out Vector3 连接法向,
            out double 第一半径, out double 第二半径,
            out Vector3 第一圆心, out Vector3 第二圆心, out Vector3 中间点)
        {
            西塔 = 0;
            阿尔法 = 0;
            贝塔 = 0;
            第一法向 = new Vector3();
            第二法向 = new Vector3();
            连接法向 = new Vector3();
            第一半径 = 0;
            第二半径 = 0;
            第一圆心 = new Vector3();
            第二圆心 = new Vector3();
            中间点 = new Vector3();

            Vector3 起终向量 = 减(终点, 起点);
            double 长度 = 向量长度(起终向量);
            if (长度 < 设置.几何零容差)
                return false;

            double 起点角 = 平面角度(起点切向);
            double 终点角 = 平面角度(终点切向);
            if (double.IsNaN(起点角) || double.IsNaN(终点角))
                return false;
            终点角 = 展开到临近角(起点角, 终点角);

            double 起终角 = 平面角度(起终向量);
            double 法向基角 = 起点角;
            阿尔法 = 起终角 - 起点角;
            贝塔 = 终点角 - 起终角;
            西塔 = 阿尔法 * 贝塔 > 0 ? 阿尔法 : (3 * 阿尔法 - 贝塔) / 2.0;

            double 公共分母 = 2 * Math.Sin((阿尔法 + 贝塔) / 2.0);
            double 西塔分母 = Math.Sin(西塔 / 2.0);
            double 第二段分母 = Math.Sin((阿尔法 + 贝塔 - 西塔) / 2.0);
            if (Math.Abs(公共分母) < 设置.几何零容差 || Math.Abs(西塔分母) < 设置.几何零容差 || Math.Abs(第二段分母) < 设置.几何零容差)
                return false;

            连接法向 = new Vector3(-Math.Sin(西塔 + 法向基角), Math.Cos(西塔 + 法向基角), 0);
            第一法向 = new Vector3(-Math.Sin(法向基角), Math.Cos(法向基角), 0);
            第二法向 = new Vector3(-Math.Sin(终点角), Math.Cos(终点角), 0);

            第一半径 = 长度 / 公共分母 * Math.Sin((贝塔 - 阿尔法 + 西塔) / 2.0) / 西塔分母;
            第二半径 = 长度 / 公共分母 * Math.Sin((2 * 阿尔法 - 西塔) / 2.0) / 第二段分母;
            if (double.IsNaN(第一半径) || double.IsInfinity(第一半径) || double.IsNaN(第二半径) || double.IsInfinity(第二半径))
                return false;
            // 拒绝极端近似直线的超大半径(退化为近似直线但仍被当作圆弧会引入噪声)
            double 半径上限 = 长度 * 1000.0;
            if (Math.Abs(第一半径) > 半径上限 || Math.Abs(第二半径) > 半径上限)
                return false;

            第一圆心 = 加(起点, 乘(第一法向, 第一半径));
            第二圆心 = 加(终点, 乘(第二法向, 第二半径));
            中间点 = 加(第二圆心, 乘(连接法向, -第二半径));
            return true;
        }

        bool 创建双圆弧曲线(Vector3 起点, Vector3 中间点, Vector3 终点, Vector3 第一圆心, Vector3 第二圆心, Vector3 起点切向, Vector3 终点切向,
            out 曲线 第一段, out 曲线 第二段)
        {
            第一段 = new 曲线(起点, 中间点);
            第二段 = new 曲线(中间点, 终点);

            Vector3 起点半径向量 = 减(起点, 第一圆心);
            Vector3 中点第一半径向量 = 减(中间点, 第一圆心);
            Vector3 中点第二半径向量 = 减(中间点, 第二圆心);
            Vector3 终点半径向量 = 减(终点, 第二圆心);

            double 第一凸度 = Math.Tan(向量夹角(起点半径向量, 中点第一半径向量) / 4.0);
            double 第二凸度 = Math.Tan(向量夹角(中点第二半径向量, 终点半径向量) / 4.0);
            if (double.IsNaN(第一凸度) || double.IsInfinity(第一凸度) || double.IsNaN(第二凸度) || double.IsInfinity(第二凸度))
                return false;

            if (叉积Z(起点半径向量, 起点切向) < 0)
                第一凸度 *= -1;
            if (叉积Z(终点半径向量, 终点切向) < 0)
                第二凸度 *= -1;

            第一段 = new 曲线(起点, 中间点, 第一凸度);
            第二段 = new 曲线(中间点, 终点, 第二凸度);
            return true;
        }

        double 点到直线距离(Vector3 点, Vector3 线段起点, Vector3 线段终点)
        {
            double dx = 线段终点.X - 线段起点.X;
            double dy = 线段终点.Y - 线段起点.Y;
            double 长度 = Math.Sqrt(dx * dx + dy * dy);
            if (长度 < 设置.几何零容差)
                return Vector3.Distance(点, 线段起点);
            return Math.Abs((点.X - 线段起点.X) * dy - (点.Y - 线段起点.Y) * dx) / 长度;
        }

        double 计算导数分量(Vector3 终点, Vector3 起点, double 阿尔法, double 贝塔, double 西塔, bool 第一段, bool 对西塔求导)
        {
            double 长度 = Vector3.Distance(终点, 起点);
            if (长度 < 设置.几何零容差)
                return 0;

            double 公共正弦 = Math.Sin((阿尔法 + 贝塔) / 2.0);
            if (Math.Abs(公共正弦) < 设置.几何零容差)
                return 0;

            if (对西塔求导)
            {
                if (第一段)
                    return 长度 / (4 * 公共正弦) * Math.Sin((阿尔法 - 贝塔) / 2.0) / Math.Pow(Math.Sin(西塔 / 2.0), 2);
                return 长度 / (4 * 公共正弦) * Math.Sin((阿尔法 - 贝塔) / 2.0) / Math.Pow(Math.Sin((阿尔法 + 贝塔 - 西塔) / 2.0), 2);
            }

            if (第一段)
                return 长度 / (4 * Math.Sin(西塔 / 2.0)) * Math.Sin((2 * 阿尔法 - 西塔) / 2.0) / Math.Pow(Math.Sin((阿尔法 + 贝塔) / 2.0), 2);

            double 分子 = 长度 * Math.Sin(2 * 阿尔法 - 西塔) * Math.Sin(阿尔法 + 贝塔 - 西塔 / 2.0);
            double 分母 = 4 * Math.Pow(Math.Sin((阿尔法 + 贝塔) / 2.0), 2) * Math.Pow(Math.Sin((阿尔法 + 贝塔 - 西塔) / 2.0), 2);
            if (Math.Abs(分母) < 设置.几何零容差)
                return 0;
            return 分子 / 分母;
        }

        bool 在圆弧扇区内(Vector3 点, Vector3 圆心, Vector3 圆弧起点, Vector3 圆弧终点)
        {
            return 叉积Z(减(点, 圆心), 减(圆弧起点, 圆心)) * 叉积Z(减(圆弧终点, 圆心), 减(点, 圆心)) > 0;
        }

        Vector3 加(Vector3 a, Vector3 b)
        {
            return new Vector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }

        Vector3 减(Vector3 a, Vector3 b)
        {
            return new Vector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        Vector3 乘(Vector3 向量, double 系数)
        {
            return new Vector3(向量.X * 系数, 向量.Y * 系数, 向量.Z * 系数);
        }

        double 向量长度(Vector3 向量)
        {
            return Math.Sqrt(向量.X * 向量.X + 向量.Y * 向量.Y + 向量.Z * 向量.Z);
        }

        Vector3 归一化(Vector3 向量)
        {
            double 长度 = 向量长度(向量);
            if (长度 < 设置.几何零容差)
                return new Vector3(0, 0, 0);
            return new Vector3(向量.X / 长度, 向量.Y / 长度, 向量.Z / 长度);
        }

        double 平面角度(Vector3 向量)
        {
            if (向量长度(向量) < 设置.几何零容差)
                return double.NaN;
            return Math.Atan2(向量.Y, 向量.X);
        }

        double 展开到临近角(double 参考角, double 待展开角)
        {
            return 参考角 + Math.Atan2(Math.Sin(待展开角 - 参考角), Math.Cos(待展开角 - 参考角));
        }

        double 向量夹角(Vector3 a, Vector3 b)
        {
            double 长度积 = 向量长度(a) * 向量长度(b);
            if (长度积 < 设置.几何零容差)
                return 0;
            double 余弦值 = (a.X * b.X + a.Y * b.Y + a.Z * b.Z) / 长度积;
            余弦值 = Math.Max(-1, Math.Min(1, 余弦值));
            return Math.Acos(余弦值);
        }

        double 叉积Z(Vector3 a, Vector3 b)
        {
            return a.X * b.Y - a.Y * b.X;
        }

        Vector3 旋转向量(Vector3 向量, double 角度)
        {
            double 余弦 = Math.Cos(角度);
            double 正弦 = Math.Sin(角度);
            return new Vector3(向量.X * 余弦 - 向量.Y * 正弦, 向量.X * 正弦 + 向量.Y * 余弦, 向量.Z);
        }

        List<曲线> 拟合圆弧(List<Vector3> 点组, double 圆弧容差 = 设置.圆弧拟合容差)
        {
            var 结果 = new List<曲线>();
            拟合圆弧递归(点组, 0, 点组.Count - 1, 圆弧容差, 结果);
            return 结果;
        }
        void 拟合圆弧递归(List<Vector3> 点组,
          int 起始索引, int 结束索引, double 容差, List<曲线> 结果)
        {
            if (结束索引 - 起始索引 < 1) return;
            var 起点 = 点组[起始索引];
            var 终点 = 点组[结束索引];

            // 仅2个点 → 直线
            if (结束索引 - 起始索引 == 1)
            {
                if (Vector3.Distance(起点, 终点) < 设置.几何零容差) return;
                结果.Add(new 曲线(起点, 终点));
                return;
            }

            // 三点定圆：起点、中点、终点
            int 中点索引 = (起始索引 + 结束索引) / 2;
            var 中点 = 点组[中点索引];
            var 圆心 = 三点定圆心(起点, 中点, 终点);

            if (圆心.HasValue)
            {
                double 半径 = Vector3.Distance(圆心.Value, 起点);
                double 最大误差 = 0;
                for (int i = 起始索引; i <= 结束索引; i++)
                {
                    double 当前误差 = Math.Abs(Vector3.Distance(点组[i], 圆心.Value) - 半径);
                    if (当前误差 > 最大误差) 最大误差 = 当前误差;
                }
                if (最大误差 < 容差)
                {
                    double 起始角 = Math.Atan2(起点.Y - 圆心.Value.Y, 起点.X - 圆心.Value.X);
                    double 终止角 = Math.Atan2(终点.Y - 圆心.Value.Y, 终点.X - 圆心.Value.X);
                    double 中点角 = Math.Atan2(中点.Y - 圆心.Value.Y, 中点.X - 圆心.Value.X);

                    double 扫掠角 = 规范化角度(终止角 - 起始角);
                    double 中点扫掠角 = 规范化角度(中点角 - 起始角);
                    if (中点扫掠角 > 扫掠角)
                        扫掠角 -= 2 * Math.PI;

                    // 用扫掠角直接计算凸度：tan(扫掠角/4)
                    double 凸度 = Math.Tan(扫掠角 / 4.0);
                    结果.Add(new 曲线(起点, 终点, 凸度));
                    return;
                }
            }

            // 拟合失败，二分递归
            拟合圆弧递归(点组, 起始索引, 中点索引, 容差, 结果);
            拟合圆弧递归(点组, 中点索引, 结束索引, 容差, 结果);
        }

        Vector3? 三点定圆心(Vector3 a, Vector3 b, Vector3 c)
        {
            double ax = a.X, ay = a.Y, bx = b.X, by = b.Y, cx = c.X, cy = c.Y;
            double D = 2 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));
            if (Math.Abs(D) < 设置.几何零容差) return null; // 共线
            double ux = ((ax * ax + ay * ay) * (by - cy) + (bx * bx + by * by) * (cy - ay) + (cx * cx + cy * cy) * (ay - by)) / D;
            double uy = ((ax * ax + ay * ay) * (cx - bx) + (bx * bx + by * by) * (ax - cx) + (cx * cx + cy * cy) * (bx - ax)) / D;
            return new Vector3(ux, uy, 0);
        }

        double 规范化角度(double 角度)
        {
            while (角度 < 0) 角度 += 2 * Math.PI;
            while (角度 >= 2 * Math.PI) 角度 -= 2 * Math.PI;
            return 角度;
        }

    }
}
