# GhostPrompter — Spécifications du MVP

Version révisée le 9 septembre 2026. Ce document remplace la spécification initiale et intègre les décisions de revue.

# 1. Objet du projet

Développer une application Windows légère de prompteur en surimpression, principalement pour enregistrer des démonstrations techniques, pédagogiques ou musicales sur un seul écran.

Le présentateur manipule Visual Studio, PowerPoint, un navigateur, VCV Rack ou une autre application tout en lisant un script superposé. L'application utilisée pour la démonstration conserve le focus. Les commandes de lecture fonctionnent par raccourcis clavier globaux.

GhostPrompter est visible pour le présentateur et absent des enregistrements dont la chaîne de capture respecte l'exclusion Windows. La compatibilité est validée avec Camtasia en priorité, puis OBS. Un retour API positif ne suffit pas à la prouver.

Deux modes :

- Blocks : blocs logiques présentés sous forme de pages successives contrôlées manuellement.
- Scroll : texte continu défilant automatiquement à vitesse réglable, avec un point de départ vertical réglable, au milieu par défaut.

La fenêtre reste au premier plan, peut être transparente, ne prend pas le focus en présentation et laisse passer les interactions souris lorsque Click Through est activé.

# 2. Périmètre

## Inclus dans le MVP

- Application WPF en C# et .NET 10, MVVM avec CommunityToolkit.Mvvm.
- Chargement et rechargement manuel de TXT, Markdown et DOCX en lecture seule.
- Import en texte brut suivi d'un parser GhostPrompter commun.
- Titres facultatifs [Titre] et séparateurs de blocs ---.
- Blocks avec pagination automatique des blocs trop longs.
- Scroll avec lecture/pause, vitesse, retour au début et hauteur de départ réglables.
- MainWindow de configuration et PrompterWindow séparées.
- TopMost, présentation sans activation, verrouillage, Click Through et hotkeys globaux.
- Position, dimensions, police et opacités indépendantes du fond et du texte réglables.
- Progression adaptée au mode, état de l'exclusion et de chaque raccourci.
- Persistance locale, journal minimal remplacé à chaque lancement.
- Documentation et recette réelle Camtasia puis OBS.
- Publication en EXE autonome self-contained single-file win-x64.

## Hors MVP

Ne pas ajouter d'éditeur, modification ou sauvegarde de script, intégration PowerPoint, synchronisation avec les slides, reconnaissance vocale, synchronisation à la voix, MIDI, intégration spécifique Stream Deck ou pédale USB, télécommande réseau, application mobile, cloud, compte utilisateur, mise à jour automatique, télémétrie, IA, résumé ou réécriture du script.

La pagination est une opération de mise en page sans modification du fichier ni réécriture du texte.

# 3. Plateforme et conventions

- C#, .NET 10, WPF, Windows uniquement.
- Recette MVP : Windows 11 x64, sur des versions prises en charge par .NET 10 au moment de la livraison. Consigner les versions exactes testées.
- Windows 10 version 2004 / build 19041 est le seuil d'introduction de WDA_EXCLUDEFROMCAPTURE, pas une promesse de support du produit sous Windows 10.
- Aucun privilège administrateur nécessaire.
- Interface, messages utilisateur, commandes et commentaires de code en anglais. Cette documentation peut rester en français.
- Code lisible et maintenable ; commentaires de documentation au format XML.

```xml
<TargetFramework>net10.0-windows</TargetFramework>
<UseWPF>true</UseWPF>
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
```

Pour toute commande dotnet build ou dotnet test, ajouter explicitement -m:1, même si un autre réglage limite déjà le parallélisme.

# 4. Validation préalable obligatoire

Avant les imports et moteurs de lecture complets, réaliser un prototype minimal avec le rendu prévu pour le produit : quelques lignes fixes, MainWindow, opacités indépendantes, affichage/masquage et hotkeys.

Valider ensemble :

1. Exclusion de PrompterWindow et MainWindow.
2. Transparence effective du fond et des caractères.
3. TopMost et absence de prise de focus en présentation.
4. Passage des interactions souris à une application d'un autre processus.
5. Hotkeys fonctionnant alors que cette application conserve le focus.
6. Conservation de ces propriétés après affichage/masquage, changement d'opacité, déplacement, redimensionnement et passage configuration/présentation.

Enregistrer avec Camtasia et visionner le fichier produit. Reproduire avec OBS. Une prévisualisation ou un retour API positif ne suffisent pas.

Consigner version du prototype, version/build Windows, logiciel de capture et version, méthode sélectionnée, DPI, paramètres et résultats. Ne pas généraliser une méthode validée à toutes les méthodes du logiciel.

Critère de passage : prompteur visible pour le présentateur, absent de la vidéo sans rectangle noir ni artefact ; focus et interactions de l'application sous-jacente conservés.

En cas d'échec, résoudre le problème de fenêtre/rendu ou revoir explicitement la compatibilité visée avant de poursuivre. Un prototype opaque ne valide pas la transparence demandée. Réutiliser autant que possible le code validé.

# 5. Architecture et dépendances

Utiliser CommunityToolkit.Mvvm : ObservableObject, ObservableProperty, RelayCommand et AsyncRelayCommand selon les besoins. Ne pas réécrire l'infrastructure MVVM.

