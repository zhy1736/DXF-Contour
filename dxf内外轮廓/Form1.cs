namespace dxf内外轮廓;

public partial class Form1 : Form
{
    public Form1()
    {
        InitializeComponent();
    }

    private void button1_Click(object sender, EventArgs e)
    {
        richTextBox1.Text = string.Empty;
        var files = DXFIO.SelectFiles();
        if (files.Length == 0) return;

        string outDir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(files[0])!, "轮廓");
        System.IO.Directory.CreateDirectory(outDir);

        foreach (var file in files)
        {
            string name = System.IO.Path.GetFileName(file);
            richTextBox1.Text += $"读取文件: {name}" + Environment.NewLine;

            var Curves = DXFIO.Read(file);
            ContourResult ext = new ContourResult(Curves);
            string outPath = System.IO.Path.Combine(outDir, name);
            DXFIO.Write(outPath, file, ext.InnerContour, ext.OuterContour);

            richTextBox1.Text += $" " + ext.AreaDescriptions + Environment.NewLine;

            richTextBox1.Text += $"读取到曲线: {ext.OriginalEntityCount};" +
                $"修正后的曲线数量: {ext.ProcessedCurveCount};" +
                $"计算出{ext.DetectedLoopCount} 个环;" +
                $"生成轮廓成功!" + Environment.NewLine;
        }
        richTextBox1.Text += $"完成，共处理 {files.Length} 个文件" + Environment.NewLine;
    }
}
