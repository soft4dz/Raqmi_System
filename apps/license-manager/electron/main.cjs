const { generateLicenseKeyPair, buildLicensePayload, signLicenseFile, serializeLicenseFile } = require('./license-sign.cjs');

const electron = require('electron');
const path = require('path');
const fsp = require('fs/promises');
const fs = require('fs');

const app = electron.app;
const BrowserWindow = electron.BrowserWindow;
const ipcMain = electron.ipcMain;
const dialog = electron.dialog;
const isDev = !app.isPackaged;

function dataPath(name) {
  return path.join(app.getPath('userData'), name);
}

async function readJson(file, fallback) {
  try {
    return JSON.parse(await fsp.readFile(file, 'utf8'));
  } catch {
    return fallback;
  }
}

async function writeJson(file, value) {
  await fsp.mkdir(path.dirname(file), { recursive: true });
  await fsp.writeFile(file, JSON.stringify(value, null, 2), 'utf8');
}

async function ensureEditorKeys() {
  const publicPath = dataPath('keys/public.jwk.json');
  const privatePath = dataPath('keys/private.jwk.json');
  if (fs.existsSync(publicPath) && fs.existsSync(privatePath)) {
    return { publicPath, privatePath };
  }

  const keys = await generateLicenseKeyPair();
  await writeJson(publicPath, keys.publicKey);
  await writeJson(privatePath, keys.privateKey);
  return { publicPath, privatePath };
}

function createWindow() {
  const win = new BrowserWindow({
    width: 1360,
    height: 860,
    title: 'Raqmi License Manager',
    autoHideMenuBar: true,
    webPreferences: {
      contextIsolation: true,
      nodeIntegration: false,
      preload: path.join(__dirname, 'preload.cjs'),
    },
  });

  if (isDev) {
    win.loadURL('http://localhost:5174');
  } else {
    win.loadFile(path.join(__dirname, '../dist/index.html'));
  }
}

ipcMain.handle('editor:load-data', () => readJson(dataPath('editor-data.json'), { tenants: [], licenses: [] }));

ipcMain.handle('editor:save-data', (_event, data) => writeJson(dataPath('editor-data.json'), data));

ipcMain.handle('editor:ensure-keys', () => ensureEditorKeys());

ipcMain.handle('editor:export-license', async (_event, payload) => {
  const { privatePath } = await ensureEditorKeys();
  const privateKey = JSON.parse(await fsp.readFile(privatePath, 'utf8'));
  const licensePayload = buildLicensePayload(payload);
  const file = await signLicenseFile(licensePayload, privateKey);
  return serializeLicenseFile(file);
});

ipcMain.handle('editor:save-license-file', async (_event, content, suggestedName) => {
  const result = await dialog.showSaveDialog({
    defaultPath: suggestedName ?? 'license.license',
    filters: [{ name: 'Raqmi License', extensions: ['license', 'json'] }],
  });
  if (result.canceled || !result.filePath) return null;
  await fsp.writeFile(result.filePath, content, 'utf8');
  return result.filePath;
});

app.whenReady().then(createWindow);

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit();
});
