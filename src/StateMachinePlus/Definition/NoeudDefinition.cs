namespace StateMachinePlus.Definition;

/// <summary>Noeud du graphe de processus.</summary>
public abstract class NoeudDefinition
{
    protected NoeudDefinition(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("L'identifiant du noeud est requis.", nameof(id));
        }

        Id = id;
    }

    public string Id { get; }

    public abstract TypeNoeud Type { get; }

    public List<TransitionDefinition> Transitions { get; } = new();

    /// <summary>Unique transition sortante, pour les noeuds qui n'en autorisent qu'une seule.</summary>
    internal TransitionDefinition ObtenirTransitionUnique()
    {
        if (Transitions.Count != 1)
        {
            throw new InvalidOperationException(
                $"Le noeud '{Id}' doit avoir exactement une transition sortante (trouve : {Transitions.Count}).");
        }

        return Transitions[0];
    }
}
