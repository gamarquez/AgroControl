import { spawn } from "node:child_process";

function withDefaultEnv(env = {}) {
  return {
    ...process.env,
    NEXT_TELEMETRY_DISABLED: "1",
    ...env,
  };
}

export function startManagedProcess({ command, args, cwd, env, name }) {
  const logs = [];
  const useWindowsCommandWrapper = process.platform === "win32" && /\.(cmd|bat)$/i.test(command);
  const spawnCommand = useWindowsCommandWrapper ? "cmd.exe" : command;
  const spawnArgs = useWindowsCommandWrapper
    ? ["/d", "/s", "/c", command, ...args]
    : args;

  const child = spawn(spawnCommand, spawnArgs, {
    cwd,
    env: withDefaultEnv(env),
    stdio: ["ignore", "pipe", "pipe"],
    shell: false,
  });

  const pushLog = (chunk) => {
    const line = chunk.toString();
    logs.push(line);

    if (logs.length > 200) {
      logs.shift();
    }
  };

  child.stdout.on("data", pushLog);
  child.stderr.on("data", pushLog);

  return {
    child,
    name,
    getLogs() {
      return logs.join("");
    },
  };
}

export async function stopManagedProcess(processHandle) {
  if (!processHandle?.child) {
    return;
  }

  const { child } = processHandle;

  if (child.exitCode !== null) {
    child.stdout?.destroy();
    child.stderr?.destroy();
    return;
  }

  child.kill();

  if (process.platform === "win32") {
    const killer = spawn("taskkill", ["/pid", String(child.pid), "/t", "/f"], {
      stdio: "ignore",
      shell: false,
    });

    await new Promise((resolve) => killer.once("exit", () => resolve()));
    child.stdout?.destroy();
    child.stderr?.destroy();
    return;
  }

  const exited = await Promise.race([
    new Promise((resolve) => child.once("exit", () => resolve(true))),
    new Promise((resolve) => setTimeout(() => resolve(false), 5000)),
  ]);

  if (!exited) {
    child.kill("SIGKILL");
  }

  child.stdout?.destroy();
  child.stderr?.destroy();
}

async function pollUntil(check, timeoutMs, intervalMs) {
  const deadline = Date.now() + timeoutMs;
  let lastError;

  while (Date.now() < deadline) {
    try {
      return await check();
    } catch (error) {
      lastError = error;
      await new Promise((resolve) => setTimeout(resolve, intervalMs));
    }
  }

  throw lastError ?? new Error("Timed out while waiting for process output.");
}

export async function waitForJson(url, predicate, timeoutMs = 60000, intervalMs = 500) {
  return pollUntil(async () => {
    const response = await fetch(url, {
      headers: {
        accept: "application/json",
      },
    });

    if (!response.ok) {
      throw new Error(`Unexpected status code ${response.status} for ${url}.`);
    }

    const payload = await response.json();

    if (!predicate(payload)) {
      throw new Error(`Response payload for ${url} did not match the expected contract.`);
    }

    return payload;
  }, timeoutMs, intervalMs);
}

export async function waitForText(url, predicate, timeoutMs = 60000, intervalMs = 500) {
  return pollUntil(async () => {
    const response = await fetch(url);

    if (!response.ok) {
      throw new Error(`Unexpected status code ${response.status} for ${url}.`);
    }

    const text = await response.text();

    if (!predicate(text)) {
      throw new Error(`Response body for ${url} did not contain the expected content.`);
    }

    return text;
  }, timeoutMs, intervalMs);
}

export function formatProcessError(message, processHandle) {
  const logs = processHandle?.getLogs()?.trim();

  if (!logs) {
    return new Error(message);
  }

  return new Error(`${message}\n\nLast logs from ${processHandle.name}:\n${logs}`);
}
