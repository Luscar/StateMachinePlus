using Microsoft.Extensions.DependencyInjection;
using StateMachinePlus.Definition;
using StateMachinePlus.Execution;
using StateMachinePlus.Persistance;

namespace StateMachinePlus.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Enregistre les services de StateMachinePlus : registre de definitions (singleton),
    /// repositories Dapper (scoped) et moteur d'execution (scoped).
    ///
    /// Le client doit en complement enregistrer lui-meme : <see cref="IConnexionProcessus"/>,
    /// ses <c>ICommandeHandler&lt;T&gt;</c> / <c>IRequeteHandler&lt;T, TResultat&gt;</c>, et
    /// eventuellement <see cref="Taches.IFournisseurTacheUtilisateur"/> si des noeuds tache
    /// utilisateur sont utilises.
    /// </summary>
    /// <param name="services">Collection de services du client.</param>
    /// <param name="configurerProcessus">
    /// Delegue invoque une seule fois, au demarrage, pour enregistrer les definitions de
    /// processus (construites via <see cref="ConstructeurProcessus"/>) dans le registre.
    /// </param>
    public static IServiceCollection AjouterStateMachinePlus(
        this IServiceCollection services, Action<IDefinitionProcessusRegistre>? configurerProcessus = null)
    {
        services.AddSingleton<IDefinitionProcessusRegistre>(_ =>
        {
            var registre = new DefinitionProcessusRegistre();
            configurerProcessus?.Invoke(registre);
            return registre;
        });

        services.AddScoped<IInstanceProcessusRepository, InstanceProcessusRepository>();
        services.AddScoped<IHistoriqueExecutionRepository, HistoriqueExecutionRepository>();
        services.AddScoped<IMoteurProcessus, MoteurProcessus>();

        return services;
    }
}
