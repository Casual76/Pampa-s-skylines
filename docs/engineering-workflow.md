# Workflow AI-first

## Regole

- Ogni modulo nuovo parte da contratti, tipi e test
- Il codice di simulazione non dipende da scene, prefab o `MonoBehaviour`
- I file di save sono versionati e migrabili
- Ogni endpoint backend deve avere test almeno di happy path e regressione base

## Ordine di lavoro consigliato

1. Definire o estendere il contratto
2. Aggiornare test e acceptance
3. Implementare dominio puro
4. Collegare UI/rendering o API
5. Verificare replay di comandi e round-trip save/load

## Vincoli

- Un solo input canonicale: `GameCommand`
- Un solo output canonicale di stato: `WorldState` / `CitySnapshot`
- Nessuna logica di business dispersa nei controller HTTP o nei componenti Unity
