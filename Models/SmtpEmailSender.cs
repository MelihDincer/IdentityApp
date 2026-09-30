using System.Net;
using System.Net.Mail;
using System.Text;

namespace IdentityApp.Models;

public class SmtpEmailSender : IEmailSender
{
    private readonly string? _host;
    private readonly int _port;
    private readonly bool _enableSSL;
    private readonly string? _username;
    private readonly string? _password;

    public SmtpEmailSender(
        string? host,
        int port,
        bool enableSSL,
        string? username,
        string? password)
    {
        _host = host;
        _port = port;
        _enableSSL = enableSSL;
        _username = username;
        _password = password;
    }

    public async Task SendEmailAsync(
        string email,
        string subject,
        string message)
    {
        if (string.IsNullOrWhiteSpace(_host) ||
            string.IsNullOrWhiteSpace(_username) ||
            string.IsNullOrWhiteSpace(_password))
        {
            throw new InvalidOperationException(
                "SMTP sunucusu, kullanıcı adı veya parola tanımlanmamış.");
        }

        using var client = new SmtpClient(_host, _port)
        {
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(_username, _password),
            EnableSsl = _enableSSL
        };

        using var mail = new MailMessage
        {
            From = new MailAddress(_username, "IdentityApp"),
            Subject = subject,
            Body = message,
            IsBodyHtml = true,
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8
        };

        mail.To.Add(new MailAddress(email));

        await client.SendMailAsync(mail);
    }
}