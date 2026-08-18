using Dapper;
using Microsoft.Extensions.DependencyInjection;
using StateMachinePlus.Commandes;
using StateMachinePlus.Definition;
using StateMachinePlus.Execution;
using StateMachinePlus.Persistance;
using StateMachinePlus.Requetes;
using StateMachinePlus.Taches;
using Xunit;

namespace StateMachinePlus.Tests;

public sealed class MoteurProcessusTests
{
    private static (IDefinitionProcessusRegistre Registre, IServiceProvider Services, FournisseurTacheUtilisateurTest Taches,
        IInstanceProcessusRepository Instances, IHistoriqueExecutionRepository Historique, IMoteurProcessus Moteur)
        ConstruireEnvironnement(Action<IDefinitionProcessusRegistre> enregistrerProcessus)
    {
        var registre = new DefinitionProcessusRegistre();
        enregistrerProcessus(registre);

        var collection = new ServiceCollection();
        collection.AddSingleton<ICommandeHandler<ReserverStockCommande>, ReserverStockCommandeHandler>();
        collection.AddSingleton<ICommandeHandler<EnvoyerNotificationCommande>, EnvoyerNotificationCommandeHandler>();
        collection.AddSingleton<ICommandeHandler<CommandeQuiEchoue>, CommandeQuiEchoueHandler>();
        collection.AddSingleton<IRequeteHandler<PaiementValideRequete, bool>, PaiementValideRequeteHandler>();
        var services = collection.BuildServiceProvider();

        var instances = new InstanceProcessusRepository();
        var historique = new HistoriqueExecutionRepository();
        var taches = new FournisseurTacheUtilisateurTest();
        var moteur = new MoteurProcessus(registre, instances, historique, services, taches);

        return (registre, services, taches, instances, historique, moteur);
    }

    private static DefinitionProcessus DefinirProcessusPrincipal(ConstructeurProcessus c)
    {
        c.Debut("debut", "verifierStock");

        c.NoeudSystemique("verifierStock")
            .Commande(ctx => new ReserverStockCommande(ctx.Variables.ObtenirRequis<Guid>("commandeId")))
            .Vers("decisionPaiement");

        c.NoeudDecision("decisionPaiement")
            .RequeteBool(ctx => new PaiementValideRequete(ctx.Variables.ObtenirRequis<Guid>("commandeId")))
            .SiVrai("attenteConfirmation")
            .SiFaux("finRefus");

        c.NoeudAttenteSignal("attenteConfirmation")
            .Signal("ConfirmationRecue")
            .Vers("tacheValidation");

        c.NoeudTacheUtilisateur("tacheValidation")
            .Tache(ctx => new DemandeCreationTache { Titre = "Valider la commande" })
            .Vers("sousProcessusNotification");

        c.NoeudSousProcessus("sousProcessusNotification")
            .Processus("Notification")
            .Vers("fin");

        c.Fin("fin");
        c.Fin("finRefus");

        return c.Construire();
    }

    private static DefinitionProcessus DefinirSousProcessusNotification(ConstructeurProcessus c)
    {
        c.Debut("debut", "envoyer");
        c.NoeudSystemique("envoyer").Commande(ctx => new EnvoyerNotificationCommande("Bonjour")).Vers("fin");
        c.Fin("fin");
        return c.Construire();
    }

    [Fact]
    public async Task CheminComplet_TraverseTousLesTypesDeNoeudsJusquaLaFin()
    {
        var env = ConstruireEnvironnement(registre =>
        {
            registre.Enregistrer(DefinirSousProcessusNotification(new ConstructeurProcessus("Notification")));
            registre.Enregistrer(DefinirProcessusPrincipal(new ConstructeurProcessus("ProcessusTest")));
        });

        var commandeId = Guid.NewGuid();
        PaiementValideRequeteHandler.Configurer(commandeId, valide: true);

        using var cnx = new ConnexionProcessusTest();

        cnx.DemarrerTransaction();
        var instance = await env.Moteur.DemarrerAsync("ProcessusTest", new { commandeId }, cnx, CancellationToken.None);
        cnx.Committer();

        Assert.Equal(StatutInstance.EnAttenteSignal, instance.Statut);
        Assert.Equal("ConfirmationRecue", instance.NomSignalAttendu);

        cnx.DemarrerTransaction();
        instance = await env.Moteur.RecevoirSignalAsync(instance.Id, "ConfirmationRecue", null, cnx, CancellationToken.None);
        cnx.Committer();

        Assert.Equal(StatutInstance.EnAttenteTacheUtilisateur, instance.Statut);
        Assert.Single(env.Taches.TachesCreees);
        var idTache = instance.IdTacheExterne!;

        cnx.DemarrerTransaction();
        instance = await env.Moteur.CompleterTacheAsync(instance.Id, idTache, null, cnx, CancellationToken.None);
        cnx.Committer();

        Assert.Equal(StatutInstance.Termine, instance.Statut);
        Assert.Equal("fin", instance.NoeudCourantId);

        var historiqueInstance = await env.Historique.ObtenirPourInstanceAsync(instance.Id, cnx, CancellationToken.None);
        Assert.Contains(historiqueInstance, h => h.NoeudId == "sousProcessusNotification");
        Assert.Contains(historiqueInstance, h => h.NoeudId == "decisionPaiement");
    }

