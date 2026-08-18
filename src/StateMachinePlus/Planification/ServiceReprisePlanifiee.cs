using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StateMachinePlus.Execution;
using StateMachinePlus.Persistance;

namespace StateMachinePlus.Planification;

/// <summary>Options de <see cref="ServiceReprisePlanifiee"/>.</summary>
public sealed class OptionsPlanificateurReprise
{
    /// <summary>Intervalle entre deux verifications des instances en attente de date/heure echue.</summary>
    public TimeSpan Intervalle { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Service d'arriere-plan optionnel qui reprend periodiquement les instances suspendues sur un
/// noeud attente date/heure dont l'echeance est atteinte.
///
/// Non enregistre automatiquement par <see cref="Extensions.ServiceCollectionExtensions.AjouterStateMachinePlus"/> :
/// le client doit l'ajouter explicitement (<c>services.AddHostedService&lt;ServiceReprisePlanifiee&gt;()</c>)
/// et enregistrer une <see cref="IConnexionProcessus"/> resolvable en tant que service scoped
/// (typiquement une nouvelle connexion, avec sa propre transaction, ouverte pour chaque cycle).
/// </summary>
public sealed class ServiceReprisePlanifiee : BackgroundService
{
    private readonly IServiceScopeFactory _fabriqueDePortee;
    private readonly ILogger<ServiceReprisePlanifiee> _journal;
    private readonly OptionsPlanificateurReprise _options;

    public ServiceReprisePlanifiee(
        IServiceScopeFactory fabriqueDePortee,
        ILogger<ServiceReprisePlanifiee> journal,
        OptionsPlanificateurReprise? options = null)
    {
        _fabriqueDePortee = fabriqueDePortee;
        _journal = journal;
        _options = options ?? new OptionsPlanificateurReprise();
    }

    protected override async Task ExecuteAsync(CancellationToken jetonArret)
    {
        while (!jetonArret.IsCancellationRequested)
        {
            try
            {
                await ReprendreInstancesEchuesAsync(jetonArret);
            }
            catch (Exception exception) when (!jetonArret.IsCancellationRequested)
            {
                _journal.LogError(exception, "Echec lors de la reprise planifiee des instances en attente de date/heure.");
            }

            try
            {
                await Task.Delay(_options.Intervalle, jetonArret);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ReprendreInstancesEchuesAsync(CancellationToken jetonAnnulation)
    {
        using var portee = _fabriqueDePortee.CreateScope();
        var connexion = portee.ServiceProvider.GetRequiredService<IConnexionProcessus>();
        var instances = portee.ServiceProvider.GetRequiredService<IInstanceProcessusRepository>();
        var moteur = portee.ServiceProvider.GetRequiredService<IMoteurProcessus>();

        var instancesEchues = await instances.ObtenirEnAttenteDateEchueAsync(DateTime.UtcNow, connexion, jetonAnnulation);
        foreach (var instance in instancesEchues)
        {
            await moteur.ReprendreAsync(instance.Id, connexion, jetonAnnulation);
        }
    }
}
