# Start-Line — wstępny plan produktu i model biznesowy (PL)

**Data:** 2026-04-03  
**Repo:** MKaluz/start-line  
**Kontekst:** aplikacja do zapisów na zawody sportowe (biegi + wyścigi kolarskie) z dodatkami/gadżetami. Celem jest konkurencja z dostawcą pobierającym ok. **4% prowizji** oraz zbudowanie platformy atrakcyjnej dla wielu organizatorów.

---

## 1) Cel produktu (co sprzedajemy organizatorowi)

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
