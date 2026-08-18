namespace StateMachinePlus.Commandes;

/// <summary>
/// Marqueur pour une commande executee par un noeud systemique. Une commande represente
/// une intention d'ecriture cote client (modification d'etat metier).
/// </summary>
public interface ICommande
{
}

/// <summary>
/// Gestionnaire cote client charge d'executer une <see cref="ICommande"/>.
/// Doit etre enregistre dans le conteneur de services du client (DI).
/// </summary>
/// <typeparam name="TCommande">Type de commande pris en charge.</typeparam>
public interface ICommandeHandler<in TCommande> where TCommande : ICommande
{
    /// <summary>
    /// Execute la commande. La <paramref name="connexion"/> fournie est celle utilisee par le
    /// moteur pour persister l'instance de processus : ecrire au travers d'elle garantit que
    /// les effets de la commande font partie de la meme transaction que l'avancement du processus.
    /// </summary>
    Task ExecuterAsync(TCommande commande, Persistance.IConnexionProcessus connexion, CancellationToken jetonAnnulation);
}