Modèles, ViewModels et services portent documents, navigation, états et persistance. Code-behind ou adaptateur de vue peut gérer HWND, hooks, styles natifs, cycle de vie, déplacement, redimensionnement, mesures WPF et application des offsets.

Calculs de progression et transitions sont testables sans fenêtre WPF. La pagination s'appuie sur les mesures du rendu réel via un adaptateur, sans second moteur typographique approximatif.

Dépendances de production autorisées : CommunityToolkit.Mvvm ; DocumentFormat.OpenXml obligatoire pour DOCX ; parseur Markdown éprouvé si nécessaire pour retirer correctement la syntaxe, sans rendu Markdown dans le prompteur. Les dépendances de tests sont autorisées.

Ne pas ajouter Serilog, MediatR, Prism, ReactiveUI, Entity Framework, base de données, conteneur DI complexe ou architecture multi-projets inutile.

Structure indicative, adaptable légèrement :

```text
GhostPrompter.sln
src/GhostPrompter/
    App.xaml
    Views/ (MainWindow, PrompterWindow)
    ViewModels/ (MainViewModel, PrompterViewModel)
    Models/ (PrompterDocument, PrompterBlock, PrompterElement, PrompterPage,
             PrompterMode, AppSettings, HotkeySettings)
    Services/ (DocumentImportService, TextImportService, MarkdownImportService,
               DocxImportService, ScriptParserService, PaginationService,
               ScrollController, CaptureExclusionService, GlobalHotkeyService,
               WindowStyleService, SettingsService, LoggingService)
    Native/ (NativeMethods)
tests/GhostPrompter.Tests/
Docs/
README.md
```

# 6. Pipeline et syntaxe commune

```text
TXT / MD / DOCX -> extraction en texte brut -> parser GhostPrompter unique
               -> document logique -> pagination Blocks ou affichage Scroll
```

Aucun style Word ou Markdown n'est conservé. Un style Word ou un titre Markdown ne crée pas de titre GhostPrompter. Le changement de mode utilise le document parsé sans relire le fichier. Aucune édition, sauvegarde, Undo/Redo ou gestion de document modifié.

Après normalisation des fins de ligne, reconnaître seulement :

- Ligne --- : séparation de blocs.
- Ligne [Titre] : titre.
- Ligne // texte : commentaire
- Toute autre ligne : texte ordinaire.

Ignorer les espaces extérieurs pour reconnaître les marqueurs. Le titre doit être non vide ; ses crochets extérieurs ne sont pas affichés. [] et les crochets au milieu d'une phrase restent du texte.
Le double-slash doit apparaitre en début de ligne (une fois espaces de tête supprimés), ce symbole n'est pas affiché, seule le commentaire l'est.

Le titre est affiché sans crochet en gras. Les commentaires sont affichés sans le double-slash en italique.

Ces lignes sont réservées. Pas de syntaxe d'échappement supplémentaire au MVP : une ligne à afficher littéralement doit être reformulée si elle correspond exactement à un marqueur.

```text
[Introduction]
// ouvrir program.cs et montrer la méthode DoSomething()

Nous allons observer le résultat.

---

[Premier test]

Je lance maintenant l'application.
```

- Sans séparateur, le document forme un seul bloc logique.
- Ne jamais afficher littéralement les séparateurs.
- Ignorer les blocs vides issus de séparateurs initiaux, finaux ou successifs.
- Conserver les lignes vides internes, retirer celles aux bordures des blocs.
- Un bloc ne contenant qu'un titre est valide.
- Document vide ou uniquement composé de séparateurs : zéro bloc/page, état vide et lecture désactivée.
- Conserver Unicode, accents et retours à la ligne.

Toute ligne [Titre] est reconnue de la même façon dans les deux modes. En Blocks, le titre en tête est le titre de bloc ; les suivants sont des intertitres. En Scroll, chacun apparaît à sa position.

Les titres sont des repères pour le présentateur, pas du texte à prononcer. Aucune synthèse vocale prévue.

# 7. Imports

## Point d'entrée

```csharp
public interface IDocumentImportService
{
    Task<PrompterDocument> LoadAsync(
        string path,
        CancellationToken cancellationToken = default);
}
```

Choisir l'importeur par extension sans distinction de casse. MainViewModel ignore les détails TXT/Markdown/DOCX.

## TXT

Accepter UTF-8 avec/sans BOM, CRLF et LF. Normaliser les fins de ligne. Un UTF-8 invalide produit une erreur compréhensible, sans remplacement silencieux. Ne pas modifier le texte au-delà de la normalisation et du parsing commun.

## Markdown

Facilité d'import de scripts préparés pour GhostPrompter. La sortie est du texte brut sans symboles de mise en forme Markdown.

- # Introduction devient Introduction, sans style et sans conversion en [Introduction].
- Gras, italique et autres styles pris en charge : conserver le texte sans délimiteurs.
- Listes/citations : retirer les marqueurs, conserver les éléments sur des lignes distinctes dans leur ordre.
- Liens : garder le libellé sans syntaxe ni ajout de cible ; garder une adresse qui est elle-même le texte affiché.
- Images : garder le texte alternatif éventuel, sans syntaxe ni chargement de ressource.
- Code inline/blocs de code : retirer les délimiteurs, conserver contenu et sauts de ligne.
- Ne pas supprimer aveuglément #, *, crochets ou tirets qui appartiennent au texte/code.
- Tableaux Markdown : aplatir les cellules en texte dans l'ordre, sans lignes de syntaxe d'alignement.
- Aucun HTML exécuté, diagramme ou extension active. Retirer les balises reconnues en conservant le texte lisible ; ignorer le contenu script/style. Aucun chargement distant.

