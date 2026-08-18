namespace StateMachinePlus.Persistance;

/// <summary>Etat courant d'une instance de processus.</summary>
public enum StatutInstance
{
    /// <summary>En cours d'execution (transitoire, ne devrait jamais etre persiste).</summary>
    EnCours = 0,

    /// <summary>Suspendue, en attente d'une date/heure d'echeance.</summary>
    EnAttenteDateHeure = 1,

    /// <summary>Suspendue, en attente d'un signal externe nomme.</summary>
    EnAttenteSignal = 2,

    /// <summary>Suspendue, en attente de la completion d'une tache utilisateur externe.</summary>
    EnAttenteTacheUtilisateur = 3,

    /// <summary>Suspendue, en attente de la fin d'un sous-processus enfant.</summary>
    EnAttenteSousProcessus = 4,

    /// <summary>Terminee avec succes (atteinte d'un noeud de fin).</summary>
    Termine = 5,

    /// <summary>Terminee en erreur.</summary>
    Erreur = 6,

    /// <summary>Annulee explicitement.</summary>
    Annule = 7,
}
