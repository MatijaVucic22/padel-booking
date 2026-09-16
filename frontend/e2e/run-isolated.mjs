import { spawnSync } from "node:child_process";

const windows = process.platform === "win32";
const shell = windows ? "powershell" : "pwsh";
const args = windows
  ? ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "./e2e/run-isolated.ps1"]
  : ["-NoProfile", "-File", "./e2e/run-isolated.ps1"];

const result = spawnSync(shell, args, { stdio: "inherit" });
if (result.error) console.error(`E2E runner nije pokrenut: ${result.error.message}`);
process.exit(result.status ?? 1);
