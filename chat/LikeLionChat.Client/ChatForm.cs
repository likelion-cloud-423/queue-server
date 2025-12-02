using LikeLionChat.Shared;

namespace LikeLionChat.Client;

public partial class ChatForm : Form
{
    private readonly ChatClient _chatClient;

    public ChatForm(string serverUrl, string ticketId)
    {
        InitializeComponent();

        _chatClient = new ChatClient();
        _chatClient.MessageReceived += ChatClient_MessageReceived;
        _chatClient.SystemMessageReceived += ChatClient_SystemMessageReceived;
        _chatClient.ServerStatusReceived += ChatClient_ServerStatusReceived;
        _chatClient.ErrorOccurred += ChatClient_ErrorOccurred;
        _chatClient.Disconnected += ChatClient_Disconnected;

        _ = ConnectAsync(serverUrl, ticketId);
    }

    private async Task ConnectAsync(string serverUrl, string ticketId)
    {
        try
        {
            await _chatClient.ConnectAsync(serverUrl, ticketId);
            AppendMessage("채팅 서버에 접속했습니다.", Color.Green);
            
            // 접속 후 서버 상태 요청
            await _chatClient.RequestServerStatusAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"서버 접속 실패: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
        }
    }

    private async void BtnSend_Click(object? sender, EventArgs e)
    {
        await SendMessageAsync();
    }

    private void TxtMessage_KeyPress(object? sender, KeyPressEventArgs e)
    {
        if (e.KeyChar == (char)Keys.Enter)
        {
            e.Handled = true;
            _ = SendMessageAsync();
        }
    }

    private async void BtnRefreshStatus_Click(object? sender, EventArgs e)
    {
        try
        {
            if (_chatClient.IsConnected)
            {
                await _chatClient.RequestServerStatusAsync();
            }
        }
        catch (Exception ex)
        {
            AppendMessage($"상태 조회 실패: {ex.Message}", Color.Red);
        }
    }

    private async void BtnDisconnect_Click(object? sender, EventArgs e)
    {
        await DisconnectAsync();
        Close();
    }

    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(txtMessage.Text))
        {
            return;
        }

        try
        {
            if (_chatClient.IsConnected)
            {
                await _chatClient.SendMessageAsync(txtMessage.Text);
                txtMessage.Clear();
            }
        }
        catch (Exception ex)
        {
            AppendMessage($"메시지 전송 실패: {ex.Message}", Color.Red);
        }
    }

    private async Task DisconnectAsync()
    {
        try
        {
            await _chatClient.DisconnectAsync();
        }
        catch { }
    }

    private void ChatClient_MessageReceived(object? sender, MessageReceivePayload e)
    {
        if (InvokeRequired)
        {
            Invoke(() => ChatClient_MessageReceived(sender, e));
            return;
        }

        AppendMessage($"[{e.Timestamp:HH:mm:ss}] {e.Nickname}: {e.Message}", Color.Black);
    }

    private void ChatClient_SystemMessageReceived(object? sender, SystemMessageReceivePayload e)
    {
        if (InvokeRequired)
        {
            Invoke(() => ChatClient_SystemMessageReceived(sender, e));
            return;
        }

        AppendMessage($"[{e.Timestamp:HH:mm:ss}] * {e.Message}", Color.Blue);
    }

    private void ChatClient_ServerStatusReceived(object? sender, ServerStatusResponsePayload e)
    {
        if (InvokeRequired)
        {
            Invoke(() => ChatClient_ServerStatusReceived(sender, e));
            return;
        }

        lblServerStatus.Text = $"접속자: {e.ClientCount}명";
    }

    private void ChatClient_ErrorOccurred(object? sender, Exception e)
    {
        if (InvokeRequired)
        {
            Invoke(() => ChatClient_ErrorOccurred(sender, e));
            return;
        }

        AppendMessage($"오류: {e.Message}", Color.Red);
    }

    private void ChatClient_Disconnected(object? sender, EventArgs e)
    {
        if (InvokeRequired)
        {
            Invoke(() => ChatClient_Disconnected(sender, e));
            return;
        }

        AppendMessage("서버와의 연결이 종료되었습니다.", Color.Red);
        MessageBox.Show("서버와의 연결이 종료되었습니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
        Close();
    }

    private void AppendMessage(string message, Color color)
    {
        rtbMessages.SelectionStart = rtbMessages.TextLength;
        rtbMessages.SelectionLength = 0;
        rtbMessages.SelectionColor = color;
        rtbMessages.AppendText(message + Environment.NewLine);
        rtbMessages.SelectionColor = rtbMessages.ForeColor;
        rtbMessages.ScrollToCaret();
    }

    private async void ChatForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        await DisconnectAsync();
        _chatClient.Dispose();
    }
}