    [Fact]
    public async Task DecisionFausse_TraverseDebutSystemiqueDecisionEtFin_DansUnSeulAppel()
    {
        var env = ConstruireEnvironnement(registre =>
            registre.Enregistrer(DefinirProcessusPrincipal(new ConstructeurProcessus("ProcessusTest"))));

        var commandeId = Guid.NewGuid();
        PaiementValideRequeteHandler.Configurer(commandeId, valide: false);

        using var cnx = new ConnexionProcessusTest();
        cnx.DemarrerTransaction();
        var instance = await env.Moteur.DemarrerAsync("ProcessusTest", new { commandeId }, cnx, CancellationToken.None);
        cnx.Committer();

        Assert.Equal(StatutInstance.Termine, instance.Statut);
        Assert.Equal("finRefus", instance.NoeudCourantId);
    }

    [Fact]
    public async Task AttenteDateHeure_SuspendPuisEstRepriseParUnPoller()
    {
        var env = ConstruireEnvironnement(registre =>
        {
            var c = new ConstructeurProcessus("ProcessusDate");
            c.Debut("debut", "attenteDate");
            c.NoeudAttenteDateHeure("attenteDate").EcheanceDans(_ => TimeSpan.FromMilliseconds(-1)).Vers("fin");
            c.Fin("fin");
            registre.Enregistrer(c.Construire());
        });

        using var cnx = new ConnexionProcessusTest();
        cnx.DemarrerTransaction();
        var instance = await env.Moteur.DemarrerAsync("ProcessusDate", null, cnx, CancellationToken.None);
        cnx.Committer();

        Assert.Equal(StatutInstance.EnAttenteDateHeure, instance.Statut);

        cnx.DemarrerTransaction();
        var echues = await env.Instances.ObtenirEnAttenteDateEchueAsync(DateTime.UtcNow, cnx, CancellationToken.None);
        Assert.Contains(echues, i => i.Id == instance.Id);

        var repris = await env.Moteur.ReprendreAsync(instance.Id, cnx, CancellationToken.None);
        cnx.Committer();

        Assert.Equal(StatutInstance.Termine, repris.Statut);
    }

    [Fact]
    public async Task SousProcessusSuspendu_PropageAutomatiquementLaReprisAuParent()
    {
        var env = ConstruireEnvironnement(registre =>
        {
            var enfant = new ConstructeurProcessus("Enfant");
            enfant.Debut("debut", "attente");
            enfant.NoeudAttenteSignal("attente").Signal("SignalEnfant").Vers("fin");
            enfant.Fin("fin");
            registre.Enregistrer(enfant.Construire());

            var parent = new ConstructeurProcessus("ParentAvecEnfant");
            parent.Debut("debut", "appelEnfant");
            parent.NoeudSousProcessus("appelEnfant").Processus("Enfant").Vers("fin");
            parent.Fin("fin");
            registre.Enregistrer(parent.Construire());
        });

        using var cnx = new ConnexionProcessusTest();
        cnx.DemarrerTransaction();
        var instanceParent = await env.Moteur.DemarrerAsync("ParentAvecEnfant", null, cnx, CancellationToken.None);
        cnx.Committer();

        Assert.Equal(StatutInstance.EnAttenteSousProcessus, instanceParent.Statut);

        var idEnfant = await cnx.SqliteConnexion.QueryFirstAsync<Guid>(
            "SELECT Id FROM InstanceProcessus WHERE InstanceParentId = @ParentId",
            new { ParentId = instanceParent.Id });

        cnx.DemarrerTransaction();
        await env.Moteur.RecevoirSignalAsync(idEnfant, "SignalEnfant", null, cnx, CancellationToken.None);
        cnx.Committer();

        var parentApres = await env.Instances.ObtenirAsync(instanceParent.Id, cnx, CancellationToken.None);
        Assert.Equal(StatutInstance.Termine, parentApres!.Statut);
    }

    [Fact]
    public async Task EchecEnCoursDeChaine_NeLaissePersisterAucunEffet_GraceAuRollbackClient()
    {
        var env = ConstruireEnvironnement(registre =>
        {
            var c = new ConstructeurProcessus("ProcessusRollback");
            c.Debut("debut", "etape1");
            c.NoeudSystemique("etape1").Commande(_ => new ReserverStockCommande(Guid.NewGuid())).Vers("etape2");
            c.NoeudSystemique("etape2").Commande(_ => new CommandeQuiEchoue()).Vers("fin");
            c.Fin("fin");
            registre.Enregistrer(c.Construire());
        });

        using var cnx = new ConnexionProcessusTest();
        cnx.DemarrerTransaction();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => env.Moteur.DemarrerAsync("ProcessusRollback", null, cnx, CancellationToken.None));

        cnx.Annuler();

        var nbInstances = await cnx.SqliteConnexion.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM InstanceProcessus");
        var nbHistorique = await cnx.SqliteConnexion.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM HistoriqueExecution");
        var nbEffets = await cnx.SqliteConnexion.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM EffetsSecondairesTest");

        Assert.Equal(0, nbInstances);
        Assert.Equal(0, nbHistorique);
        Assert.Equal(0, nbEffets);
    }

    [Fact]
    public void Construire_NoeudSystemiqueSansCommande_Leve()
    {
        var c = new ConstructeurProcessus("Invalide1");
        c.Debut("debut", "etape1");
        c.NoeudSystemique("etape1").Vers("fin");
        c.Fin("fin");

        Assert.Throws<InvalidOperationException>(() => c.Construire());
    }

    [Fact]
    public void Construire_TransitionVersNoeudInconnu_Leve()
    {
        var c = new ConstructeurProcessus("Invalide2");
        c.Debut("debut", "etapeInconnue");

        Assert.Throws<InvalidOperationException>(() => c.Construire());
    }
}
