# PentaGrammata — architekturális hibák

## Vizsgálati keret

- **Dátum:** 2026-10-05.
- **Vizsgált commit:** `a59d889c31662cc4fd33f3b063a09f0e81bf86ad`.
- **Hatókör:** réteghatárok, függőséginjektálás, konfiguráció- és sessionállapot, perzisztencia, visszaállítás, aszinkron életciklus, audiohatár és a kiadási verzió szerződése.
- **Forrás:** a repository kódja, tesztjei, `AGENTS.md` és a kiadási workflow. Nem csak az utolsó commit diffjét vizsgáltam.
- **Alapellenőrzés:** `dotnet build src/PentaGrammata.csproj` sikeres, 0 warning és 0 error; `dotnet test tests/PentaGrammata.Tests/PentaGrammata.Tests.csproj` sikeres, **280/280 teszt**, 0 kihagyás. SDK: `10.0.100`.
- **Kiegészítő ellenőrzés:** repositoryn kívüli, ideiglenes .NET konzolpróbák, valódi alkalmazásosztályokkal és szükség szerint helyettesített interfészekkel. Minden fájlművelet ideiglenes könyvtárat használt; valódi felhasználói adatot nem érintett. A hangkimenetet és a GitHub HTTP-választ helyettesítettem. Natív audioeszközzel vagy interaktív UI-val nem teszteltem.
- **Auditkori állapot:** a felmérés elkészítésekor nem történt javítás. Az azóta elvégzett A08–A13 javításokat az alábbi állapotjegyzet rögzíti.

### Jelölések

- **Magas:** adatvesztés vagy az érvényes alkalmazás-/sessionállapot sérülése.
- **Közepes:** funkcionális szerződés, aszinkron életciklus vagy több komponens közös értelmezése hibás.
- **Alacsony:** bizonyítható réteg-/függőségi probléma; önmagában nem állítok futásidejű hibát.
- **Reprodukált:** izolált futtatással is igazolt viselkedés. **Kódút alapján igazolt:** a megadott hívási útvonalból levezetett probléma. **Szerkezeti:** a függőség ténye igazolt, következménye tesztelhetőségi vagy karbantartási korlátozás.

A súlyosság nem a javítás munkaigényét jelenti. Az alábbi működési hibák azért szerepelnek architekturális találatként, mert a gyökérokuk állapottulajdonlási, modell-, interfész- vagy réteghatárbeli hiányosság, nem pusztán egy lokális elírás.

## Összefoglaló

| ID | Súlyosság | Találat | Igazolás |
| --- | --- | --- | --- |
| A01 | Magas | A visszaállítás destruktív és nincs rollback | Reprodukált DB-cserehiba; kódút alapján többfájlos részleges import |
| A02 | Magas | Az importált konfiguráció validálása az élesítés után történik | Reprodukált |
| A03 | Magas | Az adatbázis import előtti ellenőrzése csak fájlfejlécet vizsgál | Reprodukált |
| A04 | Magas | A sessionnek nincs teljes, változatlan beállításpillanatképe | Reprodukált |
| A05 | Közepes | A mentés állapota a bezárható eredményablakhoz kötött | Reprodukált állapotsorrend; az ablakbezárási út kód alapján igazolt |
| A06 | Közepes | Az időzített sessionnek nincs tényleges határideje | Reprodukált renderhosszak; kódút alapján igazolt |
| A07 | Közepes | A Morse-szimbólum fogalma eltér a pontozás két oldalán | Reprodukált |
| A08 | Közepes | A megszakítás nem jut el a teljes audio-renderelési láncig | Reprodukált |
| A09 | Közepes | A natív audiobackend nem közvetíti megbízhatóan a hibát | Kódút alapján igazolt |
| A10 | Közepes | A CSV-export aszinkron műveletét szinkron esemény választja le a parancsról | Kódút alapján igazolt |
| A11 | Közepes | A frissítésellenőrző más verzióazonosságot használ, mint a kiadási rendszer | Reprodukált |
| A12 | Alacsony | Avalonia-függő ablakadapterek szivárognak az üzleti rétegbe | Szerkezeti |
| A13 | Alacsony | A headroom-elemző megkerüli az injektált audiofüggőségeket | Szerkezeti |

