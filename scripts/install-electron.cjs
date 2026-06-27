const fs = require('fs');
const path = require('path');
const { spawnSync } = require('child_process');

const electronPkg = path.resolve(__dirname, '../node_modules/.pnpm/electron@36.9.5/node_modules/electron');
const electronExe = path.join(electronPkg, 'dist', 'electron.exe');

if (fs.existsSync(electronExe)) {
  process.exit(0);
}

const cacheZip = path.join(
  process.env.LOCALAPPDATA || '',
  'electron',
  'Cache',
  '9bba7013435de7753e6ceff232a49ca080ff8fc26705ad97ed62e68579c487f5',
  'electron-v36.9.5-win32-x64.zip',
);

if (fs.existsSync(cacheZip)) {
  const dist = path.join(electronPkg, 'dist');
  fs.mkdirSync(dist, { recursive: true });
  spawnSync('powershell.exe', [
    '-NoProfile',
    '-Command',
    `Expand-Archive -Path '${cacheZip.replace(/'/g, "''")}' -DestinationPath '${dist.replace(/'/g, "''")}' -Force`,
  ], { stdio: 'inherit' });
  fs.writeFileSync(path.join(electronPkg, 'path.txt'), 'electron.exe');
  fs.writeFileSync(path.join(dist, 'version'), '36.9.5');
  process.exit(0);
}

if (fs.existsSync(path.join(electronPkg, 'install.js'))) {
  const result = spawnSync(process.execPath, ['install.js'], {
    cwd: electronPkg,
    stdio: 'inherit',
    env: process.env,
  });
  process.exit(result.status ?? 1);
}

console.warn('Electron non installé — exécutez pnpm install dans apps/client');
process.exit(0);
