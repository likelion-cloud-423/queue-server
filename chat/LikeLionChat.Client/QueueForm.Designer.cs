namespace LikeLionChat.Client;

partial class QueueForm
{
    private System.ComponentModel.IContainer components = null;
    private Label lblTitle = null!;
    private Label lblStatus = null!;
    private Label lblRank = null!;
    private Button btnCancel = null!;
    private ProgressBar progressBar = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        
        lblTitle = new Label();
        lblStatus = new Label();
        lblRank = new Label();
        btnCancel = new Button();
        progressBar = new ProgressBar();

        SuspendLayout();

        // lblTitle
        lblTitle.AutoSize = false;
        lblTitle.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        lblTitle.Location = new Point(50, 80);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(400, 40);
        lblTitle.TabIndex = 0;
        lblTitle.Text = "대기열";
        lblTitle.TextAlign = ContentAlignment.MiddleCenter;

        // lblStatus
        lblStatus.AutoSize = false;
        lblStatus.Font = new Font("Segoe UI", 12F);
        lblStatus.Location = new Point(50, 140);
        lblStatus.Name = "lblStatus";
        lblStatus.Size = new Size(400, 30);
        lblStatus.TabIndex = 1;
        lblStatus.Text = "대기 중...";
        lblStatus.TextAlign = ContentAlignment.MiddleCenter;

        // lblRank
        lblRank.AutoSize = false;
        lblRank.Font = new Font("Segoe UI", 24F, FontStyle.Bold);
        lblRank.ForeColor = Color.DarkBlue;
        lblRank.Location = new Point(50, 190);
        lblRank.Name = "lblRank";
        lblRank.Size = new Size(400, 80);
        lblRank.TabIndex = 2;
        lblRank.Text = "대기 순번: -";
        lblRank.TextAlign = ContentAlignment.MiddleCenter;

        // progressBar
        progressBar.Location = new Point(100, 290);
        progressBar.Name = "progressBar";
        progressBar.Size = new Size(300, 23);
        progressBar.Style = ProgressBarStyle.Marquee;
        progressBar.TabIndex = 3;

        // btnCancel
        btnCancel.Font = new Font("Segoe UI", 10F);
        btnCancel.Location = new Point(175, 340);
        btnCancel.Name = "btnCancel";
        btnCancel.Size = new Size(150, 40);
        btnCancel.TabIndex = 4;
        btnCancel.Text = "취소";
        btnCancel.UseVisualStyleBackColor = true;
        btnCancel.Click += BtnCancel_Click;

        // QueueForm
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(500, 450);
        Controls.Add(btnCancel);
        Controls.Add(progressBar);
        Controls.Add(lblRank);
        Controls.Add(lblStatus);
        Controls.Add(lblTitle);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "QueueForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "LikeLion Chat - 대기열";
        FormClosing += QueueForm_FormClosing;
        ResumeLayout(false);
    }
}
