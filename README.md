# Implementeringsprojekt_RAD

Implementering af implementeringsprojektet i RAD med fokus på datastrømsanalyse.

Projektet indeholder:
- Multiply-shift hashing
- Multiply-mod-prime hashing med $p = 2^{89} - 1$
- Hashtabel med chaining
- Beregning af kvadratsummen $S = \sum_x s(x)^2$
- 4-universel hashfunktion til Count-Sketch

## Struktur

- `program.cs` indeholder hele implementeringen og benchmark-koden.
- `opgave3_results.csv` indeholder resultater fra Opgave 3.

## Krav

Projektet er lavet i C# og bygger på .NET 8.

## Kørsel

Build projektet med:

```powershell
dotnet build
```

Kør programmet med standardparametre:

```powershell
dotnet run
```

Du kan også angive egne værdier for `n` og `l`:

```powershell
dotnet run -- 200000 16
```

Kør Opgave 7-eksperimenterne med:

```powershell
dotnet run -- opgave7 200000
```

Du kan også angive egne værdier for `n`, `l`-grænsen og `t`-værdierne:

```powershell
dotnet run -- opgave7 200000 17 10,12,14 100
```

Dette skriver `opgave7_summary.csv` samt per-`t` CSV-filer med rå, sorterede og median-baserede estimater.

## Bemærkning om filer

`.gitignore` er sat op til at ignorere `bin/` og `obj/`, så build-filer ikke skal med i aflevering eller versionering.
