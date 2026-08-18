using Microsoft.Extensions.DependencyInjection;
using StateMachinePlus.Commandes;
using StateMachinePlus.Execution;
using StateMachinePlus.Requetes;
using StateMachinePlus.Taches;

namespace StateMachinePlus.Definition.Constructeurs;

/// <summary>Configuration fluent d'un noeud systemique.</summary>
public sealed class ConstructeurNoeudSystemique
{
    private readonly NoeudSystemiqueDefinition _noeud;

    internal ConstructeurNoeudSystemique(NoeudSystemiqueDefinition noeud) => _noeud = noeud;

    /// <summary>
    /// Fabrique la commande a partir du contexte d'execution. Le gestionnaire
    /// <see cref="ICommandeHandler{TCommande}"/> correspondant est resolu depuis le conteneur de
    /// services du client au moment de l'execution du noeud.
    /// </summary>
    public ConstructeurNoeudSystemique Commande<TCommande>(Func<ContexteExecution, TCommande> fabrique)
        where TCommande : ICommande
    {
        ArgumentNullException.ThrowIfNull(fabrique);
        _noeud.Executer = async (ctx, connexion, jetonAnnulation) =>
        {
            var commande = fabrique(ctx);
            var gestionnaire = ctx.Services.GetRequiredService<ICommandeHandler<TCommande>>();
            await gestionnaire.ExecuterAsync(commande, connexion, jetonAnnulation);
        };
        return this;
    }

    /// <summary>Definit l'unique transition sortante de ce noeud.</summary>
    public void Vers(string noeudCibleId) => _noeud.Transitions.Add(new TransitionDefinition(noeudCibleId));
}

/// <summary>Configuration fluent d'un noeud decision.</summary>
public sealed class ConstructeurNoeudDecision
{
    /// <summary>Cle de branche par defaut lorsque le resultat de la requete est un booleen vrai.</summary>
    public const string CleVrai = "Vrai";

    /// <summary>Cle de branche par defaut lorsque le resultat de la requete est un booleen faux.</summary>
    public const string CleFaux = "Faux";

    private readonly NoeudDecisionDefinition _noeud;

    internal ConstructeurNoeudDecision(NoeudDecisionDefinition noeud) => _noeud = noeud;

    /// <summary>
    /// Fabrique la requete a partir du contexte d'execution, resout son gestionnaire
    /// <see cref="IRequeteHandler{TRequete, TResultat}"/> depuis le conteneur de services du
    /// client, l'execute puis derive la cle de branche du resultat via <paramref name="cleBranche"/>.
    /// </summary>
    public ConstructeurNoeudDecision Requete<TRequete, TResultat>(
        Func<ContexteExecution, TRequete> fabrique,
        Func<TResultat, string> cleBranche)
        where TRequete : IRequete<TResultat>
    {
        ArgumentNullException.ThrowIfNull(fabrique);
        ArgumentNullException.ThrowIfNull(cleBranche);
        _noeud.ExecuterEtBrancher = async (ctx, connexion, jetonAnnulation) =>
        {
            var requete = fabrique(ctx);
            var gestionnaire = ctx.Services.GetRequiredService<IRequeteHandler<TRequete, TResultat>>();
            var resultat = await gestionnaire.ExecuterAsync(requete, connexion, jetonAnnulation);
            return cleBranche(resultat);
        };
        return this;
    }

    /// <summary>Raccourci pour une requete retournant un booleen, branchee via <see cref="SiVrai"/>/<see cref="SiFaux"/>.</summary>
    public ConstructeurNoeudDecision RequeteBool<TRequete>(Func<ContexteExecution, TRequete> fabrique)
        where TRequete : IRequete<bool>
        => Requete<TRequete, bool>(fabrique, resultat => resultat ? CleVrai : CleFaux);

    /// <summary>Ajoute une branche : si la requete retourne <paramref name="cle"/>, transitionne vers <paramref name="noeudCibleId"/>.</summary>
    public ConstructeurNoeudDecision Branche(string cle, string noeudCibleId)
    {
        _noeud.Transitions.Add(new TransitionDefinition(noeudCibleId, cle));
        return this;
    }

    public ConstructeurNoeudDecision SiVrai(string noeudCibleId) => Branche(CleVrai, noeudCibleId);

    public ConstructeurNoeudDecision SiFaux(string noeudCibleId) => Branche(CleFaux, noeudCibleId);