## Javítási állapot — 2026-10-05

Az alábbi hat találat javítva. Az A01–A07 változatlanul nyitott megállapítás; a részletes szakaszok a javítás előtti állapotot dokumentálják.

| ID | Állapot | Változás |
| --- | --- | --- |
| A08 | Javítva | A cancellation token végigér a Morse-rendereren, a QSB- és DSP-ciklusokon, valamint a 16 bites minták előállításán. |
| A09 | Javítva | A PulseAudio és waveOut natív hibakódjai `AudioPlaybackException`-ként jutnak vissza; a hibákat nem nyeli el a backend. |
| A10 | Javítva | A Trends VM `AsyncRelayCommand`-ot vár meg; a prezentációs export-szolgáltatás kezeli a fájlválasztót és aszinkron írást. A hiba és a siker a dialógusban látható, bezárás folyamatban lévő export alatt tiltott. |
| A11 | Javítva | A frissítésellenőrző mind a négy verziókomponenst összehasonlítja; a hiányzó build/revision nulla. |
| A12 | Javítva | Az ablakinterfészek és adapterek a `Presentation` névtérbe kerültek; a view modellek csak fontneveket kezelnek, a toolkit-hozzáférés és a konverzió a prezentációs rétegben marad. |
| A13 | Javítva | A lejátszás és a headroom-elemzés közös injektált renderer-factoryt használ; az elemző fix véletlenmagja a zajra és QSB-re is determinisztikus. |

**Ellenőrzés:** `dotnet build src/PentaGrammata.csproj --no-restore` és `dotnet build tests/PentaGrammata.Tests/PentaGrammata.Tests.csproj --no-restore` sikeres, mindkettő 0 warning / 0 error. A teszteket nem futtattam; natív audioeszközzel és interaktív fájlválasztóval sem ellenőriztem.

## Részletes találatok

### A01 — A visszaállítás nem rendelkezik biztonságos commit/rollback határral

**Súlyosság:** magas.

**Kódhelyek:**

- `src/Stores/PracticeResultStatisticsStore.cs:238-270` — a régi DB és a WAL/SHM fájlok törlése megelőzi az új DB másolását.
- `src/Services/UserBackupService.cs:186-220` — egymást követő DB-, ablakméret- és konfigurációcsere, rollback nélkül.
- `src/Services/UserBackupService.cs:17-23` — a dokumentáció is elismeri a részleges alkalmazást.

**Architekturális ok:** a visszaállítás egységes felhasználói művelet, de nincs hozzá egységes, visszagörgethető commitprotokoll. Még a DB egyedi lecserélése sem hibatűrő: az eredeti példány eltűnik, mielőtt a helyettesítő biztosan a célhelyen lenne.

**Következmény:** másolási hiba esetén az eredeti statisztikák elveszhetnek. Ha a DB-csere sikerül, de a konfiguráció írása nem, a program a backupból származó statisztikákat a régi beállításokkal használja; az import közben hibát jelent. A „megismételhető” művelet nem ugyanaz, mint a sikertelen művelet utáni eredeti állapot megőrzése.

**Ellenőrzés:** ideiglenes store-ba mentett egy rekord után a `ReplaceDatabaseAsync` nem létező forrásfájllal hibára futott; utána `File.Exists(DatabasePath)` értéke **false** volt. Az import által létrehozott temp fájl rendszerint létezik, de egy másolási I/O-hiba ugyanebben a törlés-után-másolás szakaszban következik be.

**Javítási irány:** a helyettesítőt előbb a célfájllal azonos fájlrendszeren kell előkészíteni és ellenőrizni; a régi, konzisztens DB-példányt a sikeres commitig meg kell tartani. A három fájlhoz és az éles cache-/konfigurációváltáshoz visszaállítási protokoll szükséges. Több fájl esetén az egyenkénti atomikus átnevezés önmagában nem ad közös tranzakciót; rollback és szükség esetén helyreállítási napló is kell.

**Szükséges regressziós teszt:** hibainjektálás DB-másolásnál, ablakméretírásnál és konfigurációírásnál; minden esetben az eredeti adatok, cache-ek és éles beállítások maradjanak használhatók.

### A02 — A konfiguráció invariánsai nem az éles állapotba lépés határán érvényesülnek

