# Ruční test verze 1.3.0 beta 1

Nová verze už je nainstalovaná. Hru spusť běžně. Nové položky najdeš v **Settings → Accessibility**; původní přepínač DoF zůstává ve **Video**.

## 1. Obrys antény

- Zapni **Antenna Range Outline**, potvrď **Confirm** a začni pokládat anténu na modrém biomu z obrázku.
- Ověř bílý lem s modrým středem na hranici náhledu dosahu i na hranici už pokryté oblasti. Zkus pohybovat náhledem přes vodu, souš a vyvýšená místa.
- Nejdůležitější: přesuň náhled přes **modrou neprobádanou oblast** z posledního snímku. Lem má být stejně silný na modré ploše i nad odkrytým terénem; při oddálení se nemá ztenčovat.
- Během pokládání otevři menu. Linka nesmí překrývat text nastavení ani blokovat klikání; po návratu a pohybu kamery musí sledovat správnou hranici.
- Zkus povolené i zakázané umístění a různé vzdálenosti kamery. Bílý lem nesmí změnit možnost stavět ani skutečný dosah.
- Zruš pokládání. Náhledový obrys musí zmizet spolu s původním náhledem; hranice už pokryté oblasti může zůstat.
- Přepni volbu na **Off**, potvrď a ověř návrat k původnímu zobrazení. Potom ji můžeš vrátit na **On**.

## 2. Větší oddálení

- V pohledu na herní svět porovnej maximální oddálení s **Extended Zoom → Off** a **On** (pokaždé **Confirm**). On má povolit asi o třetinu vzdálenější kameru.
- Při oddalování se má kamera plynule naklánět více shora. Vrať se na místo z posledního snímku a ověř, zda zmizel **rovný modrý pás nahoře**. Zkus kameru otočit i posunout k okraji mapy.
- Pokud hra při dalším oddalování přechází na topografickou mapu, ověř, že přechod nastane až po dosažení nového limitu.
- Z největšího oddálení přepni na **Off**. Kamera se musí vrátit do původního rozsahu, bez zaseknutí kolečka.
- Zkontroluj, že vstup do kopule, návrat do světa, otáčení a posun kamery fungují normálně.

## 3. Ukládání a původní DoF

- Změň jednu z nových voleb a odejdi bez uložení. Hra má nabídnout zahození změn; po opětovném otevření má zůstat uložená hodnota.
- Ulož zvolené hodnoty přes **Confirm**, restartuj hru a ověř, že zůstaly.
- Ve **Video** vyzkoušej **Depth of Field → On / Off**. Obě nové úpravy musí fungovat nezávisle na něm.

## 4. Filtr mapy a výkon

- V **Accessibility** nastav **Map Desaturation Filter → On**, vyber **Map Filter Hotkey → F1** (případně F2/F3) a potvrď **Confirm**.
- Ověř, že po čisté instalaci je filtr **Off** a hra se spustí běžně. Během načítací obrazovky se ikona filtru nesmí objevit.
- Ve hře ověř šedý svět přes klávesu. Barevné mají zůstat hráčské budovy a surovinové objekty; tráva, stromy, kameny a čistá voda mají zůstat odbarvené. Stavění, výběr budov a běžné značky musí zůstat použitelné. UI může zůstat barevné.
- Porovnej plynulost kamery a posunu s filtrem vypnutým a zapnutým. Nesmí dojít k razantnímu poklesu FPS ani k trvalému vytížení při běžném pohybu.
- Přepni filtr stejnou klávesou i ikonou s textem **F1** vlevo od **?** vpravo nahoře. Při změně bindingu se text ikony musí změnit na F2/F3. Modrá ikona znamená zapnuto, šedá vypnuto. Ověř, že ikona není vidět v hlavním menu ani v Settings a nepřekáží tlačítku nápovědy.
- Najdi čisté jezero nebo řeku s **0 % pollution** a zkontroluj, že zůstane odbarvená. Potom najdi znečištěnou vodu a zkontroluj, že si ponechá barevné zvýraznění.
- Otevři planetární režim hry a potom zahaj stavění. Nový filtr musí zůstat nezávislý a hra nesmí přepnout do mapového režimu ani zabránit stavění.
- S aktivním **Extended Zoom** zkus **Depth of Field → On**. Vzdálený obraz má zůstat ostrý; po vypnutí **Extended Zoom** se má DoF vrátit podle volby ve Video.

Pokud něco nesedí, pošli obrázek a stav příslušných voleb. Nejdůležitější je nyní viditelnost obrysu nad modrou mlhou, horní okraj obrazu při maximálním oddálení a použitelnost stavění s filtrem.
