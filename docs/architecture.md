# Architettura

## Monorepo

- `unity`: client Unity 6 con assembly separati per core, comandi, simulazione, save/sync e bootstrap piattaforme
- `backend`: API Node/TypeScript per login, refresh, profilo, sync città e version manifest

## Confini

- Il codice critico di dominio vive in C# puro sotto `unity/Assets/Game/*/Runtime`
- La UI Unity deve solo tradurre input in `GameCommand` e renderizzare `WorldState`
- Backend e client condividono lo stesso shape logico di `CitySnapshot`, `SyncHead` e `VersionManifest`

## Flusso dati

1. PC o Android emette `GameCommand`
2. `SimulationEngine.SimulationTick` applica i comandi e aggiorna i modelli
3. Il client serializza `CitySnapshot`
4. `LocalCitySaveStore` salva snapshot compresso con backup
5. `BackendApiClient` sincronizza la snapshot con l'API
6. Il backend applica `last-write-wins` sulla `SyncHead`
7. I cataloghi di simulazione arrivano da `unity/Assets/Game/Data/Simulation`

## Sottosistemi già bootstrap

- `Commands`: contratti versionati delle azioni di gioco
- `Core`: tipi del mondo, budget, strade, zoning, edifici, agenti, run-state demo e salvataggi
- `Simulation`: esecuzione comandi, replay deterministico, modelli di domanda, utilities, crescita, traffico, vitalita quartieri, eventi con conseguenze ritardate e outcome demo
- `SaveSync`: serializzazione compressa, store locale con manifest/recovery, migrazione snapshot e client HTTP
- `Data`: cataloghi JSON versionati per strade, servizi, zone, economia e profilo demo (`v4-demo`)
- `backend`: auth JWT, provisioning utenti, file store persistente, endpoint REST v1

## Obiettivi immediati successivi

- consolidare companion Android runtime (dashboard, tasse, eventi e quick actions) con UI dedicata
- rifinire onboarding contestuale PC (task step-by-step, feedback lock morbidi e suggerimenti azione)
- consolidare test performance/stress su mappe grandi
- integrare credenziali Firebase reali quando disponibili
