import assert from "node:assert/strict";
import { formatProcessError, startManagedProcess, stopManagedProcess, waitForJson } from "./process-utils.mjs";

const api = startManagedProcess({
  command: "dotnet",
  args: [
    "run",
    "--no-build",
    "--project",
    "apps/api/src/AgroControl.Api/AgroControl.Api.csproj",
    "--urls",
    "http://127.0.0.1:5080",
  ],
  cwd: process.cwd(),
  name: "api",
});

try {
  const payload = await waitForJson(
    "http://127.0.0.1:5080/health",
    (value) =>
      value?.status === "healthy" &&
      value?.service === "AgroControl.Api" &&
      typeof value?.version === "string" &&
      typeof value?.environment === "string" &&
      typeof value?.checkedAt === "string",
  );

  assert.equal(payload.status, "healthy");
} catch (error) {
  throw formatProcessError(
    error instanceof Error ? error.message : "The API health smoke test failed.",
    api,
  );
} finally {
  await stopManagedProcess(api);
}
