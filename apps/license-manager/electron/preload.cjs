const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('raqmiEditor', {
  loadData: () => ipcRenderer.invoke('editor:load-data'),
  saveData: (data) => ipcRenderer.invoke('editor:save-data', data),
  ensureKeys: () => ipcRenderer.invoke('editor:ensure-keys'),
  exportLicense: (payload) => ipcRenderer.invoke('editor:export-license', payload),
  saveLicenseFile: (content, suggestedName) =>
    ipcRenderer.invoke('editor:save-license-file', content, suggestedName),
});
