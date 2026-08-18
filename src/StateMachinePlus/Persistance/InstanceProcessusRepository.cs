using Dapper;
using StateMachinePlus.Execution;

namespace StateMachinePlus.Persistance;

/// <summary>Acces aux instances de processus persistees.</summary>
public interface IInstanceProcessusRepository
{
    Task<InstanceProcessus?> ObtenirAsync(Guid id, IConnexionProcessus connexion, CancellationToken jetonAnnulation);

    /// <summary>Insere ou met a jour l'instance (upsert).</summary>
    Task EnregistrerAsync(InstanceProcessus instance, IConnexionProcessus connexion, CancellationToken jetonAnnulation);

    Task<IReadOnlyList<InstanceProcessus>> ObtenirEnAttenteDateEchueAsync(
        DateTime maintenant, IConnexionProcessus connexion, CancellationToken jetonAnnulation);

    Task<IReadOnlyList<InstanceProcessus>> ObtenirEnAttenteSignalAsync(
        string nomSignal, IConnexionProcessus connexion, CancellationToken jetonAnnulation);
}

/// <summary>Implementation Dapper de <see cref="IInstanceProcessusRepository"/>.</summary>
public sealed class InstanceProcessusRepository : IInstanceProcessusRepository
{
    private const string NomTable = "InstanceProcessus";

    public async Task<InstanceProcessus?> ObtenirAsync(Guid id, IConnexionProcessus connexion, CancellationToken jetonAnnulation)
    {
        var commande = new CommandDefinition(
            $@"SELECT * FROM {NomTable} WHERE Id = @Id",
            new { Id = id },
            connexion.Transaction,
            cancellationToken: jetonAnnulation);

        var ligne = await connexion.Connexion.QuerySingleOrDefaultAsync<LigneInstanceProcessus>(commande);
        return ligne?.VersDomaine();
    }

    public async Task EnregistrerAsync(InstanceProcessus instance, IConnexionProcessus connexion, CancellationToken jetonAnnulation)
    {
        instance.DateModification = DateTime.UtcNow;
        var ligne = LigneInstanceProcessus.DepuisDomaine(instance);

        var commandeMaj = new CommandDefinition(
            $@"UPDATE {NomTable} SET
                    CodeDefinition = @CodeDefinition,
                    VersionDefinition = @VersionDefinition,
                    NoeudCourantId = @NoeudCourantId,
                    Statut = @Statut,
                    VariablesJson = @VariablesJson,
                    DateModification = @DateModification,
                    DateEcheance = @DateEcheance,
                    NomSignalAttendu = @NomSignalAttendu,
                    IdTacheExterne = @IdTacheExterne,
                    InstanceParentId = @InstanceParentId,
                    NoeudParentId = @NoeudParentId,
                    MessageErreur = @MessageErreur
                WHERE Id = @Id",
            ligne,
            connexion.Transaction,
            cancellationToken: jetonAnnulation);

        var lignesAffectees = await connexion.Connexion.ExecuteAsync(commandeMaj);
        if (lignesAffectees > 0)
        {
            return;
        }

        var commandeInsertion = new CommandDefinition(
            $@"INSERT INTO {NomTable}
                    (Id, CodeDefinition, VersionDefinition, NoeudCourantId, Statut, VariablesJson,
                     DateCreation, DateModification, DateEcheance, NomSignalAttendu, IdTacheExterne,
                     InstanceParentId, NoeudParentId, MessageErreur)
                VALUES
                    (@Id, @CodeDefinition, @VersionDefinition, @NoeudCourantId, @Statut, @VariablesJson,
                     @DateCreation, @DateModification, @DateEcheance, @NomSignalAttendu, @IdTacheExterne,
                     @InstanceParentId, @NoeudParentId, @MessageErreur)",
            ligne,
            connexion.Transaction,
            cancellationToken: jetonAnnulation);

        await connexion.Connexion.ExecuteAsync(commandeInsertion);
    }

