using netDxf.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace dxf内外轮廓
{
    internal class Ext
    {
        public int 数据对象数量;
        public int 处理之后的曲线数量;
        public int 找到的环数量;
        public Polyline2D 内轮廓;
        public Polyline2D 外轮廓;
        public string str = "";
        public Ext(IEnumerable<EntityObject> ents)
        {
            数据对象数量 = ents.Count();
            对象转换 zh = new 对象转换(ents);
            图形修正 xiuzheng = new 图形修正(zh.curs);
            处理之后的曲线数量 = xiuzheng.curs.Count;
            所有环 huan = new 所有环(xiuzheng.curs);
            找到的环数量 = huan.loops.Count;
            内外轮廓 neiwai = new 内外轮廓(huan.loops);
            内轮廓 = neiwai.内轮廓;
            外轮廓 = neiwai.外轮廓;
            str = neiwai.str;
        }
    }
}
