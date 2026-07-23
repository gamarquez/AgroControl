import { screen } from "@testing-library/react";

import { HealthStatusPanel } from "@/components/health/health-status-panel";
import { renderWithProviders } from "@/test/render";

const fetchMock = vi.fn<typeof fetch>();

describe("HealthStatusPanel", () => {
  beforeEach(() => {
    vi.stubGlobal("fetch", fetchMock);
    vi.spyOn(window.navigator, "onLine", "get").mockReturnValue(true);

    process.env.NEXT_PUBLIC_API_BASE_URL = "http://localhost:5030";
    process.env.NEXT_PUBLIC_API_HEALTH_PATH = "/health";
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
    fetchMock.mockReset();
  });

  it("shows the success state when the API responds", async () => {
    fetchMock.mockResolvedValue(
      new Response(
        JSON.stringify({
          status: "Healthy",
          service: "AgroControl API",
          timestamp: "2026-07-07T21:00:00.000Z",
          checks: {
            database: "healthy",
          },
        }),
        {
          headers: {
            "content-type": "application/json",
          },
          status: 200,
        },
      ),
    );

    renderWithProviders(<HealthStatusPanel />);

    expect(await screen.findByText("API conectada")).toBeInTheDocument();
    expect(screen.queryByText("AgroControl API")).not.toBeInTheDocument();
    expect(screen.queryByText("database")).not.toBeInTheDocument();
    expect(screen.queryByText(/localhost:5030/)).not.toBeInTheDocument();
  });

  it("shows the error state when the API fails", async () => {
    fetchMock.mockResolvedValue(
      new Response("Servicio no disponible", {
        status: 503,
      }),
    );

    renderWithProviders(<HealthStatusPanel />);

    expect(await screen.findByText("API no disponible")).toBeInTheDocument();
    expect(screen.queryByText(/503/)).not.toBeInTheDocument();
    expect(screen.queryByText(/localhost:5030/)).not.toBeInTheDocument();
  });

  it("shows the offline state without hitting the network", async () => {
    vi.spyOn(window.navigator, "onLine", "get").mockReturnValue(false);

    renderWithProviders(<HealthStatusPanel />);

    expect(await screen.findByText("Sin conexión")).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
  });
});
