# PadelBooking benchmarki

Pre pokretanja postavite connection string samo za trenutni PowerShell proces. Ne upisujte stvarne kredencijale u fajlove:

```powershell
$env:PADELBOOKING_BENCHMARK_CONNECTION_STRING = "Server=localhost;Port=3306;Database=padel_booking;User=YOUR_USER;Password=YOUR_PASSWORD"
```

Iz korena projekta pokrenite oba testa:

```powershell
.\benchmarks\run-benchmarks.ps1
```

- **Test 1** meri citanje aktivnih terena kroz EF Core tracked, EF Core `AsNoTracking` i Dapper.
- **Test 2** meri direktno EF Core/MySQL citanje, HybridCache cold miss i HybridCache warm hit.

Posle uspesnog pokretanja `benchmarks/benchmark-summary.html` sadrzi najnoviji izvestaj i automatski se otvara u browseru. Istorijski izvestaji su u `benchmarks/history/`; zadrzava se samo poslednjih 10. Detaljni BenchmarkDotNet HTML/CSV/Markdown fajlovi ostaju u `benchmarks/PadelBooking.Benchmarks/BenchmarkDotNet.Artifacts/results`.
