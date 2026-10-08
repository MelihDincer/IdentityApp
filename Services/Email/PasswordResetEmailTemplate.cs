using System.Text.Encodings.Web;

namespace IdentityApp.Services.Email;

public static class PasswordResetEmailTemplate
{
    public static string Render(string fullName, string confirmationUrl)
    {
        // Kullanıcı bilgilerini doğrudan HTML içine yerleştirmiyoruz.
        var safeName = HtmlEncoder.Default.Encode(fullName);
        var safeUrl = HtmlEncoder.Default.Encode(confirmationUrl);

        return $@"
<!DOCTYPE html>
<html lang=""tr"">
<head>
    <meta charset=""utf-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
    <title>Parolanızı sıfırlayın</title>
</head>
<body style=""margin:0;padding:0;background:#f5f7fb;
             font-family:Arial,Helvetica,sans-serif;color:#111827;"">

    <table role=""presentation"" width=""100%"" cellpadding=""0""
           cellspacing=""0"" style=""background:#f5f7fb;"">
        <tr>
            <td align=""center"" style=""padding:40px 16px;"">

                <table role=""presentation"" width=""560"" cellpadding=""0""
                       cellspacing=""0"" style=""width:100%;max-width:560px;"">

                    <tr>
                        <td style=""padding:0 0 22px;font-size:22px;
                                   font-weight:bold;letter-spacing:-0.5px;"">
                            IdentityApp
                        </td>
                    </tr>

                    <tr>
                        <td style=""padding:32px;background:#ffffff;
                                   border:1px solid #e5e7eb;border-radius:16px;"">

                            <p style=""margin:0 0 14px;font-size:11px;
                                      font-weight:bold;letter-spacing:1.5px;
                                      color:#64748b;"">
                                PAROLA SIFIRLAMA
                            </p>

                            <h1 style=""margin:0 0 22px;font-size:25px;
                                       line-height:1.35;color:#111827;"">
                                Yeni parolanızı belirleyin.
                            </h1>

                            <p style=""margin:0 0 14px;font-size:15px;
                                      line-height:1.7;color:#374151;"">
                                Merhaba {safeName},
                            </p>

                            <p style=""margin:0 0 26px;font-size:14px;
                                      line-height:1.8;color:#6b7280;"">
                                Parolanızı sıfırlamak için aşağıdaki bağlantıyı kullanın.
                                Bağlantı çalışmıyorsa yeni bir sıfırlama talebi oluşturun.
                            </p>

                            <table role=""presentation"" cellpadding=""0""
                                   cellspacing=""0"">
                                <tr>
                                    <td bgcolor=""#111827""
                                        style=""border-radius:8px;
                                               mso-padding-alt:14px 24px;"">
                                        <a href=""{safeUrl}""
                                           style=""display:inline-block;
                                                  padding:14px 24px;
                                                  border:1px solid #111827;
                                                  border-radius:8px;
                                                  color:#ffffff;
                                                  font-size:14px;
                                                  font-weight:bold;
                                                  text-decoration:none;"">
                                            Parolamı sıfırla
                                        </a>
                                    </td>
                                </tr>
                            </table>

                            <p style=""margin:26px 0 10px;font-size:12px;
                                      line-height:1.7;color:#6b7280;"">
                                Buton çalışmıyorsa aşağıdaki bağlantıyı
                                tarayıcınıza kopyalayabilirsiniz:
                            </p>

                            <p style=""margin:0;font-size:12px;line-height:1.7;
                                      overflow-wrap:anywhere;word-break:break-all;"">
                                <a href=""{safeUrl}"" style=""color:#475569;"">
                                    {safeUrl}
                                </a>
                            </p>

                            <p style=""margin:26px 0 0;padding-top:20px;
                                      border-top:1px solid #eef0f3;
                                      color:#94a3b8;font-size:12px;line-height:1.8;"">
                                Bu talebi siz oluşturmadıysanız bağlantıya
                                tıklamayın. Bu e-postayı yok sayabilirsiniz.
                            </p>

                        </td>
                    </tr>

                    <tr>
                        <td align=""center""
                            style=""padding:22px 12px;color:#94a3b8;
                                   font-size:11px;line-height:1.7;"">
                            © {DateTime.UtcNow.Year} IdentityApp
                            <br />
                            Bu e-posta otomatik olarak gönderilmiştir.
                        </td>
                    </tr>

                </table>

            </td>
        </tr>
    </table>

</body>
</html>";
    }
}
