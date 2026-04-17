using netDxf.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace dxf内外轮廓
{
    /// <summary>
    /// Holds the final processed output counts and generated contours.
    /// </summary>
    internal class ContourResult
    {
        public int OriginalEntityCount;
        public int ProcessedCurveCount;
        public int DetectedLoopCount;
        public Polyline2D? InnerContour;
        public Polyline2D? OuterContour;
        public string AreaDescriptions = "";
        public ContourResult(IEnumerable<EntityObject> ents)
        {
            OriginalEntityCount = ents.Count();
            EntityConverter zh = new EntityConverter(ents);
            GeometryFixer xiuzheng = new GeometryFixer(zh.Curves);
            ProcessedCurveCount = xiuzheng.Curves.Count;
            LoopFinder huan = new LoopFinder(xiuzheng.Curves);
            DetectedLoopCount = huan.Loops.Count;
            ContourAnalyzer neiwai = new ContourAnalyzer(huan.Loops);
            InnerContour = neiwai.InnerContour;
            OuterContour = neiwai.OuterContour;
            AreaDescriptions = neiwai.AreaDescriptions;
        }
    }
}
