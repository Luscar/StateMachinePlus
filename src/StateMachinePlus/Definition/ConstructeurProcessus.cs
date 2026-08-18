using StateMachinePlus.Definition.Constructeurs;

namespace StateMachinePlus.Definition;

/// <summary>
/// Constructeur fluent (code-first) d'une <see cref="DefinitionProcessus"/>.
/// </summary>
/// <example>
/// <code>
/// var constructeur = new ConstructeurProcessus("CommandeClient", version: 1);
/// constructeur.Debut("debut", "verifierStock");
///
/// constructeur.NoeudSystemique("verifierStock")
///     .Commande(ctx => new ReserverStockCommande(ctx.Variables.ObtenirRequis&lt;Guid&gt;("commandeId")))
///     .Vers("decisionPaiement");
///
/// constructeur.NoeudDecision("decisionPaiement")
///     .RequeteBool(ctx => new PaiementValideRequete(ctx.Variables.ObtenirRequis&lt;Guid&gt;("commandeId")))
///     .SiVrai("attenteExpedition")
///     .SiFaux("fin");
///
/// constructeur.NoeudAttenteSignal("attenteExpedition")
///     .Signal("ExpeditionConfirmee")
///     .Vers("fin");
///
/// constructeur.Fin("fin");
///
/// var definition = constructeur.Construire();
/// </code>
/// </example>
public sealed class ConstructeurProcessus
{
    private readonly string _code;
    private readonly int _version;
    private readonly Dictionary<string, NoeudDefinition> _noeuds = new();
    private string? _noeudDebutId;

