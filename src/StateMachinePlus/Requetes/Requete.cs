namespace StateMachinePlus.Requetes;

/// <summary>
/// Marqueur pour une requete de lecture executee par un noeud decision. Ne doit produire
/// aucun effet de bord metier ; sert uniquement a obtenir une information pour brancher
/// vers le noeud suivant approprie.
/// </summary>
/// <typeparam name="TResultat">Type du resultat retourne par la requete.</typeparam>
public interface IRequete<TResultat>
{
}

/// <summary>
/// Gestionnaire cote client charge d'executer une <see cref="IRequete{TResultat}"/>.
/// Doit etre enregistre dans le conteneur de services du client (DI).
/// </summary>
public interface IRequeteHandler<in TRequete, TResultat> where TRequete : IRequete<TResultat>
{
    Task<TResultat> ExecuterAsync(TRequete requete, Persistance.IConnexionProcessus connexion, CancellationToken jetonAnnulation);
}
