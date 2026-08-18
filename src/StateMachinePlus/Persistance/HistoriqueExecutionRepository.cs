using Dapper;

namespace StateMachinePlus.Persistance;

/// <summary>Acces a l'historique d'execution des noeuds.</summary>
public interface IHistoriqueExecutionRepository
{
    Task AjouterAsync(HistoriqueExecution entree, IConnexionProcessus connexion, CancellationToken jetonAnnulation);

    Task<IReadOnlyList<HistoriqueExecution>> ObtenirPourInstanceAsync(
        Guid instanceId, IConnexionProcessus connexion, CancellationToken jetonAnnulation);
}

/// <summary>Implementation Dapper de <see cref="IHistoriqueExecutionRepository"/>.</summary>
public sealed class HistoriqueExecutionRepository : IHistoriqueExecutionRepository
{
    private const string NomTable = "HistoriqueExecution";

    public async Task AjouterAsync(HistoriqueExecution entree, IConnexionProcessus connexion, CancellationToken jetonAnnulation)
    {
        var commande = new CommandDefinition(
            $@"INSERT INTO {NomTable} (Id, InstanceId, NoeudId, TypeNoeud, DateExecution, Succes, Message)
                VALUES (@Id, @InstanceId, @NoeudId, @TypeNoeud, @DateExecution, @Succes, @Message)",
            entree,
            connexion.Transaction,
            cancellationToken: jetonAnnulation);

        await connexion.Connexion.ExecuteAsync(commande);
    }

    public async Task<IReadOnlyList<HistoriqueExecution>> ObtenirPourInstanceAsync(
        Guid instanceId, IConnexionProcessus connexion, CancellationToken jetonAnnulation)
    {
        var commande = new CommandDefinition(
            $@"SELECT * FROM {NomTable} WHERE InstanceId = @InstanceId ORDER BY DateExecution ASC",
            new { InstanceId = instanceId },
            connexion.Transaction,
            cancellationToken: jetonAnnulation);

        var lignes = await connexion.Connexion.QueryAsync<HistoriqueExecution>(commande);
        return lignes.ToList();
    }
}
