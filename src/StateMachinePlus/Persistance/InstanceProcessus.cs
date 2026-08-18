using StateMachinePlus.Execution;

namespace StateMachinePlus.Persistance;

/// <summary>
/// Etat persiste d'une instance en cours (ou terminee) d'un processus.
/// </summary>
public sealed class InstanceProcessus
{
    public Guid Id { get; set; }

    public string CodeDefinition { get; set; } = string.Empty;

    public int VersionDefinition { get; set; }

    public string NoeudCourantId { get; set; } = string.Empty;

    public StatutInstance Statut { get; set; }

    public VariablesProcessus Variables { get; set; } = new();

    public DateTime DateCreation { get; set; }

    public DateTime DateModification { get; set; }

    /// <summary>Echeance attendue pour un noeud d'attente date/heure.</summary>
    public DateTime? DateEcheance { get; set; }

    /// <summary>Nom du signal attendu pour un noeud d'attente signal.</summary>
    public string? NomSignalAttendu { get; set; }

    /// <summary>Identifiant de la tache externe attendue pour un noeud tache utilisateur.</summary>
    public string? IdTacheExterne { get; set; }

    /// <summary>Instance parente lorsque ce processus a ete demarre par un noeud sous-processus.</summary>
    public Guid? InstanceParentId { get; set; }

    /// <summary>Identifiant du noeud sous-processus, dans le processus parent, qui a demarre cette instance.</summary>
    public string? NoeudParentId { get; set; }

    /// <summary>Message d'erreur en cas de <see cref="StatutInstance.Erreur"/>.</summary>
    public string? MessageErreur { get; set; }

    public static InstanceProcessus CreerNouvelle(
        string codeDefinition,
        int versionDefinition,
        string noeudDebutId,
        object? variablesInitiales,
        Guid? instanceParentId = null,
        string? noeudParentId = null)
    {
        var maintenant = DateTime.UtcNow;
        return new InstanceProcessus
        {
            Id = Guid.NewGuid(),
            CodeDefinition = codeDefinition,
            VersionDefinition = versionDefinition,
            NoeudCourantId = noeudDebutId,
            Statut = StatutInstance.EnCours,
            Variables = VariablesProcessus.DepuisObjet(variablesInitiales),
            DateCreation = maintenant,
            DateModification = maintenant,
            InstanceParentId = instanceParentId,
            NoeudParentId = noeudParentId,
        };
    }
}
