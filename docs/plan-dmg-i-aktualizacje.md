# Plan: instalator DMG i aktualizacje aplikacji

Status: **zaplanowane, nierealizowane.**
Wzór: system aktualizacji z [DarkenPL/KarKa](https://github.com/DarkenPL/KarKa) (`app/aktualizacje.js`, `.github/workflows/aplikacja-mac.yml`, `docs/aplikacja.md` → „Aktualizacje”).

Cel: użytkownik instaluje SC2CCM raz z DMG, a potem aplikacja aktualizuje się sama – bez konta Apple Developer, bez notaryzacji i bez ponownego odblokowywania w Gatekeeperze.

Tylko Apple Silicon (`osx-arm64`).

## 1. DMG (ok. 20 min)

- W workflow, po `build-mac.sh`: `hdiutil create` z folderu zawierającego `SC2CCM.app` i dowiązanie `Applications → /Applications` (okno „przeciągnij do Programów”), format `UDZO`.
- Opcjonalnie tło i rozmieszczenie ikon (np. `create-dmg` z Homebrew) – nie jest konieczne.
- Sprawdzenie w CI: `hdiutil attach -nobrowse -readonly`, `codesign --verify --deep --strict` na zamontowanej aplikacji, `test -L …/Applications`, `hdiutil detach`.
- Plik: `SC2CCM.dmg` (stała nazwa → link `releases/latest/download/SC2CCM.dmg` w README).

## 2. Wydania na GitHubie (ok. 20 min)

- Wersja z `<Version>` w `src/SC2CCM.Mac/SC2CCM.Mac.csproj`.
- Push do `main` → wydanie `v<wersja>` oznaczone jako najnowsze (`gh release create … --latest --generate-notes`), `permissions: contents: write`.
- Wersja już wydana → tylko ostrzeżenie w Actions (`::warning::`), bez nowego wydania (jak w KarKa).
- Pliki wydania:
  - `SC2CCM.dmg` – pierwsza instalacja,
  - `SC2CCM.zip` – cała aplikacja (`ditto -c -k --sequesterRsrc --keepParent`), używana przez aktualizator,
  - `aktualizacja.json` – manifest: `{ "wersja": "1.1.0", "pelna": { "plik": "SC2CCM.zip", "sha256": "…", "rozmiar": 42000000 } }`.
- Zasada dla kolejnych zmian (do CLAUDE.md): zmiana dla użytkowników = podniesienie `<Version>`.

## 3. Aktualizator w C# (ok. 45–60 min)

Nowa klasa w `SC2CCM.Core` (logika, testowalna) + okno w `SC2CCM.Mac`.

1. **Sprawdzanie:** `GET https://api.github.com/repos/DarkenPL/SC2CCM/releases/latest` (repo publiczne, bez tokenu, nagłówek `User-Agent`). Z `assets` bierzemy `aktualizacja.json`, porównujemy wersję z `AssemblyInformationalVersion`.
   - Przy starcie, po kilku sekundach, po cichu (brak internetu / brak nowej wersji = brak komunikatu).
   - W menu: „Check for Updates…” (wtedy komunikat także przy braku nowej wersji).
2. **Okno:** wersja obecna → nowa, opis wydania (`body`), przyciski **Install** / **Later** / **Skip This Version** (pominięta wersja w `~/Library/Application Support/SC2CCM/settings.json`; przy ręcznym sprawdzeniu pytamy mimo to).
3. **Pobieranie:** `HttpClient` do `~/Library/Application Support/SC2CCM/downloads/…part`, pasek postępu, kontrola SHA-256 z manifestu, dopiero potem zmiana nazwy.
4. **Rozpakowanie:** `/usr/bin/ditto -x -k` (zachowuje atrybuty i podpis w `.app`), potem `codesign --verify --deep --strict` na nowej aplikacji.
5. **Podmiana:** skrypt bash w `downloads/`, uruchomiony odłączony (`setsid`/`nohup`):
   - czeka, aż proces (PID) się zakończy,
   - `mv SC2CCM.app → SC2CCM.app.stara-wersja`, `mv nowa → SC2CCM.app`,
   - `xattr -dr com.apple.quarantine`, usunięcie starej wersji,
   - przy błędzie przywraca starą wersję,
   - `open` nowej aplikacji, log do `downloads/podmiana.log`.
6. **Warunki:** aplikacja musi działać z paczki `.app` (ścieżka z `Environment.ProcessPath`), a folder nad nią (zwykle `/Applications`) musi być zapisywalny. W przeciwnym razie okno proponuje pobranie DMG ze strony wydania.

Dlaczego bez podpisu działa: plik pobrany przez samą aplikację (nie przez przeglądarkę) nie dostaje atrybutu `com.apple.quarantine`, więc Gatekeeper nie pyta. Pyta tylko raz – przy pierwszej instalacji z DMG pobranego przeglądarką. Podpis ad-hoc (`codesign -s -`) jest wymagany na Apple Silicon i zostaje zachowany przez `ditto`.

Nie przenosimy z KarKa „małej aktualizacji” (nakładki kodu): tam kod to JavaScript ładowany z dysku, tu kod jest skompilowany wewnątrz `.app`, a podmiana bibliotek w paczce psuje jej podpis. Pełna paczka ma ok. 42 MB, więc zawsze podmieniamy całą aplikację.

## 4. Testy (ok. 30 min)

- Testy logiki (`tests/SC2CCM.Core.Tests`): porównanie wersji, parsowanie odpowiedzi API i manifestu, odrzucenie złej sumy SHA-256, generowanie skryptu podmiany (podmiana i przywracanie na sztucznych folderach `.app`).
- Test w CI na runnerze macOS: lokalny serwer HTTP udaje API GitHuba (zmienna środowiskowa z adresem, jak `KARKA_AKTUALIZACJE`), aplikacja z wersją `1.0.0` pobiera paczkę `99.0.0`, podmienia się i po restarcie zgłasza nową wersję.

## Uwagi

- Obecna wersja nie ma aktualizatora – pierwszą wersję z aktualizatorem trzeba zainstalować ręcznie z DMG.
- Szacowany łączny czas: ok. 2 godziny pracy + kilka przebiegów CI.