**Súlyosság:** magas.

**Kódhelyek:**

- `src/Services/UserBackupService.cs:174-177`, `201-220`, `317-326` — deszerializálás után fájlírás és éles reload; üzleti validálás nincs.
- `src/Services/ConfigurationService.cs:205-230` — a reload/import közvetlenül lecseréli a megosztott `Current` tartalmát.
- `src/ViewModels/MainWindowViewModel.cs:190-204` — a `TryApplySettings` csak a már befejezett import után fut.
- `src/Services/PracticeController.cs:46-56`, `208-219` — a controller a közös konfigurációból olvas; a validátor elutasítása nem állítja vissza azt.

**Architekturális ok:** az alkalmazás konfigurációs tulajdonosa nem védi minden belépési ponton az invariánsokat. A Settings útvonal előbb validál, az Import útvonal előbb publikál és csak utána validál. A validálás így UI-orchesztrációs lépés lett, nem az állapotváltás előfeltétele.

**Konkrét forgatókönyv:** szintaktikailag helyes backupban `CharacterWpm = 0`. Az import felülírja a fájlt és a `Current` értékét. A főablak később hibát jelez, de kikapcsolt auto-adjust mellett a következő gyakorlás már a hibás WPM-et olvassa.

**Ellenőrzés:** a próba eredménye: `applyAccepted=False`, `liveCharacterWpm=0`, `persistedCharacterWpm=0`; a hibaüzenet „Character and average WPM must be positive values.” A visszautasított állapot tehát már éles és lemezre került.

**Javítási irány:** import és normál szerkesztés ugyanazt a konfigurációs alkalmazási műveletet használja: izolált jelölt betöltése → normalizálás és teljes validálás → commit → éles publikálás. A startup-betöltés is kapjon ellenőrzött hibakezelési stratégiát. A controller ne kapjon eleve érvénytelen közös állapotot.

**Szükséges regressziós teszt:** érvénytelen, de helyes JSON importjának elutasítása után a fájl, `Current`, controllersebesség és UI mind az import előtti állapotot mutassa.

### A03 — A backup validálása nem az adatbázistulajdonos tényleges szerződését ellenőrzi

**Súlyosság:** magas.

**Kódhelyek:**

- `src/Services/UserBackupService.cs:162-172`, `286-305` — kizárólag az első 16 byte ellenőrzése.
- `src/Stores/PracticeResultStatisticsStore.cs:249-260` — a csere nem nyitja meg és nem ellenőrzi a jelölt DB-t.
- `src/Stores/PracticeResultStatisticsStore.cs:75-79` — a tényleges megnyitás és sémaellenőrzés csak a későbbi olvasáskor történik.

**Architekturális ok:** az archívumszolgáltatás saját, gyenge fájlformátum-ellenőrzést használ az adatbázisréteg integritás- és kompatibilitásvizsgálata helyett. A „validált backup” és a „használható statisztikai adatbázis” nem ugyanazt jelenti.

**Konkrét forgatókönyv:** a DB-bejegyzés pontosan `SQLite format 3\0`, azaz 16 byte. Átmegy a validáláson, felülírja a korábbi történetet, és az import sikerrel tér vissza.

**Ellenőrzés:** a valódi backup-/statistics-store útvonal elfogadta a mintát; az új fájl 16 byte-os lett. A következő statisztikaolvasás hibája: `SQLite Error 26: 'file is not a database'`.

**Javítási irány:** az adatbázisréteg biztosítson importjelölt-validálást. A temp példányon tényleges SQLite-megnyitás, integritásvizsgálat és támogatott séma/migrálhatóság ellenőrzése történjen, az eredeti adatbázis érintése előtt. Régi, támogatott sémát ne utasítson el pusztán az életkora miatt.

**Tesztlefedettség:** `tests/PentaGrammata.Tests/Services/UserBackupServiceTests.cs:239-252` csak olyan szemétadatot vizsgál, amelynek a fejléce is hibás. Szükséges fejléc-helyes, de csonka/sérült DB, valamint nem támogatott séma tesztje is.

### A04 — A sessionállapot összefolyik a következő session konfigurációjával

**Súlyosság:** magas.

**Kódhelyek:**