    /// <summary>Transition utilisee si aucune branche ne correspond a la cle retournee par la requete.</summary>
    public void Sinon(string noeudCibleId) => _noeud.TransitionParDefaut = new TransitionDefinition(noeudCibleId);
}

/// <summary>Configuration fluent d'un noeud attente date/heure.</summary>
public sealed class ConstructeurNoeudAttenteDateHeure
{
    private readonly NoeudAttenteDateHeureDefinition _noeud;

    internal ConstructeurNoeudAttenteDateHeure(NoeudAttenteDateHeureDefinition noeud) => _noeud = noeud;

    /// <summary>Calcule la date/heure d'echeance (UTC) a partir du contexte d'execution.</summary>
    public ConstructeurNoeudAttenteDateHeure Echeance(Func<ContexteExecution, DateTime> calcul)
    {
        _noeud.CalculEcheance = calcul ?? throw new ArgumentNullException(nameof(calcul));
        return this;
    }

    /// <summary>Raccourci : echeance calculee comme un delai a partir de l'instant d'execution du noeud.</summary>
    public ConstructeurNoeudAttenteDateHeure EcheanceDans(Func<ContexteExecution, TimeSpan> calculDelai)
    {
        ArgumentNullException.ThrowIfNull(calculDelai);
        _noeud.CalculEcheance = ctx => DateTime.UtcNow + calculDelai(ctx);
        return this;
    }

    public void Vers(string noeudCibleId) => _noeud.Transitions.Add(new TransitionDefinition(noeudCibleId));
}

/// <summary>Configuration fluent d'un noeud attente signal.</summary>
public sealed class ConstructeurNoeudAttenteSignal
{
    private readonly NoeudAttenteSignalDefinition _noeud;

    internal ConstructeurNoeudAttenteSignal(NoeudAttenteSignalDefinition noeud) => _noeud = noeud;

    /// <summary>Nom du signal externe attendu pour reprendre le processus.</summary>
    public ConstructeurNoeudAttenteSignal Signal(string nomSignal)
    {
        if (string.IsNullOrWhiteSpace(nomSignal))
        {
            throw new ArgumentException("Le nom du signal est requis.", nameof(nomSignal));
        }

        _noeud.NomSignal = nomSignal;
        return this;
    }

    public void Vers(string noeudCibleId) => _noeud.Transitions.Add(new TransitionDefinition(noeudCibleId));
}

/// <summary>Configuration fluent d'un noeud tache utilisateur.</summary>
public sealed class ConstructeurNoeudTacheUtilisateur
{
    private readonly NoeudTacheUtilisateurDefinition _noeud;

    internal ConstructeurNoeudTacheUtilisateur(NoeudTacheUtilisateurDefinition noeud) => _noeud = noeud;

    /// <summary>
    /// Fabrique la demande de creation de tache, transmise a l'implementation cliente de
    /// <see cref="IFournisseurTacheUtilisateur"/> lors de l'execution du noeud.
    /// </summary>
    public ConstructeurNoeudTacheUtilisateur Tache(Func<ContexteExecution, DemandeCreationTache> fabrique)
    {
        _noeud.FabriqueTache = fabrique ?? throw new ArgumentNullException(nameof(fabrique));
        return this;
    }

    public void Vers(string noeudCibleId) => _noeud.Transitions.Add(new TransitionDefinition(noeudCibleId));
}

/// <summary>Configuration fluent d'un noeud sous-processus.</summary>
public sealed class ConstructeurNoeudSousProcessus
{
    private readonly NoeudSousProcessusDefinition _noeud;

    internal ConstructeurNoeudSousProcessus(NoeudSousProcessusDefinition noeud) => _noeud = noeud;

    /// <summary>
    /// Code de la definition du processus fils a demarrer, et fabrique optionnelle des variables
    /// initiales de l'instance enfant a partir du contexte du parent.
    /// </summary>
    public ConstructeurNoeudSousProcessus Processus(string codeProcessusFils, Func<ContexteExecution, object?>? variablesInitiales = null)
    {
        if (string.IsNullOrWhiteSpace(codeProcessusFils))
        {
            throw new ArgumentException("Le code du processus fils est requis.", nameof(codeProcessusFils));
        }

        _noeud.CodeProcessusFils = codeProcessusFils;
        _noeud.FabriqueVariablesInitiales = variablesInitiales;
        return this;
    }

    public void Vers(string noeudCibleId) => _noeud.Transitions.Add(new TransitionDefinition(noeudCibleId));
}
