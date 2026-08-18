using StateMachinePlus.Persistance;

namespace StateMachinePlus.Execution;

/// <summary>
/// Contexte transmis aux delegues de configuration des noeuds (fabrique de commande, de requete,
/// de tache, etc.) pendant l'execution d'une instance de processus.
/// </summary>
public sealed class ContexteExecution
{
    public ContexteExecution(InstanceProcessus instance, IServiceProvider services)
    {
        Instance = instance;
        Services = services;
    }

    /// <summary>Instance de processus en cours d'execution.</summary>
    public InstanceProcessus Instance { get; }

    /// <summary>Variables de l'instance en cours d'execution.</summary>
    public VariablesProcessus Variables => Instance.Variables;

    /// <summary>Conteneur de services du client, pour resoudre au besoin des dependances additionnelles.</summary>
    public IServiceProvider Services { get; }
}
