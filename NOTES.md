# Note di come funziona AOE

Tutti gli edifici sono 3x3 tranne: muro 1x1 e casa e torre di guardia 2x2
Tutti gli edifici hanno 420HP tranne: casa 90, town center 720, torre di guardia 240, muro 480
Tutte le unità hanno visione pari al loro range di attacco
Statistiche: HP, attacco, armor, armor PEN, range. mostrare solo se != 0
Il porto sta dentro l'acqua ma attaccato al terreno

AttackSpeed e MovementSpeed: non ci sono dati visibili su AOE, quindi farò delle stime. I valori indicati per questi due campi andranno poi moltiplicati per un coefficiente di AS e uno di MS. Quindi i valori indicheranno solo il livello (1=lento, ~5= veloce)

| BUILDING       | UNIT            | HP  | DMG  | ARMOR | ARMOR PEN | RANGE | AS | MS |
|----------------|-----------------|-----|------|-------|-----------|-------|----|----|
| City center    | Villager        | 65  | 4    | 0     | 0         | 4     | 2  | 2  |
| Barracks       | Legion          | 160 | 20   | 8     | 2         | 0     | 4  | 2  |
| Barracks       | Centurion       | 160 | 38   | 14    | 2         | 0     | 3  | 2  |
| Archery range  | Bowman          | 45  | 6    | 6     | 0         | 10    | 4  | 2  |
| Archery range  | Horse archer    | 103 | 9    | 6     | 2         | 10    | 5  | 3  |
| Archery range  | Elephant archer | 600 | 6    | 6     | 0         | 10    | 4  | 2  |
| Stable         | Scout           | 68  | 20   | 6     | 0         | 0     | 1  | 5  |
| Stable         | Cataphract      | 206 | 19   | 9     | 1         | 0     | 3  | 4  |
| Stable         | War elephant    | 600 | 15   | 6     | 0         | 0     | 1  | 2  |
| Temple         | Priest          | 50  | 4 HS | 0     | 0         | 13    | 1  | 2  |
| Siege workshop | Catapult        | 150 | 61   | 0     | 0         | 15    | 1  | 1  |
| Siege workshop | Ballista        | 55  | 41   | 0     | 0         | 12    | 2  | 2  |
| Dock           | Fishing         | 75  | 0    | 0     | 0         | 0     | 0  | 3  |
| Dock           | Merchant        | 250 | 0    | 0     | 0         | 0     | 0  | 4  |
| Dock           | Transport       | 200 | 0    | 0     | 0         | 0     | 0  | ~  |
| Dock           | Trireme         | 200 | 13   | 0     | 0         | 10    | 3  | 3  |
| Dock           | Juggernaught    | 200 | 35   | 0     | 0         | 12    | 1  | 3  |
| Siege workshop | Siege tower(?)  |      Idea mia, non in AOE, non implementare      |
| Siege workshop | Trebuchet(?)    |      Idea mia, non in AOE, non implementare      |

Altri edifici:
Casa (90 HP, +4 population cap)
Granaio (immagazzina cibo)
Magazzino (immagazzina legno, oro e pietra)
Fattoria (produce 475 cibo aggiuntivo)
Torre di guardia (240 HP, 20 DMG, 10 RANGE, 4 AS)
Muro (480 HP)
Mercato (Abilita i tributi agli alleati, da definire se tenere o no)
Centro di governo (nessuna utilità dopo gli upgrade)
City center (720 HP, spawna i villager e evolve l'era)

Praticamente tutti gli edifici, tranne casa, fattoria, torre, muro e TC forniscono gli upgrade
La Meraviglia non la considero nemmeno


Risorse:
Legna (40 nelle foreste, 75 negli alberi isolati)
Cibo (150 nei bush, 300 elefanti (45 HP, 10 DMG, AS medio, spawnano in 2), 100 leoni/alligatori (20 HP, 2 DMG, AS elevato), 150 gazzelle (8HP, gruppi da 6~8), 250 pesce)
Oro (400)
Pietra (250)
Oro e pietra spawnano in gruppi da 6~8 nodidd

L'armatura riduce FLAT i danni subiti (10 DMG vs 6 ARMOR = 4 DMG)
