import { render, screen } from "@testing-library/react";

import GuidePage from "@/app/dashboard/guide/page";

describe("GuidePage", () => {
  it("documents the main operational flows and links to their modules", () => {
    render(<GuidePage />);

    expect(screen.getByRole("heading", { level: 1, name: "Guía de uso de AgroControl" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Realizar una venta" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Operar la caja" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Controlar y corregir stock" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Gestionar clientes, deuda y cobranzas" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Preparar comprobantes para ARCA" })).toBeInTheDocument();

    expect(screen.getByRole("link", { name: /Ir a Nueva venta/ })).toHaveAttribute("href", "/dashboard/pos");
    expect(screen.getByRole("link", { name: /Ir a Caja/ })).toHaveAttribute("href", "/dashboard/cash");
    expect(screen.getByText(/todavía no autoriza CAE/)).toBeInTheDocument();
  });
});
