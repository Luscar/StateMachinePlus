using Microsoft.Data.Sqlite;
using StateMachinePlus.Commandes;
using StateMachinePlus.Persistance;
using StateMachinePlus.Requetes;
using StateMachinePlus.Taches;

namespace StateMachinePlus.Tests;

public sealed record ReserverStockCommande(Guid CommandeId) : ICommande;

public sealed record EnvoyerNotificationCommande(string Message) : ICommande;

public sealed record CommandeQuiEchoue : ICommande;

public sealed record PaiementValideRequete(Guid CommandeId) : IRequete<bool>;

public sealed class ReserverStockCommandeHandler : ICommandeHandler<ReserverStockCommande>
{
    public Task ExecuterAsync(ReserverStockCommande commande, IConnexionProcessus connexion, CancellationToken jetonAnnulation)
        => EnregistrerEffetSecondaireAsync(connexion);

    internal static async Task EnregistrerEffetSecondaireAsync(IConnexionProcessus connexion)
    {
        var sqlite = (SqliteConnection)connexion.Connexion;
        using var commandeSql = sqlite.CreateCommand();
        commandeSql.Transaction = (SqliteTransaction?)connexion.Transaction;
        commandeSql.CommandText = "INSERT INTO EffetsSecondairesTest (Id) VALUES ($id)";
        commandeSql.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        await commandeSql.ExecuteNonQueryAsync();
    }
}

public sealed class EnvoyerNotificationCommandeHandler : ICommandeHandler<EnvoyerNotificationCommande>
{
    public Task ExecuterAsync(EnvoyerNotificationCommande commande, IConnexionProcessus connexion, CancellationToken jetonAnnulation)
        => Task.CompletedTask;
}

public sealed class CommandeQuiEchoueHandler : ICommandeHandler<CommandeQuiEchoue>
{
    public Task ExecuterAsync(CommandeQuiEchoue commande, IConnexionProcessus connexion, CancellationToken jetonAnnulation)
        => throw new InvalidOperationException("Echec simule pour valider le rollback transactionnel.");
}

/// <summary>Gestionnaire de requete dont la reponse est controlee par le test via une variable statique par cle.</summary>
public sealed class PaiementValideRequeteHandler : IRequeteHandler<PaiementValideRequete, bool>
{
    private static readonly Dictionary<Guid, bool> Reponses = new();

    public static void Configurer(Guid commandeId, bool valide) => Reponses[commandeId] = valide;

    public Task<bool> ExecuterAsync(PaiementValideRequete requete, IConnexionProcessus connexion, CancellationToken jetonAnnulation)
        => Task.FromResult(Reponses.TryGetValue(requete.CommandeId, out var valide) && valide);
}

public sealed class FournisseurTacheUtilisateurTest : IFournisseurTacheUtilisateur
{
    public List<DemandeCreationTache> TachesCreees { get; } = new();

    public Task<string> CreerAsync(DemandeCreationTache demande, IConnexionProcessus connexion, CancellationToken jetonAnnulation)
    {
        TachesCreees.Add(demande);
        return Task.FromResult($"tache-externe-{TachesCreees.Count}");
    }
}
