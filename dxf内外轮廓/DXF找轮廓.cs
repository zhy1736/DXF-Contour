namespace dxf内外轮廓;

public partial class DXF找轮廓 : Form
{
    public DXF找轮廓()
    {
        InitializeComponent();
    }

    private void 选择文件按钮_Click(object sender, EventArgs e)
    {
        输出框.Text = string.Empty;
        var 文件组 = DXF读写器.选择文件();
        if (文件组.Length == 0) return;

        string 输出目录 = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(文件组[0])!, "轮廓");
        System.IO.Directory.CreateDirectory(输出目录);

        foreach (var 文件路径 in 文件组)
        {
            string 文件名 = System.IO.Path.GetFileName(文件路径);
            输出框.Text += $"读取文件: {文件名}" + Environment.NewLine;

            var 实体组 = DXF读写器.读取(文件路径);
            轮廓结果 结果 = new 轮廓结果(实体组);
            string 输出路径 = System.IO.Path.Combine(输出目录, 文件名);
            DXF读写器.写入(输出路径, 文件路径, 结果.内轮廓, 结果.外轮廓, 结果.修正后曲线组);

            输出框.Text += $" " + 结果.面积描述 + Environment.NewLine;

            输出框.Text += $"读取到曲线: {结果.原始实体数量};" +
                $"修正后的曲线数量: {结果.处理后曲线数量};" +
                $"计算出{结果.检出环数量} 个环;" +
                $"生成轮廓成功!" + Environment.NewLine;
        }
        输出框.Text += $"完成，共处理 {文件组.Length} 个文件" + Environment.NewLine;
    }
}
