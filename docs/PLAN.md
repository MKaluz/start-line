# Start-Line — Initial Product Plan and Business Model

**Date:** 2026-04-03  
**Repo:** MKaluz/start-line  
**Context:** A sports event registration application (running + cycling races) with add-ons/merchandise. The goal is to compete with providers charging ~**4% commission** and to build a platform attractive to multiple event organizers.

---

## 1) Product Goal (what we sell to the organizer)

Nie tylko „formularz + płatność”, ale **system operacyjny eventu**:

- uruchomienie zapisów i sprzedaży dodatków,
- automatyzacja komunikacji i obsługi uczestników,
- narzędzia na dzień zawodów (check-in/QR),
- raportowanie, eksporty, rozliczenia (wg realnych potrzeb PL).

**Teza:** jeśli platforma zmniejsza pracę organizatora i/lub zwiększa przychód eventu, łatwiej obronić marżę i utrzymać klienta (lock‑in przez procesy, nie przez „najniższą prowizję”).

---

## 2) Założenia startowe

- **Rynek:** Polska
- **Typ eventów:** biegi, wyścigi kolarskie
- **Skala:** do ~5000 uczestników/event (z możliwością wzrostu)
- **Merch:**
  - etap 1: dodatki do zapisów (koszulka, buff, czapka) — wspólny checkout,
  - etap 2: sklep z wysyłką,
  - etap 3: print-on-demand (POD).
- **Płatności:** zewnętrzna bramka płatności (platforma, nie procesor)
- **Tech:** backend .NET, frontend React; w przyszłości aplikacja mobilna.

---

## 3) Jak wygrać z konkurencją (nie tylko ceną)

### 3.1. Wyróżniki B2B (dla organizatora)
1) **Panel operacyjny**
- listy startowe, limity, progi cenowe,
- statusy opłat, zwroty/przeniesienia,
- eksporty (CSV), integracje (w kolejnych etapach).

2) **Komunikacja**
- e-mail transakcyjny (MVP),
- segmentacja + kampanie e-mail/SMS (v1).

3) **Obsługa na miejscu**
- check-in QR (PWA na telefon) (v1),
- tryb offline + szybkie wyszukiwanie (v1/v2).

4) **Branding i kontrola**
- landing eventu, subdomena/domena,
- szablony maili, regulaminy i zgody.

### 3.2. Wyróżniki B2C (dla uczestnika)
- **Profil zawodnika** (raz wypełnione dane → szybkie zapisy),
- **rejestracja wieloosobowa** (rodzina/klub) + jeden checkout,
- upsell jako **pakiety** (Standard/Premium/VIP) zamiast „czy chcesz gadżet?”.

---

## 4) Model biznesowy (jak zarabiamy)

### 4.1. Założenie
Chcemy być atrakcyjni vs ~4% prowizji, ale nie bazować wyłącznie na prowizji.

Rekomendacja: **hybryda SaaS + mała opłata per zapis** + opcja enterprise.

### 4.2. Plany cenowe (propozycja)
**Plan A — SaaS + opłata per opłacony zapis (rekomendowany):**
- abonament (miesięczny/sezonowy) dla organizatora,
- + opłata per opłacony zapis (np. 1–3 PLN) albo niski %.

**Plan B — prowizja z limitem (cap):**
- np. 2–3% + 1 PLN, ale max X PLN od transakcji.

**Plan C — White-label / Enterprise:**
- stała opłata za event lub licencja roczna,
- płatne wdrożenie + SLA.

### 4.3. Dodatkowe źródła przychodu (później)
- moduł sklepu (wysyłka, POD),
- SMS/push (koszt + marża),
- integracje (pomiar czasu/wyniki),
- usługi: konfiguracja, migracje, support premium.

---

## 5) Plan produktu: MVP → v1 → v2

### 5.1. MVP (pierwszy klient; szybka wartość)
**Event & konkurencje**
- konfiguracja eventu, dystansów/konkurencji,
- limity, ceny, progi cenowe (early bird).

**Zapisy**
- konto uczestnika + profil,
- zapisy indywidualne.

**Płatności**
- integracja z bramką (do decyzji: Przelewy24 / PayU / Stripe),
- statusy: utworzone / oczekuje / opłacone / anulowane / zwrot.

**Dodatki (add-ons do zapisu)**
- produkty z wariantami (rozmiary),
- zakup w tym samym checkout.

**Panel organizatora**
- lista zapisów, filtry, eksport CSV,
- podstawowe akcje ręczne (korekta danych).

**E-mail transakcyjny**
- potwierdzenie zapisu i płatności,
- podstawowe szablony.

### 5.2. v1 (produkt konkurencyjny)
- kupony/rabaty, zniżki grupowe,
- rejestracja klubowa/firmowa,
- check-in QR (PWA) + wydawanie pakietów,
- polityki zwrotów i przeniesień,
- segmentacja i kampanie e-mail/SMS,
- role i uprawnienia w panelu (organizator, wolontariusz, księgowość).

