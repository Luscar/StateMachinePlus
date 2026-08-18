namespace StateMachinePlus.Definition;

/// <summary>Type d'un noeud du graphe de processus.</summary>
public enum TypeNoeud
{
    Debut,
    Fin,

    /// <summary>Execute une commande cote client (ecriture).</summary>
    Systemique,

    /// <summary>Execute une requete cote client (lecture) et branche selon le resultat.</summary>
    Decision,

    /// <summary>Suspend l'instance jusqu'a une date/heure donnee.</summary>
    AttenteDateHeure,

    /// <summary>Suspend l'instance jusqu'a reception d'un signal externe nomme.</summary>
    AttenteSignal,

    /// <summary>Suspend l'instance et cree une tache dans un systeme externe.</summary>
    TacheUtilisateur,

    /// <summary>Demarre (et attend, si necessaire) un sous-processus.</summary>
    SousProcessus,
}
