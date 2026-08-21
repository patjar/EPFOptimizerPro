# EPFOptimizerPro - Feuille de route

## Vision

EPFOptimizerPro évolue vers une plateforme Windows d'analyse, de maintenance et d'assistance intelligente.

Le projet repose sur un principe directeur :

> **Un moteur unique, deux expériences complémentaires.**

- **Mode visuel** : l'utilisateur explore, comprend et contrôle son système grâce à des cartes, indicateurs, graphiques et détails techniques.
- **Mode intelligent** : l'application analyse les résultats, classe les anomalies, prépare un plan et recommande des actions sûres.

Les deux modes doivent partager le même socle :

- `AdaptiveTaskCatalog`
- `AdaptiveTaskEngine`
- `AdaptiveTaskResult`
- `IncrementalTaskPlanner`
- `TaskExecutionMetadataStore`

---

## Principes de développement

Chaque évolution doit rester petite, réversible et vérifiable.

Une nouvelle tâche doit fournir :

1. une définition centralisée dans le catalogue ;
2. un résultat structuré ;
3. un affichage visuel ;
4. une règle de recommandation intelligente ;
5. des métadonnées d'exécution ;
6. une sortie exploitable dans le rapport HTML ;
7. un comportement contrôlé lors d'une relance individuelle.

Le Centre IA ne doit jamais :

- inventer une commande PowerShell ;
- lancer une commande libre ;
- contourner le catalogue ;
- modifier directement le Registre ;
- exécuter une réparation fondée sur une supposition ;
- présenter une information inconnue comme une anomalie certaine ;
- transmettre des journaux à un service externe sans action explicite.

---

# Axe 1 - Catalogue et tâches actives

## Objectif

Faire évoluer le catalogue actuel vers un référentiel complet de diagnostics et d'actions capables d'alimenter l'audit, l'optimisation, le tableau de bord, le Centre IA et les rapports.

## Modèle cible d'une tâche

`AdaptiveTaskDefinition` doit progressivement devenir la source unique de vérité avec les propriétés suivantes :

```text
Name
Category
Description
Command
TimeoutSeconds
AvailableInAudit
AvailableInOptimize
ExecutionKind
RiskLevel
DurationKind
CanManualRerun
CanAutoRun
RequiresConfirmation
RequiresAdministrator
HaloKind
ResultParserKey
RecommendationKey
```

Les confirmations, durées, risques, possibilités de relance et indicateurs visuels doivent être déduits du catalogue, sans listes nominales dupliquées dans les services ou le XAML.

## Résultat structuré cible

Chaque tâche doit progressivement produire :

```text
TaskName
Verdict
Severity
Summary
Evidence
RecommendedTaskName
EvaluatedAt
```

Gravités prévues :

- Information
- Success
- Warning
- Critical
- Unknown

`Unknown` doit rester distinct d'une erreur système.

---

## Famille Santé Windows

### Première vague

- Redémarrage en attente
- Espace disque système
- DISM CheckHealth
- Services critiques

### Deuxième vague

- DISM ScanHealth
- Intégrité Windows avancée
- Analyse du magasin de composants

### Règles

- Les diagnostics restent non destructifs.
- `DISM ScanHealth` n'est pas lancé systématiquement si `CheckHealth` indique un magasin sain.
- Les services critiques sont d'abord contrôlés en lecture seule.
- Un redémarrage en attente doit être signalé avant les opérations longues de réparation.

---

## Famille Sécurité

Contrôles prévus :

- Pare-feu Windows
- Microsoft Defender
- BitLocker
- Secure Boot
- TPM
- UAC

Première implémentation strictement informative : aucune correction automatique.

Le contrôle Defender doit distinguer :

- Defender actif ;
- Defender remplacé par une autre solution de sécurité ;
- information indisponible.

---

## Famille Réseau

Contrôles prévus :

