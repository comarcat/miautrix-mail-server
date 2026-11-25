using System.Globalization;

namespace Miautrix.Mail.Application.Mail;

public static class DeliveryStatusNotificationBuilder
{
    public static string Build(string recipient, string failedRecipient, string status, string smtpCode, string reportingMta, DateTimeOffset arrival)
    {
        static string Safe(string value) => value.Replace("\r", " ").Replace("\n", " ");
        var boundary = "=_miautrix_dsn_" + Guid.NewGuid().ToString("N");
        return $"From: Mail Delivery Subsystem <MAILER-DAEMON@{Safe(reportingMta)}>\r\n" +
               $"To: {Safe(recipient)}\r\n" +
               "Subject: Delivery Status Notification (Failure)\r\n" +
               "MIME-Version: 1.0\r\n" +
               $"Content-Type: multipart/report; report-type=delivery-status; boundary=\"{boundary}\"\r\n\r\n" +
               $"--{boundary}\r\nContent-Type: text/plain; charset=utf-8\r\n\r\n" +
               $"Delivery to {Safe(failedRecipient)} failed. {(status == "4.4.7" ? "The message expired while retrying." : "The recipient server permanently rejected the message.")}\r\n" +
               $"--{boundary}\r\nContent-Type: message/delivery-status\r\n\r\n" +
               $"Reporting-MTA: dns; {Safe(reportingMta)}\r\n" +
               $"Arrival-Date: {arrival.ToUniversalTime().ToString("ddd, dd MMM yyyy HH:mm:ss +0000", CultureInfo.InvariantCulture)}\r\n\r\n" +
               $"Final-Recipient: rfc822; {Safe(failedRecipient)}\r\n" +
               "Action: failed\r\n" +
               $"Status: {status}\r\n" +
               $"Diagnostic-Code: smtp; {smtpCode} Delivery failed\r\n" +
               $"--{boundary}--\r\n";
    }
}
