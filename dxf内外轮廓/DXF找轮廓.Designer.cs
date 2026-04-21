namespace dxf内外轮廓;

partial class DXF找轮廓
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    ///  Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        选择文件按钮 = new Button();
        输出框 = new RichTextBox();
        SuspendLayout();
        // 
        // 选择文件按钮
        // 
        选择文件按钮.Location = new Point(307, 669);
        选择文件按钮.Name = "选择文件按钮";
        选择文件按钮.Size = new Size(481, 48);
        选择文件按钮.TabIndex = 0;
        选择文件按钮.Text = "选择dxf文件";
        选择文件按钮.UseVisualStyleBackColor = true;
        选择文件按钮.Click += 选择文件按钮_Click;
        // 
        // 输出框
        // 
        输出框.Location = new Point(12, 12);
        输出框.Name = "输出框";
        输出框.Size = new Size(776, 639);
        输出框.TabIndex = 1;
        输出框.Text = "";
        // 
        // DXF找轮廓
        // 
        AutoScaleDimensions = new SizeF(11F, 24F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(800, 729);
        Controls.Add(输出框);
        Controls.Add(选择文件按钮);
        Name = "DXF找轮廓";
        Text = "DXF找轮廓";
        ResumeLayout(false);
    }

    #endregion

    private Button 选择文件按钮;
    private RichTextBox 输出框;
}
