const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('raqmi', {
  getConfig: () => ipcRenderer.invoke('raqmi:get-config'),
  setConfig: (config) => ipcRenderer.invoke('raqmi:set-config', config),
  testServer: (serverUrl) => ipcRenderer.invoke('raqmi:test-server', serverUrl),
  isDesktop: true,
});
