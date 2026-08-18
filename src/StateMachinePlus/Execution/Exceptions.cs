namespace StateMachinePlus.Execution;

/// <summary>Exception de base du moteur d'execution.</summary>
public abstract class ProcessusException : Exception
{
    protected ProcessusException(string message) : base(message)
    {
    }
}

public sealed class InstanceIntrouvableException : ProcessusException
{
    public InstanceIntrouvableException(Guid instanceId)
        : base($"L'instance de processus '{instanceId}' est introuvable.")
    {
        InstanceId = instanceId;
    }

    public Guid InstanceId { get; }
}

/// <summary>
/// Levee lorsqu'on tente de reprendre une instance qui n'est pas dans le statut attendu pour
/// l'operation demandee (ex : recevoir un signal alors que l'instance n'attend pas de signal).
/// </summary>
public sealed class StatutInstanceInvalideException : ProcessusException
{
    public StatutInstanceInvalideException(Guid instanceId, string message) : base(message)
    {
        InstanceId = instanceId;
    }

    public Guid InstanceId { get; }
}

/// <summary>Levee lorsqu'un noeud decision retourne une cle de branche sans transition correspondante.</summary>
public sealed class BrancheIntrouvableException : ProcessusException
{
    public BrancheIntrouvableException(string noeudId, string cleBranche)
        : base($"Le noeud decision '{noeudId}' n'a pas de transition pour la branche '{cleBranche}' et aucune transition par defaut n'est definie.")
    {
        NoeudId = noeudId;
        CleBranche = cleBranche;
    }

    public string NoeudId { get; }

    public string CleBranche { get; }
}