- Configuration IP
- Connectivité de la passerelle
- Connectivité Internet
- Proxy Windows
- DNS configurés
- Adaptateurs réseau
- État Winsock

Actions séparées :

- DNS Flush
- Réparation Winsock sur demande

Les diagnostics et les réparations doivent rester deux tâches distinctes.

---

## Famille Maintenance

Mesures prévues :

- Taille de `Temp User`
- Taille de `Temp Win`
- Taille de la Corbeille
- Cache Windows Update
- Rapports d'erreurs Windows
- Journaux temporaires
- Miniatures Windows

Chaque nettoyage doit idéalement produire :

```text
Taille avant nettoyage
Taille après nettoyage
Espace réellement récupéré
```

---

## Famille Applications et mises à jour

Contrôles prévus :

- Applications obsolètes
- Mises à jour Microsoft Store
- Disponibilité de winget
- Sources winget
- Applications au démarrage
- Applications installées récemment

Les diagnostics Windows Update, Microsoft Store et applications doivent rester identifiables séparément dans les résultats structurés.

---

# Axe 2 - Mode visuel et graphique

## Objectif

Transformer les résultats techniques en informations immédiatement compréhensibles, sans masquer les preuves et les détails d'exécution.

## Vue par familles

Familles prévues :

- Santé Windows
- Sécurité
- Réseau
- Maintenance
- Applications et mises à jour

Chaque carte de famille doit afficher :

- nombre de contrôles terminés ;
- nombre de contrôles actifs ;
- nombre d'attentions ;
- nombre d'erreurs ;
- nombre de résultats inconnus ou indisponibles.

## Graphiques prévus

### Anneau d'état global

- Sain
- Attention
- Critique
- Inconnu

### Barres par famille

Comparaison synthétique de la santé des différentes familles.

### Évolution de la session

- avant optimisation ;
- après optimisation ;
- après relance manuelle.

### Gains de nettoyage

Afficher uniquement des valeurs réellement mesurées avant et après l'action.

## Interaction

Une carte de famille doit pouvoir révéler :

- le nom du contrôle ;
- le verdict ;
- le résumé ;
- les preuves ;
- la durée ;
- l'origine du résultat ;
- l'heure d'évaluation ;
- les actions disponibles.

---

# Axe 3 - Mode intelligent et Centre IA

## Objectif

Faire évoluer EPFOptimizerPro d'un moteur d'exécution vers un moteur de diagnostic assisté et de conseil sécurisé.

## Capacités prévues

Le Centre IA doit pouvoir :

- résumer une session ;
- détecter les contrôles manquants ;
- classer les anomalies ;
- expliquer un résultat ;
- proposer une tâche connue du catalogue ;
- construire un plan recommandé ;
- réévaluer la situation après exécution.

## Synthèse automatique

Exemple de restitution :

```text
12 contrôles terminés
2 anomalies détectées
3 résultats réutilisés
1 tâche relancée manuellement
Aucune erreur d'exécution
```

## Niveaux d'automatisation

### Manuel

L'application analyse et l'utilisateur choisit chaque tâche.

### Assisté

L'application prépare un plan et l'utilisateur le valide. Les confirmations sensibles sont conservées.

### Automatique sécurisé

Une tâche ne peut être exécutée automatiquement que si elle est :

- connue du catalogue ;
- explicitement autorisée ;
- sans conflit avec une autre exécution ;
- classée sans risque ou à faible risque ;
- configurée sans confirmation obligatoire.

Les tâches à risque moyen ou élevé restent soumises à confirmation ou interdites en automatique.

## Chaîne de responsabilité

```text
Centre IA
  -> analyse les résultats structurés
  -> propose une tâche connue

Catalogue
  -> vérifie la définition
  -> expose le risque et les autorisations
  -> fournit la commande validée

Moteur
  -> contrôle les conflits
  -> exécute
  -> journalise
  -> conserve les métadonnées
```

---

# Plan de versions

## V4.0.1-A - Catalogue enrichi

