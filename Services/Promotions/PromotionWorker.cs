using AldaJoyeros.Services.Interfaces;

namespace AldaJoyeros.Services.Promotions;
public sealed class PromotionWorker(IServiceScopeFactory scopes, PromotionStore store, ILogger<PromotionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                await store.RecoverAsync();
                var delivery = await store.ClaimAsync();
                if (delivery == null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(15), stop);
                    continue;
                }

                try
                {
                    if (!await store.EligibleAsync(delivery))
                    {
                        await store.FinishAsync(delivery, "omitido");
                        continue;
                    }

                    await using var scope = scopes.CreateAsyncScope();
                    var catalog = scope.ServiceProvider.GetRequiredService<CatalogoService>();
                    var current = await catalog.GetAsync();
                    var valid = new List<PromotionEmailProduct>();
                    foreach (var product in delivery.Products)
                    {
                        var item = product.SizeId.HasValue ? await catalog.DetailAsync(product.ProductId, product.SizeId) : current.SingleOrDefault(c => c.Id == product.ProductId);
                        if (item != null && item.TieneDescuento && (decimal)item.Precio == product.Price)
                            valid.Add(product with { Percent=item.DescuentoPorcentaje, Previous=(decimal)(item.PrecioAnterior??item.PrecioSinDescuento??item.Precio), BasePrice=(decimal?)item.PrecioSinDescuento });
                    }

                    if (valid.Count == 0)
                    {
                        await store.FinishAsync(delivery, "omitido");
                        continue;
                    }

                    if (!await store.EligibleAsync(delivery))
                        {
                            await store.FinishAsync(delivery, "omitido");
                            continue;
                        }
                        var mail = scope.ServiceProvider.GetRequiredService<PromotionMail>();
                    await scope.ServiceProvider.GetRequiredService<IEmailService>().SendEmailAsync(delivery.Email, delivery.Subject, PromotionMail.Render(delivery.Subject, delivery.BaseUrl, valid, mail.Token(delivery.UserId, delivery.Version)));
                    await store.FinishAsync(delivery, "enviado");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "No se confirmó el envío de la campaña {Campaign}", delivery.CampaignId);
                    await store.FinishAsync(delivery, "error");
                }

                await Task.Delay(TimeSpan.FromSeconds(2), stop);
            }
            catch (OperationCanceledException)when (stop.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "No se pudieron procesar las campañas");
                await Task.Delay(TimeSpan.FromSeconds(30), stop);
            }
        }
    }
}
