# CodinGame Log Extractor

Outil pour extraire les logs de replays depuis l'API CodinGame.

## Prérequis

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Un compte CodinGame

## Configuration

### Récupérer le `testSessionHandle`

1. Ouvrir le challenge sur CodinGame (ex: `https://www.codingame.com/ide/challenge/...`)
2. Lancer un batch de parties (test session)
3. Ouvrir les **DevTools** (F12) → onglet **Network**
4. Chercher l'appel à `findLastBattlesByTestSessionHandle`
5. Copier le premier paramètre du body (c'est le `testSessionHandle`)

## Utilisation

```powershell
dotnet run -- "<testSessionHandle>"
```

Le programme vous demandera de coller votre cookie d'authentification :

```
🔑 Cookie d'authentification requis.
   (DevTools F12 → Network → Clic sur une requête codingame → Headers → Cookie)
   Vous pouvez coller le cookie complet ou juste rememberMe=...

   Collez votre cookie: 
```

Vous pouvez coller :
- Le cookie **complet** : `cgSession=...; rememberMe=...; AWSALB=...; AWSALBCORS=...`
- Ou juste `rememberMe=VOTRE_VALEUR`

Pour récupérer le cookie complet :
1. **DevTools** (F12) → onglet **Network**
2. Cliquer sur n'importe quelle requête vers `codingame.com`
3. Dans **Headers**, copier la valeur du header `Cookie`

### Arguments optionnels

```
dotnet run -- <testSessionHandle> [userId] [outputDirectory] [maxConcurrentRequests]
```

| # | Argument | Défaut | Description |
|---|----------|--------|-------------|
| 1 | `testSessionHandle` | *(hardcodé)* | Identifiant de la session de test |
| 2 | `userId` | `3138357` | Votre userId CodinGame |
| 3 | `outputDirectory` | `./codingame_logs` | Dossier de sortie |
| 4 | `maxConcurrentRequests` | `5` | Requêtes parallèles max |

> **Astuce** : vous pouvez définir `$env:CODINGAME_COOKIE` pour éviter le prompt à chaque exécution :
> ```powershell
> # Pour la session courante
> $env:CODINGAME_COOKIE="cgSession=...; rememberMe=...; AWSALB=...; AWSALBCORS=..."
> dotnet run -- "<testSessionHandle>"
> ```
>
> Pour la rendre persistante (permanent pour l'utilisateur) :
> ```powershell
> [System.Environment]::SetEnvironmentVariable("CODINGAME_COOKIE", "cgSession=...; rememberMe=...; AWSALB=...; AWSALBCORS=...", "User")
> ```

## Sortie

Les logs sont sauvegardés par résultat (win/loss), puis par tranche de rank adverse. Un fichier `summary.md` est généré à la racine avec les statistiques globales.

```
codingame_logs/
  summary.md
  win/
    0001-0100/
      WIN_0042_Boriza_seed=373368691909388100_gameId=876334184.txt
      WIN_0087_PlayerX_seed=...txt
    0101-0200/
      WIN_0150_SomeBot_seed=...txt
  loss/
    0001-0100/
      LOSS_0023_TopPlayer_seed=...txt
    0201-0300/
      LOSS_0250_OtherBot_seed=...txt
    timeout/
      0001-0100/
        TIMEOUT_0015_FastBot_seed=...txt
```

### Noms de fichiers

Format : `{RESULT}_{RANK}_{PSEUDO}_seed={SEED}_gameId={ID}.txt`

- **RESULT** : `WIN`, `LOSS` ou `TIMEOUT` (vrai timeout referee)
- **RANK** : rank adverse sur 4 chiffres
- **PSEUDO** : pseudo de l'adversaire

### En-tête de chaque fichier log

```
GAME_ID: 876622856
REPLAY: https://www.codingame.com/replay/876622856
SEED: 956876924376484500
SCORES: 9, 11
SCORE_DIFF: -2
TURNS: 200
RANKS: 1, 0
AGENT_0: QzL (score=21,04, rank=132, valid=True)
AGENT_1: Boriza (score=27,31, rank=12, valid=True)
FALLBACK_TIMEOUTS: 3
```

- `SCORE_DIFF` : différence de score (positif = avantage, négatif = défaite)
- `TURNS` : nombre de tours joués
- `FALLBACK_TIMEOUTS` : nombre de fois où votre bot a détecté un manque de temps et donné une réponse rapide (affiché uniquement si > 0)

### `summary.md`

Fichier Markdown généré à la racine avec :

- **Statistiques globales** : games, wins, losses, winrate, timeouts, fallback TOs
- **Winrate par tranche de rank** : tableau W/L/TO/winrate par bucket (0001-0100, 0101-0200, etc.)
- **Tableau des losses** : rank, adversaire, score diff, tours, fallbacks, lien replay
- **Tableau des wins** : idem

### Détection timeout vs fallback

- **TIMEOUT** (dossier `loss/timeout/`) : vrai timeout détecté par le referee CodinGame (`has not provided`)
- **FALLBACK_TIMEOUTS** : votre bot a détecté un manque de temps et a donné une réponse rapide pour *éviter* un vrai timeout (ex: `Bot 2: TIMEOUT Defend -> Left 1ms` dans stderr)

### Autres

- Le contenu du dossier est nettoyé à chaque exécution
- Les fichiers sont triés par rank adverse
- Chaque fichier contient : en-tête, puis le détail de chaque turn (stderr, stdout, summary)