Préserver les lignes autonomes [Titre] et --- pour le parser commun. Elles priment sur l'interprétation Markdown, notamment --- comme soulignement d'un titre Setext. La même règle s'applique si l'extraction d'un bloc de code produit ces lignes : préparer la source en conséquence.

Les autres séparations décoratives Markdown reconnues sont retirées ou deviennent un espace de paragraphe ; elles ne créent pas de bloc GhostPrompter.

Ne pas retirer la syntaxe par remplacement global de caractères. Documenter le sous-ensemble d'import avec des exemples ; aucun moteur de rendu Markdown complet n'est demandé.

## DOCX

Utiliser DocumentFormat.OpenXml et WordprocessingDocument en lecture seule. Extraire simplement le texte principal préparé pour GhostPrompter, dans l'ordre des paragraphes.

- Oublier styles, gras, italique, polices, couleurs, tailles, alignements, retraits et mise en page.
- Conserver frontières de paragraphes et sauts de ligne explicites.
- Conserver les caractères ; normaliser les tabulations en espaces.
- Ne pas recréer indentations ou numéros/puces provenant de la mise en forme Word.
- Heading 1, Heading 2 et Title ne créent pas de titre GhostPrompter.
- Seuls [Titre] et --- // texte dans le texte extrait sont interprétés par le parser commun.
- Ignorer tableaux et contenu, images, formes, zones de texte, SmartArt, notes, commentaires, en-têtes et pieds de page.
- Ne pas récupérer par inadvertance une zone de texte imbriquée dans un paragraphe.
- Révisions éventuelles : texte final, insertions incluses, suppressions exclues ; aucun rendu du suivi des modifications.
- Champs/hyperliens : texte affiché enregistré, sans recalcul ni suivi de lien.

Parcourir les paragraphes directs du corps est acceptable. Un parcours non filtré des paragraphes descendants importerait notamment les tableaux, ce qui est interdit.

Signaler les tableaux ignorés par un avertissement non bloquant. Le format attendu est un document composé de paragraphes préparés pour GhostPrompter.

# 8. Limites et robustesse

Définir 10 Mo comme 10 000 000 octets. Appliquer deux limites indépendantes :

1. Source sur disque : au maximum 10 Mo, vérifiés avant chargement.
2. Texte brut extrait : au maximum 10 Mo de contenu UTF-16, soit 5 000 000 unités de code de 16 bits, hors surcoût des objets et structures.

Ce choix ne dépend pas du nombre de mots ou de la langue. Il ne limite pas la mémoire totale du processus à 10 Mo. Un DOCX est compressé : sa taille sur disque ne suffit pas à borner le texte extrait.

Vérifier la limite pendant la construction du texte autant que le permet l'importeur, sans créer d'abord une sortie illimitée. Éviter les copies intégrales répétées. Compter le texte extrait avant retrait des blocs vides.

Garder l'interface réactive : traitement lourd hors du thread UI, état Loading et commande Cancel. Sérialiser les imports ; un résultat annulé ou obsolète ne doit pas remplacer le document courant.

Ne pas créer un contrôle WPF par ligne d'un document de 10 Mo. Utiliser un rendu limité au contenu utile, une pagination progressive et/ou une virtualisation adaptée. La taille maximale n'est pas une garantie de temps de chargement ou de faible consommation mémoire. Vérifier de gros fichiers représentatifs lors de la recette.

En cas de fichier inexistant, inaccessible, verrouillé, corrompu, mal encodé, trop volumineux ou non pris en charge :

- Ne pas planter ; journaliser et afficher une erreur compréhensible en anglais.
- Conserver le document précédent, sa position et son chemin courant.
- Mettre la lecture en pause au début de l'import et rester en pause en cas d'échec/annulation.
- Remplacer document et chemin seulement après réussite complète.

Un import réussi mais vide remplace le document par l'état vide. Aucun index invalide ni division par zéro.

# 9. Modèle de contenu

```csharp
public enum PrompterMode { Blocks, Scroll }
public enum PrompterElementKind { Text, Title, Comment, Separator }
```

Un élément contient au minimum type et texte. Le document conserve éléments ordonnés, blocs logiques et positions stables dans le texte importé. Un bloc peut exposer son titre de tête et son contenu.

Les pages sont une projection selon dimensions, police et préférences visuelles. Elles ne remplacent pas les blocs et ne doivent pas dupliquer tout le texte. Préférer des références/intervalles.

Une position textuelle stable permet de retrouver le passage courant après mise en page. Ne pas conserver uniquement un index de page ou un offset en pixels.

# 10. Mode Blocks : pagination automatique

Un bloc délimité par --- ou les limites du document produit une ou plusieurs pages.

- Afficher une seule page à la fois, sans défilement automatique.
- Scinder automatiquement tout bloc trop long en autant de pages successives que nécessaire.
- Next affiche la page suivante, puis la première page du bloc suivant.
- Previous effectue le parcours inverse.
- Ne pas boucler après la dernière page ni avant la première.
- Ne pas tronquer le texte et ne pas réduire automatiquement la police choisie.

Calculer les pages selon la zone réellement disponible : dimensions, padding, titres et indicateurs visibles compris.

