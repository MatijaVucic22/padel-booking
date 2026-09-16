# Playwright E2E

Pokreni `npm.cmd --prefix frontend run test:e2e:isolated` iz korena projekta. Potrebni su Docker, Node.js i slobodni lokalni portovi 5189 i 5248.

Skripta pokreće zaseban MySQL 8.4 i pravi backend sa postojećim migracijama, dodaje jedan aktivan teren samo u E2E bazu, pokreće Vite/Chromium i izvršava postojeća tri testa. Zatim gasi samo svoj E2E Compose projekat i briše njegov volume, čak i kada test ne uspe. Razvojna baza i obični `docker-compose.yml` se ne koriste.

Playwright Chromium se instalira automatski pri prvom pokretanju. Testovi stvarno kreiraju dva jedinstvena test korisnika, ali ne prave rezervacije niti otvaraju Stripe Checkout.
