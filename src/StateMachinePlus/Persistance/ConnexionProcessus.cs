using System.Data;

namespace StateMachinePlus.Persistance;

/// <summary>
/// Abstraction de la connexion base de donnees utilisee par le moteur (via Dapper) pour
/// persister l'etat des instances de processus. A implementer cote client de maniere a
/// exposer la connexion/transaction ambiante de l'unite de travail courante : cela garantit
/// que la persistance du processus et les ecritures metier (commandes) partagent la meme
/// transaction, et sont donc validees ou annulees ensemble.
/// </summary>
public interface IConnexionProcessus
{
    /// <summary>Connexion ouverte a utiliser pour toute operation Dapper.</summary>
    IDbConnection Connexion { get; }

    /// <summary>
    /// Transaction ambiante en cours, ou null si aucune transaction explicite n'est utilisee.
    /// Lorsqu'elle est fournie, toutes les operations du moteur l'utilisent.
    /// </summary>
    IDbTransaction? Transaction { get; }
}

/// <summary>
/// Implementation simple de <see cref="IConnexionProcessus"/> qui enveloppe une connexion et
/// une transaction deja ouvertes par le client. Pratique lorsque le client n'a pas besoin d'un
/// comportement particulier (resolution depuis un contexte ambiant, etc.).
/// </summary>
public sealed class ConnexionProcessus : IConnexionProcessus
{
    public ConnexionProcessus(IDbConnection connexion, IDbTransaction? transaction = null)
    {
        Connexion = connexion ?? throw new ArgumentNullException(nameof(connexion));
        Transaction = transaction;
    }

    public IDbConnection Connexion { get; }

    public IDbTransaction? Transaction { get; }
}
