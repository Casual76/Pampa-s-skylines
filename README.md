<<<<<<< HEAD
# Pampa Skylines

Bootstrap monorepo per un city builder AI-first stile Cities: Skylines, con:

- client principale Unity 6 per Windows
- companion Android sugli stessi contratti dati
- backend Node/TypeScript per auth, sync e version manifest
- kernel di simulazione in C# puro, separato dalla UI

## Stato attuale

Questa repository implementa una vertical slice giocabile del core con focus demo:

- struttura monorepo e convenzioni
- contratti condivisi per comandi, salvataggi e sync
- kernel di simulazione con tick deterministico, replay log, zoning, budget, utilities, crescita, traffico ed eventi
- metriche demo `schema v4`: vitalita quartieri, pressioni sistemiche e stato run/onboarding
- snapshot envelope con hash contenuto, metadata, recovery locale e migrazione formato
- cataloghi gameplay JSON esterni (`v4-demo`) sotto `unity/Assets/Game/Data/Simulation`
- backend HTTP v1 con auth username/password, profile, city head, snapshot upload/download idempotente e version manifest
- scena bootstrap PC con HUD runtime, overlay, save/load locale e strumenti base
- onboarding guidato in 12 step con lock morbidi contestuali e messaggi di rifiuto comando espliciti
- companion Android esteso con dashboard KPI, alert prioritizzati e quick actions su eventi/tasse/tempo
- test backend per sync, migrazione snapshot e validazione cataloghi

Non include ancora asset finali, pipeline build completa, sistemi avanzati (trasporto pubblico/disastri) o full UI Android.

## Struttura

- `docs/architecture.md`
- `docs/engineering-workflow.md`
- `docs/unity-setup.md`
- `backend`
- `unity`

## Backend

```powershell
cd "C:\Pampa's skylines\backend"
npm.cmd install
npm.cmd run create-user -- --username amico --password segreta
npm.cmd test
npm.cmd run dev
```
