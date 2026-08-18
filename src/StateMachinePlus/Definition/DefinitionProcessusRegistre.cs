namespace StateMachinePlus.Definition;

/// <summary>
/// Registre en memoire des definitions de processus enregistrees au demarrage de l'application
/// cliente (ex : dans une extension d'enregistrement des services). Le moteur consulte ce
/// registre pour resoudre le graphe correspondant a une instance.
/// </summary>
public interface IDefinitionProcessusRegistre
{
    void Enregistrer(DefinitionProcessus definition);

    /// <summary>Recupere une definition par code et, optionnellement, une version precise (sinon la plus recente).</summary>
    DefinitionProcessus Obtenir(string code, int? version = null);
}

public sealed class DefinitionProcessusRegistre : IDefinitionProcessusRegistre
{
    private readonly Dictionary<string, Dictionary<int, DefinitionProcessus>> _definitions = new();

    public void Enregistrer(DefinitionProcessus definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (!_definitions.TryGetValue(definition.Code, out var versions))
        {
            versions = new Dictionary<int, DefinitionProcessus>();
            _definitions[definition.Code] = versions;
        }

        if (!versions.TryAdd(definition.Version, definition))
        {
            throw new InvalidOperationException(
                $"La version {definition.Version} du processus '{definition.Code}' est deja enregistree.");
        }
    }

    public DefinitionProcessus Obtenir(string code, int? version = null)
    {
        if (!_definitions.TryGetValue(code, out var versions) || versions.Count == 0)
        {
            throw new InvalidOperationException($"Aucune definition enregistree pour le processus '{code}'.");
        }

        if (version.HasValue)
        {
            if (!versions.TryGetValue(version.Value, out var definition))
            {
                throw new InvalidOperationException($"La version {version.Value} du processus '{code}' n'est pas enregistree.");
            }

            return definition;
        }

        return versions[versions.Keys.Max()];
    }
}
