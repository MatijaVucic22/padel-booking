# Stripe Checkout (samo test režim)

API prihvata samo Stripe `sk_test_` ključ. Kartične podatke unosiš isključivo na Stripe-hosted Checkout stranici; aplikacija ih ne prima niti čuva.

Za lokalni razvoj u API projektu postavi .NET User Secrets (primeri su placeholderi, ne stvarni ključevi):

```powershell
dotnet user-secrets set STRIPE_SECRET_KEY "<Stripe test secret key>" --project backend/src/PadelBooking.Api/PadelBooking.Api.csproj
dotnet user-secrets set STRIPE_WEBHOOK_SECRET "<Stripe CLI webhook signing secret>" --project backend/src/PadelBooking.Api/PadelBooking.Api.csproj
dotnet user-secrets set STRIPE_FRONTEND_URL "http://localhost:5173" --project backend/src/PadelBooking.Api/PadelBooking.Api.csproj
```

U Docker okruženju prosledi iste tri vrednosti kao environment varijable backend kontejneru, bez upisivanja tajni u Git. Ako je potrebno, restartuj backend nakon promene konfiguracije.

Pokreni Stripe CLI forwarding na API URL, na primer:

```powershell
stripe listen --forward-to http://localhost:5238/api/payments/webhook
```

Potpisni `whsec_` iz tog CLI procesa koristi za `STRIPE_WEBHOOK_SECRET`. Otvori `/book`, izaberi slobodan termin, klikni „Nastavi na plaćanje“ i završi plaćanje isključivo [Stripe test karticom](https://docs.stripe.com/testing). Na `/payment/success` proverava se stanje iz baze; samo povratak iz Stripe-a ne potvrđuje rezervaciju. Ne koristi stvarne kartice.

Neuspeo ili istekao Checkout oslobađa privremeno zadržan termin kada stigne Stripe događaj ili kada ga pozadinska provera potvrdi. Otvaranje `/payment/cancel` samo po sebi ne potvrđuje niti naplaćuje rezervaciju.
