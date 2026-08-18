# Constructeurs (builders) de processus

Reference de l'API fluent code-first utilisee pour definir un processus : `ConstructeurProcessus`
et les six constructeurs de noeuds specialises. Voir aussi le [README](../README.md) pour la vue
d'ensemble et le cycle de vie transactionnel.

## Principe general

`ConstructeurProcessus` est le point d'entree. Chaque appel `NoeudXxx(id)` cree un noeud, l'ajoute
au graphe, et retourne un **constructeur specifique a ce type de noeud**. La configuration du
noeud (commande/requete/echeance/signal/tache/sous-processus) et sa transition sortante
(`.Vers(...)`) se font sur ce constructeur specifique — ce ne sont **pas** des appels chaines les
uns a la suite des autres sur `ConstructeurProcessus` : chaque noeud est une instruction
independante.

```csharp
var c = new ConstructeurProcessus("CodeDuProcessus", version: 1);

c.Debut("debut", "premierNoeud");

c.NoeudSystemique("premierNoeud")
    .Commande(ctx => new MaCommande(...))
    .Vers("noeudSuivant");                 // se termine ici, pas de retour vers `c`

c.NoeudDecision("noeudSuivant")
    .RequeteBool(ctx => new MaRequete(...))
    .SiVrai("finOk")
    .SiFaux("finKo");

c.Fin("finOk");
c.Fin("finKo");

var definition = c.Construire();           // valide le graphe puis le rend immuable
registre.Enregistrer(definition);
```

`Construire()` valide l'integralite du graphe (voir [Validation](#validation-a-la-construction))
et leve une `InvalidOperationException` explicite au premier probleme trouve. Rien n'est valide
"a moitie" : soit `Construire()` reussit et vous obtenez une `DefinitionProcessus` immuable, soit
il leve.

---

## `ConstructeurProcessus`

| Membre | Signature | Role |
|---|---|---|
| Constructeur | `ConstructeurProcessus(string code, int version = 1)` | Cree un nouveau builder pour le processus `code`/`version`. |
| `Debut` | `ConstructeurProcessus Debut(string noeudId, string noeudSuivantId)` | Declare le noeud de depart et sa transition unique. **Un seul appel autorise par processus.** |
| `Fin` | `ConstructeurProcessus Fin(string noeudId)` | Declare un noeud de fin (aucune transition sortante). Un processus peut avoir **plusieurs** noeuds de fin. |
| `NoeudSystemique` | `ConstructeurNoeudSystemique NoeudSystemique(string noeudId)` | Cree un noeud systemique et retourne son constructeur. |
| `NoeudDecision` | `ConstructeurNoeudDecision NoeudDecision(string noeudId)` | Cree un noeud decision et retourne son constructeur. |
| `NoeudAttenteDateHeure` | `ConstructeurNoeudAttenteDateHeure NoeudAttenteDateHeure(string noeudId)` | Cree un noeud attente date/heure. |
| `NoeudAttenteSignal` | `ConstructeurNoeudAttenteSignal NoeudAttenteSignal(string noeudId)` | Cree un noeud attente signal. |
| `NoeudTacheUtilisateur` | `ConstructeurNoeudTacheUtilisateur NoeudTacheUtilisateur(string noeudId)` | Cree un noeud tache utilisateur. |
| `NoeudSousProcessus` | `ConstructeurNoeudSousProcessus NoeudSousProcessus(string noeudId)` | Cree un noeud sous-processus. |
| `Construire` | `DefinitionProcessus Construire()` | Valide le graphe et retourne la definition immuable. |

Chaque `noeudId` doit etre unique dans le processus (sinon `Construire()`/l'ajout leve une
exception au premier doublon detecte).

---

## `ConstructeurNoeudSystemique`

Execute une **commande** cote client (ecriture) via `ICommandeHandler<TCommande>`. Transition
sortante unique.

| Methode | Signature | Description |
|---|---|---|
| `Commande<TCommande>` | `Commande<TCommande>(Func<ContexteExecution, TCommande> fabrique) where TCommande : ICommande` | Fabrique la commande depuis le contexte ; le gestionnaire est resolu par DI au moment de l'execution. |
| `Vers` | `void Vers(string noeudCibleId)` | Definit la transition sortante (obligatoire, appel terminal). |

