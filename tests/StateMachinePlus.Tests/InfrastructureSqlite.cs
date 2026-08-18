using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using StateMachinePlus.Persistance;

namespace StateMachinePlus.Tests;

/// <summary>
/// Implementation de <see cref="IConnexionProcessus"/> pour les tests, basee sur SQLite en
/// memoire. La transaction est mutable pour permettre a un test de simuler plusieurs
/// "transactions client" successives (une par appel au moteur) sur la meme connexion.
/// </summary>
internal sealed class ConnexionProcessusTest : IConnexionProcessus, IDisposable
{
    static ConnexionProcessusTest()
    {
        // SQLite n'a pas de type natif Guid ; Microsoft.Data.Sqlite le persiste comme du texte.
        // Ces handlers permettent a Dapper de re-parser correctement les Guid lus depuis SQLite
        // (non necessaire avec SQL Server, qui gere UNIQUEIDENTIFIER nativement).
        SqlMapper.AddTypeHandler(new GuidTypeHandlerSqlite());
        SqlMapper.AddTypeHandler(new GuidNullableTypeHandlerSqlite());
    }

    public ConnexionProcessusTest()
    {
        SqliteConnexion = new SqliteConnection("DataSource=:memory:");
        SqliteConnexion.Open();
        CreerSchema();
    }

    public SqliteConnection SqliteConnexion { get; }

    public IDbConnection Connexion => SqliteConnexion;

    public IDbTransaction? Transaction { get; private set; }

    public IDbTransaction DemarrerTransaction()
    {
        Transaction = SqliteConnexion.BeginTransaction();
        return Transaction;
    }

    public void Committer()
    {
        Transaction?.Commit();
        Transaction = null;
    }

    public void Annuler()
    {
        Transaction?.Rollback();
        Transaction = null;
    }

    private void CreerSchema()
    {
        using var commande = SqliteConnexion.CreateCommand();
        commande.CommandText =
            """
            CREATE TABLE InstanceProcessus
            (
                Id                TEXT PRIMARY KEY,
                CodeDefinition    TEXT NOT NULL,
                VersionDefinition INTEGER NOT NULL,
                NoeudCourantId    TEXT NOT NULL,
                Statut            INTEGER NOT NULL,
                VariablesJson     TEXT NOT NULL,
                DateCreation      TEXT NOT NULL,
                DateModification  TEXT NOT NULL,
                DateEcheance      TEXT NULL,
                NomSignalAttendu  TEXT NULL,
                IdTacheExterne    TEXT NULL,
                InstanceParentId  TEXT NULL,
                NoeudParentId     TEXT NULL,
                MessageErreur     TEXT NULL
            );

            CREATE TABLE HistoriqueExecution
            (
                Id            TEXT PRIMARY KEY,
                InstanceId    TEXT NOT NULL,
                NoeudId       TEXT NOT NULL,
                TypeNoeud     TEXT NOT NULL,
                DateExecution TEXT NOT NULL,
                Succes        INTEGER NOT NULL,
                Message       TEXT NULL
            );

            CREATE TABLE EffetsSecondairesTest
            (
                Id TEXT PRIMARY KEY
            );
            """;
        commande.ExecuteNonQuery();
    }

    public void Dispose() => SqliteConnexion.Dispose();
}

internal sealed class GuidTypeHandlerSqlite : SqlMapper.TypeHandler<Guid>
{
    public override void SetValue(IDbDataParameter parameter, Guid value) => parameter.Value = value.ToString();

    public override Guid Parse(object value) => value switch
    {
        Guid guid => guid,
        string texte => Guid.Parse(texte),
        _ => throw new NotSupportedException($"Impossible de convertir '{value}' en Guid."),
    };
}

internal sealed class GuidNullableTypeHandlerSqlite : SqlMapper.TypeHandler<Guid?>
{
    public override void SetValue(IDbDataParameter parameter, Guid? value) => parameter.Value = value?.ToString() ?? (object)DBNull.Value;

    public override Guid? Parse(object value) => value switch
    {
        null or DBNull => null,
        Guid guid => guid,
        string texte => Guid.Parse(texte),
        _ => throw new NotSupportedException($"Impossible de convertir '{value}' en Guid."),
    };
}
