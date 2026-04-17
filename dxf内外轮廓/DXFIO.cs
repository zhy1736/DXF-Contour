using System;
using System.Collections.Generic;
using netDxf;
using netDxf.Entities;

namespace dxf内外轮廓
{
    internal class DXFIO
    {
        public static string[] SelectFiles()
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "DXF 文件 (*.dxf)|*.dxf|所有文件 (*.*)|*.*";
            ofd.Multiselect = true;
            if (ofd.ShowDialog() == DialogResult.OK)
                return ofd.FileNames;
            return Array.Empty<string>();
        }

        public static IEnumerable<EntityObject> Read(string filePath)
        {
            var curves = new List<EntityObject>();
            DxfDocument dxf = DxfDocument.Load(filePath);
            if (dxf != null)
            {
                curves.AddRange(dxf.Entities.Lines);
                curves.AddRange(dxf.Entities.Arcs);
                curves.AddRange(dxf.Entities.Circles);
                curves.AddRange(dxf.Entities.Ellipses);
                curves.AddRange(dxf.Entities.Polylines2D);
                curves.AddRange(dxf.Entities.Polylines3D);
                curves.AddRange(dxf.Entities.Splines);
            }
            return curves;
        }

        public static void Write(string filePath, string sourceFile, Polyline2D? InnerContour, Polyline2D? OuterContour)
        {
            var dxf = DxfDocument.Load(sourceFile);
            if (dxf == null) dxf = new DxfDocument();

            // 原有线改为白色
            foreach (var e in dxf.Entities.Lines) e.Color = AciColor.Default;
            foreach (var e in dxf.Entities.Arcs) e.Color = AciColor.Default;
            foreach (var e in dxf.Entities.Circles) e.Color = AciColor.Default;
            foreach (var e in dxf.Entities.Ellipses) e.Color = AciColor.Default;
            foreach (var e in dxf.Entities.Polylines2D) e.Color = AciColor.Default;
            foreach (var e in dxf.Entities.Polylines3D) e.Color = AciColor.Default;
            foreach (var e in dxf.Entities.Splines) e.Color = AciColor.Default;

            // 添加轮廓：正面积（OuterContour）黄色，负面积（内孔）红色
            if (InnerContour != null)
            {
                InnerContour.Color = AciColor.Yellow;
                dxf.Entities.Add(InnerContour);
            }
            if (OuterContour != null)
            {
                OuterContour.Color = AciColor.Red;
                dxf.Entities.Add(OuterContour);
            }
            dxf.Save(filePath);
        }
    }
}
