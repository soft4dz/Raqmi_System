const electron = require('electron');
const path = require('path');
const fs = require('fs');
const fsp = require('fs/promises');

const app = electron.app;
const BrowserWindow = electron.BrowserWindow;
const ipcMain = electron.ipcMain;
const isDev = !app.isPackaged;

function getConfigPath() {
  return path.join(app.getPath('userData'), 'config.json');
}

async function readConfig() {
  try {
    const raw = await fsp.readFile(getConfigPath(), 'utf8');
    return JSON.parse(raw);
  } catch {
    return { serverUrl: 'http://localhost:3000' };
  }
}

async function writeConfig(config) {
  await fsp.mkdir(path.dirname(getConfigPath()), { recursive: true });
  await fsp.writeFile(getConfigPath(), JSON.stringify(config, null, 2), 'utf8');
}

function createWindow() {
  const win = new BrowserWindow({
    width: 1440,
    height: 900,
    title: 'Raqmi System',
    autoHideMenuBar: true,
    webPreferences: {
      contextIsolation: true,
      nodeIntegration: false,
      preload: path.join(__dirname, 'preload.cjs'),
    },
  });

  if (isDev) {
    win.loadURL('http://localhost:5173');
  } else {
    win.loadFile(path.join(__dirname, '../dist/index.html'));
  }
}

ipcMain.handle('raqmi:get-config', () => readConfig());
ipcMain.handle('raqmi:set-config', async (_event, config) => {
  const current = await readConfig();
  await writeConfig({ ...current, ...config });
});
ipcMain.handle('raqmi:test-server', async (_event, serverUrl) => {
  try {
    const response = await fetch(`${serverUrl.replace(/\/$/, '')}/health`);
    return response.ok;
  } catch {
    return false;
  }
});

app.whenReady().then(createWindow);

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit();
});

app.on('activate', () => {
  if (BrowserWindow.getAllWindows().length === 0) createWindow();
});