- `src/Services/PracticeController.cs:31-34`, `117-120` — csak a tényleges WPM kerül tartósan sessionpillanatképbe.
- `src/Services/PracticeController.cs:185-205` — értékelés és beállításpillanatkép az aktuális konfigurációból készül.
- `src/ViewModels/PracticeViewModel.cs:166-179` — az eredményablak a kiértékeléskori küszöböt és zajbeállítást kapja.
- `src/ViewModels/MainWindowViewModel.cs:97-105` — Settings/Import parancsok nem függnek a gyakorlás állapotától.
- `src/Services/PracticeController.cs:62-66`, `208-219` — settings apply a már kiértékelt session jelzőjét is visszaállítja.

**Architekturális ok:** nincs teljes, változatlan `PracticeSession`/sessionkontextus, amely együtt birtokolná a küldött szöveget, az alkalmazott beállításokat és az értékelési/mentési állapotot. A globális konfiguráció egyszerre szolgál következő-session preferenciaként és múltbeli sessionadatként.

**Következmény:** Settings vagy Import után egy korábbi session más küszöbbel értékelhető, és olyan zaj/QSB-beállítás kerülhet a statisztikába, amelyet nem használt a lejátszás. A settings apply az egyszeri auto-adjust védőjelzőjét is törli; ugyanazon session újranyitása ismét az adjusterhez kerülhet.

**Ellenőrzés:** ugyanazt az `ABCDE` → `ABCDF` sessiont 5%-os küszöbbel sikertelennek, majd settings apply után 30%-kal sikeresnek értékelte a controller. A lejátszáskor zajmentes sessionhez az utólagos snapshot már `Pink` zajt mutatott.

**Javítási irány:** sessionindításkor teljes immutable kontextus készüljön: azonosító, időpontok, tényleges WPM, audio/zaj/QSB, értékelési küszöb és gyakorlási mód. A pontozás, az adaptáció és a mentés ebből dolgozzon; a konfigurációváltás kizárólag a következő sessionre hasson. Az egyszeri kiértékelés/adaptáció állapota sessionazonosítóhoz kötődjön.

**Szükséges regressziós teszt:** beállításmódosítás és import futó, befejezett, valamint már egyszer kiértékelt session mellett; a régi session adatai és az adaptáció darabszáma ne változzanak.

### A05 — A tartós mentés életciklusát egy eldobható dialógus birtokolja

**Súlyosság:** közepes.

**Kódhelyek:**

- `src/ViewModels/PracticeResultWindowViewModel.cs:119-143` — a mentés és a sikerjelző a dialógus VM-jében él.
- `src/Presentation/PracticeResultWindowService.cs:56-58` — csak az ablak bezárását várja, majd egyszer kiolvassa az `IsSaveCompleted` értékét.
- `src/Views/PracticeResultWindow.axaml.cs:5-10` — nincs bezárásvédelem vagy függő mentéshez kapcsolt lifecycle-kezelés.
- `src/ViewModels/PracticeViewModel.cs:173-184` — a session mentettsége csak a visszaadott booleanből frissül.
- `src/Stores/PracticeResultStatisticsStore.cs:305-348` — új sor beszúrása, sessionazonosítóhoz kapcsolt idempotencia nélkül.

**Architekturális ok:** a mentés tartós sessionművelet, de az állapotának tulajdonosa rövidebb életű, mint a művelet. Az ablakbezárás és a mentésbefejezés két külön esemény; a `Task<bool>` dialóguseredmény ezt nem modellezi.

**Konkrét forgatókönyv:** Save → lassú DB-művelet közben X → a dialógus `false`-t ad vissza → a háttérben befejeződik a mentés → ugyanaz a session újra megnyitva ismét menthető. Duplikált statisztikai és confusion rekordok keletkezhetnek.

**Ellenőrzés:** késleltetett `SaveAsync` mellett a bezáráskor kiolvasott érték `false`; befejezés után a régi VM-ben `true`, az új VM-ben viszont a mentés továbbra is engedélyezett. Ez izolált állapotsorrend-próba; a valódi ablak bezárását nem automatizáltam.

**Javítási irány:** a mentés állapotát a session vagy alkalmazásszolgáltatás birtokolja. Az ablakbezárás várja meg a mentést, vagy a mentés eredménye az ablaktól függetlenül frissítse a sessiont. A store-ba kerüljön egyedi sessionazonosító és idempotens mentés.

