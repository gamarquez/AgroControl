import { render, screen } from "@testing-library/react";
import { usePathname } from "next/navigation";

import { DashboardNav } from "@/components/dashboard/dashboard-nav";

vi.mock("next/navigation", () => ({
  usePathname: vi.fn(),
}));

vi.mock("@/app/dashboard/actions", () => ({
  logoutAction: vi.fn(),
}));

describe("DashboardNav", () => {
  it("includes the usage guide in the management dropdown", () => {
    vi.mocked(usePathname).mockReturnValue("/dashboard/guide");

    render(<DashboardNav displayName="Ada Admin" email="ada@example.com" />);

    const guideLinks = screen.getAllByRole("link", { name: "Guía de uso" });
    expect(guideLinks).toHaveLength(2);
    expect(guideLinks.every((link) => link.getAttribute("href") === "/dashboard/guide")).toBe(true);
    expect(guideLinks.at(-1)).toHaveClass("bg-muted");
  });
});
