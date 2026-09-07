# Gameplay bewaken

De tests leggen de huidige spelregels vast. Een bewuste gameplaywijziging vereist een review van de bijbehorende verwachtingen. Pas falende tests of balansbestanden niet automatisch aan om ze groen te krijgen.

## Automatisch in Unity

Dit project gebruikt **Unity 2018.4.36f1**, zoals vastgelegd in `ProjectSettings/ProjectVersion.txt`, met de ingebouwde NUnit/Test Runner. Er is geen extra package of wijziging aan de runtime assemblies nodig. Alle testcode staat in een `Editor`-map en komt niet in de gamebuild.

- Na een succesvolle scriptcompilatie of assetimport draait de categorie `GameplayRegression` automatisch wanneer de editor klaar is en buiten Play Mode staat.
- `Tools > Card Wars > Run Gameplay Regression Tests` start dezelfde suite handmatig.
- De Unity Test Runner onder `Window > General > Test Runner` kan individuele Edit Mode-tests uitvoeren. Filter op `GameplayRegression`.
- Een spelerbuild wordt afgebroken als een test faalt, wordt overgeslagen, geen resultaat oplevert of geen tests vindt.
- De Console toont het aantal geslaagde tests of een fout. `Logs/GameplayRegression.xml` bevat NUnit XML met testnamen, verwachte en werkelijke waarden en foutlocaties. Compilatiefouten moeten eerst worden opgelost; die blokkeren ook de tests.

De tests herstellen de bestaande singletonreferenties en verwijderen hun tijdelijke verborgen objecten. Ze laden geen savegame en starten geen netwerkverkeer. Controles die tijdens Play Mode worden aangevraagd wachten tot Play Mode voorbij is. De automatische suite gebruikt NUnit op de hoofdthread; voeg asynchrone `[UnityTest]`-scenario's toe aan een aparte Play Mode-suite.

## Commandoregel / CI

Sluit deze projectkopie in Unity en voer vanuit PowerShell uit:

```powershell
./scripts/Test-Gameplay.ps1
# Of een expliciet editorpad:
./scripts/Test-Gameplay.ps1 -UnityPath 'C:/Program Files/Unity/Hub/Editor/2018.4.36f1/Editor/Unity.exe'
```

Het script gebruikt de versie uit `ProjectVersion.txt`, of `UNITY_EDITOR_PATH`, wacht op Unity en controleert zowel de exitcode als een nieuw XML-resultaat. Fouten, ontbrekende resultaten en overgeslagen tests geven een foutstatus. Het Unity-log staat in `Logs/GameplayRegression-unity.log`. Een CI-agent heeft dezelfde Unity-versie en een werkende Unity-licentie nodig; gebruik dit script als verplichte stap voor een build of merge. Er is geen externe CI-provider of branch protection ingesteld door deze wijziging.

## Geautomatiseerde dekking

| Gebied | Vastgelegd gedrag |
| --- | --- |
| Bord | Twee spelers, vier gespiegeld gekoppelde lanes, aangrenzende lanes zonder wraparound, gebouw en creature hebben aparte plaatsen. |
| Kaartwaarden | ATK, DEF en abilitywaarden schalen met level. |
| Kaart spelen | Genoeg magie, faction/landscape, universele kaarten, rarity-grens, disabled lanes en castingblokkades per speler en type. |
| Kosten | Passende kortingen stapelen, minimum nul, floopkosten inclusief lane- en spelermodifiers en vaste kosten. |
| Floop | Ability vereist een geldig doel waar van toepassing; beschikbaarheid hangt af van kosten, gebruikt-status en blokkades. |
| Gevecht | Schadereductie vóór vermenigvuldiging, afkappen van fracties, lethale schade en gebouwcallback, gevaar- en winstbeoordeling. |
| Genezing | Afronding van halve waarden naar even, genezingsfactor en geen overheal. |
| Effecten | ATK-buffs, herhaalde halvering, genezing en reset in samengestelde scenario's; permanente en tijdelijke waarden blijven onderscheiden. |
| Landscapes | Omgedraaide landschappen, extra getelde landschappen en landscape-overrides. |
| Deck/hand | Volgorde van trekken en terugleggen, vijf kaarten trekken, maximum zeven bij trekken, discard en opnieuw trekken, leeg deck, clone en duplicaatgrens. |
| Beurtwissel | Refresh van actieve kaarten, aanhoudende schade, verlopen spells, OutOfCards voor de speler, magie tot negen, eenmalige bonuspunten en opruimen van tijdelijke kosten/blokkades. |
| Balansdata | 341 creatures, 27 buildings, 48 spells, 72 leaders en 25 parameters uit de meegeleverde Blueprints. |

