# GR_17_Programimi_me_Sockets
# Protokolli TCP per manipulimin e files ne C#

Ky projekt është një aplikacion TCP i ndarë në dy pjesë: **Serveri** dhe **Klienti**.  
Serveri mund të pranojë deri në 4 klient njëkohësisht nga pajisje të ndryshme në një rrjetë reale dhe ofron komandat për menaxhimin e files (listim, lexim, upload, download, fshirje, kërkim, info, statistika etj.). Në qoftë se më shumë se 4 pajisje kërkojnë qasje atëherë lidhjet e reja refuzohen derisa të lirohen resurset.
Klientët mund të lidhen si **admin** ose **read-only**, ku admin ka të gjitha privilegjet, kurse useri mund vetëm të listojë dhe lexojë. Klientët me privilegje të plota kanë kohë përgjigjeje më të shpejtë se klientët e tjerë që
kanë vetëm read permission. 

