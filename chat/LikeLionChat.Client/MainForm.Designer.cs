namespace LikeLionChat.Client;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;
    private TextBox txtQueueUrl = null!;
    private TextBox txtChatUrl = null!;
    private TextBox txtNickname = null!;
    private Button btnStart = null!;
    private Label lblQueueUrl = null!;
    private Label lblChatUrl = null!;
    private Label lblNickname = null!;
    private Label lblTitle = null!;

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
        txtQueueUrl = new TextBox();
        txtChatUrl = new TextBox();
        txtNickname = new TextBox();
        btnStart = new Button();
        lblQueueUrl = new Label();
        lblChatUrl = new Label();
        lblNickname = new Label();
        lblTitle = new Label();
        SuspendLayout();
        // 
        // txtQueueUrl
        // 
        txtQueueUrl.Location = new Point(71, 200);
        txtQueueUrl.Margin = new Padding(4, 5, 4, 5);
        txtQueueUrl.Name = "txtQueueUrl";
        txtQueueUrl.Size = new Size(570, 31);
        txtQueueUrl.TabIndex = 2;
        txtQueueUrl.Text = "http://localhost:8080";
        // 
        // txtChatUrl
        // 
        txtChatUrl.Location = new Point(71, 300);
        txtChatUrl.Margin = new Padding(4, 5, 4, 5);
        txtChatUrl.Name = "txtChatUrl";
        txtChatUrl.Size = new Size(570, 31);
        txtChatUrl.TabIndex = 4;
        txtChatUrl.Text = "ws://localhost:8081/gameserver";
        // 
        // txtNickname
        // 
        txtNickname.Location = new Point(71, 400);
        txtNickname.Margin = new Padding(4, 5, 4, 5);
        txtNickname.Name = "txtNickname";
        txtNickname.Size = new Size(570, 31);
        txtNickname.TabIndex = 6;
        // 
        // btnStart
        // 
        btnStart.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        btnStart.Location = new Point(214, 483);
        btnStart.Margin = new Padding(4, 5, 4, 5);
        btnStart.Name = "btnStart";
        btnStart.Size = new Size(286, 83);
        btnStart.TabIndex = 7;
        btnStart.Text = "접속 시작";
        btnStart.UseVisualStyleBackColor = true;
        btnStart.Click += BtnStart_Click;
        // 
        // lblQueueUrl
        // 
        lblQueueUrl.AutoSize = true;
        lblQueueUrl.Location = new Point(71, 167);
        lblQueueUrl.Margin = new Padding(4, 0, 4, 0);
        lblQueueUrl.Name = "lblQueueUrl";
        lblQueueUrl.Size = new Size(142, 25);
        lblQueueUrl.TabIndex = 1;
        lblQueueUrl.Text = "Queue API URL:";
        // 
        // lblChatUrl
        // 
        lblChatUrl.AutoSize = true;
        lblChatUrl.Location = new Point(71, 267);
        lblChatUrl.Margin = new Padding(4, 0, 4, 0);
        lblChatUrl.Name = "lblChatUrl";
        lblChatUrl.Size = new Size(148, 25);
        lblChatUrl.TabIndex = 3;
        lblChatUrl.Text = "Chat Server URL:";
        // 
        // lblNickname
        // 
        lblNickname.AutoSize = true;
        lblNickname.Location = new Point(71, 367);
        lblNickname.Margin = new Padding(4, 0, 4, 0);
        lblNickname.Name = "lblNickname";
        lblNickname.Size = new Size(97, 25);
        lblNickname.TabIndex = 5;
        lblNickname.Text = "Nickname:";
        // 
        // lblTitle
        // 
        lblTitle.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
        lblTitle.Location = new Point(71, 50);
        lblTitle.Margin = new Padding(4, 0, 4, 0);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(571, 67);
        lblTitle.TabIndex = 0;
        lblTitle.Text = "LikeLion Chat Client";
        lblTitle.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // MainForm
        // 
        AutoScaleDimensions = new SizeF(10F, 25F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(714, 633);
        Controls.Add(btnStart);
        Controls.Add(txtNickname);
        Controls.Add(lblNickname);
        Controls.Add(txtChatUrl);
        Controls.Add(lblChatUrl);
        Controls.Add(txtQueueUrl);
        Controls.Add(lblQueueUrl);
        Controls.Add(lblTitle);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Margin = new Padding(4, 5, 4, 5);
        MaximizeBox = false;
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "LikeLion Chat Client";
        ResumeLayout(false);
        PerformLayout();
    }
}
