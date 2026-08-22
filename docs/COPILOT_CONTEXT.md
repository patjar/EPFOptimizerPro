# EPFOptimizerPro - Contexte Copilot

Derniere version stable connue : **v3.9.97 stable**
Depot GitHub : https://github.com/patjar/EPFOptimizerPro.git
Branche locale au moment de la creation : **main**
Commit local au moment de la creation : **f1d897b**

## Mode de travail prefere

- Repondre en francais, ton direct, pragmatique, style mode combat.
- Une seule etape a la fois.
- Une seule commande ou un seul script a appliquer, puis attendre le resultat.
- Preferer les scripts PowerShell telechargeables aux longs blocs de code a copier-coller.
- Toujours valider par `dotnet build .\EPFOptimizerPro.csproj -c Release` avant d aller plus loin.
- Ne jamais enchainer publication, tag, release ou signature tant que le build n est pas confirme OK.
- Pour les gros changements, creer un backup avant modification.
- Eviter les patchs regex massifs sur des fichiers C# critiques.
- Preferer des modifications ciblees, petites, verifiables.
- Si une erreur arrive, corriger vite, sans justification inutile.

## Etat projet connu

- Application : **EPFOptimizerPro**.
- Type : application Windows desktop .NET / WPF.
- Environnement de travail habituel : Windows 11, PowerShell, Git, GitHub, MSI.
- Version stable publiee connue : **v3.9.97 stable**.
- La version v3.9.97 etait un checkpoint de validation auto-update avec bump depuis v3.9.96.
- Le projet compile a nouveau apres recuperation du patch UpdateDetails rate.
- Le fichier `MainWindow.xaml.cs` etait autour de 745 lignes lors des dernieres optimisations.

## Services deja extraits ou connus

- `UpdateStatusFormatter`
- `ApplicationVersionProvider`
- `SystemPrivilegeService`
- `StartupAdviceProvider`
- `UpdateUiTextProvider`
- `ActionStatusTextProvider`
- `AppLogTextProvider`
- `GitHubUpdateService`
- `AdaptiveTaskEngine`
- `AiAdvisorService`

## Workflow release habituel

1. Modifier le code de facon ciblee.
2. Lancer un build Release.
3. Tester localement.
4. Commit Git avec message clair.
5. Incrementer la version si necessaire.
6. Construire le MSI.
7. Signer le MSI avec Authenticode.
8. Verifier la signature.
9. Publier le tag et la release GitHub.
10. Verifier l asset MSI et le hash SHA256.
11. Tester un cycle d auto-update depuis une ancienne version.

## Regles de securite projet

- Ne pas integrer de token personnel GitHub dans l application publique.
- Conserver la verification MSI avant installation.
- Conserver la verification Authenticode.
- Conserver le wrapper PowerShell de diagnostic d installation MSI.
- Ne pas casser le flux auto-update GitHub existant.
- Ne pas modifier simultanement update, MSI, signature et UI sans validation intermediaire.

## Point sensible recent

Une tentative de patch automatique pour enrichir la tache `Updates` a casse `Services\AdaptiveTaskEngine.cs` en injectant du PowerShell hors chaine C#.
Le projet a ete recupere avec un script de restauration depuis backup, puis le build Release a reussi.

Important pour la suite :

- Ne plus injecter de long script PowerShell multiline dans C# avec un patch regex non teste.
- Pour enrichir `Updates`, faire une modification beaucoup plus petite.
- Commencer par afficher uniquement le detail Windows Update dans le message existant.
- Ajouter Microsoft Store ensuite, dans une etape separee.
- Ajouter les options Windows Update / Microsoft Store / Pilotes / Facultatives dans une future etape separee.

## Objectif fonctionnel souhaite pour Updates

A terme, lors du survol ou clic sur la tache **Updates**, afficher davantage que :

```text
Mises a jour disponibles : 1
```

Objectif vise :

```text
Windows Update : 2
- KBxxxx / titre de mise a jour
- Defender Platform Update

Microsoft Store : 3
- Microsoft Photos
- PowerToys
- Calculatrice

Options :
- Windows Update : actif
- Microsoft Store : actif
- Pilotes : non inclus
- Updates facultatives : non incluses
```

Mais l implementation doit etre progressive et verifiee a chaque build.

## Commandes de validation rapides

```powershell
git status --short
dotnet build .\EPFOptimizerPro.csproj -c Release
(Get-Content .\MainWindow.xaml.cs).Count
Select-String -Path .\Services\AdaptiveTaskEngine.cs -Pattern 'AddTask\("Updates"|Updates' -CaseSensitive:$false
```

## Etat Git au moment de la creation du contexte

```text
M EPFOptimizerPro.csproj
 M MainWindow.xaml.cs
?? MainWindow.xaml.bak-update-tooltip-v2-20260817-151633
?? MainWindow.xaml.before-recover-20260817-152649
?? MainWindow.xaml.cs.before-ternary-fix-20260817-152649
?? Services/AdaptiveTaskEngine.cs.bak-update-details-20260817-151404
?? Services/AdaptiveTaskEngine.cs.bak-update-details-v2-20260817-151633
?? Services/AdaptiveTaskEngine.cs.before-recover-20260817-152649
```

## Phrase de reprise pour une nouvelle conversation

Copier-coller ceci dans une nouvelle conversation :

```text
On reprend EPFOptimizerPro. Lis et respecte docs\COPILOT_CONTEXT.md. Mode combat : une seule etape ou un seul script a la fois. Derniere stable connue : v3.9.97. Avant toute modification, demande ou analyse git status --short et build Release.
```
## Synchronisation lors d'un changement d'ordinateur

Un script PowerShell dedie existe pour mettre a jour ou cloner le depot local lorsque Patrick change d'ordinateur de travail :

```text
Update-EPFOptimizerPro-GitLocal-WorkstationSwitch-v1.0.ps1
```

Regle importante pour Copilot :
- proposer ce script telechargeable plutot qu'une simple liste de commandes Git ;
- conserver exactement son nom explicite ;
- le script cible par defaut `Documents\EPFOptimizerPro-Clean` ;
- il clone le depot s'il est absent ;
- il refuse la synchronisation si des changements locaux non committes existent ;
- il utilise `fetch --prune`, `switch main` et `pull --ff-only` ;
- il verifie que le depot local est propre et synchronise avec `origin/main` ;
- il ne doit jamais executer `reset --hard` ni ecraser automatiquement des changements locaux ;
- apres synchronisation, valider avec `dotnet build .\EPFOptimizerPro.csproj -c Release`.