Couper de préférence entre paragraphes, sinon entre lignes rendues. Un paragraphe peut continuer sur la page suivante. Ne pas couper un caractère Unicode au milieu de sa représentation. Les chaînes longues sans espace doivent pouvoir revenir à la ligne.

Éviter un intertitre seul en bas de page lorsqu'il peut être placé avec la première ligne du texte qui suit. Répéter le titre de tête sur les pages de continuation si cela laisse de la place au corps. Un titre exceptionnellement long doit être paginé lui-même, sans répétition intégrale qui rendrait la lecture impossible.

Imposer une taille minimale de fenêtre permettant au moins une ligne de contenu avec les polices autorisées. Les décorations ne doivent jamais empêcher le texte de s'afficher.

Recalculer après modification de police, dimensions, DPI, Show titles ou Show progress. Conserver la page contenant le premier caractère de contenu de l'ancienne page. Une répétition partielle est acceptable pour garder le repère ; sauter du texte ne l'est pas.

Progression :

```text
Block 4 / 17 · Page 2 / 3
```

Le total de pages concerne le bloc courant. Omettre Page pour un bloc d'une page si souhaité. Si le total est encore en calcul, l'indiquer explicitement sans présenter une estimation comme définitive ; garder la navigation réactive.

# 11. Mode Scroll

## 11.1 Contenu et hauteur de départ

Afficher le document dans l'ordre. Les séparateurs deviennent un espace vertical supplémentaire. Les titres sont en gras, de même famille et de même taille par défaut que le corps.

Show titles permet de masquer les titres dans les deux modes sans retirer le texte des blocs ni leurs séparations. Si tout le contenu est masqué, appliquer l'état sans contenu visible et désactiver la lecture.

Le texte ne démarre ni au bord supérieur ni au bord inférieur de la fenêtre. La première ligne visible commence à mi-hauteur par défaut, avec les lignes suivantes visibles dessous. Le présentateur peut ainsi anticiper la suite de la phrase, la ponctuation et le ton sans perdre immédiatement la première ligne.

Réglage dans MainWindow : Starting height.

```text
Unité : pourcentage de la hauteur utile, mesuré depuis son bord supérieur
Valeur initiale : 50 %
Plage : 10 à 80 %
Pas de l'interface : 5 points de pourcentage
```

La hauteur utile exclut padding et barre de progression. Ce pourcentage définit le haut de la première ligne rendue ; la ligne peut être un titre si Show titles est activé. Ne pas déplacer les titres indépendamment du corps.

À l'ouverture, au rechargement et après Restart, ajouter un espace initial pour placer cette ligne au point choisi. L'offset de départ reste zéro. Cette marge est visuelle : elle n'est ni du texte importé ni un séparateur logique.

Pendant la lecture, le texte monte normalement. Le point de départ sert aussi de repère de lecture pour définir la fin et conserver la position lors des changements de mise en page ; aucune ligne-guide visible n'est imposée au MVP.

Prévoir une marge finale suffisante pour que la dernière ligne visible atteigne ce même repère avant l'arrêt automatique. La fin ne doit pas survenir simplement parce que le bas du texte devient visible au bord inférieur.

Exemple conceptuel pour des hauteurs mesurées en DIP :

```text
H = hauteur utile
Y = position du repère dans cette hauteur
L = hauteur de la dernière ligne visible
Marge initiale = Y
Marge finale = max(0, H - Y - L)
```

Utiliser les dimensions réellement mesurées et neutraliser les marges de bordure parasites. Si le point choisi ne laisse pas tenir une ligne entière, borner sa position effective à la zone lisible. Préserver autant que possible de la place sous le repère pour anticiper les lignes suivantes.

Le réglage est persisté. S'il change pendant la lecture : mettre en pause, replacer la ligne de lecture courante au nouveau repère et rester en pause. Avant toute lecture, repositionner simplement la première ligne au nouveau point.

Un changement de police, dimensions, DPI ou Show titles/Show progress recalcule les marges et la mise en page : conserver autant que possible le caractère/élément au niveau du repère, puis rester en pause. Si un titre servant de repère disparaît, utiliser le contenu visible suivant, ou le précédent s'il n'y en a plus.

## 11.2 Mouvement et commandes

```text
Unité de vitesse : DIP/s, indépendante du DPI
Vitesse initiale : 50 DIP/s
Minimum : 10 DIP/s
Maximum : 300 DIP/s
Pas : 10 DIP/s
```

ScrollController maintient position, vitesse, IsRunning et progression. Calculer le mouvement indépendamment de la fréquence d'affichage : offset += speed * elapsedSeconds, avec une horloge monotone.

CompositionTarget.Rendering est recommandé, abonné seulement pendant la lecture. Un autre mécanisme est acceptable s'il est aussi fluide, sans boucle bloquante ni polling permanent. Les offsets représentent des DIP, pas des indices d'éléments.

- Play démarre depuis la position courante ; Pause la conserve.
- Modifier la vitesse en pause laisse la lecture en pause.
- Respecter les bornes sans inversion automatique du sens.
- À la fin définie par le repère : arrêter, conserver la position et indiquer 100 %.
- Play à la fin ne revient pas implicitement au début ; utiliser Restart.
- Restart revient à la première ligne au point de départ choisi et reste en pause.
- Après veille, verrouillage de session ou longue interruption du rendu : passer/rester en pause sans saut pour rattraper tout le temps écoulé.