### 5.3. v2 (skalowanie i przewagi platformy)
- sklep z wysyłką + POD,
- integracje z pomiarem czasu / wynikami,
- multievent dla organizatora (serie),
- API/webhooki,
- analityka lejka: konwersja, porzucone płatności, skuteczność upsellu.

---

## 6) Architektura techniczna (wstęp)

### 6.1. Backend (.NET)
- ASP.NET Core (REST API),
- moduły/domeny: Events, Registrations, Payments, MerchAddons, Users/Profiles, OrganizerPanel,
- zadania w tle (np. Hangfire / background services) do e-maili i synchronizacji statusów płatności,
- baza: PostgreSQL (rekomendacja) lub SQL Server (jeśli wygodniej na start).

### 6.2. Frontend (React)
- web dla uczestników + panel organizatora,
- PWA dla check-in (v1) — działa na telefonach, docelowo offline.

### 6.3. Mobile (przyszłość)
- React Native (spójnie z React) **lub**
- .NET MAUI (spójnie z .NET) — decyzja później.

---

## 7) Największe niewiadome do odkrycia (discovery)

Ponieważ nie masz jeszcze pełnej wiedzy o bólu organizatorów, kluczowe jest szybkie discovery:

1) Co boli najbardziej poza prowizją: obsługa na miejscu? faktury? wsparcie dla zawodników? marketing?
2) Czy potrzebne są faktury VAT/paragony B2C/B2B od pierwszej wersji?
3) Jakie wymagania dot. wypłat (częstotliwość), zwrotów i zmian dystansu?
4) Która bramka płatności jest „must” (BLIK, Apple Pay/Google Pay, przelewy) i jak wygląda proces webhooks/refundów?

---

## 8) Najbliższe kroki (praktycznie)

1) Rozmowy discovery z 2–3 organizatorami (30–45 min):
- ich proces dziś, gdzie tracą czas/pieniądze,
- must-have na dzień zawodów,
- akceptowalny model cenowy (abonament vs prowizja).

2) Ustalenie modelu rozliczeń (Plan A/B/C) + widełek cen.

3) Doprecyzowanie MVP jako user stories + makiety 3 ekranów:
- landing eventu,
- formularz zapisu,
- checkout z dodatkami.

4) Decyzja: bramka płatności + scenariusze płatności/zwrotów.

---

## 9) Otwarte pytania do dalszej iteracji planu

1) Wypłaty: po evencie czy cykliczne (np. co tydzień)?
2) Czy w MVP musi być: faktury VAT, NIP, dane firmy?
3) Czy potrzebne są kody rabatowe/afiliacje już na start?
4) Integracje z pomiarem czasu: od razu czy dopiero v2?

---

## 10) Decyzje architektoniczne backendu (sesja 2026-05-04)

Poniżej zestawienie wszystkich decyzji z sesji planowania.

### 10.1. Architektura ogólna
- **Styl:** modularny monolit (ASP.NET Core). Moduły wyraźnie oddzielone, gotowe do późniejszego wydzielenia w mikroserwisy.
- **Multi-tenancy:** single-tenant na start; `OrganizerId` opcjonalne w kluczowych encjach, żeby nie blokować przyszłej tenantyzacji.
- **API:** REST (ASP.NET Core Web API + OpenAPI/Swagger). GraphQL odkładamy na później.

### 10.2. Role użytkowników
| Rola | Zakres |
|---|---|
| Zawodnik | rejestracja, zapis, opłata, status |
| Organizator | tworzenie zawodów, limity, listy startowe |
| Obsługa/Sędzia | weryfikacja zgłoszeń, potwierdzanie obecności |
| Admin systemu | konfiguracja globalna, uprawnienia |

### 10.3. Baza danych i ORM
- **Baza:** PostgreSQL
- **ORM:** EF Core z migracjami

### 10.4. Uwierzytelnianie i autoryzacja
- Własne konto + hasło (bez social login w MVP)
- JWT access token (czas życia: 15 min)
- Refresh token rotowany i unieważniany po użyciu
- Hasła haszowane Argon2id lub BCrypt

### 10.5. Zapisy i miejsca
- Rezerwacja miejsca trwa **30 minut** od zapisu (pole `ReservationExpiresAt`, status `Reserved`)
- Po wygaśnięciu: worker zwalnia miejsce automatycznie
- Gdy brak miejsc: **lista rezerwowa** (automatyczna promocja po zwolnieniu miejsca)
- Jednoczesne zapisy na ostatnie miejsce: atomowa transakcja bazodanowa (sprawdzenie limitu + zapis w jednej transakcji)

### 10.6. Płatności
- **Mock** `MockPaymentProvider` za interfejsem `IPaymentProvider`
- Architektura gotowa na podmianę na Stripe / Przelewy24 / PayU
- Obsługiwane tryby:
  - płatność online (status: `Reserved` → `Paid`)
  - płatność późniejsza (status: `Reserved` z limitem czasu)

