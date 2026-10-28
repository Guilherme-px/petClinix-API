using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PetClinix.BuildingBlocks.Application;

namespace PetClinix.Api.Services;

public class SmtpFallbackEmailService : IEmailService
{
    private readonly ResendEmailService _inner;
    private readonly string? _resendDeliverableEmail;
    private readonly string? _frontendBaseUrl;
    private readonly SmtpFallbackOptions? _smtp;
    private readonly ILogger<SmtpFallbackEmailService> _logger;

    public SmtpFallbackEmailService(
        ResendEmailService inner,
        IConfiguration configuration,
        ILogger<SmtpFallbackEmailService> logger)
    {
        _inner = inner;
        _logger = logger;
        _resendDeliverableEmail = configuration["Resend:DevOverrideEmail"];
        _frontendBaseUrl = configuration["App:FrontendBaseUrl"];

        var host = configuration["SmtpFallback:Host"];
        var username = configuration["SmtpFallback:Username"];
        var appPassword = configuration["SmtpFallback:AppPassword"];
        var fromEmail = configuration["SmtpFallback:FromEmail"];

        if (!string.IsNullOrWhiteSpace(host) &&
            !string.IsNullOrWhiteSpace(username) &&
            !string.IsNullOrWhiteSpace(appPassword) &&
            !string.IsNullOrWhiteSpace(fromEmail))
        {
            var port = int.TryParse(configuration["SmtpFallback:Port"], out var parsedPort) ? parsedPort : 587;
            var fromName = configuration["SmtpFallback:FromName"];

            _smtp = new SmtpFallbackOptions(host, port, username, appPassword, fromEmail, fromName);
        }
    }

    public async Task<string?> SendWelcomeEmailAsync(string toEmail, string userName, string passwordResetToken, CancellationToken cancellationToken = default)
    {
        if (IsDeliverableByResend(toEmail) || _smtp is null)
        {
            return await _inner.SendWelcomeEmailAsync(toEmail, userName, passwordResetToken, cancellationToken);
        }

        var resetLink = $"{GetFrontendBaseUrl()}/define-senha?token={passwordResetToken}";

        try
        {
            using var client = new SmtpClient(_smtp.Host, _smtp.Port);
            client.EnableSsl = true;
            client.Credentials = new NetworkCredential(_smtp.Username, _smtp.AppPassword);

            using var message = new MailMessage
            {
                From = new MailAddress(_smtp.FromEmail, _smtp.FromName ?? "PetClinix", Encoding.UTF8),
                Subject = "Bem-vindo ao PetClinix! Defina sua senha de acesso",
                SubjectEncoding = Encoding.UTF8,
                Body = BuildWelcomeHtml(userName, resetLink),
                BodyEncoding = Encoding.UTF8,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            await client.SendMailAsync(message, cancellationToken);

            _logger.LogInformation("E-mail de boas-vindas enviado via SMTP fallback ({From}) para {Email}.", _smtp.FromEmail, toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao enviar e-mail via SMTP fallback para {Email}.", toEmail);
        }

        return null;
    }

    private bool IsDeliverableByResend(string toEmail) =>
        !string.IsNullOrWhiteSpace(_resendDeliverableEmail) &&
        toEmail.Equals(_resendDeliverableEmail, StringComparison.OrdinalIgnoreCase);

    private string GetFrontendBaseUrl()
    {
        var baseUrl = string.IsNullOrWhiteSpace(_frontendBaseUrl)
            ? "http://localhost:5173"
            : _frontendBaseUrl;

        return baseUrl.TrimEnd('/');
    }

    private static string BuildWelcomeHtml(string userName, string resetLink) => $@"
            <h1>Bem-vindo ao PetClinix, {userName}!</h1>
            <p>Sua conta foi criada com sucesso. Sua clínica já está pronta para uso.</p>
            <p>Para acessar o sistema, você precisa definir sua senha de acesso através do link abaixo:</p>
            <p><a href='{resetLink}' style='padding: 10px 20px; background-color: #3b82f6; color: white; text-decoration: none; border-radius: 5px;'>Definir Minha Senha</a></p>
            <p>Se o botão não funcionar, copie e cole este link no navegador: {resetLink}</p>
            <br>
            <p>Equipe PetClinix.</p>";

    private sealed record SmtpFallbackOptions(
        string Host,
        int Port,
        string Username,
        string AppPassword,
        string FromEmail,
        string? FromName);
}