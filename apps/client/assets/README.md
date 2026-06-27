# Icône application client

Placez `icon.ico` ici pour personnaliser l'installateur et le raccourci bureau.

Sans icône, electron-builder utilise l'icône Electron par défaut.

Pour activer une icône, ajoutez dans `package.json` → `build.win.icon` :

```json
"win": {
  "icon": "assets/icon.ico",
  "target": ["nsis"]
}
```
