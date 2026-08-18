using StateMachinePlus.Definition;
using StateMachinePlus.Persistance;
using StateMachinePlus.Taches;

namespace StateMachinePlus.Execution;

/// <summary>
/// Moteur d'execution des processus. Fait progresser une instance a travers la chaine de noeuds
/// jusqu'a un noeud en suspens (attente date/heure, attente signal, tache utilisateur ou
/// sous-processus non termine) ou jusqu'a un noeud de fin.
///
/// Toutes les operations de persistance realisees pendant un appel (etat de l'instance,
/// historique, et effets des commandes/requetes cote client qui utilisent la
/// <see cref="IConnexionProcessus"/> fournie) passent par la meme connexion/transaction : si une
/// exception est levee en cours de route, rien n'est valide et l'instance reste, en base, dans
/// l'etat de son dernier point de suspension reussi. C'est au code appelant (proprietaire de la
/// transaction) de la committer en cas de succes ou de la faire rollback en cas d'echec.
/// </summary>
public interface IMoteurProcessus
{
    /// <summary>Demarre une nouvelle instance du processus identifie par <paramref name="codeProcessus"/>.</summary>
    Task<InstanceProcessus> DemarrerAsync(
        string codeProcessus, object? variablesInitiales, IConnexionProcessus connexion, CancellationToken jetonAnnulation = default);

    /// <summary>Reprend une instance suspendue sur un noeud attente date/heure dont l'echeance est atteinte.</summary>
    Task<InstanceProcessus> ReprendreAsync(Guid instanceId, IConnexionProcessus connexion, CancellationToken jetonAnnulation = default);

    /// <summary>Reprend une instance suspendue sur un noeud attente signal, en recevant le signal attendu.</summary>
    Task<InstanceProcessus> RecevoirSignalAsync(
        Guid instanceId, string nomSignal, object? donnees, IConnexionProcessus connexion, CancellationToken jetonAnnulation = default);

    /// <summary>Reprend une instance suspendue sur un noeud tache utilisateur, une fois la tache externe completee.</summary>
    Task<InstanceProcessus> CompleterTacheAsync(
        Guid instanceId, string idTacheExterne, object? resultat, IConnexionProcessus connexion, CancellationToken jetonAnnulation = default);
}

public sealed class MoteurProcessus : IMoteurProcessus
{
    private readonly IDefinitionProcessusRegistre _registre;
    private readonly IInstanceProcessusRepository _instances;
    private readonly IHistoriqueExecutionRepository _historique;
    private readonly IServiceProvider _services;
    private readonly IFournisseurTacheUtilisateur? _fournisseurTaches;

    public MoteurProcessus(
        IDefinitionProcessusRegistre registre,
        IInstanceProcessusRepository instances,
        IHistoriqueExecutionRepository historique,
        IServiceProvider services,
        IFournisseurTacheUtilisateur? fournisseurTaches = null)
    {
        _registre = registre;
        _instances = instances;
        _historique = historique;
        _services = services;
        _fournisseurTaches = fournisseurTaches;
    }

    public Task<InstanceProcessus> DemarrerAsync(
        string codeProcessus, object? variablesInitiales, IConnexionProcessus connexion, CancellationToken jetonAnnulation = default)
    {
        var definition = _registre.Obtenir(codeProcessus);
        var instance = InstanceProcessus.CreerNouvelle(
            definition.Code, definition.Version, definition.NoeudDebutId, variablesInitiales);

        return ExecuterEtPropagerAsync(definition, instance, connexion, jetonAnnulation);
    }

    public async Task<InstanceProcessus> ReprendreAsync(Guid instanceId, IConnexionProcessus connexion, CancellationToken jetonAnnulation = default)
    {
        var instance = await ObtenirInstanceAsync(instanceId, connexion, jetonAnnulation);
        if (instance.Statut != StatutInstance.EnAttenteDateHeure)
        {
            throw new StatutInstanceInvalideException(instanceId,
                $"L'instance '{instanceId}' n'est pas en attente d'une date/heure (statut actuel : {instance.Statut}).");
        }

        instance.DateEcheance = null;
        return await PoursuivreDepuisAsync(instance, connexion, jetonAnnulation);
    }

    public async Task<InstanceProcessus> RecevoirSignalAsync(
        Guid instanceId, string nomSignal, object? donnees, IConnexionProcessus connexion, CancellationToken jetonAnnulation = default)
    {
        var instance = await ObtenirInstanceAsync(instanceId, connexion, jetonAnnulation);
        if (instance.Statut != StatutInstance.EnAttenteSignal ||
            !string.Equals(instance.NomSignalAttendu, nomSignal, StringComparison.Ordinal))
        {
            throw new StatutInstanceInvalideException(instanceId,
                $"L'instance '{instanceId}' n'attend pas le signal '{nomSignal}' " +
                $"(statut actuel : {instance.Statut}, signal attendu : {instance.NomSignalAttendu ?? "aucun"}).");
        }

        instance.Variables.FusionnerDepuis(donnees);
        instance.NomSignalAttendu = null;
        return await PoursuivreDepuisAsync(instance, connexion, jetonAnnulation);
    }