## 11.3 Progression

La progression mesure le déplacement entre la position initiale et la position où la dernière ligne atteint le repère : offset / déplacement maximal, borné entre 0 et 1. Inclure les marges visuelles de départ/fin dans le calcul cohérent de l'étendue. Elle ne mesure pas le texte effectivement prononcé.

Un texte court tenant entièrement dans la fenêtre peut donc défiler : ses lignes suivantes doivent pouvoir atteindre le repère. Ne pas désactiver la lecture uniquement parce que le texte sans marges tient dans la fenêtre.

- Aucun contenu visible : 0 %, lecture désactivée.
- Contenu visible ne nécessitant aucun déplacement, par exemple une seule ligne déjà au repère : 100 %, lecture désactivée.
- Aucune division par zéro.

Afficher une barre fine en bas, désactivable. Ne pas afficher une progression en nombre de blocs.

# 12. Fenêtres et interface

## MainWindow

Panneau de chargement, configuration et état, sans éditeur :

- Source : fichier courant, Load, Reload, Loading/Cancel.
- Mode : Blocks / Scroll.
- Affichage : taille de police, Background opacity, Text opacity, Show titles, Show progress.
- Fenêtre : Configuration/Presentation, verrouillage, Click Through, Show/Hide, Reset window position.
- Lecture : commandes adaptées au mode ; vitesse, Starting height et Restart en Scroll.
- État : exclusion de chaque fenêtre, chaque hotkey, progression et fichier courant.

La préparation peut prendre le focus : choix du fichier/mode, réglages précis, déplacement/redimensionnement.

Fermer MainWindow quitte l'application, ferme le prompteur et libère les ressources. Minimiser MainWindow laisse le prompteur et les hotkeys actifs. Pas d'icône de zone de notification imposée.

## PrompterWindow

Fenêtre indépendante sans chrome classique :

```text
WindowStyle=None
Topmost=True
ShowInTaskbar=False
ShowActivated=False
```

En présentation, affichage, réaffichage, clics et raccourcis ne doivent pas l'activer.

# 13. Aspect, dimensions et opacités

```text
Police : Segoe UI
Taille : 30 DIP ; plage 16 à 72 DIP
Texte : blanc
Fond : noir
Opacité du fond : 75 % ; plage 0 à 100 %
Opacité du texte : 75 % ; plage 20 à 100 %
Largeur : 700 DIP
Hauteur : 250 DIP
Padding : 20 DIP
```

Fond et caractères peuvent être transparents séparément. Le présentateur doit voir les parties de l'application sous le texte ; le fond permet d'accentuer le contraste.

Text opacity s'applique au corps, titres et indicateurs du prompteur. Background opacity concerne seulement le fond. MainWindow reste normalement lisible.

Les valeurs sont indépendantes : fond 50 % et texte 50 % ne produisent pas un texte à 25 % par multiplication des opacités d'un parent commun.

Tester le fond à 0 % pour le hit-testing. Déplacement et récupération restent disponibles via MainWindow même si les zones complètement transparentes ne reçoivent pas les clics.

# 14. États et transitions

Distinguer Blocks/Scroll de Configuration/Presentation.

Configuration : déplacement/redimensionnement autorisés, activation possible, lecture en pause, Click Through effectivement désactivé, exclusion toujours demandée. Conserver séparément la préférence Click Through de présentation.

Presentation : TopMost, position verrouillée, aucune activation, commandes par hotkeys, Click Through selon préférence, exclusion toujours demandée.

Lock entre en Presentation ; Unlock revient en Configuration. Aucun état Presentation déverrouillé ambigu. Entrer en Presentation ne lance pas automatiquement la lecture. Play fonctionne en Presentation ; MainWindow peut proposer l'entrée en présentation avant lecture si la préparation n'est pas terminée.

- Masquer met en pause et conserve la position ; réafficher reste en pause à cette position.
- Changer de mode garde le document, revient au début du nouveau mode et reste en pause.
- Charger/recharger avec succès revient à la première page ou à la première ligne au point de départ choisi, en pause.
- Entrer en Configuration met en pause sans réinitialiser la position.
- Changer Click Through en présentation ne modifie ni focus ni position de lecture.

MainWindow reste accessible par la barre des tâches et permet de récupérer un prompteur verrouillé ou mal positionné sans dépendre d'un hotkey en échec.

# 15. Focus, styles natifs et Click Through

Utiliser les styles et appels natifs nécessaires, notamment WS_EX_NOACTIVATE, GetWindowLongPtr et SetWindowLongPtr. ShowActivated=false ne dispense pas de tester clics et réaffichages.

Pour Click Through, valider la combinaison de fenêtre layered et WS_EX_TRANSPARENT réellement utilisée par WPF. Un simple HTTRANSPARENT ne suffit pas à garantir le passage à un autre processus.

Modifier les bits nécessaires sans écraser les autres styles. Vérifier les erreurs natives. Préserver ou réappliquer l'exclusion si un HWND est recréé.

Test focus : saisir dans Visual Studio, utiliser navigation/vitesse puis continuer à saisir sans recliquer.

Test Click Through : clic, double-clic, clic droit, glisser et molette au-dessus du prompteur doivent se comporter comme en son absence, dans une application d'un autre processus. Tester aussi Click Through désactivé en présentation : le prompteur ne doit pas s'activer.

