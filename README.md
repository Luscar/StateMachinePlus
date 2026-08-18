# StateMachinePlus

Moteur de state machine pour processus longue duree (*long running process*) en C# / .NET 8,
distribuable comme package NuGet dans une solution cliente.

> Reference complete de l'API fluent de definition des processus : [docs/builders.md](docs/builders.md).

## Concepts

Un **processus** est un graphe de **noeuds** relies par des **transitions**, defini une fois en
code (au demarrage de l'application) via `ConstructeurProcessus`, puis instancie en base a chaque
execution (`InstanceProcessus`). Le moteur (`IMoteurProcessus`) fait progresser une instance a
travers les noeuds jusqu'a :

- un **noeud en suspens** (attente date/heure, attente signal, tache utilisateur, ou
  sous-processus non termine) : l'instance est persistee dans cet etat et l'appel retourne ;
- un **noeud de fin** : l'instance est marquee `Termine`.

Types de noeuds pris en charge :

| Noeud | Role |
|---|---|
| **Systemique** | Execute une **commande** cote client (ecriture) via `ICommandeHandler<TCommande>`. |
| **Decision** | Execute une **requete** cote client (lecture) via `IRequeteHandler<TRequete, TResultat>` et branche selon le resultat. |
| **Attente date/heure** | Suspend l'instance jusqu'a une echeance calculee. Reprise via `ReprendreAsync` (typiquement depuis un job planifie). |
| **Attente signal** | Suspend l'instance jusqu'a reception d'un signal externe nomme. Reprise via `RecevoirSignalAsync`. |
| **Tache utilisateur** | Suspend l'instance et cree une tache via `IFournisseurTacheUtilisateur` (systeme externe). Reprise via `CompleterTacheAsync`. |
| **Sous-processus** | Demarre une instance d'un autre processus. Si elle se termine immediatement, l'execution continue dans la meme boucle (donc la meme transaction) ; sinon le parent suspend et reprend automatiquement a la fin de l'enfant. |

## Execution transactionnelle

Le moteur ne connait pas votre ORM : vous lui fournissez une **connexion** deja ouverte (et,
generalement, une transaction en cours) au travers de l'abstraction `IConnexionProcessus`, que
vous implementez cote client :

```csharp
public sealed class ConnexionProcessus : IConnexionProcessus
{
    public ConnexionProcessus(IDbConnection connexion, IDbTransaction? transaction)
    {
        Connexion = connexion;
        Transaction = transaction;
    }

    public IDbConnection Connexion { get; }
    public IDbTransaction? Transaction { get; }
}
```

(Une implementation minimale equivalente est fournie : `StateMachinePlus.Persistance.ConnexionProcessus`.)

Le moteur persiste l'etat des instances (et l'historique d'execution) via **Dapper**, en utilisant
cette meme connexion/transaction. Vos gestionnaires de commandes/requetes recoivent egalement
cette connexion : en l'utilisant pour vos propres ecritures, celles-ci font partie de la **meme
transaction** que l'avancement du processus.

Consequence directe : le moteur execute une **suite de noeuds jusqu'au prochain noeud en
suspens** (ou jusqu'a la fin) **dans un seul appel**, donc dans une seule transaction. Si une
exception est levee en cours de route (commande ou requete client qui echoue), rien n'est
persiste ; c'est a vous, proprietaire de la transaction, de la committer en cas de succes ou de
faire un rollback en cas d'echec — l'instance reste alors, en base, a son dernier point de
suspension reussi.

## Installation cote client

```csharp
services.AjouterStateMachinePlus(registre =>
{
    var c = new ConstructeurProcessus("CommandeClient", version: 1);

    c.Debut("debut", "verifierStock");

    c.NoeudSystemique("verifierStock")
        .Commande(ctx => new ReserverStockCommande(ctx.Variables.ObtenirRequis<Guid>("commandeId")))
        .Vers("decisionPaiement");

    c.NoeudDecision("decisionPaiement")
        .RequeteBool(ctx => new PaiementValideRequete(ctx.Variables.ObtenirRequis<Guid>("commandeId")))
        .SiVrai("attenteExpedition")
        .SiFaux("finRefus");

    c.NoeudAttenteSignal("attenteExpedition")
        .Signal("ExpeditionConfirmee")
        .Vers("fin");

    c.Fin("fin");
    c.Fin("finRefus");

    registre.Enregistrer(c.Construire());
});

// A enregistrer vous-meme :
services.AddScoped<IConnexionProcessus>(/* votre connexion/transaction ambiante */);
services.AddScoped<ICommandeHandler<ReserverStockCommande>, ReserverStockCommandeHandler>();
services.AddScoped<IRequeteHandler<PaiementValideRequete, bool>, PaiementValideRequeteHandler>();
services.AddScoped<IFournisseurTacheUtilisateur, MonFournisseurDeTaches>(); // si des noeuds tache utilisateur sont utilises
```

Le schema de base de donnees (SQL Server) est fourni dans
`src/StateMachinePlus/Persistance/Scripts/schema-sqlserver.sql` : deux tables,
`InstanceProcessus` (etat courant de chaque instance) et `HistoriqueExecution` (audit des noeuds
executes). A adapter si vous ciblez un autre moteur de base de donnees.

## Utilisation

```csharp
// Demarrer une instance
var instance = await moteur.DemarrerAsync("CommandeClient", new { commandeId }, connexion, ct);

// Reprendre une instance en attente d'une echeance (ex: depuis un job planifie)
await moteur.ReprendreAsync(instanceId, connexion, ct);

// Recevoir un signal externe attendu
await moteur.RecevoirSignalAsync(instanceId, "ExpeditionConfirmee", donnees, connexion, ct);

// Completer une tache utilisateur externe
await moteur.CompleterTacheAsync(instanceId, idTacheExterne, resultat, connexion, ct);
```

Un service d'arriere-plan optionnel (`StateMachinePlus.Planification.ServiceReprisePlanifiee`)
peut etre ajoute (`services.AddHostedService<ServiceReprisePlanifiee>()`) pour reprendre
periodiquement les instances dont l'echeance (attente date/heure) est atteinte.

## Projet

- `src/StateMachinePlus` : le package.
- `tests/StateMachinePlus.Tests` : tests d'integration (xUnit + SQLite en memoire) couvrant les
  six types de noeuds, la suspension/reprise, la propagation sous-processus -> parent, et
  l'atomicite transactionnelle.

```bash
dotnet build
dotnet test
```