    public async Task<InstanceProcessus> CompleterTacheAsync(
        Guid instanceId, string idTacheExterne, object? resultat, IConnexionProcessus connexion, CancellationToken jetonAnnulation = default)
    {
        var instance = await ObtenirInstanceAsync(instanceId, connexion, jetonAnnulation);
        if (instance.Statut != StatutInstance.EnAttenteTacheUtilisateur ||
            !string.Equals(instance.IdTacheExterne, idTacheExterne, StringComparison.Ordinal))
        {
            throw new StatutInstanceInvalideException(instanceId,
                $"L'instance '{instanceId}' n'attend pas la tache '{idTacheExterne}' " +
                $"(statut actuel : {instance.Statut}, tache attendue : {instance.IdTacheExterne ?? "aucune"}).");
        }

        instance.Variables.FusionnerDepuis(resultat);
        instance.IdTacheExterne = null;
        return await PoursuivreDepuisAsync(instance, connexion, jetonAnnulation);
    }

    private async Task<InstanceProcessus> ObtenirInstanceAsync(Guid instanceId, IConnexionProcessus connexion, CancellationToken jetonAnnulation)
    {
        return await _instances.ObtenirAsync(instanceId, connexion, jetonAnnulation)
               ?? throw new InstanceIntrouvableException(instanceId);
    }

    /// <summary>Fait avancer une instance suspendue vers le noeud suivant sa transition unique, puis relance la boucle.</summary>
    private Task<InstanceProcessus> PoursuivreDepuisAsync(InstanceProcessus instance, IConnexionProcessus connexion, CancellationToken jetonAnnulation)
    {
        var definition = _registre.Obtenir(instance.CodeDefinition, instance.VersionDefinition);
        var noeudActuel = definition.ObtenirNoeud(instance.NoeudCourantId);
        instance.NoeudCourantId = noeudActuel.ObtenirTransitionUnique().NoeudCibleId;
        instance.Statut = StatutInstance.EnCours;

        return ExecuterEtPropagerAsync(definition, instance, connexion, jetonAnnulation);
    }

    /// <summary>
    /// Execute la boucle d'une instance puis, si elle se termine et qu'elle a ete demarree par un
    /// noeud sous-processus d'un parent suspendu, propage automatiquement la reprise au parent
    /// (dans la meme connexion, donc la meme transaction).
    /// </summary>
    private async Task<InstanceProcessus> ExecuterEtPropagerAsync(
        DefinitionProcessus definition, InstanceProcessus instance, IConnexionProcessus connexion, CancellationToken jetonAnnulation)
    {
        var resultat = await ExecuterBoucleAsync(definition, instance, connexion, jetonAnnulation);

        if (resultat.Statut == StatutInstance.Termine && resultat.InstanceParentId.HasValue)
        {
            var parent = await _instances.ObtenirAsync(resultat.InstanceParentId.Value, connexion, jetonAnnulation);
            if (parent is not null && parent.Statut == StatutInstance.EnAttenteSousProcessus)
            {
                await PoursuivreDepuisAsync(parent, connexion, jetonAnnulation);
            }
        }

        return resultat;
    }

