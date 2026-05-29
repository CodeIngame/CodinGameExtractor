# CodinGame Log Extractor

Outil pour extraire les logs de replays et jouer des parties via l'API CodinGame.

## Prérequis

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Un compte CodinGame
- Package NuGet : [CommandLineParser](https://github.com/commandlineparser/commandline)

## Configuration

### 🔑 Variables d'environnement (obligatoires)

Les deux variables suivantes doivent être définies **avant** de lancer l'outil :

| Variable | Description | Utilisé par |
|----------|-------------|-------------|
| `CODINGAME_COOKIE` | Cookie d'authentification CodinGame | Extract + Play |
| `CODINGAME_SESSION` | `testSessionHandle` de la session de test | Extract + Play |

#### Définir les variables d'environnement

```powershell
# Pour la session PowerShell courante
$env:CODINGAME_COOKIE="rememberMe=VOTRE_VALEUR"
$env:CODINGAME_SESSION="votre_test_session_handle"
```

```powershell
# Persistant (toutes les sessions)
[System.Environment]::SetEnvironmentVariable("CODINGAME_COOKIE", "rememberMe=VOTRE_VALEUR", "User")
[System.Environment]::SetEnvironmentVariable("CODINGAME_SESSION", "votre_test_session_handle", "User")
```

#### Récupérer le cookie

1. **DevTools** (F12) → onglet **Network**
2. Cliquer sur n'importe quelle requête vers `codingame.com`
3. Dans **Headers**, copier la valeur du header `Cookie`

Le cookie ressemble à :
```
intercom-id-xxx=...; rememberMe=...; cgSession=...; AWSALB=...; AWSALBCORS=...
```

Vous pouvez utiliser le cookie **complet** ou juste `rememberMe=VOTRE_VALEUR`.

#### Récupérer le `testSessionHandle`

1. Ouvrir le challenge sur CodinGame (ex: `https://www.codingame.com/ide/challenge/...`)
2. Lancer un batch de parties (test session)
3. Ouvrir les **DevTools** (F12) → onglet **Network**
4. Chercher l'appel à `findLastBattlesByTestSessionHandle`
5. Copier le premier paramètre du body (c'est le `testSessionHandle`)

---

## Commandes

L'outil utilise [CommandLineParser](https://github.com/commandlineparser/commandline) avec 2 verbes :

```
dotnet run --project CodinGameExtractor -- extract [options]
dotnet run --project CodinGameExtractor -- play <codeFile> [options]
dotnet run --project CodinGameExtractor -- --help
```

---

## Mode 1 : `extract` (extraction des logs d'arena)

Extrait les logs de toutes les parties d'une test session existante (arena battles).

### Utilisation

```powershell
# Lancement simple
dotnet run --project CodinGameExtractor -- extract

# Avec options
dotnet run --project CodinGameExtractor -- extract -o ./mes_logs -c 10 --user 1234567
```

### Options

| Option | Court | Défaut | Description |
|--------|-------|--------|-------------|
| `--user <id>` | `-u` | `0` (auto-détecté) | Votre userId CodinGame |
| `--output <dir>` | `-o` | `./codingame_logs` | Dossier de sortie |
| `--concurrency <n>` | `-c` | `5` | Nombre de requêtes parallèles max |

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

## Mode 2 : `play` (jouer des parties)

Lance X parties contre le boss ou un joueur spécifique via l'API CodinGame, en utilisant votre code source local.

### Utilisation

```powershell
# Jouer 10 parties contre le boss (langage auto-détecté via l'extension)
dotnet run --project CodinGameExtractor -- play mon_bot.cs -n 10

# Jouer 5 parties contre un joueur spécifique
dotnet run --project CodinGameExtractor -- play mon_bot.cs -n 5 --player 1234567

# Avec un seed manuel
dotnet run --project CodinGameExtractor -- play mon_bot.cs -n 1 --seed 42

# Code Python (langage auto-détecté → Python3)
dotnet run --project CodinGameExtractor -- play mon_bot.py -n 20 --boss

# Forcer un langage spécifique
dotnet run --project CodinGameExtractor -- play mon_bot.cs --lang Java
```

### Arguments et options

| Argument / Option | Court | Défaut | Description |
|-------------------|-------|--------|-------------|
| `<codeFile>` | | *(obligatoire)* | Chemin vers le fichier source de votre bot |
| `--games <n>` | `-n` | `10` | Nombre de parties à jouer |
| `--boss` | | *(défaut)* | Jouer contre le boss |
| `--player <id>` | | — | Jouer contre un joueur spécifique (userId) |
| `--seed <seed>` | `-s` | *(auto)* | Seed manuel (sinon généré automatiquement) |
| `--output <dir>` | `-o` | `./codingame_play_logs` | Dossier de sortie |
| `--lang <id>` | `-l` | *(auto-détecté)* | Identifiant du langage de programmation |

### Auto-détection du langage

Le langage est automatiquement déterminé via l'extension du fichier de code :

| Extension | Langage |
|-----------|---------|
| `.cs` | C# |
| `.java` | Java |
| `.py` | Python3 |
| `.js` | Javascript |
| `.ts` | Typescript |
| `.cpp`, `.cc`, `.cxx` | C++ |
| `.c` | C |
| `.rs` | Rust |
| `.go` | Go |
| `.rb` | Ruby |
| `.kt`, `.kts` | Kotlin |
| `.scala` | Scala |
| `.swift` | Swift |
| `.php` | PHP |
| `.hs` | Haskell |
| `.d` | D |
| `.dart` | Dart |
| `.lua` | Lua |
| `.bash`, `.sh` | Bash |
| `.fs` | F# |
| `.vb` | VB.NET |
| `.groovy` | Groovy |
| `.pas` | Pascal |
| `.m` | Objective-C |
| `.pl` | Perl |

L'option `--lang` permet de forcer un langage si l'auto-détection ne convient pas.

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
