# BioEden – Visual Accessibility 1.3.0 beta 1

Accessibility mod pro BioEden 1.2.0.0 (Unity 6000.0.56f2).

Veřejný repozitář: <https://github.com/h0n24/bioeden-visual-accessibility> · [Releases](https://github.com/h0n24/bioeden-visual-accessibility/releases)

## Nastavení

Všechny volby používají původní menu hry, šipky On / Off, tlačítko **Confirm**, ukládání nastavení a upozornění při odchodu bez uložení.

| Sekce | Položka | Off | On | Výchozí stav modu |
|---|---|---|---|---|
| Video | Depth of Field | Vypnuté rozmazání vzdálené krajiny | Původní efekt hry | Off |
| Accessibility | Antenna Range Outline | Původní hranice | Bílý lem s modrým středem na náhledu dosahu antény a hranici pokryté oblasti | On |
| Accessibility | Extended Zoom | Původní limit a náklon kamery | O 35 % větší maximální vzdálenost, při oddálení plynulý přechod k pohledu více shora | On |
| Accessibility | Map Desaturation Filter | Barevný svět | Odbarvený svět při zachování běžného stavění a herního ovládání | Off |
| Accessibility | Map Filter Hotkey | F1 | F1, F2 nebo F3 | F1 |

Po změně stiskni **Confirm**. Restart hry kvůli přepnutí není potřeba. Dříve uložené volby zůstávají zachované. Tlačítko **Default** obnovuje výchozí hodnoty všech nastavení hry, včetně těchto položek. Filtr lze kdykoli přepnout nastavenou klávesou nebo ikonou planety vlevo od nápovědy vpravo nahoře.

Obrys nemění dosah ani pravidla stavby. Vychází ze stejné geometrie jako původní hranice a sleduje její přesun a skrytí. Verze 1.2.1 promítá tuto hranici do obrazové vrstvy nad efektem mlhy: bílá linka má šířku 5,5 pixelu, modrý střed 2 pixely. Díky tomu ji nemá překrýt neprobádaná oblast ani terén. Obrys se tedy záměrně ukazuje také přes překážky. Vrstva nezachytává vstup a řadí se pod běžné překryvné menu. Vypnutí skryje přidaný obrys. Svět se nepřevádí do odstínů šedi.

Filtr používá obrazovou úpravu hry s parametrem saturation 0. Neotevírá planetární režim, proto zůstává možné stavět, vybírat objekty a sledovat běžné herní značky. UI zůstává barevné. Ikona vzniká až po vstupu do herního světa, je umístěná před nápovědou a zobrazuje aktuální klávesu. Filtr je po instalaci vypnutý. Po zapnutí zůstávají barevné pouze hráčské budovy, surovinové objekty a znečištěná voda; tráva, stromy, kameny a čistá voda zůstávají odbarvené. Překresluje se jen tato vybraná skupina, aby se nezvyšovalo zatížení celé scény. Při zapnutém Extended Zoom je Depth of Field automaticky potlačen, aby se vzdálený obraz nerozmazal; po vypnutí Extended Zoom se vrátí volba Depth of Field z menu.

Větší zoom platí pro běžný pohled na svět; nezvětšuje rozsah uvnitř kopule, topografickou mapu ani filmové kamery. V první polovině rozsahu zůstává původní náklon, ve druhé se plynule zvyšuje. Při maximálním oddálení je rozsah náklonu 70–82° směrem dolů (pokud původní hodnota není ještě vyšší). Tím se omezuje pohled na vzdálený horizont, kde předchozí verze ukazovala nevykreslený pás. Vzdálenost vykreslování se nemění. Automatický přechod na mapu zůstává na konci nového rozsahu. Vypnutí obnoví původní náklon a omezí cílovou vzdálenost na původní rozsah.

## Instalace a odinstalace

1. Ukonči BioEden.
2. Stáhni ZIP z **Releases**, rozbal ho a spusť `install.ps1` nebo `Nainstalovat.cmd`.
3. Pokud instalátor hru nenajde, předej cestu pomocí `-GamePath`.

Pro kontrolu použij `Stav.cmd`. `Odinstalovat.cmd` obnoví původní herní knihovny a odstraní runtime modu. Uložené preference modu zůstanou v nastavení hry jako neaktivní položky pro případ opětovné instalace. Mod nevyžaduje mod loader ani instalaci .NET SDK.

Jinou cestu lze zadat pomocí `install.ps1 -GamePath "cesta ke hře"` nebo přímo `NoDOF.ps1 -Action Install -GamePath "cesta ke hře"`.

Instalátor rozpoznává předchozí verze 1.0, 1.1, 1.2 beta 1 a 1.2.1 beta 2. Při přechodu použij tento nový instalátor. Starší balíčky jsou pouze záloha.

## Zálohy a kompatibilita

Originály jsou vedle příslušných knihoven v `BioEden_Data\Managed` s příponou `.NoDOF.original`. Instalátor kontroluje SHA-256 originálů, balíčku i vytvořených souborů a odmítne neznámou verzi. Nejprve připraví všechny změny, poté je nahradí; při selhání se vrátí k předchozím souborům.

Po aktualizaci nebo ověření souborů hry může být nutná nová verze patche. Nekopíruj staré zálohy přes novější verzi hry.

## Stav ověření

- DoF a jeho položku v menu potvrdil uživatel jako funkční.
- Verze 1.3.0 se sestavila bez chyb a varování.
- Ověřeny instalace, přechody z 1.0, nainstalované 1.2 beta 1 a 1.2.1 beta 2, opakované spuštění, odinstalace a odmítnutí neznámých souborů. Přechod z 1.1 byl ověřen v předchozích vydáních.
- Vynucené selhání při druhé knihovně úspěšně vrátilo již provedené změny včetně runtime.
- Kontrola kódu potvrzuje jen 6 zamýšlených zásahů do metod; ostatních 19 572 těl metod zůstává významově identických.
- Prošly kontroly návratu původního zoomu a náklonu, plynulosti náklonu a ořezu čar na obrazovku včetně 10 000 segmentů v obou směrech.
- **Selektivní barvy budov a materiálů, rozlišení čisté/znečištěné vody, výkon filtru, automatické potlačení DoF při Extended Zoom a vzhled nové ikony čekají na ruční ověření ve hře.** Viz `RUCNI-TEST.md`.

Pokud selže přidaná vrstva nebo se změní očekávaná geometrie, mod ponechá původní hranici a zapíše chybu s označením `[BioEden.NoDOF]` do `Player.log`. Na jednu hranici se vykreslí nejvýše 8 000 viditelných úseků kvůli limitu geometrie Unity UI.

## Zdrojový kód

`src/Runtime` obsahuje menu, řízení efektů a geometrii obrysu. `src/Patcher` obsahuje generátor úprav knihoven. `tests` obsahuje kontroly instalace a rozsahu změn. Sestavení vyžaduje .NET SDK 10 a lokální původní knihovny hry; viz `Build.ps1`. Herní knihovny nejsou součástí distribučního ZIP.

Instalátor používá Mono.Cecil 0.11.6 (MIT); jeho licenci obsahuje `THIRD-PARTY-LICENSES.txt`.
