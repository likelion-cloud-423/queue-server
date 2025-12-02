namespace LikeLionChat.Client;

public partial class QueueForm : Form
{
    private readonly QueueClient _queueClient;
    private readonly System.Windows.Forms.Timer _pollingTimer;
    private string? _userId;
    private string? _ticketId;
    private bool _isCancelled;

    public string? TicketId => _ticketId;
    public bool IsCancelled => _isCancelled;

    public QueueForm(string queueApiUrl, string nickname)
    {
        InitializeComponent();

        _queueClient = new QueueClient(queueApiUrl);
        _pollingTimer = new System.Windows.Forms.Timer
        {
            Interval = 1000 // 1초마다 폴링
        };
        _pollingTimer.Tick += PollingTimer_Tick;

        // 대기열 진입
        _ = EnterQueueAsync(nickname);
    }

    private async Task EnterQueueAsync(string nickname)
    {
        try
        {
            lblStatus.Text = "대기열에 진입 중...";

            var response = await _queueClient.EnterQueueAsync(nickname);
            _userId = response.UserId;

            lblStatus.Text = "대기 중";
            lblRank.Text = $"대기 순번: {response.Rank}";

            // 폴링 시작
            _pollingTimer.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"대기열 진입 실패: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _isCancelled = true;
            Close();
        }
    }

    private async void PollingTimer_Tick(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_userId))
        {
            return;
        }

        try
        {
            var status = await _queueClient.GetStatusAsync(_userId);

            if (status.Status == "PROMOTED" && !string.IsNullOrEmpty(status.TicketId))
            {
                // 티켓 발급 완료
                _pollingTimer.Stop();
                _ticketId = status.TicketId;

                lblStatus.Text = "입장 준비 완료!";
                lblStatus.ForeColor = Color.Green;
                lblRank.Text = "곧 접속합니다...";
                progressBar.Style = ProgressBarStyle.Continuous;
                progressBar.Value = 100;

                await Task.Delay(500); // 잠시 대기
                DialogResult = DialogResult.OK;
                Close();
            }
            else if (status.Status == "WAITING")
            {
                // 대기 중
                lblRank.Text = $"대기 순번: {status.Rank}";
            }
        }
        catch (Exception ex)
        {
            // 일시적인 네트워크 오류는 무시하고 계속 폴링
            Console.WriteLine($"Polling error: {ex.Message}");
        }
    }

    private void BtnCancel_Click(object? sender, EventArgs e)
    {
        _isCancelled = true;
        _pollingTimer.Stop();
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void QueueForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        _pollingTimer.Stop();
        _pollingTimer.Dispose();
        _queueClient.Dispose();
    }
}