```csharp
c.NoeudSystemique("reserverStock")
    .Commande(ctx => new ReserverStockCommande(ctx.Variables.ObtenirRequis<Guid>("commandeId")))
    .Vers("decisionPaiement");
```

**Requiert** `.Commande(...)` avant `Construire()`.

---

## `ConstructeurNoeudDecision`

Execute une **requete** cote client (lecture) via `IRequeteHandler<TRequete, TResultat>` et
branche selon le resultat. Peut avoir plusieurs transitions sortantes (une par cle de branche) et
une transition par defaut.

| Methode | Signature | Description |
|---|---|---|
| `Requete<TRequete, TResultat>` | `Requete<TRequete, TResultat>(Func<ContexteExecution, TRequete> fabrique, Func<TResultat, string> cleBranche) where TRequete : IRequete<TResultat>` | Cas general : fabrique la requete, execute, puis derive une cle de branche (`string`) du resultat. |
| `RequeteBool<TRequete>` | `RequeteBool<TRequete>(Func<ContexteExecution, TRequete> fabrique) where TRequete : IRequete<bool>` | Raccourci pour une requete booleenne, a utiliser avec `.SiVrai(...)`/`.SiFaux(...)`. |
| `Branche` | `ConstructeurNoeudDecision Branche(string cle, string noeudCibleId)` | Ajoute une transition pour la cle `cle`. Chainable pour plusieurs branches. |
| `SiVrai` | `ConstructeurNoeudDecision SiVrai(string noeudCibleId)` | Raccourci pour `Branche(ConstructeurNoeudDecision.CleVrai, noeudCibleId)`. |
| `SiFaux` | `ConstructeurNoeudDecision SiFaux(string noeudCibleId)` | Raccourci pour `Branche(ConstructeurNoeudDecision.CleFaux, noeudCibleId)`. |
| `Sinon` | `void Sinon(string noeudCibleId)` | Transition par defaut si aucune branche ne correspond a la cle retournee (appel terminal). |

Constantes : `ConstructeurNoeudDecision.CleVrai = "Vrai"`, `ConstructeurNoeudDecision.CleFaux = "Faux"`.

```csharp
// Cas booleen
c.NoeudDecision("decisionPaiement")
    .RequeteBool(ctx => new PaiementValideRequete(ctx.Variables.ObtenirRequis<Guid>("commandeId")))
    .SiVrai("attenteExpedition")
    .SiFaux("finRefus");

// Cas multi-branches
c.NoeudDecision("aiguillage")
    .Requete<StatutCommandeRequete, StatutCommande>(
        ctx => new StatutCommandeRequete(ctx.Variables.ObtenirRequis<Guid>("commandeId")),
        statut => statut.ToString())
    .Branche("EnPreparation", "attenteExpedition")
    .Branche("Annulee", "finAnnulee")
    .Sinon("finErreur");
```

**Requiert** `.Requete(...)` ou `.RequeteBool(...)`, et au moins une branche (`.Branche(...)`,
`.SiVrai(...)`/`.SiFaux(...)`, ou `.Sinon(...)`) avant `Construire()`. Si, a l'execution, aucune
branche ne correspond a la cle retournee et qu'aucune transition par defaut n'est definie, le
moteur leve une `BrancheIntrouvableException`.

---

## `ConstructeurNoeudAttenteDateHeure`

Suspend l'instance jusqu'a une echeance calculee. Transition sortante unique, empruntee lors de la
reprise (`IMoteurProcessus.ReprendreAsync`).

| Methode | Signature | Description |
|---|---|---|
| `Echeance` | `Echeance(Func<ContexteExecution, DateTime> calcul)` | Calcule l'echeance (UTC) a partir du contexte. |
| `EcheanceDans` | `EcheanceDans(Func<ContexteExecution, TimeSpan> calculDelai)` | Raccourci : echeance = instant d'execution du noeud + delai. |
| `Vers` | `void Vers(string noeudCibleId)` | Transition sortante (obligatoire, appel terminal). |

```csharp
c.NoeudAttenteDateHeure("attenteRelance")
    .EcheanceDans(ctx => TimeSpan.FromDays(3))
    .Vers("envoyerRelance");
```

**Requiert** `.Echeance(...)` ou `.EcheanceDans(...)` avant `Construire()`. La reprise effective
des instances echues n'est pas automatique : voir `ServiceReprisePlanifiee` dans le README, ou
interrogez `IInstanceProcessusRepository.ObtenirEnAttenteDateEchueAsync(...)` vous-meme.