    public ConstructeurProcessus(string code, int version = 1)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Le code du processus est requis.", nameof(code));
        }

        _code = code;
        _version = version;
    }

    /// <summary>Definit le noeud de depart et sa transition vers le premier noeud metier.</summary>
    public ConstructeurProcessus Debut(string noeudId, string noeudSuivantId)
    {
        if (_noeudDebutId is not null)
        {
            throw new InvalidOperationException("Un seul noeud de debut est autorise par processus.");
        }

        var noeud = new NoeudDebutDefinition(noeudId);
        noeud.Transitions.Add(new TransitionDefinition(noeudSuivantId));
        AjouterNoeud(noeud);
        _noeudDebutId = noeudId;
        return this;
    }

    /// <summary>Declare un noeud de fin.</summary>
    public ConstructeurProcessus Fin(string noeudId)
    {
        AjouterNoeud(new NoeudFinDefinition(noeudId));
        return this;
    }

    public ConstructeurNoeudSystemique NoeudSystemique(string noeudId)
    {
        var noeud = new NoeudSystemiqueDefinition(noeudId);
        AjouterNoeud(noeud);
        return new ConstructeurNoeudSystemique(noeud);
    }

    public ConstructeurNoeudDecision NoeudDecision(string noeudId)
    {
        var noeud = new NoeudDecisionDefinition(noeudId);
        AjouterNoeud(noeud);
        return new ConstructeurNoeudDecision(noeud);
    }

    public ConstructeurNoeudAttenteDateHeure NoeudAttenteDateHeure(string noeudId)
    {
        var noeud = new NoeudAttenteDateHeureDefinition(noeudId);
        AjouterNoeud(noeud);
        return new ConstructeurNoeudAttenteDateHeure(noeud);
    }

    public ConstructeurNoeudAttenteSignal NoeudAttenteSignal(string noeudId)
    {
        var noeud = new NoeudAttenteSignalDefinition(noeudId);
        AjouterNoeud(noeud);
        return new ConstructeurNoeudAttenteSignal(noeud);
    }

    public ConstructeurNoeudTacheUtilisateur NoeudTacheUtilisateur(string noeudId)
    {
        var noeud = new NoeudTacheUtilisateurDefinition(noeudId);
        AjouterNoeud(noeud);
        return new ConstructeurNoeudTacheUtilisateur(noeud);
    }

    public ConstructeurNoeudSousProcessus NoeudSousProcessus(string noeudId)
    {
        var noeud = new NoeudSousProcessusDefinition(noeudId);
        AjouterNoeud(noeud);
        return new ConstructeurNoeudSousProcessus(noeud);
    }

    /// <summary>Valide le graphe et construit la definition immuable.</summary>
    public DefinitionProcessus Construire()
    {
        if (_noeudDebutId is null)
        {
            throw new InvalidOperationException(
                $"Le processus '{_code}' n'a pas de noeud de debut. Appelez Debut(...) avant Construire().");
        }

        Valider();

        return new DefinitionProcessus(_code, _version, _noeudDebutId, _noeuds);
    }

    private void AjouterNoeud(NoeudDefinition noeud)
    {
        if (!_noeuds.TryAdd(noeud.Id, noeud))
        {
            throw new InvalidOperationException(
                $"Un noeud avec l'identifiant '{noeud.Id}' existe deja dans le processus '{_code}'.");
        }
    }

    private void Valider()
    {
        foreach (var noeud in _noeuds.Values)
        {
            switch (noeud)
            {
                case NoeudFinDefinition:
                    break;

                case NoeudDebutDefinition:
                    ValiderCible(noeud, noeud.ObtenirTransitionUnique());
                    break;

                case NoeudSystemiqueDefinition sys:
                    if (sys.Executer is null)
                    {
                        throw new InvalidOperationException(
                            $"Le noeud systemique '{sys.Id}' doit definir une commande via .Commande(...).");
                    }

                    ValiderCible(noeud, noeud.ObtenirTransitionUnique());
                    break;

                case NoeudDecisionDefinition dec:
                    if (dec.ExecuterEtBrancher is null)
                    {
                        throw new InvalidOperationException(
                            $"Le noeud decision '{dec.Id}' doit definir une requete via .Requete(...) ou .RequeteBool(...).");
                    }

                    if (dec.Transitions.Count == 0 && dec.TransitionParDefaut is null)
                    {
                        throw new InvalidOperationException(
                            $"Le noeud decision '{dec.Id}' doit definir au moins une branche.");
                    }

                    foreach (var transition in dec.Transitions)
                    {
                        ValiderCible(noeud, transition);
                    }

                    if (dec.TransitionParDefaut is not null)
                    {
                        ValiderCible(noeud, dec.TransitionParDefaut);
                    }

                    break;

                case NoeudAttenteDateHeureDefinition att:
                    if (att.CalculEcheance is null)
                    {
                        throw new InvalidOperationException(
                            $"Le noeud attente date/heure '{att.Id}' doit definir une echeance via .Echeance(...) ou .EcheanceDans(...).");
                    }

                    ValiderCible(noeud, noeud.ObtenirTransitionUnique());
                    break;

                case NoeudAttenteSignalDefinition sig:
                    if (string.IsNullOrWhiteSpace(sig.NomSignal))
                    {
                        throw new InvalidOperationException(
                            $"Le noeud attente signal '{sig.Id}' doit definir un nom de signal via .Signal(...).");
                    }

                    ValiderCible(noeud, noeud.ObtenirTransitionUnique());
                    break;

                case NoeudTacheUtilisateurDefinition tache:
                    if (tache.FabriqueTache is null)
                    {
                        throw new InvalidOperationException(
                            $"Le noeud tache utilisateur '{tache.Id}' doit definir une tache via .Tache(...).");
                    }

                    ValiderCible(noeud, noeud.ObtenirTransitionUnique());
                    break;

                case NoeudSousProcessusDefinition sp:
                    if (string.IsNullOrWhiteSpace(sp.CodeProcessusFils))
                    {
                        throw new InvalidOperationException(
                            $"Le noeud sous-processus '{sp.Id}' doit definir un processus fils via .Processus(...).");
                    }

                    ValiderCible(noeud, noeud.ObtenirTransitionUnique());
                    break;
            }
        }
    }

    private void ValiderCible(NoeudDefinition noeud, TransitionDefinition transition)
    {
        if (!_noeuds.ContainsKey(transition.NoeudCibleId))
        {
            throw new InvalidOperationException(
                $"Le noeud '{noeud.Id}' du processus '{_code}' reference un noeud cible inconnu : '{transition.NoeudCibleId}'.");
        }
    }
}
