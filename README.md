# GR_17_Programimi_me_Sockets
# Protokolli TCP per manipulimin e files ne C#

Ky projekt është një aplikacion TCP i ndarë në dy pjesë: **Serveri** dhe **Klienti**.  
Serveri mund të pranojë deri në 4 klient njëkohësisht nga pajisje të ndryshme në një rrjetë reale dhe ofron komandat për menaxhimin e files (listim, lexim, upload, download, fshirje, kërkim, info, statistika etj.). Në qoftë se më shumë se 4 pajisje kërkojnë qasje atëherë lidhjet e reja refuzohen derisa të lirohen resurset.
Klientët mund të lidhen si **admin** ose **read-only**, ku admin ka të gjitha privilegjet, kurse useri mund vetëm të listojë dhe lexojë. Klientët me privilegje të plota kanë kohë përgjigjeje më të shpejtë se klientët e tjerë që
kanë vetëm read permission. 

## Funksionaliteti kryesor

### Serveri
- Dëgjon klientët në portin `9000` (mund të ndryshohet).
- Mbështet maksimum 4 klientë aktivë në të njëjtën kohë.
- Menaxhon:
  - `/list` – liston të gjitha files në `ServerStorage`
  - `/read <filename>` – lexon përmbajtjen e një file
  - `/upload <filename> <base64>` – ngarkon një file nga klienti
  - `/download <filename>` – dërgon file në formë base64 (vetëm admin)
  - `/delete <filename>` – fshin një file (vetëm admin)
  - `/search <keyword>` – kërkon file që përmbajnë fjalën kyç (vetëm admin)
  - `/info <filename>` – jep informacion mbi file (vetëm admin)
  - `/STATS` – tregon statistikat e serverit (vetëm admin)
- Timeout idle: 500 sekonda
- Statistikat e trafikut (bytes dërguar/marrë) shfaqen në kohë reale në console.

### Client
- Lidhet me serverin duke përdorur IP dhe port.
- Identifikohet me `username` dhe `role` (admin ose readonly).
- Menu interaktive për të ekzekutuar komandat e sipërpërmendura.\
- Në varësi të numrit të zgjedhur përcaktohet komanda që do të ekzekutohet, p.sh. numri 1 i korrespondon funksionit list etj.

---

## Struktura e projektit
/ServerStorage - ruhen files pas upload ose për shfaqjen e stats me gjenerimin e server_stats
/Serveri/Program.cs - kodi i serverit
/Klienti/Program.cs - kodi i klientit

## Ekzekutimi
Për të ekzekutuar kodin së pari duhet të hyjmë në direktorumin e serverit:
cd Serveri

Bëjmë run Serverin:
dotnet run

Pastaj hyjmë në direktoriumin e klientit:
cd Klienti

Dhe bëjmë run klientit, me komandën si më poshtë duke marrë parasysh se ip adresa duhet të jetë e pajisjes aktuale, kurse usernamin dhe rolin e caktojmë sipas dëshirës:
dotnet run -- 127.0.0.1 9000 username admin/user



