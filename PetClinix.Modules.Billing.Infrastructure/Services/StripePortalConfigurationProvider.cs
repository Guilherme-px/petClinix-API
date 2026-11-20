using Microsoft.Extensions.Logging;
using PetClinix.Modules.Billing.Application.Contracts;
using Stripe;
using Stripe.BillingPortal;

namespace PetClinix.Modules.Billing.Infrastructure.Services;

public class StripePortalConfigurationProvider : IPortalConfigurationProvider
{
    private static readonly SemaphoreSlim SyncLock = new(1, 1);
    private static string? _cachedConfigurationId;

    private readonly ConfigurationService _configurationService;
    private readonly PriceService _priceService;
    private readonly ILogger<StripePortalConfigurationProvider> _logger;

    public StripePortalConfigurationProvider(ILogger<StripePortalConfigurationProvider> logger)
    {
        _configurationService = new ConfigurationService();
        _priceService = new PriceService();
        _logger = logger;
    }

    public async Task<string?> GetOrCreateConfigurationIdAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedConfigurationId != null)
        {
            return _cachedConfigurationId;
        }

        await SyncLock.WaitAsync(cancellationToken);

        try
        {
            if (_cachedConfigurationId != null)
            {
                return _cachedConfigurationId;
            }

            var reusableConfigurationId = await FindConfigurationWithPlanUpdatesAsync(cancellationToken);
            if (reusableConfigurationId != null)
            {
                _cachedConfigurationId = reusableConfigurationId;
                return _cachedConfigurationId;
            }

            _cachedConfigurationId = await CreateConfigurationAsync(cancellationToken);
            return _cachedConfigurationId;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Falha ao garantir a configuração do portal do Stripe com troca de plano. O portal abrirá com a configuração padrão.");
            return null;
        }
        finally
        {
            SyncLock.Release();
        }
    }

    private async Task<string?> FindConfigurationWithPlanUpdatesAsync(CancellationToken cancellationToken)
    {
        var configurations = await _configurationService.ListAsync(
            new ConfigurationListOptions { Active = true, Limit = 100 },
            cancellationToken: cancellationToken);

        return configurations
            .Where(configuration => configuration.Active)
            .Where(IsCompatibleWithDesiredBehavior)
            .Select(configuration => configuration.Id)
            .FirstOrDefault();
    }

    private static bool IsCompatibleWithDesiredBehavior(Configuration configuration)
    {
        var subscriptionUpdate = configuration.Features?.SubscriptionUpdate;

        if (subscriptionUpdate?.Enabled != true || subscriptionUpdate.Products?.Any() != true)
        {
            return false;
        }

        var allowedUpdates = subscriptionUpdate.DefaultAllowedUpdates;
        if (allowedUpdates?.Contains("price") != true || allowedUpdates.Contains("quantity"))
        {
            return false;
        }

        if (subscriptionUpdate.ProrationBehavior != "always_invoice")
        {
            return false;
        }

        var scheduledConditions = subscriptionUpdate.ScheduleAtPeriodEnd?.Conditions;
        if (scheduledConditions == null
            || !scheduledConditions.Any(condition => condition?.Type == "decreasing_item_amount")
            || !scheduledConditions.Any(condition => condition?.Type == "shortening_interval"))
        {
            return false;
        }

        if (subscriptionUpdate.ScheduleAtPeriodEnd?.Conditions?.Count != 0)
        {
            return false;
        }

        return subscriptionUpdate.Products.All(product => product.AdjustableQuantity?.Enabled != true);
    }

    private async Task<string> CreateConfigurationAsync(CancellationToken cancellationToken)
    {
        var products = await ListProductsForPlanUpdatesAsync(cancellationToken);

        var features = new ConfigurationFeaturesOptions
        {
            CustomerUpdate = new ConfigurationFeaturesCustomerUpdateOptions
            {
                Enabled = true,
                AllowedUpdates = new List<string> { "address", "phone", "tax_id" }
            },
            InvoiceHistory = new ConfigurationFeaturesInvoiceHistoryOptions
            {
                Enabled = true
            },
            PaymentMethodUpdate = new ConfigurationFeaturesPaymentMethodUpdateOptions
            {
                Enabled = true
            },
            SubscriptionCancel = new ConfigurationFeaturesSubscriptionCancelOptions
            {
                Enabled = true,
                CancellationReason = new ConfigurationFeaturesSubscriptionCancelCancellationReasonOptions
                {
                    Enabled = true,
                    Options = new List<string> { "too_expensive", "missing_features", "switched_service", "other" }
                }
            }
        };

        if (products.Count > 0)
        {
            features.SubscriptionUpdate = new ConfigurationFeaturesSubscriptionUpdateOptions
            {
                Enabled = true,
                DefaultAllowedUpdates = new List<string> { "price", "promotion_code" },
                ProrationBehavior = "always_invoice",
                ScheduleAtPeriodEnd = new ConfigurationFeaturesSubscriptionUpdateScheduleAtPeriodEndOptions
                {
                    Conditions = new List<ConfigurationFeaturesSubscriptionUpdateScheduleAtPeriodEndConditionOptions>
        {
            new() { Type = "decreasing_item_amount" },
            new() { Type = "shortening_interval" }
        }
                },
                Products = products
            };
        }
        else
        {
            _logger.LogWarning("Nenhum preço recorrente ativo encontrado no Stripe. O portal será criado sem a opção de troca de plano.");
        }

        var options = new ConfigurationCreateOptions
        {
            BusinessProfile = new ConfigurationBusinessProfileOptions
            {
                Headline = "Gerencie sua assinatura PetClinix"
            },
            Features = features
        };

        var configuration = await _configurationService.CreateAsync(options, cancellationToken: cancellationToken);

        _logger.LogInformation("Configuração do portal do Stripe criada com troca de plano imediata: {ConfigurationId}", configuration.Id);

        return configuration.Id;
    }

    private async Task<List<ConfigurationFeaturesSubscriptionUpdateProductOptions>> ListProductsForPlanUpdatesAsync(CancellationToken cancellationToken)
    {
        var prices = await _priceService.ListAsync(
            new PriceListOptions { Active = true, Type = "recurring", Limit = 100 },
            cancellationToken: cancellationToken);

        return prices
            .Where(price => !string.IsNullOrEmpty(price.ProductId))
            .GroupBy(price => price.ProductId)
            .Select(group => new ConfigurationFeaturesSubscriptionUpdateProductOptions
            {
                Product = group.Key,
                Prices = group.Select(price => price.Id).ToList(),
                AdjustableQuantity = new ConfigurationFeaturesSubscriptionUpdateProductAdjustableQuantityOptions
                {
                    Enabled = false
                }
            })
            .ToList();
    }
}