# 16. Exclusion de capture

Utiliser SetWindowDisplayAffinity avec WDA_EXCLUDEFROMCAPTURE = 0x00000011 après création effective du HWND, par exemple à SourceInitialized.

Pour PrompterWindow et MainWindow : obtenir le HWND, appliquer l'affinité, vérifier le retour, récupérer immédiatement l'erreur Win32 en cas d'échec, journaliser et transmettre l'état. Contrôler l'affinité avec GetWindowDisplayAffinity lorsque disponible.

CaptureExclusionService retourne un résultat structuré : état, affinité connue et erreur éventuelle, plutôt qu'un simple booléen. États : Not initialized, Active, Unavailable, Failed. Ne pas afficher un succès agrégé si une fenêtre nécessaire est en échec.

Formulation positive : Windows capture exclusion active. Ne pas afficher une promesse absolue telle que Capture protected. Distinguer l'affinité Windows de la compatibilité du logiciel/mode de capture, que l'application ne peut pas garantir automatiquement.

L'échec ne doit pas planter ni être silencieux : avertissement persistant dans MainWindow et état clairement visible. L'application peut rester utilisable sans prétendre être exclue de la capture.

## Fenêtres temporaires

Ne pas supposer que protéger MainWindow valide automatiquement dialogues, popups ou autres HWND.

- Préférer des messages intégrés à MainWindow aux MessageBox pendant la présentation.
- Réserver le dialogue système de sélection de fichiers à la préparation.
- Toute fenêtre temporaire requise pendant la présentation doit être protégée et testée, ou remplacée par un affichage dans une fenêtre déjà protégée.
- Documenter les dialogues système non validés comme hors garantie d'exclusion.

## Limites

L'exclusion dépend de la chaîne de capture. Elle ne constitue pas un DRM et ne masque pas le prompteur à une caméra physique ou à une carte d'acquisition HDMI enregistrant le signal envoyé à l'écran.

# 17. Raccourcis globaux

Utiliser RegisterHotKey et UnregisterHotKey ; les InputBindings WPF seuls sont insuffisants.

| Raccourci | Blocks | Scroll |
| --- | --- | --- |
| Ctrl+Alt+Up | Page suivante, puis bloc suivant | Accélérer |
| Ctrl+Alt+Down | Page précédente, puis bloc précédent | Ralentir |
| Ctrl+Alt+P | Sans action | Lecture/pause |
| Ctrl+Alt+Space | Afficher/masquer | Afficher/masquer |
| Ctrl+Alt+L | Configuration/Presentation | Configuration/Presentation |
| Ctrl+Alt+T | Préférence Click Through | Préférence Click Through |
| Ctrl+Alt+Add | Agrandir le texte | Agrandir le texte |
| Ctrl+Alt+Subtract | Réduire le texte | Réduire le texte |
| Ctrl+Alt+Home | Première page du document | Retour au début en pause |

Pas de taille de texte : 2 DIP, borné entre 16 et 72. En Scroll, le changement applique la règle de pause et de conservation du repère.

Prévoir des alternatives configurables sans pavé numérique pour Add/Subtract et les documenter selon le clavier testé. Éviter F5, F9, F10 et F11.

Utiliser MOD_NOREPEAT pour éviter les navigations/bascules multiples par appui maintenu. Pour le MVP, l'appliquer aussi aux réglages de vitesse et taille.

En Configuration, Toggle Click Through change la préférence mais pas l'état traversant effectif ; l'interface montre cette distinction. Toutes les commandes de tournage sont disponibles sans focus, y compris Restart. Starting height est un réglage de préparation : son ajustement précis ne nécessite pas de hotkey supplémentaire.

## Configuration et collisions

L'interface complète de capture de hotkeys n'est pas obligatoire. Le modèle et settings.json permettent leur redéfinition ; les changements manuels prennent effet au redémarrage.

- Afficher action, combinaison et résultat d'enregistrement pour chaque raccourci.
- Détecter les doublons internes avant RegisterHotKey.
- Journaliser/signaler les collisions externes et conserver les raccourcis valides.
- Fournir Restore default shortcuts, sans prétendre que les valeurs par défaut sont exemptes de conflits externes.
- Libérer les anciens raccourcis avant remplacement et tous les raccourcis à la fermeture.
- Ne pas contourner les collisions par des hooks clavier intrusifs.

Une seule instance est autorisée, pour éviter les conflits de hotkeys, settings et journal. Un second lancement indique que l'application est déjà ouverte puis quitte, ou réactive MainWindow. Il ne doit pas écraser le log actif.

# 18. Démarrage et persistance

Chemin : %LOCALAPPDATA%\GhostPrompter\settings.json. Créer le répertoire si nécessaire.

Persister : position, dimensions, opacités fond/texte, taille de police, mode Blocks/Scroll, Show titles, Show progress, préférence Click Through, vitesse, Starting height, dernier chemin de fichier et hotkeys.

Ne pas restaurer automatiquement Presentation, lecture en cours ou visibilité masquée. Démarrer en Configuration et en pause pour garder une récupération simple.

Premier lancement : deux fenêtres visibles, aucun document, texte d'accueil :

```text
GhostPrompter is ready.
Load a TXT, Markdown or DOCX script.
```

Lancements suivants : restaurer les préférences valides. Ne pas charger automatiquement le dernier fichier ; garder son chemin pour Reload et le dialogue Load. Reload sans document en mémoire charge ce dernier chemin selon les règles normales.