**Tesztlefedettség:** a meglévő VM-tesztek a befejezett mentést/`alreadySaved` esetet vizsgálják, nem a bezárás közben függő műveletet. Késleltetett mentés + bezárás + újranyitás teszt szükséges.

### A06 — A session időtartama szövegmennyiség-becslés, nem időbeli invariáns

**Súlyosság:** közepes.

**Kódhelyek:**

- `src/Services/PracticeController.cs:16`, `144-147`, `154-162` — becsült csoportszám és teljes szöveglejátszás, deadline nélkül.
- `src/ViewModels/PracticeViewModel.cs:229-243` — eltelt idő kijelzése, nem visszaszámlálás és nem sessionleállítás.
- `src/Players/MorseSignalRenderer.cs:94-147` — a valódi hossz a karakterek Morse-elemeitől függ.

**Architekturális ok:** az időzített session időkorlátjának nincs üzleti rétegbeli tulajdonosa. A generátor becslése és a UI kijelzője együtt sem kényszeríti ki a „konfigurált idő lejártakor véget ér” szerződést.

**Ellenőrzés:** 1 perc, 18/18 WPM, 13 generált csoport, 8000 Hz mellett — a `vvv =` bevezetővel és `<ar>` lezárással együtt — az `E`-készlethez **25,911 s**, a `0`-készlethez **104,301 s** hosszú hang készült. Nem natív lejátszási mérés, hanem a valódi renderer által előállított mintaszám.

**Javítási irány:** a controller/sessionmodell kezelje a monotón időalapú határidőt. Az adás folyamatosan generált csoportokkal vagy időkeretbe illesztett szöveggel dolgozzon; egyértelmű szabály legyen az éppen küldött jel/csoport befejezésére. Az értékeléshez csak a ténylegesen elküldött tartalom maradjon. A UI ebből kapjon hátralévő időt. A custom text mód továbbra is külön, nem időkorlátos mód maradjon.

**Szükséges regressziós teszt:** injektált időforrással rövid/hosszú Morse-karakterek, Farnsworth-beállítások és custom text; a random mód időhatára ne függjön a karakterkészlettől.

### A07 — Hiányzik az egységes Morse-szimbólummodell

**Súlyosság:** közepes.

**Kódhelyek:**

- `src/Services/PracticeResultEvaluator.cs:19`, `27-39` — a nevező `string.Length`, a számláló tokenalapú edit distance.
- `src/Services/LevenshteinAlignment.cs:20-25`, `130-155` — a `<...>` prosign egy szimbólum.
- `src/Services/MorseGenerator.cs:18-28`, `38-44` — a prosign egy elem az ötelemű csoportban.
- `src/Players/MorseSignalRenderer.cs:99-105` — az adás szintén egy tokenként kezeli.

**Architekturális ok:** a domain szimbólumait formázott string hordozza, és külön komponensek külön szabályokkal értelmezik. A pontozás egymással inkompatibilis mértékegységeket oszt el: szimbólumhibák / megjelenítési karakterek.

**Ellenőrzés:** `<bk><bk><bk><bk><bk>` küldése és üres vétel esetén `CharacterCount=20`, `ErrorCount=5`, `ErrorRatePercent=25%`. Öt teljesen hiányzó szimbólumhoz **100%** tartozna. Ez a sikerességre, az auto-adjustra és a mentett analyticsadatokra is kihat.

**Javítási irány:** közös tokenizálás vagy típusos Morse-szimbólum-/csoportmodell a generálás, adás, pontozás és diff számára. A karakterdarabszám és a távolság ugyanazon szimbólumegységgel számoljon; a `<bk>` csak szöveges reprezentáció legyen.

**Tesztlefedettség:** `tests/PentaGrammata.Tests/Services/PracticeResultEvaluatorTests.cs:50-57`, `88-95` ellenőrzi a prosign egyhibás diffjét, de a nevezőt és a hibaszázalékot nem. Prosign-only és vegyes csoportokra teljes pontozási teszt szükséges.

### A08 — A megszakítási szerződés megszakad a renderelési határon

**Súlyosság:** közepes.

**Kódhelyek:**

