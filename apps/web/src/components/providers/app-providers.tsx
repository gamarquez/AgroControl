"use client";

import type { PropsWithChildren } from "react";

import { QueryProvider } from "@/components/providers/query-provider";

export function AppProviders({ children }: PropsWithChildren) {
  return <QueryProvider>{children}</QueryProvider>;
}

