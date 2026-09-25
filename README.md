# Padel Booking

Web aplikacija za rezervaciju padel terena.

## Technologies

- React
- ASP.NET Core
- MySQL

## Lokalna konfiguracija

Kopirajte `.env.example` u `.env` i unesite lokalne DB/JWT vrednosti i Stripe test
credentials. `.env` je ignorisan u Git-u i koristi se u oba razvojna režima.

## Lokalni app režim

```powershell
.\scripts\dev-local.ps1
```

- frontend: `http://localhost:5173`
- backend: `http://localhost:5238`
- MySQL ostaje u Docker kontejneru na `localhost:3307`

Vite prosleđuje relativne `/api` i `/hubs` zahteve lokalnom backendu, uključujući
SignalR WebSocket saobraćaj.

## Full Docker režim

```powershell
docker compose up --build
```

Oba režima koriste isti `mysql` servis, bazu i named volume `padel_mysql_data`, pa
korisnici, rezervacije i plaćanja ostaju isti pri promeni načina pokretanja.
