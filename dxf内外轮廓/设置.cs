using System;
using System.Collections.Generic;
using System.Text;

namespace dxf内外轮廓
{
    /// <summary>
    /// 应用范围的全局常量与配置。
    /// </summary>
    internal static class 设置
    {
        // 基础几何判断
        public const double 几何零容差 = 1e-10;
        public const double 角度容差 = 1e-9;
        public const double 参数容差 = 1e-6;
        public const double 共线判断容差 = 1e-6;

        // 拓扑与图处理
        public const double 二度点合并叉积容差 = 1e-3;
        public const double 二度点反向点积阈值 = -0.999;
        public const double 容差 = 0.05; // 通用几何容差(样条首尾闭合、点去重等非关键判断)
        public const double 建图点重合容差 = 0.015;
        public const double 端点吸附容差 = 0.01;
        public const double 独立线连接容差 = 0.01;

        // 拟合与采样
        public const double 样条采样最小间距 = 0.02;
        public const double 样条拟合容差 = 0.05;
        public const double 圆弧拟合容差 = 0.005;       // 椭圆/整圆拟合的最大偏差
        public const double 圆弧起点匹配容差 = 0.005;

        // 曲线打断与修正
        public const double 打断端点避让容差 = 0.015;   // 打断点到原端点的最小距离,必须 ≥ 端点吸附容差
        public const double 最小可打断曲线长度 = 0.04;  // ≥ 2 × 打断端点避让容差;支持短弧(半径 ~1,角度 2° 级别的弦长 ~0.04)
        public const double 过滤重叠容差 = 0.01;       // 与端点吸附对齐,避免吃掉短线
        public const double 合并共圆容差 = 0.08;       // 合并重叠阶段比较两弧圆心/半径的容差(略大于样条拟合容差,容纳样条双圆弧拟合的圆心漂移)
        public const double 包含吸收点距容差 = 0.08;   // 采样点被视为"落在另一曲线上"的距离阈值
        public const double 是内部点容差 = 0.01;       // 判断交点是否在曲线内部使用的容差
        public const double 弧上点半径容差 = 0.1;      // 判断点是否在弧面上时 |距离-半径| 的阈值
        public const double 打断网格尺寸 = 5.0;        // 交点求解空间索引的网格边长(图纸单位)
        public const int 打断网格最大单边格数 = 200;   // 单条曲线的包围盒跨越格数上限,超过则进入"大包围盒"兜底桶

        // 轮廓筛选
        public const double 最小环面积 = 0.01;
    }
}
