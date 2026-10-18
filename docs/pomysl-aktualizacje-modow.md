# Pomysł: śledzenie aktualizacji samych modów (kampanii)

Status: **pomysł, do przemyślenia.** Niezależny od [aktualizacji aplikacji](plan-dmg-i-aktualizacje.md).

## Problem

Autorzy kampanii wrzucają nowe wersje (linki do pobrania) na Discord CCM – serwer **GiantGrantGames Custom Campaign Manager**
(ok. 103 tys. członków): [discord.gg/5EDwDpcu](https://discord.gg/5EDwDpcu), w oryginalnym README także `discord.gg/UCDyZyr6gg`.
Uwaga: zaproszenia mogą wygasać – do aplikacji lepiej wpisać stałe zaproszenie od administratorów albo link do kanału.
Użytkownik nie wie, że wyszła nowa wersja, dopóki sam nie sprawdzi. Przykład: „Artanis has Gone Zeratul(like)!”
miał ręcznie wgraną wersję 1.0.8, a na Discordzie była już 1.0.10.

SC2CCM zna już wersję każdego zainstalowanego moda – z pola `version=` w `metadata.txt`.

## Źródła informacji o nowych wersjach

1. **Wiki / arkusz projektu (Google Sheets).** Większe projekty mają publiczny arkusz, np.
   [Artanis Has Gone Zeratul(like) – Wiki](https://docs.google.com/spreadsheets/d/e/2PACX-1vTGlEOBSsNjGr59EoZgW_GWHCiIhrhpLXE0GCMC2r0SukuHWj4MeVl1fTK9GnX8gA/pubhtml#gid=1771237588)
   (zakładki m.in. „Patch notes”, „Changes from Vanilla”). Opublikowany arkusz da się pobrać jako CSV bez logowania:
   `…/pub?output=csv&gid=<id zakładki>`. Z zakładki „Patch notes” można wyczytać najnowszy numer wersji i opis zmian.
   - Plus: bez kont i tokenów, dane prowadzi autor.
   - Minus: każdy arkusz ma inny układ – potrzebny osobny „przepis” na moda (która zakładka, jak znaleźć wersję). Arkusz zwykle nie ma linku do pobrania.
2. **Discord.** Tam są aktualne linki, ale czytanie kanałów wymaga bota dodanego do serwera przez administratorów (token, uprawnienia) – aplikacja użytkownika nie może tego robić sama.
   - Wariant: bot/GitHub Action po stronie projektu zbiera nowe wiadomości z kanałów wydań i aktualizuje wspólny indeks (punkt 3). Wymaga zgody i współpracy administratorów Discorda.
   - Wariant minimum: przycisk „Open on Discord” przy modzie (link do kanału/wiadomości), bez automatyki.
3. **Wspólny indeks modów (JSON w repo).** Plik np. `mods-index.json` w tym repo (albo osobnym), czytany przez aplikację z GitHuba:
   ```json
   [{ "title": "Artanis has Gone Zeratul(like)!", "author": "CaptainKian", "campaign": "LotV",
      "latest": "1.0.10", "updated": "2026-10-18",
      "download": "https://…", "wiki": "https://docs.google.com/…", "discord": "https://discord.com/channels/…",
      "changelog": "…" }]
   ```
   Dopasowanie do zainstalowanego moda po `title` (+ `author`). Indeks aktualizowany ręcznie (pull request), półautomatycznie
   (GitHub Action raz dziennie czyta arkusze z punktu 1) albo przez bota z punktu 2.
4. **Pole w `metadata.txt` (długoterminowo).** Propozycja dla autorów: opcjonalne `updateUrl=` (adres pliku z najnowszą
   wersją i linkiem) i `wiki=`. Stare wersje CCM ignorują nieznane klucze (`processLine` czyta tylko znane), więc to bezpieczne.
   Wymaga przekonania autorów – na początek indeks z punktu 3.

## Jak mogłoby to wyglądać w aplikacji

- Przy starcie (i przyciskiem „Check Mod Updates”) aplikacja pobiera indeks i porównuje `latest` z `version` z `metadata.txt`.
- Przy modzie z nowszą wersją: znaczek „Update available: 1.0.8 → 1.0.10”, opis zmian, przyciski **Download** (otwiera link w przeglądarce) i **Wiki**.
- Etap później: jeśli link prowadzi bezpośrednio do pliku `.zip` (nie do strony Google Drive/Discorda), pobranie i import jednym kliknięciem – z tą samą ścieżką co ręczny import (`CampaignManager.Import`) i pytaniem, czy ustawić jako aktywny.
- Porównanie wersji tolerancyjne (`1.0.10` > `1.0.8` liczbowo, a nie tekstowo; wersje typu `v2.1b` – porównanie po częściach).

## Do ustalenia

- Czy administratorzy Discorda CCM zgodzą się na bota albo na prowadzenie indeksu (najlepiej po ich stronie, żeby nie dublować pracy).
- Gdzie są pliki do pobrania (Discord CDN wygasa po czasie, Google Drive wymaga obejścia strony potwierdzenia) – od tego zależy, czy da się zrobić „aktualizuj jednym kliknięciem”.
- Które mody mają arkusze z historią zmian i w jakim układzie.
