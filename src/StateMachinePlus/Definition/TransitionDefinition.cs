namespace StateMachinePlus.Definition;

/// <summary>
/// Transition sortante d'un noeud. Pour un noeud decision, <see cref="Condition"/> porte la cle
/// de branche (retournee par le selecteur de branche) ; pour les autres noeuds, une seule
/// transition sans condition est autorisee.
/// </summary>
public sealed class TransitionDefinition
{
    public TransitionDefinition(string noeudCibleId, string? condition = null)
    {
        if (string.IsNullOrWhiteSpace(noeudCibleId))
        {
            throw new ArgumentException("L'identifiant du noeud cible est requis.", nameof(noeudCibleId));
        }

        NoeudCibleId = noeudCibleId;
        Condition = condition;
    }

    public string NoeudCibleId { get; }

    public string? Condition { get; }
}
