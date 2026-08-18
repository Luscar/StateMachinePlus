namespace StateMachinePlus.Definition;

/// <summary>
/// Definition (design-time) d'un processus : un graphe de noeuds relies par des transitions.
/// Immuable une fois construite via <see cref="ConstructeurProcessus"/>. Doit etre enregistree
/// dans un <see cref="IDefinitionProcessusRegistre"/> au demarrage de l'application cliente.
/// </summary>
public sealed class DefinitionProcessus
{
    private readonly IReadOnlyDictionary<string, NoeudDefinition> _noeuds;

    internal DefinitionProcessus(string code, int version, string noeudDebutId, IReadOnlyDictionary<string, NoeudDefinition> noeuds)
    {
        Code = code;
        Version = version;
        NoeudDebutId = noeudDebutId;
        _noeuds = noeuds;
    }

    public string Code { get; }

    public int Version { get; }

    public string NoeudDebutId { get; }

    public NoeudDefinition ObtenirNoeud(string id)
    {
        if (!_noeuds.TryGetValue(id, out var noeud))
        {
            throw new InvalidOperationException(
                $"Le noeud '{id}' est introuvable dans la definition de processus '{Code}' (version {Version}).");
        }

        return noeud;
    }

    public IReadOnlyCollection<NoeudDefinition> Noeuds => _noeuds.Values.ToList();
}
