using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PetClinix.BuildingBlocks.Application;
using Resend;

namespace PetClinix.Api.Services;

public class ResendEmailService : IEmailService
{
    private readonly ResendClient _client;
    private readonly string _fromEmail = "PetClinix <onboarding@resend.dev>";
    private readonly string? _devOverrideEmail;
    private readonly string? _frontendBaseUrl;
    private readonly ILogger<ResendEmailService> _logger;

    public ResendEmailService(ResendClient client, IConfiguration configuration, ILogger<ResendEmailService> logger)
    {
        _client = client;
        _logger = logger;
        _devOverrideEmail = configuration["Resend:DevOverrideEmail"];
        _frontendBaseUrl = configuration["App:FrontendBaseUrl"];
    }

    public async Task<string?> SendWelcomeEmailAsync(string toEmail, string userName, string passwordResetToken, CancellationToken cancellationToken = default)
    {
        bool isDevOverrideTarget =
            !string.IsNullOrEmpty(_devOverrideEmail) &&
            toEmail.Equals(_devOverrideEmail, StringComparison.OrdinalIgnoreCase);

        if (isDevOverrideTarget)
        {
            var resetLink = $"{GetFrontendBaseUrl()}/reset-password?token={passwordResetToken}";

            var htmlContent = $@"
            <h1>Bem-vindo ao PetClinix, {userName}!</h1>
            <p>Sua conta foi criada com sucesso. Sua clínica já está pronta para uso.</p>
            <p>Para acessar o sistema, você precisa definir sua senha de acesso através do link abaixo:</p>
            <p><a href='{resetLink}' style='padding: 10px 20px; background-color: #3b82f6; color: white; text-decoration: none; border-radius: 5px;'>Definir Minha Senha</a></p>
            <p>Se o botão não funcionar, copie e cole este link no navegador: {resetLink}</p>
            <br>
            <p>Equipe PetClinix.</p>";

            var actualToEmail = !string.IsNullOrEmpty(_devOverrideEmail) ? _devOverrideEmail : toEmail;

            if (!string.IsNullOrEmpty(_devOverrideEmail) && _devOverrideEmail != toEmail)
            {
                _logger.LogWarning("⚠️ AMBIENTE DEV: E-mail original era {Original}, mas será enviado para {Override}", toEmail, _devOverrideEmail);
            }

            var message = new EmailMessage
            {
                From = _fromEmail,
                To = new EmailAddressList { actualToEmail },
                Subject = "Bem-vindo ao PetClinix! Defina sua senha de acesso",
                HtmlBody = htmlContent
            };

            try
            {
                var response = await _client.EmailSendAsync(message, cancellationToken);

                if (response.Content != Guid.Empty)
                {
                    _logger.LogInformation("E-mail de boas-vindas enviado com sucesso via Resend. ID: {Id}", response.Content);
                }
                else
                {
                    _logger.LogWarning("Resend retornou um ID vazio ao tentar enviar.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exceção ao tentar enviar e-mail via Resend.");
            }

            return null;
        }

        _logger.LogInformation("DEMO MODE: E-mail não enviado para {Email}. Token retornado na resposta da API.", toEmail);
        return passwordResetToken;
    }

    private string GetFrontendBaseUrl()
    {
        var baseUrl = string.IsNullOrWhiteSpace(_frontendBaseUrl)
            ? "http://localhost:5173"
            : _frontendBaseUrl;

        return baseUrl.TrimEnd('/');
    }
}