De leesbare snapshots in `Assets/Tests/Editor/Baselines` vergelijken gameplayvelden per ID. Opmaak en volgorde van JSON-objectvelden zijn vrij; artwork, geluidsnamen en vertaalteksten worden niet vastgezet. Kaartkosten, stats, scripts, abilitywaarden, factions, rarity, leader-cooldowns en timing van de battle ring worden wel bewaakt. Een fout benoemt bestand, ID en veld. Toegevoegde of verwijderde records worden ook gemeld. Nieuwe gameplayvelden moeten expliciet aan de snapshots en tests worden toegevoegd. Gedownloade live data vallen buiten deze lokale baseline.

Enkele bewuste karakteriseringen: `DamageLastTurn` bevat de laatste hit, niet de som; gevaar gebruikt een strikt grotere vijandelijke ATK; `ResetSelf` verwijdert additive bonussen maar behoudt factoren; een gebouw alleen maakt een lane niet bezet voor creatures. Verander deze verwachtingen alleen samen met een bewuste spelregelwijziging.

## Aanvullende spelscenario's voor een release

Deze suite dekt niet automatisch ieder kaartscript of de volledige geanimeerde match. Gebruik voor onderstaande checks een apart testprofiel en dezelfde quest, decks, kaartlevels en startspeler op de vorige goedgekeurde build en de nieuwe build. Noteer per stap hand, magie, HP, lanes en fase; vergelijk de uitkomsten. Een volledige garantie dat alle gameplay gelijk blijft vraagt uitbreiding met nieuwe regressies zodra nieuwe functies of fouten worden gevonden.

| Scenario | Stappen en controle |
| --- | --- |
| Start / tutorial | Start dezelfde eerste quest. Controleer vijf handkaarten, vier landschappen per speler, leader-HP, startspeler, beginmagie en tutorialvolgorde. |
| Summon en vervangen | Speel creature en gebouw op dezelfde lane. Probeer een verkeerde faction, te dure kaart en vervanging. Controleer betaalde magie, discard, summontrigger en stats. |
| Floop / annuleren | Activeer een gerichte ability, annuleer en voer daarna uit. Controleer dat annuleren niets verbruikt, uitvoering eenmaal betaalt en opnieuw floopen volgens dezelfde regels wordt geblokkeerd. |
| Battle ring | Voer miss, normale hit, crit, block en tegenaanval uit met dezelfde stats. Vergelijk schade, critfactor, animatievolgorde en HP-labels. |
| Lege lane / dood | Val een lege lane en een bijna dood creature aan. Vergelijk hero-schade, deathtriggers, discard, gebouwtriggers en het vrijmaken van de lane. |
| Volledige beurt | Speel, floop, val aan en wissel twee keer van beurt. Vergelijk draws, AP, leader-cooldown, buffs, blokkades en opnieuw beschikbare kaarten. |
| Deck uitgeput | Trek het deck leeg. Controleer OutOfCards, bleed, reshuffle, nieuwe hand en behoud van kaarten; test ook het gedrag van de tegenstander. |
| Einde gevecht | Test overwinning, verlies en surrender. Vergelijk resultaatfase, loot, questprogressie en dat de beloning na heropenen niet dubbel wordt toegekend. |
| Opslaan / hervatten | Sluit op de ondersteunde momenten af en hervat. Controleer deck, collectie, profiel en questprogressie tegen de referentiebuild. |

## Nieuwe regressie toevoegen

Voeg een kleine test toe met een concrete beginsituatie, een aanroep naar echte productiecode en vaste verwachte uitkomsten. Gebruik `[Test]` of `[TestCase]` en categorie `GameplayRegression`. Test grenswaarden en beide spelers waar mogelijk. Vermijd verwachte waarden die met dezelfde formule als de productiecode worden berekend. Bij een gevonden bug: maak eerst een falende test en pas daarna het gedrag aan. Review bewuste veranderingen aan snapshots als gameplaywijzigingen.
