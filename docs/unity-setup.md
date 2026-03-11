# Setup locale Unity

## Installa

- Unity Hub
- Unity 6 URP
- Android Build Support
- Android SDK & NDK Tools
- OpenJDK
- Visual Studio 2022 oppure VS Code con estensioni C#

## Quando Unity ha finito

1. Apri la cartella `unity`
2. Lascia che Unity importi il progetto e generi i file locali
3. Verifica che compaiano errori di compilazione o warning in Console
4. Esegui `Pampa Skylines > PC > Rebuild Bootstrap Scene` per generare:
   - materiali base clean-sim
   - `PcCleanSimTheme.asset`
   - scena `Assets/Game/PC/Scenes/PcBootstrap.unity`
5. Apri `Assets/Game/PC/Scenes/PcBootstrap.unity`
6. Entra in Play Mode e verifica la shell UGUI + TextMeshPro: top bar, tool rail, stats/inspector, modali menu/load/settings/help
7. Prova i controlli base: `WASD` move, middle-drag pan, edge scroll, wheel zoom, `Q/E` rotate, `1-9/F/P/H/J/0` tools
8. Verifica il loop save/load locale: `Ctrl+S` save manuale, `F5` load current, autosave periodico, browser dei backup nel menu Load
9. Verifica progressione: milestone con lock tool rigido, card avanzamento, bailout morbido e pannello tasse dopo `Municipio`
10. Verifica overlay e quality-of-life: `L` land value, `U` utilities, `T` traffic, `R` service range, `G` grid, `Ctrl+Z/Y` undo/redo, `F1` help
11. Verifica asset CC0 in `Assets/Game/PC/Art/CC0` (vedi `docs/cc0-assets.md`)
12. Installa eventualmente i package mancanti dal `manifest.json`
13. Dimmi il primo set di errori esatto, se presente

## Cosa non serve ancora

- asset finali
- Firebase credentials definitive
- build Android firmata
- pipeline store/update