- Ajouter les catégories et descriptions.
- Ajouter les types d'exécution, de risque et de durée.
- Ajouter les règles de relance, d'automatisation et de confirmation.
- Préserver strictement le comportement existant.

## V4.0.1-B - Catalogue comme source unique

- Supprimer les listes nominales dupliquées.
- Générer les confirmations depuis les définitions.
- Déduire les comportements visuels depuis les métadonnées du catalogue.

## V4.0.1-C - Résultats structurés

- Ajouter `AdaptiveTaskResult`.
- Ajouter les verdicts, gravités et preuves.
- Adapter une première tâche de bout en bout.

## V4.0.1-D - Première tranche verticale

- Ajouter les premiers contrôles Santé Windows.
- Produire leurs résultats structurés.
- Afficher les résultats dans le mode visuel.
- Exploiter les mêmes résultats dans le Centre IA.

## V4.0.1-E - Regroupement visuel

- Créer les cartes par famille.
- Ajouter le résumé global.
- Préparer les graphiques sans surcharger l'interface principale.

## V4.0.1-F - Famille Sécurité

- Pare-feu
- Defender
- BitLocker
- Secure Boot
- TPM
- UAC

## V4.0.1-G - Famille Réseau

- Configuration IP
- Passerelle
- Internet
- Proxy
- Adaptateurs
- DNS configurés

## V4.0.1-H - Maintenance mesurée

- Mesures avant nettoyage
- Nettoyages ciblés
- Mesures après nettoyage
- Gains réels dans les rapports et graphiques

## V4.1 - Centre IA version 1

- État général
- Points d'attention
- Explications
- Plan recommandé
- Historique de session
- Tâches proposées

La première version reste sans zone de commande libre.

## V4.2 - Mode assisté avancé

- Plans multi-étapes
- Validation par niveau de risque
- Réévaluation après chaque action
- Exécution automatique des seules tâches sûres et autorisées

## V5.0 - Plateforme intelligente unifiée

- Tableau de bord visuel complet
- Diagnostic intelligent consolidé
- Historique local multi-sessions
- Tendances dans le temps
- Recommandations contextuelles
- Plans automatiques sécurisés

---

# Matrice de validation

Chaque nouvelle tâche doit valider :

- nom unique ;
- catégorie correcte ;
- commande connue et validée ;
- délai d'expiration cohérent ;
- besoin administrateur déclaré ;
- annulation prise en charge ;
- absence d'exécution simultanée non autorisée ;
- résultat structuré ;
- métadonnées renseignées ;
- relance explicitement autorisée ou refusée ;
- info-bulle correcte ;
- rendu du Centre IA correct ;
- rapport HTML correct ;
- absence de duplication après un second cycle Optimiser.

## Cycle de test standard

1. `dotnet build .\EPFOptimizerPro.csproj -c Release`
2. Lancer l'application.
3. Exécuter Audit.
4. Exécuter un premier cycle Optimiser.
5. Exécuter un second cycle Optimiser.
6. Tester la relance individuelle.
7. Vérifier les métadonnées.
8. Vérifier le Centre IA.
9. Vérifier le rapport HTML.
10. Exécuter `git diff --check`.
11. Tester sur le second PC à chaque checkpoint majeur.

---

# Ordre de priorité

1. Consolider le catalogue enrichi.
2. Supprimer les règles dupliquées.
3. Stabiliser les résultats structurés et leur formatage.
4. Terminer la première tranche Santé Windows.
5. Construire les cartes visuelles à partir des mêmes résultats.
6. Construire les recommandations du Centre IA.
7. Ajouter les familles Sécurité, Réseau et Maintenance.
8. Étendre progressivement le mode assisté.

---

# Règle finale

> Une nouvelle tâche doit alimenter simultanément le moteur, le mode visuel, le Centre IA et le rapport.
>
> Aucun axe ne doit évoluer seul au point de créer une seconde logique métier ou une source de vérité concurrente.
