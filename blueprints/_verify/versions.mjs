const nuget = [
  'microsoft.entityframeworkcore',
  'npgsql',
  'npgsql.entityframeworkcore.postgresql',
  'mailkit',
  'mimekit',
  'quartz.extensions.hosting',
  'stackexchange.redis',
  'serilog',
  'serilog.sinks.console',
  'serilog.sinks.file',
  'opentelemetry.extensions.hosting',
  'opentelemetry.instrumentation.aspnetcore',
  'hangfire.core',
  'xunit.v3',
  'testcontainers',
  'fluentassertions',
  'spectre.console.cli',
  'system.commandline',
  'otp.net',
  'fido2netlib',
  'fido2',
  'argon2',
];

const npm = [
  'react','react-dom','typescript','vite','tailwindcss','zod','react-hook-form',
  '@hookform/resolvers','@tanstack/react-query','@tanstack/react-router','react-router-dom',
  '@tanstack/react-table','@xyflow/react','dompurify','recharts','electron','electron-builder',
  'electron-updater','vitest','@playwright/test','@testing-library/react','msw','i18next','react-i18next'
];

for (const p of nuget) {
  try {
    const r = await fetch(`https://api.nuget.org/v3-flatcontainer/${p}/index.json`);
    if (!r.ok) { console.log(`NU  ${p} :: HTTP ${r.status}`); continue; }
    const j = await r.json();
    const stable = j.versions.filter(v => !v.includes('-'));
    console.log(`NU  ${p} :: ${stable.slice(-3).join(' ')}`);
  } catch (e) { console.log(`NU  ${p} :: ERR ${e.message}`); }
}

for (const p of npm) {
  try {
    const r = await fetch(`https://registry.npmjs.org/${p}/latest`);
    if (!r.ok) { console.log(`NPM ${p} :: HTTP ${r.status}`); continue; }
    const j = await r.json();
    console.log(`NPM ${p} :: ${j.version}`);
  } catch (e) { console.log(`NPM ${p} :: ERR ${e.message}`); }
}
