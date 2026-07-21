import assert from "node:assert/strict";
import test from "node:test";

import { authSessionSchema } from "./contracts";

test("authSessionSchema accepts PostgreSQL UUID values used by seeded organizations", () => {
  const result = authSessionSchema.safeParse({
    user: {
      userId: "e272d991-d0ea-4c23-afdf-2000ac088407",
      organizationId: "11111111-1111-1111-1111-111111111111",
      email: "admin@agrocontrol.local",
      displayName: "Administrador AgroControl",
      isActive: true,
      isLocked: false,
      mustChangePassword: false,
      roles: [
        {
          roleId: "04d31241-df85-4930-8cae-3e89857a163d",
          code: "administrator",
          name: "Administrador",
        },
      ],
      permissions: ["auth:session"],
    },
    tokens: {
      accessToken: "access-token",
      accessTokenExpiresAt: "2026-07-21T18:00:00+00:00",
      refreshToken: "refresh-token",
      refreshTokenExpiresAt: "2026-08-04T18:00:00+00:00",
    },
  });

  assert.equal(result.success, true);
});
