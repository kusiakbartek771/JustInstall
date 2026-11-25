# JustInstall ⬆️

Instalator aplikacji Just – **JustMusic** i **JustMinecraft**: instalacja, aktualizacja, naprawa i odinstalowanie jednym kliknięciem.

- **[`JustInstall.exe`](https://github.com/kusiakbartek771/JustInstall/releases/latest/download/JustInstall.exe)** (~460 KB) – Windows 10 / 11, bez instalacji (.NET Framework 4.8 jest wbudowany w system).
- Pobiera najnowsze wydania prosto z GitHuba i sprawdza je sumą **SHA-512** z `latest.yml`.
- **Napraw** – czyści zepsutą pamięć aktualizacji i instaluje najnowszą wersję na istniejącą (ustawienia zostają).
- **Odinstaluj** – uruchamia oficjalny deinstalator; opcjonalnie usuwa też dane (z ostrzeżeniem – w JustMinecraft to także światy).
- Zamyka uruchomioną aplikację przed zmianami (pyta wcześniej).
- Działa bez uprawnień administratora (instalacja dla bieżącego użytkownika, `asInvoker`).
- Linux: działa przez Mono (`mono JustInstall.exe`) – AppImage w `~/Applications` + skrót w menu.

## Windows Defender / SmartScreen

JustInstall nie jest spakowany ani zaciemniony, nie uruchamia PowerShella ani skryptów, pobiera pliki tylko z `github.com`
i weryfikuje je przed uruchomieniem – to rzeczy, które zwykle wywołują fałszywe alarmy. Plik nie ma jednak podpisu
cyfrowego, więc przy pierwszym uruchomieniu pobranego pliku SmartScreen może pokazać „System Windows ochronił komputer”
→ **Więcej informacji → Uruchom mimo to**. Całkowicie usuwa to dopiero certyfikat do podpisywania kodu.

## Budowanie

```bash
./build.sh        # wymaga mono (mcs); wynik: JustInstall.exe
```

Test bez klikania: `JUSTINSTALL_AUTO="install:justmusic,repair:justmusic,uninstall:justmusic" JUSTINSTALL_SHOT=/tmp/zrzut.png mono JustInstall.exe`