---

## `ConstructeurNoeudAttenteSignal`

Suspend l'instance jusqu'a reception d'un signal externe nomme. Transition sortante unique,
empruntee lors de la reprise (`IMoteurProcessus.RecevoirSignalAsync`).

| Methode | Signature | Description |
|---|---|---|
| `Signal` | `Signal(string nomSignal)` | Nom du signal attendu. |
| `Vers` | `void Vers(string noeudCibleId)` | Transition sortante (obligatoire, appel terminal). |

```csharp
c.NoeudAttenteSignal("attenteExpedition")
    .Signal("ExpeditionConfirmee")
    .Vers("fin");
```

**Requiert** `.Signal(...)` avant `Construire()`. `RecevoirSignalAsync` verifie que le nom du
signal recu correspond exactement (`StringComparison.Ordinal`) a celui attendu.

---

## `ConstructeurNoeudTacheUtilisateur`

Suspend l'instance et cree une tache dans le systeme externe via `IFournisseurTacheUtilisateur`.
Transition sortante unique, empruntee lors de la reprise (`IMoteurProcessus.CompleterTacheAsync`).

| Methode | Signature | Description |
|---|---|---|
| `Tache` | `Tache(Func<ContexteExecution, DemandeCreationTache> fabrique)` | Fabrique la demande de creation de tache depuis le contexte. |
| `Vers` | `void Vers(string noeudCibleId)` | Transition sortante (obligatoire, appel terminal). |

```csharp
c.NoeudTacheUtilisateur("validationManager")
    .Tache(ctx => new DemandeCreationTache
    {
        Titre = "Valider la commande",
        Assignation = ctx.Variables.Obtenir<string>("managerId"),
    })
    .Vers("suite");
```

**Requiert** `.Tache(...)` avant `Construire()`, et une implementation de
`IFournisseurTacheUtilisateur` enregistree dans les services du client (sinon le moteur leve une
`InvalidOperationException` a l'execution de ce noeud, pas a la construction).

---

## `ConstructeurNoeudSousProcessus`

Demarre une instance d'un autre processus. Si le sous-processus se termine immediatement (sans
suspendre), l'execution du parent continue dans la meme boucle — donc la meme transaction ; sinon
le parent suspend et sa reprise, a la fin du sous-processus, est automatique. Transition sortante
unique, empruntee quand le sous-processus se termine.

| Methode | Signature | Description |
|---|---|---|
| `Processus` | `Processus(string codeProcessusFils, Func<ContexteExecution, object?>? variablesInitiales = null)` | Code de la definition fille a demarrer, et fabrique optionnelle des variables initiales de l'instance enfant. |
| `Vers` | `void Vers(string noeudCibleId)` | Transition sortante (obligatoire, appel terminal). |

```csharp
c.NoeudSousProcessus("notifierClient")
    .Processus("Notification", ctx => new { commandeId = ctx.Variables.ObtenirRequis<Guid>("commandeId") })
    .Vers("fin");
```

**Requiert** `.Processus(...)` avant `Construire()`. Le processus fils (`"Notification"` ci-dessus)
doit etre enregistre dans le meme `IDefinitionProcessusRegistre` que le parent avant l'execution.

---

## Validation a la construction

`Construire()` verifie, pour chaque noeud du graphe :

| Type de noeud | Verifications |
|---|---|
| Debut | Transition unique renseignee et cible existante. |
| Fin | Aucune (pas de transition sortante). |
| Systemique | `.Commande(...)` appele ; transition unique et cible existante. |
| Decision | `.Requete(...)`/`.RequeteBool(...)` appele ; au moins une branche ou une transition par defaut ; toutes les cibles existent. |
| Attente date/heure | `.Echeance(...)`/`.EcheanceDans(...)` appele ; transition unique et cible existante. |
| Attente signal | `.Signal(...)` appele ; transition unique et cible existante. |
| Tache utilisateur | `.Tache(...)` appele ; transition unique et cible existante. |
| Sous-processus | `.Processus(...)` appele ; transition unique et cible existante. |

Toute violation leve une `InvalidOperationException` avec un message identifiant le noeud fautif.
`Construire()` ne verifie en revanche pas l'existence du processus fils reference par un noeud
sous-processus (verifie seulement a l'execution, via le registre).
