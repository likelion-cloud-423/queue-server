using Bogus;

namespace LikeLionChat.Client;

public partial class MainForm : Form
{
    private readonly Faker _faker = new();

    public MainForm()
    {
        InitializeComponent();
        Load += MainForm_Load;
    }

    private void MainForm_Load(object? sender, EventArgs e)
    {
        txtNickname.Text = _faker.Internet.UserName();
    }

    private async void BtnStart_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtQueueUrl.Text) ||
            string.IsNullOrWhiteSpace(txtChatUrl.Text) ||
            string.IsNullOrWhiteSpace(txtNickname.Text))
        {
            MessageBox.Show("모든 필드를 입력해주세요.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        btnStart.Enabled = false;

        try
        {
            // 1. 대기열 진입 및 폴링
            using var queueForm = new QueueForm(txtQueueUrl.Text, txtNickname.Text);
            var queueResult = queueForm.ShowDialog();

            if (queueResult == DialogResult.OK && !string.IsNullOrEmpty(queueForm.TicketId))
            {
                // 2. 티켓 발급 완료 - 채팅 서버 접속
                var chatForm = new ChatForm(txtChatUrl.Text, queueForm.TicketId);
                chatForm.ShowDialog();
            }
            else if (queueForm.IsCancelled)
            {
                // 사용자가 취소함
                MessageBox.Show("대기열에서 나갔습니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"오류 발생: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnStart.Enabled = true;
        }
    }
}
