# CodinGame Log Extractor

Outil pour extraire les logs de replays et jouer des parties via l'API CodinGame.

## Prérequis

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Un compte CodinGame

## Configuration

### 🔑 Cookie d'authentification (obligatoire)

Le cookie est nécessaire pour les deux modes. **Il est recommandé de le définir en variable d'environnement** pour éviter de le saisir à chaque exécution.

#### Récupérer le cookie

1. **DevTools** (F12) → onglet **Network**
2. Cliquer sur n'importe quelle requête vers `codingame.com`
3. Dans **Headers**, copier la valeur du header `Cookie`

Le cookie ressemble à :
```
intercom-id-xxx=...; rememberMe=...; cgSession=...; AWSALB=...; AWSALBCORS=...
```

Vous pouvez utiliser le cookie **complet** ou juste `rememberMe=VOTRE_VALEUR`.

#### Définir la variable d'environnement (recommandé)

```powershell
# Pour la session PowerShell courante
$env:CODINGAME_COOKIE="rememberMe=VOTRE_VALEUR"
```

```powershell
# Persistant (toutes les sessions)
[System.Environment]::SetEnvironmentVariable("CODINGAME_COOKIE", "rememberMe=VOTRE_VALEUR", "User")
```

> Si le cookie n'est pas défini, le programme le demandera interactivement.

### Récupérer le `testSessionHandle`

1. Ouvrir le challenge sur CodinGame (ex: `https://www.codingame.com/ide/challenge/...`)
2. Lancer un batch de parties (test session)
3. Ouvrir les **DevTools** (F12) → onglet **Network**
4. Chercher l'appel à `findLastBattlesByTestSessionHandle`
5. Copier le premier paramètre du body (c'est le `testSessionHandle`)

Le `testSessionHandle` peut être hardcodé directement dans `Program.cs` pour éviter de le passer à chaque exécution.

### Variables d'environnement

| Variable | Description | Utilisé par |
|----------|-------------|-------------|
| `CODINGAME_COOKIE` | Cookie d'authentification CodinGame | Extract + Play |
| `CODINGAME_OUTPUT` | Dossier de sortie | Extract + Play |
| `CODINGAME_SESSION` | `testSessionHandle` par défaut | Play |

---

## Mode 1 : Extract (extraction des logs d'arena)

Extrait les logs de toutes les parties d'une test session existante (arena battles).

### Utilisation

```powershell
# Lancement simple (utilise le testSessionHandle hardcodé dans Program.cs)
dotnet run --project CodinGameExtractor

# Avec un testSessionHandle spécifique
dotnet run --project CodinGameExtractor -- "votre_test_session_handle"
```

### Arguments positionnels

```
dotnet run --project CodinGameExtractor -- [testSessionHandle] [userId] [outputDirectory] [maxConcurrentRequests] [cookie]
```

| # | Argument | Défaut | Description |
|---|----------|--------|-------------|
| 1 | `testSessionHandle` | *(hardcodé dans Program.cs)* | Identifiant de la session de test |
| 2 | `userId` | `0` (auto-détecté) | Votre userId CodinGame |
| 3 | `outputDirectory` | `CODINGAME_OUTPUT` ou `./codingame_logs` | Dossier de sortie |
| 4 | `maxConcurrentRequests` | `5` | Requêtes parallèles max |
| 5 | `cookie` | `CODINGAME_COOKIE` | Cookie d'authentification |

> **Note** : passer une chaîne vide `""` pour un argument positionnel l'ignore et conserve la valeur par défaut.

### Détection automatique du userId

Le programme tente de détecter votre userId dans l'ordre suivant :
1. Via l'API ranking (si disponible)
2. Via l'API session (`CodinGamer/getMyProperties`) avec le cookie
3. Extraction depuis le cookie `rememberMe`

### Sortie (Extract)

```
codingame_logs/
  summary.md
  win/
    0001-0100/
      WIN_0042_Boriza_seed=373368691909388100_gameId=876334184.txt
    0101-0200/
      WIN_0150_SomeBot_seed=...txt
  loss/
    0001-0100/
      LOSS_0023_TopPlayer_seed=...txt
    timeout/
      0001-0100/
        TIMEOUT_0015_FastBot_seed=...txt
```

---

## Mode 2 : Play (jouer des parties)

Lance X parties contre le boss ou un joueur spécifique via l'API CodinGame, en utilisant votre code source local.

### Utilisation

```powershell
# Jouer 10 parties contre le boss
dotnet run --project CodinGameExtractor -- play mon_bot.cs 10

# Jouer 5 parties contre un joueur spécifique
dotnet run --project CodinGameExtractor -- play mon_bot.cs 5 --player 1234567

# Avec un seed manuel
dotnet run --project CodinGameExtractor -- play mon_bot.cs 1 --seed 42

# Avec un testSessionHandle spécifique
dotnet run --project CodinGameExtractor -- play mon_bot.cs 10 --session "votre_handle"
```

### Syntaxe

```
dotnet run --project CodinGameExtractor -- play <codeFile> [numberOfGames] [options]
```

### Arguments et options

| Argument / Option | Défaut | Description |
|-------------------|--------|-------------|
| `<codeFile>` | *(obligatoire)* | Chemin vers le fichier source de votre bot |
| `[numberOfGames]` | `1` | Nombre de parties à jouer |
| `--boss` | *(défaut)* | Jouer contre le boss |
| `--player <id>` | — | Jouer contre un joueur spécifique (userId) |
| `--seed <seed>` | *(auto)* | Seed manuel (sinon généré automatiquement) |
| `--session <handle>` | `CODINGAME_SESSION` | `testSessionHandle` à utiliser |
| `--output <dir>` | `CODINGAME_OUTPUT` ou `./codingame_play_logs` | Dossier de sortie |
| `--lang <id>` | `C#` | Identifiant du langage de programmation |
| `--cookie <cookie>` | `CODINGAME_COOKIE` | Cookie d'authentification |

### Sortie (Play)

Les résultats sont organisés par win/loss dans le dossier de sortie, avec un `summary.md` récapitulatif.

---

## Format des fichiers logs

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