    public async Task<IReadOnlyList<InstanceProcessus>> ObtenirEnAttenteDateEchueAsync(
        DateTime maintenant, IConnexionProcessus connexion, CancellationToken jetonAnnulation)
    {
        var commande = new CommandDefinition(
            $@"SELECT * FROM {NomTable}
                WHERE Statut = @Statut AND DateEcheance IS NOT NULL AND DateEcheance <= @Maintenant",
            new { Statut = (int)StatutInstance.EnAttenteDateHeure, Maintenant = maintenant },
            connexion.Transaction,
            cancellationToken: jetonAnnulation);

        var lignes = await connexion.Connexion.QueryAsync<LigneInstanceProcessus>(commande);
        return lignes.Select(l => l.VersDomaine()).ToList();
    }

    public async Task<IReadOnlyList<InstanceProcessus>> ObtenirEnAttenteSignalAsync(
        string nomSignal, IConnexionProcessus connexion, CancellationToken jetonAnnulation)
    {
        var commande = new CommandDefinition(
            $@"SELECT * FROM {NomTable}
                WHERE Statut = @Statut AND NomSignalAttendu = @NomSignal",
            new { Statut = (int)StatutInstance.EnAttenteSignal, NomSignal = nomSignal },
            connexion.Transaction,
            cancellationToken: jetonAnnulation);

        var lignes = await connexion.Connexion.QueryAsync<LigneInstanceProcessus>(commande);
        return lignes.Select(l => l.VersDomaine()).ToList();
    }

    /// <summary>Representation tabulaire (a plat) utilisee pour le mapping Dapper.</summary>
    private sealed class LigneInstanceProcessus
    {
        public Guid Id { get; set; }
        public string CodeDefinition { get; set; } = string.Empty;
        public int VersionDefinition { get; set; }
        public string NoeudCourantId { get; set; } = string.Empty;
        public int Statut { get; set; }
        public string VariablesJson { get; set; } = "{}";
        public DateTime DateCreation { get; set; }
        public DateTime DateModification { get; set; }
        public DateTime? DateEcheance { get; set; }
        public string? NomSignalAttendu { get; set; }
        public string? IdTacheExterne { get; set; }
        public Guid? InstanceParentId { get; set; }
        public string? NoeudParentId { get; set; }
        public string? MessageErreur { get; set; }

        public static LigneInstanceProcessus DepuisDomaine(InstanceProcessus instance) => new()
        {
            Id = instance.Id,
            CodeDefinition = instance.CodeDefinition,
            VersionDefinition = instance.VersionDefinition,
            NoeudCourantId = instance.NoeudCourantId,
            Statut = (int)instance.Statut,
            VariablesJson = instance.Variables.VersJson(),
            DateCreation = instance.DateCreation,
            DateModification = instance.DateModification,
            DateEcheance = instance.DateEcheance,
            NomSignalAttendu = instance.NomSignalAttendu,
            IdTacheExterne = instance.IdTacheExterne,
            InstanceParentId = instance.InstanceParentId,
            NoeudParentId = instance.NoeudParentId,
            MessageErreur = instance.MessageErreur,
        };

        public InstanceProcessus VersDomaine() => new()
        {
            Id = Id,
            CodeDefinition = CodeDefinition,
            VersionDefinition = VersionDefinition,
            NoeudCourantId = NoeudCourantId,
            Statut = (StatutInstance)Statut,
            Variables = VariablesProcessus.DepuisJson(VariablesJson),
            DateCreation = DateCreation,
            DateModification = DateModification,
            DateEcheance = DateEcheance,
            NomSignalAttendu = NomSignalAttendu,
            IdTacheExterne = IdTacheExterne,
            InstanceParentId = InstanceParentId,
            NoeudParentId = NoeudParentId,
            MessageErreur = MessageErreur,
        };
    }
}