Prévoir une version du format, des valeurs par défaut et la validation des limites. Un JSON absent, invalide ou inexploitable ne bloque jamais le démarrage : journaliser et utiliser les valeurs par défaut nécessaires.

Écriture atomique par fichier temporaire dans le même répertoire puis remplacement. Sauvegarder en fin d'interaction, après temporisation courte des réglages et à la fermeture ; jamais à chaque pixel.

Si l'écriture échoue, continuer avec les valeurs en mémoire et informer sans multiplier les messages.

# 19. Écrans et DPI

Le scénario principal reste mono-écran. Fonctionner sans plantage en multi-écrans et lors du retrait d'un écran.

- Dimensions en DIP ; conversions explicites avec les coordonnées natives en pixels physiques.
- Privilégier PerMonitorV2.
- Tester 100 %, 125 %, 150 % et déplacement entre écrans de DPI différents.
- Si la position est hors des écrans, recentrer sur l'écran principal.
- Ajuster une fenêtre trop grande ou pratiquement inaccessible à la zone de travail.
- Reset window position reste disponible dans MainWindow.
- Tout changement de DPI recalcule la mise en page en conservant le repère textuel selon le mode.

# 20. Journalisation et exceptions

Mécanisme local minimal, sans bibliothèque supplémentaire. Fichier unique :

```text
%LOCALAPPDATA%\GhostPrompter\logs\GhostPrompter.log
```

Après vérification de l'instance unique, ouvrir ce fichier en remplacement. Chaque lancement efface le journal précédent. Aucune rotation, archive, rétention ou sauvegarde automatique.

L'utilisateur qui souhaite conserver un journal le copie avant de relancer. Documenter clairement ce fonctionnement.

Journaliser démarrage, versions application/Windows, fichier chargé, résultats d'exclusion, erreurs de hotkeys, import, settings et exceptions non gérées. Ajouter horodatage et niveau simple. Ne pas journaliser le script, chaque frame, déplacement ou étape de pagination.

Synchroniser les écritures ; vider les tampons sur les erreurs importantes et à la fermeture. Une impossibilité d'écrire le log ne doit pas empêcher le démarrage ni créer une boucle d'erreurs ; signaler simplement son indisponibilité.

Brancher Application.DispatcherUnhandledException et AppDomain.CurrentDomain.UnhandledException pour journaliser autant que possible. Traiter les erreurs récupérables au niveau de l'opération. Ne pas masquer toutes les exceptions pour continuer dans un état corrompu. Une erreur fatale termine proprement si possible, sans redémarrage automatique qui écraserait le journal.

# 21. Réseau et performance

Aucun appel réseau applicatif, télémétrie, collecte ou reporting externe. Ne pas charger de ressource distante à partir d'un document. Les téléchargements de dépendances pendant le développement ne font pas partie de l'exécution du produit.

Au repos : CPU négligeable, aucun polling permanent. Blocks fonctionne de manière événementielle ; la pagination est déclenchée par chargement ou changement de mise en page. Scroll n'anime que pendant IsRunning=true.

Annuler les calculs obsolètes après changement de document ou de mise en page. Ne pas appliquer une pagination calculée pour une ancienne taille. Aucun abonnement de rendu actif après pause, masquage ou fermeture.

# 22. Tests automatisés

## Parser et imports

Tester document vide, un/plusieurs blocs, séparateurs initiaux/finals/successifs, espaces/lignes vides, titre en tête/intertitre/titre seul, [], crochets dans une phrase, absence de séparateur, Unicode, accents, CRLF et LF.

Le même texte brut doit produire le même modèle, indépendamment du format source ou du mode.

Markdown : retrait des styles sans perte du texte, # non converti en titre GhostPrompter, préservation de [Titre] et ---, conflit Setext, liens, listes, code, caractères littéraux et absence de réseau.

DOCX : créer programmatiquement un document avec paragraphes, styles, gras, accents, [Titre], ---, sauts de ligne, tabulation et tableau. Vérifier texte brut et exclusion du tableau. Couvrir les contenus exclus et les règles de révisions/champs.

Erreurs : extension inconnue, UTF-8 invalide, fichier absent, DOCX corrompu, annulation, dépassement des deux limites et maintien de l'ancien document. Tester les bornes sans imposer à tous les tests unitaires d'allouer 100 Mo.

## Pagination

Tester bloc court, bloc sur plusieurs pages, navigation pages/blocs dans les deux sens et bornes, conservation complète et ordonnée du texte, paragraphe long, chaîne sans espace, Unicode, titre long, taille minimale et conservation du repère après recalcul.

Vérifier l'annulation des calculs obsolètes. Tester la logique avec des mesures déterministes, puis compléter par intégration/recette du rendu WPF réel : aucune ligne coupée ou omise, hors répétition volontaire du titre.

## ScrollController et états

Tester vitesse/clamp, pause/reprise, début/fin, progression, contenu vide/court, division par zéro, temps écoulé, longue interruption, masquage, changement de mode et mise en page.

Tester spécifiquement :

