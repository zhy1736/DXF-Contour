using netDxf.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace dxf内外轮廓
{
    /// <summary>
    /// 保存最终处理统计和生成出的轮廓。
    /// </summary>
    internal class 轮廓结果
    {
        public int 原始实体数量;
        public int 处理后曲线数量;
        public int 检出环数量;
        public Polyline2D? 内轮廓;
        public Polyline2D? 外轮廓;
        public List<曲线> 修正后曲线组 = new List<曲线>();
        public string 面积描述 = "";
        public 轮廓结果(IEnumerable<EntityObject> 实体组)
        {
            原始实体数量 = 实体组.Count();
            实体转换器 转换器 = new 实体转换器(实体组);
            几何修正器 修正器 = new 几何修正器(转换器.曲线组);
            处理后曲线数量 = 修正器.曲线组.Count;
            修正后曲线组 = 修正器.曲线组;
            环查找器 环查找 = new 环查找器(修正器.曲线组);
            检出环数量 = 环查找.环组.Count;
            轮廓分析器 分析器 = new 轮廓分析器(环查找.环组);
            内轮廓 = 分析器.内轮廓;
            外轮廓 = 分析器.外轮廓;
            面积描述 = 分析器.面积描述;
        }
    }
}
