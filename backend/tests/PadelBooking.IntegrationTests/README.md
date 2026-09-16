# Backend integration testovi

Testovi proveravaju registraciju i JWT, konkurentno zauzimanje termina, blokirane periode, plaćanje i pravila rezervacija kroz stvarni HTTP API. Koriste pravi MySQL jer EF InMemory ne može pouzdano da proveri MySQL advisory lock, migracije i konkurentne upise.

Testcontainers automatski pokreće izolovani `mysql:8.4` kontejner sa jednokratnom bazom i nasumičnom lozinkom. Testovi ne koriste razvojnu bazu, Stripe niti SMTP: za spoljne pozive koriste se test implementacije postojećih interfejsa. Potreban je pokrenut Docker.

Iz korena projekta:

```powershell
dotnet test backend/PadelBooking.sln -c Release
```
