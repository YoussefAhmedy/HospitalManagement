namespace Hospital.BLL.Helpers;

public sealed class EmailOptions
{
    public bool Enabled { get; set; }
    public string From { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string SmtpServer { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
}
