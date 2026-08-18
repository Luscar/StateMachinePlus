using StateMachinePlus.Execution;
using StateMachinePlus.Persistance;
using StateMachinePlus.Taches;

namespace StateMachinePlus.Definition;

/// <summary>Noeud de depart du processus. Ne possede qu'une transition sortante.</summary>
public sealed class NoeudDebutDefinition : NoeudDefinition
{
    public NoeudDebutDefinition(string id) : base(id)
    {
    }

    public override TypeNoeud Type => TypeNoeud.Debut;
}

/// <summary>Noeud de fin du processus. Ne possede aucune transition sortante.</summary>
public sealed class NoeudFinDefinition : NoeudDefinition
{
    public NoeudFinDefinition(string id) : base(id)
    {
    }

    public override TypeNoeud Type => TypeNoeud.Fin;
}

/// <summary>
/// Noeud systemique : execute une commande cote client (ecriture). Une seule transition sortante.
/// </summary>
public sealed class NoeudSystemiqueDefinition : NoeudDefinition
{
    public NoeudSystemiqueDefinition(string id) : base(id)
    {
    }

    public override TypeNoeud Type => TypeNoeud.Systemique;

    /// <summary>
    /// Construit la commande a partir du contexte, resout son gestionnaire via le conteneur de
    /// services du client et l'execute au travers de la connexion fournie par le moteur.
    /// </summary>
    public Func<ContexteExecution, IConnexionProcessus, CancellationToken, Task>? Executer { get; set; }
}

/// <summary>
/// Noeud decision : execute une requete cote client (lecture) et retourne la cle de branche
/// utilisee pour choisir la transition sortante a suivre.
/// </summary>
public sealed class NoeudDecisionDefinition : NoeudDefinition
{
    public NoeudDecisionDefinition(string id) : base(id)
    {
    }

    public override TypeNoeud Type => TypeNoeud.Decision;

    public Func<ContexteExecution, IConnexionProcessus, CancellationToken, Task<string>>? ExecuterEtBrancher { get; set; }

    /// <summary>Transition utilisee lorsque aucune branche ne correspond a la cle retournee.</summary>
    public TransitionDefinition? TransitionParDefaut { get; set; }
}

/// <summary>
/// Noeud attente date/heure : suspend l'instance jusqu'a l'echeance calculee. Une seule
/// transition sortante, empruntee lors de la reprise.
/// </summary>
public sealed class NoeudAttenteDateHeureDefinition : NoeudDefinition
{
    public NoeudAttenteDateHeureDefinition(string id) : base(id)
    {
    }

    public override TypeNoeud Type => TypeNoeud.AttenteDateHeure;

    public Func<ContexteExecution, DateTime>? CalculEcheance { get; set; }
}

/// <summary>
/// Noeud attente signal : suspend l'instance jusqu'a reception d'un signal externe nomme. Une
/// seule transition sortante, empruntee lors de la reprise.
/// </summary>
public sealed class NoeudAttenteSignalDefinition : NoeudDefinition
{
    public NoeudAttenteSignalDefinition(string id) : base(id)
    {
    }

    public override TypeNoeud Type => TypeNoeud.AttenteSignal;

    public string? NomSignal { get; set; }
}

/// <summary>
/// Noeud tache utilisateur : suspend l'instance et cree une tache dans le systeme externe. Une
/// seule transition sortante, empruntee lorsque la tache est completee.
/// </summary>
public sealed class NoeudTacheUtilisateurDefinition : NoeudDefinition
{
    public NoeudTacheUtilisateurDefinition(string id) : base(id)
    {
    }

    public override TypeNoeud Type => TypeNoeud.TacheUtilisateur;

    public Func<ContexteExecution, DemandeCreationTache>? FabriqueTache { get; set; }
}

/// <summary>
/// Noeud sous-processus : demarre une instance d'un autre processus. Si le sous-processus se
/// termine immediatement (sans suspendre), l'execution du parent continue dans la meme boucle et
/// donc dans la meme transaction ; sinon, le parent suspend jusqu'a la fin du sous-processus.
/// </summary>
public sealed class NoeudSousProcessusDefinition : NoeudDefinition
{
    public NoeudSousProcessusDefinition(string id) : base(id)
    {
    }

    public override TypeNoeud Type => TypeNoeud.SousProcessus;

    public string? CodeProcessusFils { get; set; }

    public Func<ContexteExecution, object?>? FabriqueVariablesInitiales { get; set; }
}
