using System.Net;

namespace Hospital.BLL.Notifications;

/// <summary>Versioned, centralized HTML templates for transactional email.</summary>
public static class EmailTemplates
{
    public static string AccountConfirmation(string recipientName, string actionUrl) => Layout(
        "Confirm your email",
        $"<p>Hello {WebUtility.HtmlEncode(recipientName)},</p>" +
        "<p>Please confirm your email address to activate your CareAxis account.</p>" +
        ActionLink(actionUrl, "Confirm email") +
        "<p>If you did not create this account, you can ignore this message.</p>");

    public static string PasswordReset(string recipientName, string actionUrl) => Layout(
        "Reset your password",
        $"<p>Hello {WebUtility.HtmlEncode(recipientName)},</p>" +
        "<p>We received a request to reset your password. The link expires according to the account security policy.</p>" +
        ActionLink(actionUrl, "Reset password") +
        "<p>If you did not request a reset, no action is required.</p>");

    public static string EmailChangeConfirmation(string recipientName, string actionUrl) => Layout(
        "Confirm your new email address",
        $"<p>Hello {WebUtility.HtmlEncode(recipientName)},</p>" +
        "<p>Confirm this address to update the email on your CareAxis account.</p>" +
        ActionLink(actionUrl, "Confirm new email") +
        "<p>If you did not request this change, ignore the message and review your account security.</p>");

    public static string AppointmentUpdate(string recipientName, string status, string appointmentDate) => Layout(
        "Appointment update",
        $"<p>Hello {WebUtility.HtmlEncode(recipientName)},</p>" +
        $"<p>Your appointment request is now <strong>{WebUtility.HtmlEncode(status)}</strong>.</p>" +
        $"<p>Appointment date: {WebUtility.HtmlEncode(appointmentDate)}</p>" +
        "<p>Sign in to CareAxis to review your appointment details.</p>");

    private static string ActionLink(string actionUrl, string label) =>
        $"<p><a href=\"{WebUtility.HtmlEncode(actionUrl)}\" style=\"display:inline-block;padding:12px 20px;border-radius:8px;background:#176b65;color:#fff;text-decoration:none;font-weight:600\">{WebUtility.HtmlEncode(label)}</a></p>";

    private static string Layout(string title, string content) =>
        "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>" +
        WebUtility.HtmlEncode(title) +
        "</title></head><body style=\"margin:0;background:#f3f7f6;font-family:Arial,sans-serif;color:#1d302f\"><div style=\"max-width:600px;margin:32px auto;padding:32px;background:#fff;border:1px solid #dce8e5;border-radius:16px\"><p style=\"font-weight:700;letter-spacing:.08em;color:#176b65\">CAREAXIS</p><h1 style=\"font-size:24px\">" +
        WebUtility.HtmlEncode(title) + "</h1>" + content +
        "<hr style=\"border:0;border-top:1px solid #e5eeec;margin:28px 0\"><p style=\"font-size:12px;color:#667673\">This is a transactional message from CareAxis. Do not reply with medical information.</p></div></body></html>";
}
