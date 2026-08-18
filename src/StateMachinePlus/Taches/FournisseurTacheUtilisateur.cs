namespace StateMachinePlus.Taches;

/// <summary>
/// Demande de creation d'une tache utilisateur, transmise au systeme externe de gestion de taches.
/// </summary>
public sealed class DemandeCreationTache
{
    public required string Titre { get; init; }
    public string? Description { get; init; }
    public string? Assignation { get; init; }
    public IDictionary<string, object?> Donnees { get; init; } = new Dictionary<string, object?>();
}

/// <summary>
/// Integration cote client avec le systeme externe de gestion de taches utilisateur
/// (ex : outil de ticketing, systeme de taches internes, etc.).
/// </summary>
public interface IFournisseurTacheUtilisateur
{
    /// <summary>
    /// Cree la tache dans le systeme externe et retourne son identifiant, qui sera utilise
    /// pour correler la completion de la tache avec la reprise du processus.
    /// </summary>
    Task<string> CreerAsync(DemandeCreationTache demande, Persistance.IConnexionProcessus connexion, CancellationToken jetonAnnulation);
}