- `src/Players/MorsePlayer.cs:15-23`, `32-40` — a token a `Task.Run` ütemezéséhez jut el, a renderelési törzshöz nem.
- `src/Players/MorseSignalRenderer.cs:24-27`, `94-149`, `187-190`, `210-214`, `255-267` — teljes bufferek és hosszú ciklusok cancellation nélkül.
- `src/Services/PracticeController.cs:177-182` — a Stop kizárólag tokent jelez.

**Architekturális ok:** az `IMorsePlayer` megszakítható API-t kínál, de az alatta lévő CPU-/memóriaigényes feldolgozás nem része ennek a szerződésnek. A teljes üzenet előállítása megelőzi a lejátszást, több teljes méretű köztes pufferrel.

**Következmény:** hosszú custom text vagy zajos, lassú session renderelése alatt Stop után is végigmegy a munka és a memóriaallokáció. A `Task.Run(..., token)` a már futó delegate-et nem szakítja meg.

**Ellenőrzés:** helyettesített zajgenerátorral kontrolláltan megállított renderelés közben cancellation után a playback task nem fejeződött be; csak a renderer elengedése után jelent meg az `OperationCanceledException`.

**Javítási irány:** a renderer és DSP-ciklusok kapjanak tokent és rendszeres ellenőrzést; nagy üzeneteknél megfontolandó a blokkonkénti renderelés/lejátszás korlátozott pufferrel. A streamingre váltásnál a DSP- és QSB-állapotot meg kell őrizni a blokkok között.

**Tesztlefedettség:** `tests/PentaGrammata.Tests/Players/MorsePlayerTests.cs:274-302` a lejátszás utáni és indulás előtti cancellationt vizsgálja. Renderelés közbeni megszakítási teszt is szükséges.

### A09 — A natív audioadapter megsérti a felső réteg hiba-/befejezési szerződését

**Súlyosság:** közepes.

**Kódhelyek:**

- `src/Players/LinuxAudioPlayer.cs:66-69`, `83-100` — open/write hibák után normál visszatérés, általános exception-elnyelés; a drain eredménye sincs ellenőrizve.
- `src/Players/WindowsAudioPlayer.cs:92-95`, `110-130` — open hiba után normál visszatérés; prepare/write eredmények ellenőrizetlenek; kivételek elnyelve.
- `src/ViewModels/PracticeViewModel.cs:103-105`, `120-124` — a sikeresen visszatért task „Practice completed!” állapotot jelent.

**Architekturális ok:** a platformadapter a natív hibát debugüzenetté alakítja ahelyett, hogy a domain által megfigyelhető hibává fordítaná. Az `IAudioPlayer` sikeres befejezése így nem garantálja a sikeres adást. A Windows várakozás ráadásul ellenőrizetlen `waveOutWrite` után indul.

**Konkrét forgatókönyv:** PulseAudio-stream megnyitása vagy `waveOutOpen` sikertelen; a felső réteg hibajelzés nélkül befejezett gyakorlást mutat. Windows write/prepare hiba esetén olyan completion eseményre várhat, amely az el nem indult adáshoz nem érkezik meg.

**Javítási irány:** minden releváns natív return code ellenőrzése; egységes, megfigyelhető audiohiba a réteghatáron; cancellation továbbengedése; garantált erőforrás-felszabadítás. Az adapter ne nyeljen el minden exceptiont. A natív hívások helyettesíthető vékony adaptere lehetővé tenné az eszköz nélküli hibaszimulációt.

**Ellenőrzési korlát:** ezek a konkrét kódbeli elágazások igazoltak; eszközhiba és Windows-eseményvárakozás futtatásával nem reprodukáltam őket.

### A10 — A CSV-exportból hiányzik az aszinkron művelet felelőse

**Súlyosság:** közepes.

**Kódhelyek:**

- `src/ViewModels/TrendsDialogViewModel.cs:36`, `99-108` — `Action<string>` esemény, szinkron `RelayCommand`.
- `src/Views/TrendsDialog.axaml.cs:43-69` — `async void` callback; picker, streamnyitás és írás helyi hibakezelés nélkül.

**Architekturális ok:** a VM és a nézet közötti szerződés nem viszi át a valódi exportművelet taskját. A parancs a fájlművelet előtt befejezettnek látszik; nincs egy tulajdonos, amely az elfoglaltságot, befejezést, hibát és ablakbezárást együtt kezelné.

