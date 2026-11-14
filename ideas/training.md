# Training

Most egy nagyobb fejlesztésre kerül sor, készüljön új branch: features/2026-11-14-topics

Jelenleg a pentagrammata csak random 5-betűs csoportokat gyakorol, ezt kellene kiegészíteni úgy, hogy tudjon:
* random 5-betűk,
* hívójeleket (callsign) EMEA,
* hívójeleket (callsign) global,
* városokat (city, towk mittudomén),
* neveket (name)
* rádióamatőr rövidítéseket, Q-kódokat,
* saját szöveget,
* és ezeket vegyesen
generálni.

Tehát azt kellene, hogy a UI-on legyen külön egy lenyíló menü, ahol kiválasztom a gyakorlás témakörét (random 5-betűs csoportok, hívójelek...), hasonlóan, mint ahogy most a character set van. Értelemszerűen, a character set csak a 5-betűs csoportoknál van engedélyezve.

A háttérben legyen egy factory, ami a random generátort adja vissza az adott témakörek és a paramétereknek megfelelően.

A settings ablakban a PRACTICE fül után legyen egy topics (témakörök) vagy ilyesmi fül, ott lehessen beállítani:
1. hívójelek (EMEA) esetén az országok patternjét, pl. HA#xyz, HG#xyz, DL#xy, DL#xyz stb. (# legyen a szám helyettesítő, bármelyik kisbetű a random betűt helyettesít, a nagybetű és a szám a fix szöveg). Itt legyenek benne az EMEA régió országai.
2. hívójelek (global) esetén ugyanez.
3. Városok: itt legyenek felsorolva az EMEA régió nagyobb városai (pl. országonként 20, vagy ahogy látod, rád bízom), illetve globálisan a fővárosok.
4. nevek: itt legyenek az európában népszerű keresztnevek, illetve a rádióamatőrök köreiben népszerűbb becenevek. Becézett formák is.
5. Rádióamatőr rövidítések, Q-kódok... nem kell magyaráznom.
6. A régi practice tabról ide kerüljön át. Legyen egy checkbox, hogy sequenciálisan vagy ransomizálva a szavakat a szövegben.

A characters set kerüljön az új topics (vagy tudomisén) után és az AUDIO elé.

Elméletileg a gyakorlás ellenőrzése nem változik jelentősen, sem az értékelés módja, sem a megjelenítésé, ezeket a részeket talán nem is kell bántani.

Mondanom sem kell, az új settingeket használnia kell a PentaGrammaténak és mentenie is kell tudni a settings.prmt-be.

Természetesen, tesztekkel alaposan legyen lefedve és headless tesztekkel is.
