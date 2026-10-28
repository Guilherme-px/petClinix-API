using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PetClinix.BuildingBlocks.Application;

namespace PetClinix.Api.Services;

public class MailjetFallbackEmailService : IEmailService
{
    private const string MailjetEndpoint = "https://api.mailjet.com/v3.1/send";

    private readonly ResendEmailService _inner;
    private readonly HttpClient _httpClient;
    private readonly string? _resendDeliverableEmail;
    private readonly string? _frontendBaseUrl;
    private readonly MailjetOptions? _mailjet;
    private readonly ILogger<MailjetFallbackEmailService> _logger;

    public MailjetFallbackEmailService(
        ResendEmailService inner,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<MailjetFallbackEmailService> logger)
    {
        _inner = inner;
        _httpClient = httpClientFactory.CreateClient("MailjetFallback");
        _logger = logger;
        _resendDeliverableEmail = configuration["Resend:DevOverrideEmail"];
        _frontendBaseUrl = configuration["App:FrontendBaseUrl"];

        var apiKey = configuration["Mailjet:ApiKey"];
        var secretKey = configuration["Mailjet:SecretKey"];
        var senderEmail = configuration["Mailjet:SenderEmail"];

        if (!string.IsNullOrWhiteSpace(apiKey) &&
            !string.IsNullOrWhiteSpace(secretKey) &&
            !string.IsNullOrWhiteSpace(senderEmail))
        {
            _mailjet = new MailjetOptions(apiKey, secretKey, senderEmail, configuration["Mailjet:SenderName"]);

            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{apiKey}:{secretKey}"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }
    }

    public async Task<string?> SendWelcomeEmailAsync(string toEmail, string userName, string passwordResetToken, CancellationToken cancellationToken = default)
    {
        if (IsDeliverableByResend(toEmail) || _mailjet is null)
        {
            return await _inner.SendWelcomeEmailAsync(toEmail, userName, passwordResetToken, cancellationToken);
        }

        var resetLink = $"{GetFrontendBaseUrl()}/define-senha?token={passwordResetToken}";

        try
        {
            var request = new MailjetSendRequest(
            [
                new MailjetMessage(
                    new MailjetContact(_mailjet.SenderEmail, _mailjet.SenderName ?? "PetClinix"),
                    [new MailjetContact(toEmail, null)],
                    "Bem-vindo ao PetClinix! Defina sua senha de acesso",
                    BuildWelcomeHtml(userName, resetLink))
            ]);

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, MailjetEndpoint)
            {
                Content = JsonContent.Create(request)
            };

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Mailjet retornou {StatusCode} ao enviar e-mail para {Email}: {Body}", (int)response.StatusCode, toEmail, body);
                return null;
            }

            _logger.LogInformation("E-mail de boas-vindas enviado via Mailjet para {Email}.", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao enviar e-mail via Mailjet para {Email}.", toEmail);
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

    private sealed record MailjetOptions(string ApiKey, string SecretKey, string SenderEmail, string? SenderName);

    private sealed record MailjetContact(string? Email, string? Name);

    private sealed record MailjetMessage(
        [property: JsonPropertyName("From")] MailjetContact From,
        [property: JsonPropertyName("To")] MailjetContact[] To,
        [property: JsonPropertyName("Subject")] string Subject,
        [property: JsonPropertyName("HTMLPart")] string HtmlPart);

    private sealed record MailjetSendRequest(
        [property: JsonPropertyName("Messages")] MailjetMessage[] Messages);
}