**Következmény:** jogosultság-, megtelt lemez- vagy storage-provider hiba az `async void` UI-callbackből kezeletlenül jut ki. A VM nem tudja visszajelezni a hibát vagy a művelet idejére letiltani az ismételt exportot.

**Javítási irány:** Task-alapú prezentációs export-/fájldialógus-szolgáltatás és `IAsyncRelayCommand`, megőrzött `CanExecute` feltételekkel. A CSV-formázás maradjon az exporterben, a picker a prezentációs rétegben; az orchesztráló hívó kezelje a művelet végét és a felhasználói hibajelzést.

**Szükséges regressziós teszt:** picker cancellation, sikertelen streamnyitás/írás, folyamatban lévő export és ablakbezárás. Az `async void` eseménykezelő önmagában nem hiba; itt az elveszett műveleti szerződés és az elmaradt hibakezelés a probléma.

### A11 — A frissítésellenőrzés eldobja a release-azonosság negyedik komponensét

**Súlyosság:** közepes.

**Kódhelyek:**

- `src/Services/GitHubUpdateChecker.cs:70`, `93-99` — a verzió-összehasonlítás kizárólag három komponensre normalizál.
- `AGENTS.md:105-118` — a release-azonosság négykomponensű; a build komponens önálló új kiadást azonosít.
- `scripts/Build-Deb-Installer.sh:110-112`, `scripts/Build-Installer.ps1:107-109` — a teljes verzió assembly-/fileversionként is bekerül.

**Architekturális ok:** a frissítő saját verziópolitikát definiál, amely ellentmond a csomagolás és a kiadási folyamat közös szerződésének. A negyedik komponens nem jelentéktelen technikai zaj ebben a projektben.

**Ellenőrzés:** stub HTTP-válasszal az aktuális `1.0.0.0` assemblyhez `1.0.0.1` érkezett; `Succeeded=True`, `UpdateAvailable=False`. Ugyanez a probléma a valódi `1.11.4.4` → `1.11.4.5` kiadási mintára is fennáll.

**Javítási irány:** a teljes négykomponensű verzió összehasonlítása, hiányzó komponensek nullával való pótlásával. A release-azonosság definíciója legyen egységes a frissítőben és a csomagolásban.

**Szükséges regressziós teszt:** csak a negyedik komponensben újabb/régebbi verzió, valamint három- és négykomponensű azonos verzió.

**Elkülönített, nem hibaként számolt döntés:** a frissítő `/releases/latest` stable-only végpontot használ, miközben az automatikus workflow prerelease-t készít (`.github/workflows/create-installer.yml:158-163`). Ezek a kiadások ezért nem látszanak ebben a csatornában, de a stable-only viselkedés az `IUpdateChecker` dokumentált szerződése is. A csatornapolitikát tisztázni érdemes; ezt önmagában nem minősítem hibának.

### A12 — A prezentációs adapterek nem maradnak a prezentációs határon

**Súlyosság:** alacsony; szerkezeti, nem bizonyított futásidejű hiba.

**Kódhelyek:**

- `src/Interfaces/IWindowContext.cs:1-13` és `src/Interfaces/IWindowSizeService.cs:1-16` — közös interfészekben Avalonia `Window`.
- `src/Services/WindowContext.cs:1-24` — `Application.Current` és desktop lifetime használata a `Services` könyvtárban.
- `src/Services/WindowSizeService.cs:3-41` — Avalonia-ablakhoz és Closing eseményhez kötött adapter ugyanott.
- `src/ViewModels/MainWindowViewModel.cs:4`, `58-59`, `94`, `132`, `211` — közvetlen `FontFamily` tárolás és példányosítás.

**Architekturális ok:** az üzleti `Services`/közös `Interfaces` felület és a prezentációs adapterek határa nem következetes. A projekt külön `Presentation` réteget és UI-típusokat kívül tartó convertereket ír elő, de az ablakkezelés és a betűtípus-konverzió részben megkerüli ezt.

