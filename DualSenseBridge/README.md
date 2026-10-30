# DualSense Bridge

Windows 11 aplikacija koja PS5 **DualSense** (i DualSense Edge) kontroler spojen preko **Bluetootha** pretvara u
**Xbox 360 kontroler**. Radi u svim PC igrama koje podržavaju Xbox kontroler, i u onima koje DualSense inače
prepoznaju samo preko USB kabela.

- Automatski prepoznaje DualSense preko Bluetootha (i USB-a) čim ga uključite.
- Emulira Xbox 360 kontroler preko provjerenog **ViGEmBus** drivera.
- Sprječava dvostruke unose: originalni DualSense skriva od igara preko **HidHide** drivera.
- Radi u pozadini, uz ikonu u traci pored sata. Boja točkice na ikoni pokazuje stanje baterije.
- Prikazuje bateriju (%, punjenje) i status veze, a upozorava kad je baterija slaba.
- Pokreće se s Windowsima, bez UAC upita.
- Prosljeđuje vibraciju iz igara, upravlja svjetlosnom trakom i LED-icama igrača (Xbox igrač 1–4).
- Moderno tamno sučelje s prikazom palica, okidača i tipki uživo.

---

## 1. Instalacija (za korisnike)

### Najlakše: instalacijski program

1. Pokrenite `DualSenseBridge-Setup-<verzija>.exe` (kako ga izraditi piše u [poglavlju 4](#4-izrada-exe-instalacije)).
2. Ostavite označene opcije **ViGEmBus** i **HidHide**. Prikazuju se samo ako driveri još nisu instalirani.
   Instalacija pokreće službene, digitalno potpisane instalacijske programe drivera, pa samo kliknite kroz njih.
3. Ako HidHide zatraži ponovno pokretanje računala, pristanite.
4. Na kraju pokrenite DualSense Bridge. Windows će jednom zatražiti administratorsku dozvolu (UAC).

Aplikacija je *self-contained*, pa nije potrebno posebno instalirati .NET.

### Ručna instalacija drivera

Ako drivere želite instalirati sami (ili ih instalacija nije uključila):

| Driver | Čemu služi | Preuzimanje |
|---|---|---|
| **ViGEmBus** (obavezno) | Virtualni bus driver koji Windowsu i igrama predstavlja pravi Xbox 360 kontroler. Koriste ga i DS4Windows, Steam i drugi. | <https://github.com/nefarius/ViGEmBus/releases/latest> → `ViGEmBus_1.22.0_x64_x86_arm64.exe` |
| **HidHide** (preporučeno) | Filter driver koji originalni DualSense skriva od igara, pa one vide samo Xbox kontroler. | <https://github.com/nefarius/HidHide/releases/latest> → `HidHide_1.5.230_x64.exe` |

Koraci:

1. Preuzmite i pokrenite instalaciju **ViGEmBus**. Ponovno pokretanje uglavnom nije potrebno.
2. Preuzmite i pokrenite instalaciju **HidHide**, pa **ponovno pokrenite računalo**.
3. HidHide **nije potrebno ručno konfigurirati**. DualSense Bridge sam sebe dodaje na popis dopuštenih
   aplikacija, skriva samo DualSense kontrolere i pri izlazu sve vraća kako je bilo.
4. Provjera: u aplikaciji otvorite **Driveri i pomoć**. Oba drivera trebaju imati zelenu točku.

> ViGEmBus je projekt u statusu „završen / bez novih značajki“, ali je i dalje standardni i stabilni
> driver za emulaciju kontrolera na Windowsima 10/11.

### Uparivanje DualSense kontrolera preko Bluetootha

1. Na isključenom kontroleru držite **Create** (mala tipka lijevo od touchpada) i **PS** oko 3 sekunde,
   dok svjetlo oko touchpada ne počne brzo treptati.
2. Otvorite **Postavke → Bluetooth i uređaji → Dodaj uređaj → Bluetooth**.
3. Odaberite **Wireless Controller**.
4. Od sada je dovoljno pritisnuti **PS** na kontroleru i DualSense Bridge ga automatski preuzima.

---

## 2. Korištenje

- **Kontroleri**: svaki spojeni DualSense ima karticu s nazivom, vrstom veze (Bluetooth/USB), Xbox brojem
  igrača, frekvencijom izvještaja (Hz), baterijom i prikazom unosa uživo. Gumbi:
  - **Test vibracije**: kratka vibracija, korisna kad je spojeno više kontrolera.
  - **Isključi kontroler**: prekida Bluetooth vezu i gasi kontroler da štedi bateriju.
- **Postavke**: autostart, ponašanje trake, obavijesti, upozorenje za bateriju, skrivanje originala (HidHide),
  jačina vibracije, mrtve zone palica i okidača, funkcija klika na touchpad, svjetlosna traka (boja igrača,
  boja prema bateriji, vlastita boja ili isključeno) i njezina svjetlina.
- **Ikona u traci**: lijevi klik otvara prozor. Desni klik nudi izbornik (otvori, isključi kontrolere,
  autostart, izlaz). Točkica na ikoni je zelena, žuta ili crvena prema bateriji, a plava dok se kontroler puni.
- Zatvaranje prozora (X) aplikaciju samo skriva u traku. Za potpuni izlaz koristite **Izlaz**.

### Raspored tipki

| DualSense | Xbox 360 |
|---|---|
| ✕ / ○ / □ / △ | A / B / X / Y |
| L1 / R1 | LB / RB |
| L2 / R2 (analogno) | LT / RT |
| L3 / R3 | LS / RS (klik palice) |
| Lijeva / desna palica | Lijeva / desna palica |
| D-pad | D-pad |
| Create | Back (View) |
| Options | Start (Menu) |
| PS | Guide (Xbox) |
| Klik na touchpad | Back (može se promijeniti: ništa / Start / Guide) |

### Pokretanje iz naredbenog retka

| Argument | Značenje |
|---|---|
| `--tray` | Pokreni bez prozora (koristi ga autostart, ako je uključena postavka „ostani u traci“). |
| `--register-hidhide` | Samo dodaj aplikaciju na HidHide popis dopuštenih (koristi ga instalacija). |
| `--cleanup` | Ukloni HidHide unose i autostart zadatak (koristi ga deinstalacija). |

---

## 3. Kako radi

```
DualSense ──Bluetooth HID──► HidSharp (čitanje ulaznih izvještaja 0x31)
                                 │
                                 ▼
                     DualSenseReportParser (CRC32 provjera, palice, tipke, baterija)
                                 │
                                 ▼
                     Xbox360Mapper (mrtve zone, raspored tipki)
                                 │
                                 ▼
          ViGEmBus ◄── Nefarius.ViGEm.Client (virtualni Xbox 360 kontroler) ──► igra (XInput)
                                 │
        vibracija / LED igrača ◄─┘  →  DualSense izlazni izvještaj 0x31 (CRC32)

HidHide: originalni DualSense vidljiv je samo procesu DualSense Bridge → nema dvostrukog unosa.
```

- Preko Bluetootha DualSense nakon spajanja šalje skraćeni izvještaj. Aplikacija čitanjem kalibracijskog
  *feature* izvještaja (0x05) prebacuje kontroler u puni način (0x31) koji sadrži i bateriju.
- Svaki izvještaj s Bluetootha provjerava se CRC32 kontrolnim zbrojem, a neispravni se odbacuju.
- Virtualni Xbox kontroler dodaje se tek kad stigne prvi ispravan izvještaj. Pri odspajanju se prvo pošalje
  neutralno stanje (da nijedna tipka ne ostane „pritisnuta“), a zatim se kontroler uklanja.
- Svaki kontroler ima svoju I/O dretvu povišenog prioriteta. Igri se šalje izvještaj samo kad se stanje promijeni.
- Sve HidHide promjene zapisuju se u `%LocalAppData%\DualSenseBridge\state.json`. Pri izlazu, pri odjavi iz
  Windowsa i pri deinstalaciji poništavaju se, pa original ponovno postaje vidljiv.
- Već viđeni kontroleri skrivaju se odmah pri pokretanju aplikacije, prije nego se uključe, tako da ih igra
  ne stigne „zgrabiti“.
- Autostart je zadatak u Task Scheduleru (`DualSenseBridge`), pokreće se pri prijavi s najvišim ovlastima
  (bez UAC upita), normalnim prioritetom i bez vremenskog ograničenja.

Datoteke:

| Putanja | Sadržaj |
|---|---|
| `%AppData%\DualSenseBridge\settings.json` | Postavke |
| `%LocalAppData%\DualSenseBridge\state.json` | HidHide evidencija (za sigurno vraćanje) |
| `%LocalAppData%\DualSenseBridge\logs\` | Dnevnici (čuvaju se 7 dana) |

---

## 4. Izrada EXE instalacije

### Potrebno (na Windowsima)

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0): `winget install Microsoft.DotNet.SDK.8`
- [Inno Setup 6.5+](https://jrsoftware.org/isdl.php): `winget install JRSoftware.InnoSetup`
  (sa starijom verzijom 6.x instalacija se izradi samo na engleskom)

### Jedna naredba

```powershell
cd DualSenseBridge
powershell -ExecutionPolicy Bypass -File build\build-installer.ps1 -Version 1.0.0
```

Isto možete napraviti dvoklikom na `build\build-installer.cmd`.

Skripta:

1. pokreće unit testove,
2. objavljuje aplikaciju kao self-contained `win-x64` (ReadyToRun) u `artifacts\publish`,
3. preuzima službene instalacije ViGEmBus i HidHide drivera u `installer\redist`
   (provjerava SHA-256 i digitalni potpis),
4. Inno Setupom izrađuje `artifacts\installer\DualSenseBridge-Setup-1.0.0.exe`.

Opcije: `-SkipTests`, `-SkipDrivers` (instalacija bez drivera; tada ih korisnik instalira ručno).

### GitHub Actions

Workflow `.github/workflows/dualsense-bridge.yml` na `windows-latest` pokreće testove i izrađuje instalaciju.
Gotov EXE preuzima se kao artefakt **DualSenseBridge-Setup** na stranici izvođenja workflowa. Ručno se
pokreće preko **Actions → DualSense Bridge → Run workflow**.

### Ručno, bez instalacije

```powershell
dotnet publish src\DualSenseBridge\DualSenseBridge.csproj -c Release -r win-x64 --self-contained true -o publish
publish\DualSenseBridge.exe
```

---

## 5. Razvoj

```
DualSenseBridge/
├─ DualSenseBridge.sln
├─ src/
│  ├─ DualSenseBridge.Core/        # Čista logika (bez Windows ovisnosti): protokol, CRC32, mapiranje, postavke
│  │  ├─ DualSense/                # Parser ulaznih i graditelj izlaznih izvještaja, konstante
│  │  ├─ Mapping/                  # DualSense → Xbox 360, svjetlosna traka
│  │  └─ Configuration/            # AppSettings
│  └─ DualSenseBridge/             # WPF aplikacija (.NET 8, Windows)
│     ├─ Controllers/              # ControllerManager (otkrivanje), ControllerSession (HID ↔ ViGEm)
│     ├─ Services/                 # HidHide, autostart, Bluetooth, traka, postavke, log, jedna instanca
│     ├─ ViewModels/ Themes/       # MVVM i tamna tema
│     └─ MainWindow.xaml
├─ tests/DualSenseBridge.Tests/    # xUnit testovi protokola i mapiranja
├─ installer/                      # Inno Setup skripta (+ hrvatski prijevod)
└─ build/                          # Skripta za izradu instalacije
```

- Otvorite `DualSenseBridge.sln` u Visual Studiju 2022 (17.8+) ili Rideru.
- Aplikacija traži administratorska prava (zbog HidHide-a), pa Visual Studio pokrenite **kao administrator**
  da bi F5 radio.
- Testovi: `dotnet test` (rade i na Linuxu/macOS-u, jer `Core` nema Windows ovisnosti).

Korištene biblioteke: [HidSharp](https://www.zer7.com/software/hidsharp),
[Nefarius.ViGEm.Client](https://github.com/ViGEm/ViGEm.NET),
[Nefarius.Drivers.HidHide](https://github.com/nefarius/Nefarius.Drivers.HidHide).
Format izvještaja DualSense kontrolera prati Linux driver `hid-playstation`.

---

## 6. Rješavanje problema

| Problem | Rješenje |
|---|---|
| „Potreban je ViGEmBus driver“ | Instalirajte ViGEmBus (poglavlje 1) i kliknite **Provjeri ponovno**. |
| Igra reagira dvostruko ili prikazuje PlayStation ikone | Instalirajte HidHide i uključite **Sakrij originalni DualSense**. Igru pokrenite **nakon** aplikacije. |
| Steam dodaje još jedan kontroler | Steam → Postavke → Kontroler → isključite *PlayStation Configuration Support* (ili *Steam Input* za tu igru). |
| Kontroler se ne pojavljuje | Provjerite je li spojen u Bluetooth postavkama. Ako ne pomogne, uklonite ga, uparite ponovno (Create + PS) i provjerite logove. |
| „Kontroler nije moguće otvoriti…“ nakon instalacije HidHide-a | Zatvorite aplikaciju (Izlaz) i pokrenite je ponovno. |
| Vibracija ne radi | Provjerite postavku **Vibracija iz igara** i gumb **Test vibracije**. Neke igre vibriraju samo u igri, ne u izbornicima. |
| Palica „kliže“ | Povećajte mrtvu zonu palice u postavkama. |
| Kašnjenje preko Bluetootha | Držite kontroler bliže prijemniku. USB 3 portovi i Wi-Fi na 2,4 GHz mogu ometati Bluetooth, pa pomaže USB produžni kabel za Bluetooth adapter. |
| Drugi alati | Ne pokrećite istovremeno DS4Windows, DualSenseX ili slične alate. |

Dnevnici su u `%LocalAppData%\DualSenseBridge\logs` (gumb **Otvori logove** u aplikaciji).

### Deinstalacija

**Postavke → Aplikacije → DualSense Bridge → Deinstaliraj.** Deinstalacija uklanja autostart zadatak i HidHide
unose aplikacije. ViGEmBus i HidHide ostaju instalirani jer ih mogu koristiti i drugi programi. Možete ih
zasebno ukloniti na istom mjestu.

### Ograničenja

- Xbox 360 kontroler nema adaptivne okidače, napredni haptički feedback, žiroskop ni touchpad, pa se te
  značajke DualSense kontrolera u emulaciji ne koriste.
- Igre koje izvorno podržavaju DualSense (npr. preko Steam Inputa) dok je aplikacija pokrenuta vide samo
  Xbox kontroler. Za takve igre aplikaciju zatvorite.
