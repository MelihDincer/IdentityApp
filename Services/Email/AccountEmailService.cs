using System.Text;
using IdentityApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;

namespace IdentityApp.Services.Email;

public class AccountEmailService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly LinkGenerator _linkGenerator;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AccountEmailService> _logger;

    public AccountEmailService(
        UserManager<AppUser> userManager,
        IEmailSender emailSender,
        LinkGenerator linkGenerator,
        IConfiguration configuration,
        ILogger<AccountEmailService> logger)
    {
        _userManager = userManager;
        _emailSender = emailSender;
        _linkGenerator = linkGenerator;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> TrySendConfirmationAsync(AppUser user)
    {
        try
        {
            var baseUrl = _configuration["App:BaseUrl"];

            if (string.IsNullOrWhiteSpace(baseUrl) ||
                !Uri.TryCreate(baseUrl, UriKind.Absolute, out var appUri) ||
                appUri.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException(
                    "App:BaseUrl geçerli bir HTTPS adresi olmalıdır.");
            }

            if (string.IsNullOrWhiteSpace(user.Email))
            {
                throw new InvalidOperationException(
                    "Kullanıcının e-posta adresi bulunamadı.");
            }

            var token =
                await _userManager.GenerateEmailConfirmationTokenAsync(user);

            var encodedToken = WebEncoders.Base64UrlEncode(
                Encoding.UTF8.GetBytes(token));

            var path = _linkGenerator.GetPathByAction(
                action: "ConfirmEmail",
                controller: "Account",
                values: new
                {
                    userId = user.Id,
                    code = encodedToken
                });

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new InvalidOperationException(
                    "Doğrulama bağlantısı oluşturulamadı.");
            }

            // Alan adını isteğin Host bilgisinden değil ayarlardan al.
            var confirmationUrl = $"{baseUrl.TrimEnd('/')}{path}";

            var fullName = string.IsNullOrWhiteSpace(user.FullName)
                ? "Kullanıcımız"
                : user.FullName;

            var html = EmailTemplates.ConfirmEmail(fullName, confirmationUrl);

            await _emailSender.SendEmailAsync(
                user.Email,
                "IdentityApp | E-posta adresinizi doğrulayın",
                html);

            return true;
        }
        catch (Exception ex)
        {
            // Teknik hatayı kullanıcıya göstermeden logla.
            _logger.LogError(
                ex,
                "Doğrulama e-postası gönderilemedi. Kullanıcı: {UserId}",
                user.Id);

            return false;
        }
    }
}