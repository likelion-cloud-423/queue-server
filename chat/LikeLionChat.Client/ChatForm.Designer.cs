namespace LikeLionChat.Client;

partial class ChatForm
{
    private System.ComponentModel.IContainer components = null;
    private TextBox txtMessage = null!;
    private Button btnSend = null!;
    private RichTextBox rtbMessages = null!;
    private Label lblServerStatus = null!;
    private Button btnRefreshStatus = null!;
    private Button btnDisconnect = null!;

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
        
        txtMessage = new TextBox();
        btnSend = new Button();
        rtbMessages = new RichTextBox();
        lblServerStatus = new Label();
        btnRefreshStatus = new Button();
        btnDisconnect = new Button();

        SuspendLayout();

        // rtbMessages
        rtbMessages.Location = new Point(12, 12);
        rtbMessages.Name = "rtbMessages";
        rtbMessages.ReadOnly = true;
        rtbMessages.Size = new Size(776, 370);
        rtbMessages.TabIndex = 0;
        rtbMessages.Text = "";

        // lblServerStatus
        lblServerStatus.AutoSize = true;
        lblServerStatus.Font = new Font("Segoe UI", 10F);
        lblServerStatus.Location = new Point(12, 390);
        lblServerStatus.Name = "lblServerStatus";
        lblServerStatus.Size = new Size(150, 19);
        lblServerStatus.TabIndex = 1;
        lblServerStatus.Text = "접속자: -";

        // btnRefreshStatus
        btnRefreshStatus.Location = new Point(180, 388);
        btnRefreshStatus.Name = "btnRefreshStatus";
        btnRefreshStatus.Size = new Size(80, 23);
        btnRefreshStatus.TabIndex = 2;
        btnRefreshStatus.Text = "새로고침";
        btnRefreshStatus.UseVisualStyleBackColor = true;
        btnRefreshStatus.Click += BtnRefreshStatus_Click;

        // btnDisconnect
        btnDisconnect.Location = new Point(700, 388);
        btnDisconnect.Name = "btnDisconnect";
        btnDisconnect.Size = new Size(88, 23);
        btnDisconnect.TabIndex = 3;
        btnDisconnect.Text = "접속 종료";
        btnDisconnect.UseVisualStyleBackColor = true;
        btnDisconnect.Click += BtnDisconnect_Click;

        // txtMessage
        txtMessage.Location = new Point(12, 419);
        txtMessage.Name = "txtMessage";
        txtMessage.Size = new Size(695, 23);
        txtMessage.TabIndex = 4;
        txtMessage.KeyPress += TxtMessage_KeyPress;

        // btnSend
        btnSend.Location = new Point(713, 419);
        btnSend.Name = "btnSend";
        btnSend.Size = new Size(75, 23);
        btnSend.TabIndex = 5;
        btnSend.Text = "전송";
        btnSend.UseVisualStyleBackColor = true;
        btnSend.Click += BtnSend_Click;

        // ChatForm
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(800, 454);
        Controls.Add(btnSend);
        Controls.Add(txtMessage);
        Controls.Add(btnDisconnect);
        Controls.Add(btnRefreshStatus);
        Controls.Add(lblServerStatus);
        Controls.Add(rtbMessages);
        Name = "ChatForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "LikeLion Chat";
        FormClosing += ChatForm_FormClosing;
        ResumeLayout(false);
        PerformLayout();
    }
}