- Première ligne à 50 % par défaut et au pourcentage demandé.
- Lignes suivantes visibles sous le point de départ.
- Restart/rechargement restaurant ce point.
- Défilement des scripts courts comportant plusieurs lignes.
- Dernière ligne atteignant le repère avant arrêt/100 %.
- Réglage de hauteur et redimensionnement conservant le repère textuel en pause.
- Titre initial visible/masqué, contenu entièrement masqué et ligne exceptionnellement haute.
- Persistance et bornes de Starting height.

## Settings et hotkeys

Tester sérialisation, désérialisation, version, JSON absent/invalide, valeurs par défaut, opacités indépendantes, valeurs hors limites et position invalide.

Tester doublons, états d'enregistrement, restauration des valeurs par défaut et libération via une abstraction native si pertinent. La recette réelle reste nécessaire pour focus et collisions externes.

# 23. Recette manuelle finale

Répéter les validations du prototype sur l'EXE publié final.

- Camtasia en priorité puis OBS, configurations exactes consignées.
- Blocks : plusieurs pages du même bloc, puis bloc suivant et navigation inverse.
- Scroll : lecture, pause, vitesse, fin et Restart.
- Première ligne au milieu par défaut, départ réglable, anticipation des lignes suivantes et dernière ligne au repère avant arrêt.
- Opacités indépendantes, y compris fond à 0 %.
- Absence du prompteur, rectangle noir et flash dans la vidéo.
- Affichage/masquage des deux fenêtres et transitions Configuration/Presentation.
- Focus et interactions traversantes dans une autre application.
- Collisions de raccourcis et clavier sans pavé numérique.
- DPI, écrans, récupération de position et redimensionnement.
- Gros documents : réactivité, annulation et absence de création massive de contrôles.
- Second lancement sans écrasement du journal actif ; relancement après fermeture avec remplacement du journal.
- Exécution sans .NET préinstallé, sous compte standard Windows 11 x64.

La recette capture exige un enregistrement réel visionné. Si l'environnement ne permet pas un test, identifier ce qui reste non exécuté et ne pas déclarer le MVP validé pour la production.

# 24. Compilation et publication

Publier un EXE self-contained single-file win-x64 sans runtime .NET préinstallé ni installation administrateur.

Configuration Release indicative :

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>true</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
    <PublishTrimmed>false</PublishTrimmed>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
    <DebugType>None</DebugType>
    <DebugSymbols>false</DebugSymbols>
</PropertyGroup>
```

Ne pas activer le trimming WPF. Conserver les symboles dans les configurations normales de développement.

Exécuter avant livraison, avec chemins adaptés aux noms réels :

```text
dotnet build -m:1
dotnet test -m:1
dotnet publish src/GhostPrompter/GhostPrompter.csproj -c Release -r win-x64 --self-contained true -m:1
```

Publier explicitement le projet applicatif, pas les projets de tests. Livrable : GhostPrompter.exe. Settings, logs et scripts sont stockés séparément.

L'extraction éventuelle de bibliothèques natives au lancement ne constitue pas une installation préalable du runtime ; vérifier le lancement sous compte standard.

# 25. Documentation à livrer

README : objectif, lancement de l'EXE, Windows validés, imports en texte brut, syntaxe réservée, pagination, Scroll et hauteur de départ, progression visuelle, raccourcis et alternatives sans pavé numérique, collisions/configuration JSON, opacités indépendantes, Click Through, états de fenêtre, récupération, fermeture, settings, limites de 100 Mo, journal écrasé, compilation/publication.

Inclure exemples TXT, Markdown et DOCX cohérents, procédures Camtasia/OBS et limites de capture. Ne pas promettre une compatibilité non testée.

# 26. Ordre de réalisation

1. Solution minimale et prototype : capture, transparence, focus, Click Through et hotkeys ensemble.
2. Recette préliminaire Camtasia puis OBS, consignée avant les fonctions complètes.
3. Modèle, parser commun, TXT et tests.
4. Blocks, pagination automatique, navigation et conservation du repère.
5. Scroll, hauteur de départ, progression, états et tests.
6. Imports Markdown/DOCX en texte brut et tests.
7. MainWindow, hotkeys configurables et transitions finales.
8. Settings/journal minimal ; vérification du démarrage et de la récupération.
9. Gros documents, annulation, écrans et DPI.
10. Publication, recette réelle finale et README.

Introduire plus tôt les préoccupations transverses nécessaires au prototype. Ne pas repousser la validation des risques critiques.

# 27. Évolutions et définition finale

Exposer les actions indépendamment du clavier : NextPage, PreviousPage, IncreaseSpeed, DecreaseSpeed, TogglePlayPause, Restart, ToggleVisibility, TogglePresentation et ToggleClickThrough.

Une future version pourra utiliser PowerPoint, pédale, Stream Deck ou MIDI sans dupliquer ces actions. Ne pas implémenter maintenant SlideNumber, @slide ou protocole externe inutilisé.

Ne pas surarchitecturer ni ajouter de fonctionnalité hors périmètre. Ne pas livrer de fonction requise laissée en TODO, stub, fake ou NotImplementedException. Compiler, tester et publier réellement avant livraison de l'application.

Priorités : exclusion validée, focus conservé, hotkeys fiables, Click Through, lecture complète et lisible des blocs, fluidité, fidélité du texte importé, persistance simple et esthétique.

Le MVP est accepté lorsque le présentateur peut manipuler son application, lire l'intégralité du script — blocs longs compris — et piloter GhostPrompter sans interrompre sa démonstration, tandis que le prompteur est absent de la chaîne de capture effectivement validée.
