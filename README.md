# Padel Booking

Web aplikacija za rezervaciju padel terena.

## Technologies

- React
- ASP.NET Core
- MySQL

## Lokalna konfiguracija

Kopirajte `.env.example` u `.env` i zamenite placeholder vrednosti lokalnim DB/JWT
vrednostima i Stripe test credentials. `.env` je ignorisan u Git-u i koriste ga sva
tri razvojna režima. Jedina razvojna baza je lokalni MySQL 8.4 na `localhost:3306`.

## Full local / bez Dockera

Preduslovi su .NET 10 SDK, Node.js/npm i lokalno instaliran MySQL 8.4 servis.

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev-no-docker.ps1
```

- frontend: `http://localhost:5173`
- backend: `http://localhost:5238`
- MySQL: `localhost:3306`

Ovaj režim direktno koristi zajedničku lokalnu MySQL bazu `padel_booking`.

Za prvo pokretanje ručno napravite bazu i MySQL korisnika iz `.env`, ako već ne postoje,
i dodelite korisniku dozvole nad tom bazom. Koristite sopstvenu administratorsku sesiju
i stvarnu lozinku samo lokalno:

```sql
CREATE DATABASE IF NOT EXISTS padel_booking;
CREATE USER IF NOT EXISTS 'padel_user'@'localhost' IDENTIFIED BY '<LOCAL_PASSWORD>';
ALTER USER 'padel_user'@'localhost' IDENTIFIED BY '<LOCAL_PASSWORD>';
GRANT ALL PRIVILEGES ON padel_booking.* TO 'padel_user'@'localhost';
```

API pri pokretanju primenjuje postojeće EF Core migracije i ne briše postojeće podatke.

## Lokalna aplikacija

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev-local.ps1
```

- frontend: `http://localhost:5173`
- backend: `http://localhost:5238`
- MySQL: `localhost:3306`

Ova komanda je kompatibilan alias za `dev-no-docker.ps1`; ne pokreće Docker.

## Mobilno testiranje preko HTTPS tunela

Preduslov je instaliran `cloudflared`. Pokretanje je jednom komandom:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev-mobile.ps1
```

Skripta kreira privremeni Cloudflare Quick Tunnel, sačeka lokalni API, Vite i javni
HTTPS URL, pa ispiše adresu koju treba otvoriti na telefonu. Samo za taj proces postavlja
tačan dodatni CORS origin i Stripe povratni URL; root `.env` se ne menja. `Ctrl+C`
zaustavlja tunel, frontend i backend.

## Full Docker

```powershell
docker compose up --build
```

Frontend i backend rade u kontejnerima, ali backend koristi isti Windows MySQL preko
`host.docker.internal:3306`. Zato sva tri režima prikazuju iste korisnike, terene,
rezervacije, plaćanja i ostale podatke iz baze `padel_booking`.

Stari Docker MySQL i named volume `padel_mysql_data` nisu obrisani. Servis je dostupan
samo kroz eksplicitni Compose profil `legacy-db` radi provere ili jednokratne migracije.
Normalni `docker compose up --build` ga ne pokreće.

Za bezbedan izvoz stare Docker baze prvo pokrenite:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\migrate-docker-db-to-local.ps1
```

Skripta čuva izvoz u ignorisanom `.local-backups` direktorijumu. Ako lokalna baza već
sadrži tabele, pravi i njen backup pa odbija automatski import; podatke tada treba ručno
spojiti nakon pregleda. Samo za potvrđeno praznu lokalnu bazu import se eksplicitno
odobrava parametrom `-ImportIntoEmptyLocalDatabase`. Skripta nikada ne briše bazu ili
Docker volume.

U oba lokalna frontend režima Vite prosleđuje relativne `/api`, `/hubs` i `/uploads`
zahteve API-ju na `http://localhost:5238`, uključujući SignalR WebSocket saobraćaj.
