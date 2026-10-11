import React from "react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import axios from "axios";
import "./i18n";
import withRouter from "./shared/withRouter";
import Footer from "./shared/footer/footer";
import ConfigService from "./services/configService";

vi.mock("axios");

describe("ConfigService", () => {
  beforeEach(() => {
    ConfigService._needLoadSettings = true;
    ConfigService._loading = null;
    axios.get.mockReset();
  });

  it("loads runtime settings once from /api/settings", async () => {
    axios.get.mockResolvedValue({
      data: { apiUrl: "/api/v1", apiUrlShoppingCart: "/api/v1", browserTelemetry: { otlpTracesEndpoint: "" } },
    });
    await Promise.all([ConfigService.loadSettings(), ConfigService.loadSettings()]);
    await ConfigService.loadSettings();
    expect(axios.get).toHaveBeenCalledTimes(1);
    expect(axios.get).toHaveBeenCalledWith("/api/settings");
    expect(ConfigService._apiUrl).toBe("/api/v1");
  });

  it("builds bearer headers", () => {
    expect(ConfigService.HeadersConfig("t0k")).toEqual({ headers: { Authorization: "Bearer t0k" } });
    expect(ConfigService.HeadersConfig()).toEqual({ headers: {} });
  });
});

describe("withRouter (react-router 7 shim for class components)", () => {
  it("passes match.params and location like react-router 4 did", () => {
    const Probe = withRouter(({ match, location }) => (
      <span>
        {match.params.productId}@{location.pathname}
      </span>
    ));
    render(
      <MemoryRouter initialEntries={["/product/detail/42"]}>
        <Routes>
          <Route path="/product/detail/:productId" element={<Probe />} />
        </Routes>
      </MemoryRouter>
    );
    expect(screen.getByText("42@/product/detail/42")).toBeInTheDocument();
  });
});

describe("Footer", () => {
  it("renders translated links (i18next + react-i18next 15)", () => {
    const { container } = render(
      <MemoryRouter>
        <Footer />
      </MemoryRouter>
    );
    expect(container.querySelectorAll("a").length).toBeGreaterThan(0);
    expect(container.textContent).not.toMatch(/shared\.footer\./); // keys resolved
  });
});