### 10.7. Dane zawodnika (obowiązkowe przy zapisie)
1. Imię
2. Nazwisko
3. Email
4. Data urodzenia
5. Płeć (wymagana gdy kategorie tego wymagają)
6. Klub/Miasto (opcjonalne)
7. Telefon (opcjonalny)

### 10.8. Dynamiczne pola formularza
Organizator może definiować własne pola dla zawodów. Dostępne typy:
`text`, `number`, `select`, `checkbox`, `date`

### 10.9. Dystanse i kategorie wiekowe
- Wiele dystansów w ramach jednych zawodów; każdy dystans ma własny limit i cenę
- Kategorie wiekowe liczone **na dzień zawodów** (nie dzień zapisu)
- Walidacja: wiek i płeć względem reguł kategorii sprawdzane przy zapisie

### 10.10. Trwałość danych
- **Soft delete** (`IsDeleted`, `DeletedAt`, `DeletedBy`) dla encji biznesowych: `Event`, `Race`, `Registration`, `User`
- **Stany rejestracji** (zamiast prostego usunięcia): `Active`, `Cancelled`, `Expired`, `Refunded`
- **Hard delete** tylko dla danych technicznych/tymczasowych (wygasłe tokeny, stare logi)

### 10.11. Powiadomienia e-mail
- Tylko e-mail w MVP, kanał asynchroniczny
- Wzorzec **Outbox** (tabela w bazie) + **Hosted Service** worker z mechanizmem retry
- Zdarzenia wyzwalające maila:
  1. Potwierdzenie rejestracji
  2. Potwierdzenie opłaty (mock)
  3. Wygaśnięcie rezerwacji
  4. Awans z listy rezerwowej

### 10.12. Bezpieczeństwo API
- Rate limiting na logowaniu i rejestracji
- CORS ograniczony do znanych originów
- Walidacja wejścia na każdym endpointzie (FluentValidation)
- Globalna obsługa błędów bez ujawniania szczegółów technicznych
- Audyt logowań i nieudanych prób

### 10.13. Obserwowalność
- **OpenTelemetry** z exportem OTLP do **OpenTelemetry Collector**
- **Problem Details** (RFC 7807) z `traceId` w każdej odpowiedzi błędu
- Stack self-hosted przez Docker Compose:
  - OpenTelemetry Collector
  - Prometheus (metryki)
  - Loki (logi)
  - Tempo (trace)
  - Grafana (dashboard)
- Health check endpointy: liveness i readiness

### 10.14. Infrastruktura i środowiska
- **Konteneryzacja:** Docker Compose (nie Kubernetes na start)
- **Środowiska:** `local` + `test`
- **CI:** GitHub Actions — automatyczny build + testy przy każdym pushu

### 10.15. Testy (zakres MVP)
1. Testy jednostkowe: logika domenowa (limity, kolejka, wygasanie rezerwacji, kategorie wiekowe)
2. Testy integracyjne API z bazą PostgreSQL na kontenerach testowych
3. 1 test E2E: rejestracja → rezerwacja → mock płatności → potwierdzenie
4. Testy kontraktu Problem Details dla błędów 400, 401, 403, 404, 409

### 10.16. Struktura projektu

```
src/
  StartLine.Api/              ← host HTTP, endpoints, middleware
  StartLine.Application/      ← CQRS (Commands/Queries), MediatR, FluentValidation
  StartLine.Domain/           ← encje, value objects, agregaty, domain events
  StartLine.Infrastructure/   ← EF Core, repozytoria, email, payment mock
  StartLine.Worker/           ← Hosted Service, zadania cykliczne, Outbox processor
tests/
  StartLine.UnitTests/
  StartLine.IntegrationTests/
docker/
  docker-compose.yml                    ← API, Worker, PostgreSQL
  docker-compose.observability.yml      ← OTel Collector, Prometheus, Loki, Tempo, Grafana
```

### 10.17. Podejście do DDD (pragmatyczne)
Stosujemy DDD bez fanatyzmu:

**Używamy:**
- **Ubiquitous Language** — pojęcia w kodzie tożsame z domeną (np. `Registration`, `Capacity`, `AgeCategory`)
- **Encje i Value Objects** — `Registration` ma tożsamość; `Money`, `DateRange`, `AgeCategory` są niezmienne
- **Agregaty** — `Event` jest agregatem dla `Race` i `Capacity`; modyfikacja tylko przez korzeń agregatu
- **Domain Events** — np. `RegistrationConfirmed` emitowany po opłacie; worker wysyła mail

**Pomijamy w MVP:**
- Event Sourcing
- CQRS z osobną bazą odczytu
- Bounded Contexts jako osobne projekty/serwisy

```
StartLine.Domain/
  Events/
    Event.cs              ← Agregat
    Race.cs               ← Encja
    Capacity.cs           ← Value Object
  Registrations/
    Registration.cs       ← Agregat
    RegistrationStatus.cs ← Enum/Value Object
    WaitlistEntry.cs      ← Encja
  Users/
    User.cs               ← Agregat
    Email.cs              ← Value Object
  Shared/
    Money.cs              ← Value Object
    AgeCategory.cs        ← Value Object
```
