using netDxf;
using System.Collections.Generic;

namespace dxf内外轮廓
{
    /// <summary>
    /// 基于空间网格的顶点近邻查找,将线性扫描 O(n) 降到平均 O(1)。
    /// 网格边长取容差,则只需检查当前格及 8 邻格共 9 格,保证 &lt; 容差 的点一定被发现。
    /// </summary>
    internal sealed class 顶点网格
    {
        readonly double 容差;
        readonly double 格长;
        readonly Dictionary<(int, int), List<int>> 格到索引 = new Dictionary<(int, int), List<int>>();
        public readonly List<Vector3> 顶点组 = new List<Vector3>();

        public 顶点网格(double 容差)
        {
            this.容差 = 容差;
            // 格长略大于容差,保证"距离 < 容差"的两点最多跨越一格
            this.格长 = 容差 > 0 ? 容差 : 1e-6;
        }

        public int 查找或加入(Vector3 点)
        {
            int gx = (int)System.Math.Floor(点.X / 格长);
            int gy = (int)System.Math.Floor(点.Y / 格长);
            double 容差平方 = 容差 * 容差;

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (格到索引.TryGetValue((gx + dx, gy + dy), out var 索引列表))
                    {
                        foreach (int 索引 in 索引列表)
                        {
                            var 候选 = 顶点组[索引];
                            double ex = 候选.X - 点.X, ey = 候选.Y - 点.Y;
                            if (ex * ex + ey * ey < 容差平方)
                                return 索引;
                        }
                    }
                }
            }

            int 新索引 = 顶点组.Count;
            顶点组.Add(点);
            if (!格到索引.TryGetValue((gx, gy), out var 列表))
            {
                列表 = new List<int>();
                格到索引[(gx, gy)] = 列表;
            }
            列表.Add(新索引);
            return 新索引;
        }
    }
}
