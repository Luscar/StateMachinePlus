namespace StateMachinePlus.Persistance;

/// <summary>Entree d'audit correspondant a l'execution d'un noeud pour une instance donnee.</summary>
public sealed class HistoriqueExecution
{
    public Guid Id { get; set; }

    public Guid InstanceId { get; set; }

    public string NoeudId { get; set; } = string.Empty;

    public string TypeNoeud { get; set; } = string.Empty;

    public DateTime DateExecution { get; set; }

    public bool Succes { get; set; }

    public string? Message { get; set; }

    public static HistoriqueExecution Creer(Guid instanceId, string noeudId, string typeNoeud, bool succes, string? message = null)
    {
        return new HistoriqueExecution
        {
            Id = Guid.NewGuid(),
            InstanceId = instanceId,
            NoeudId = noeudId,
            TypeNoeud = typeNoeud,
            DateExecution = DateTime.UtcNow,
            Succes = succes,
            Message = message,
        };
    }
}