    private async Task<InstanceProcessus> ExecuterBoucleAsync(
        DefinitionProcessus definition, InstanceProcessus instance, IConnexionProcessus connexion, CancellationToken jetonAnnulation)
    {
        var noeudCourant = definition.ObtenirNoeud(instance.NoeudCourantId);

        while (true)
        {
            jetonAnnulation.ThrowIfCancellationRequested();
            instance.NoeudCourantId = noeudCourant.Id;
            var contexte = new ContexteExecution(instance, _services);

            switch (noeudCourant)
            {
                case NoeudDebutDefinition debut:
                    noeudCourant = definition.ObtenirNoeud(debut.ObtenirTransitionUnique().NoeudCibleId);
                    continue;

                case NoeudFinDefinition fin:
                    instance.Statut = StatutInstance.Termine;
                    await _instances.EnregistrerAsync(instance, connexion, jetonAnnulation);
                    await HistoriserAsync(instance, fin, succes: true, message: null, connexion, jetonAnnulation);
                    return instance;

                case NoeudSystemiqueDefinition systemique:
                    await systemique.Executer!(contexte, connexion, jetonAnnulation);
                    await HistoriserAsync(instance, systemique, succes: true, message: null, connexion, jetonAnnulation);
                    noeudCourant = definition.ObtenirNoeud(systemique.ObtenirTransitionUnique().NoeudCibleId);
                    continue;

                case NoeudDecisionDefinition decision:
                    var cleBranche = await decision.ExecuterEtBrancher!(contexte, connexion, jetonAnnulation);
                    var transitionChoisie = decision.Transitions.FirstOrDefault(t => t.Condition == cleBranche)
                                             ?? decision.TransitionParDefaut
                                             ?? throw new BrancheIntrouvableException(decision.Id, cleBranche);
                    await HistoriserAsync(instance, decision, succes: true, $"Branche : {cleBranche}", connexion, jetonAnnulation);
                    noeudCourant = definition.ObtenirNoeud(transitionChoisie.NoeudCibleId);
                    continue;

                case NoeudAttenteDateHeureDefinition attenteDate:
                    instance.DateEcheance = attenteDate.CalculEcheance!(contexte);
                    instance.Statut = StatutInstance.EnAttenteDateHeure;
                    await _instances.EnregistrerAsync(instance, connexion, jetonAnnulation);
                    await HistoriserAsync(instance, attenteDate, succes: true,
                        $"En attente jusqu'au {instance.DateEcheance:O}", connexion, jetonAnnulation);
                    return instance;

                case NoeudAttenteSignalDefinition attenteSignal:
                    instance.NomSignalAttendu = attenteSignal.NomSignal;
                    instance.Statut = StatutInstance.EnAttenteSignal;
                    await _instances.EnregistrerAsync(instance, connexion, jetonAnnulation);
                    await HistoriserAsync(instance, attenteSignal, succes: true,
                        $"En attente du signal '{attenteSignal.NomSignal}'", connexion, jetonAnnulation);
                    return instance;

                case NoeudTacheUtilisateurDefinition tacheUtilisateur:
                    if (_fournisseurTaches is null)
                    {
                        throw new InvalidOperationException(
                            $"Le noeud tache utilisateur '{tacheUtilisateur.Id}' necessite un " +
                            $"{nameof(IFournisseurTacheUtilisateur)} enregistre dans les services du client.");
                    }

                    var demande = tacheUtilisateur.FabriqueTache!(contexte);
                    instance.IdTacheExterne = await _fournisseurTaches.CreerAsync(demande, connexion, jetonAnnulation);
                    instance.Statut = StatutInstance.EnAttenteTacheUtilisateur;
                    await _instances.EnregistrerAsync(instance, connexion, jetonAnnulation);
                    await HistoriserAsync(instance, tacheUtilisateur, succes: true,
                        $"Tache creee : {instance.IdTacheExterne}", connexion, jetonAnnulation);
                    return instance;

                case NoeudSousProcessusDefinition sousProcessus:
                    var definitionEnfant = _registre.Obtenir(sousProcessus.CodeProcessusFils!);
                    var variablesEnfant = sousProcessus.FabriqueVariablesInitiales?.Invoke(contexte);
                    var instanceEnfant = InstanceProcessus.CreerNouvelle(
                        definitionEnfant.Code, definitionEnfant.Version, definitionEnfant.NoeudDebutId,
                        variablesEnfant, instance.Id, sousProcessus.Id);

                    var resultatEnfant = await ExecuterBoucleAsync(definitionEnfant, instanceEnfant, connexion, jetonAnnulation);
                    await HistoriserAsync(instance, sousProcessus, succes: true,
                        $"Sous-processus '{sousProcessus.CodeProcessusFils}' -> {resultatEnfant.Statut}", connexion, jetonAnnulation);

                    if (resultatEnfant.Statut != StatutInstance.Termine)
                    {
                        instance.Statut = StatutInstance.EnAttenteSousProcessus;
                        await _instances.EnregistrerAsync(instance, connexion, jetonAnnulation);
                        return instance;
                    }

                    noeudCourant = definition.ObtenirNoeud(sousProcessus.ObtenirTransitionUnique().NoeudCibleId);
                    continue;

                default:
                    throw new NotSupportedException($"Type de noeud non pris en charge : {noeudCourant.GetType().Name}");
            }
        }
    }

    private async Task HistoriserAsync(
        InstanceProcessus instance, NoeudDefinition noeud, bool succes, string? message, IConnexionProcessus connexion, CancellationToken jetonAnnulation)
    {
        var entree = HistoriqueExecution.Creer(instance.Id, noeud.Id, noeud.Type.ToString(), succes, message);
        await _historique.AjouterAsync(entree, connexion, jetonAnnulation);
    }
}