**Konkrét szerkezeti következmény:** a szolgáltatás-/interfészréteg jelen formában nem választható le Avalonia nélkül; az ablakadapterek teszteléséhez toolkitobjektum kell. A főablak VM-jének konfigurációs állapota szintén toolkitfüggő. Nem állítom, hogy az egyetlen assembly önmagában hiba vagy hogy az adapterek üzleti algoritmusokat rontanak el.

**Javítási irány:** `WindowContext`, `WindowSizeService` és UI-specifikus interfészeik kerüljenek a `Presentation` rétegbe. A VM betűtípusnévvel dolgozzon, `FontFamily`-t converter állítson elő. Könnyű architekturális teszt őrizze a megengedett namespace-függőségeket; ehhez nem szükséges azonnal több projektbe szétbontani az alkalmazást.

### A13 — A headroom-elemző saját mini composition rootot épít

**Súlyosság:** alacsony; szerkezeti.

**Kódhelyek:**

- `src/Services/AudioHeadroomAnalyzer.cs:51-57` — közvetlen `new NoiseGeneratorFactory(...)` és `new MorseSignalRenderer(...)`.
- `src/Players/MorsePlayer.cs:10-13` — a lejátszás az injektált `INoiseGeneratorFactory` példányt használja.
- `src/Composition/ServiceCollectionExtensions.cs:31-32`, `58` — külön DI-regisztráció a lejátszáshoz és az elemzőhöz, közös renderelési factory nélkül.

**Architekturális ok:** az elemző ugyanazt a rendererosztályt használja, ami helyes újrahasznosítás, de a renderelő függőségeit maga építi fel a központi DI helyett. Emiatt a lejátszás injektált zajgenerátor-szerződése nem vonatkozik az elemzésre.

**Konkrét szerkezeti következmény:** az `INoiseGeneratorFactory` DI-beli cseréje vagy dekorálása a lejátszásra hat, a clipping/headroom figyelmeztetésre nem. Az elemzőbe nem injektálható kontrollált renderer/factory a saját munkafolyamatának teszteléséhez. A jelenlegi alapimplementációk eltérését nem állítom: a probléma az, hogy a két út külön compositionpolitikával működik.

**Javítási irány:** közös rendererfactory vagy injektált renderelési absztrakció, explicit determinisztikus seed/elemzési mód támogatásával. A fix seed maradjon meg: szükséges ahhoz, hogy a figyelmeztetés szerkesztés közben ne villogjon. Nem célszerű egyszerűen egy közös, változó Random-állapotú singleton rendererre váltani.

**Szükséges regressziós teszt:** a közös factory/renderer helyettesítése mind a lejátszást, mind az elemzést érintse; az elemzés ugyanarra a beállításra determinisztikus maradjon.

## Javasolt javítási sorrend

1. **Adatbiztonság:** A01–A03 — importjelölt teljes ellenőrzése, eredeti adatok megőrzése, visszagörgethető commit és csak utána éles állapotváltás.
2. **Session mint üzleti egység:** A04–A06 — immutable kontextus, egyedi azonosító, egyszeri adaptáció, ablaktól független/idempotens mentés, valódi időhatár.
3. **Közös szerződések:** A07 és A11 — szimbólumalapú pontozás, teljes releaseverzió.
4. **Aszinkron és platformhatárok:** A08–A10 — végigvezetett cancellation, megfigyelhető audiohibák, awaitelhető export.
5. **Rétegfegyelem:** A12–A13 — prezentációs függőségek helyretétele, közös renderelési függőségek, architekturális tesztek.

## Amit nem minősítettem hibának

- A VM-ek és store-ok közötti vékony service facade a projekt tudatos konvenciója; önmagában nem fölösleges réteg.
- Az egyetlen alkalmazásprojekt és a mappák szerinti rétegzés önmagában nem architekturális hiba.
- A dialógus-VM factory önmagában nem service-locator antipattern; nem találtam bizonyítékot a korábban javított körfüggőség fennmaradására.
- A macOS audio placeholder dokumentált platformkorlát, nem új architekturális találat.
- A headroom-elemzéshez a valódi DSP-lánc használata helyes; A13 a függőségek külön felépítését kifogásolja, nem az algoritmus újrahasznosítását.
- A sikeres 280 teszt nem cáfolja a találatokat: a célzott izolált próbák több olyan állapotsorrendet és hibás bemenetet igazoltak, amelyet a meglévő tesztek nem fednek le.
