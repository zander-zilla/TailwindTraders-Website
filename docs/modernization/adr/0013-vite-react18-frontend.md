# 0013. Frontend: Vite + React 18 + react-router 7; drop Azure-only UI features

- **Status:** Accepted (implemented in Phase 7)
- **Date:** 2026-10-10

## Context
- After Phase 1 the SPA still ran on Create React App 5 (deprecated, unmaintained) with React 16 and react-router 4.
- It needed `npm install --legacy-peer-deps` because of 2021-era pins: `@azure/communication-*` (ACS chat/video), `@fluentui/react` 7 / `office-ui-fabric-react`, `msal` 1.x (AAD B2C), the Application Insights React plugin, and `react-s-alert`.
- The ACS feature read an **ACS connection string from `/api/settings`**, a secret served to every browser.
- `index.html` pulled **Bootstrap 4 from a CDN** for the video UI. Its `.modal { display: none }` hid the shop's own login modal. That is a legacy bug, found by the new Playwright smoke test.

## Decision
- **Vite 7** replaces CRA:
  - `@vitejs/plugin-react`, plus `vite-plugin-svgr` (`import Logo from "./x.svg?react"`).
  - The output dir stays `ClientApp/build`, so the .NET host and Dockerfile are unchanged.
  - The dev server proxies `/api` to the ASP.NET Core app.
- **React 18** (`createRoot`). React 19 was not chosen yet: the class components and react-redux `connect` work on 18 with no rewrite, and 19 is a follow-up once components move to hooks.
- **react-router 7** (`BrowserRouter`/`Routes`/`element`). A ~20-line `withRouter` shim gives the class components their v4-era props (`history.push`, `match.params`, `location`).
- Upgrades: **react-redux 9 / redux 5**, **i18next 25 / react-i18next 15** (`Translation`/`withTranslation`), **axios 1.x**. `qs` was replaced by `URLSearchParams`, and `require()` by ESM imports.
- **Removed:**
  - ACS chat, video and Teams-meeting UI (with its Logic App call), plus Bootstrap 4 CDN
  - AAD B2C/msal: the demo JWT login is kept
  - Application Insights: OpenTelemetry web (Phase 6) stays
  - Fluent UI 7 / office-ui-fabric, react-s-alert (replaced by the existing iziToast helper), recompose, history, redux-logger/thunk, query-string
- Server side: `/api/settings` no longer has `acs`, `useB2C`, `b2CAuth`, `applicationInsights`, `logicAppUrl`, `email` or the Dev Spaces route header. A test asserts this.
- **Tests:**
  - Vitest + Testing Library (jsdom) unit tests
  - Playwright smoke against the running compose stack in CI: browse, log in, add to cart
  - CI also checks that browser spans share trace ids with the API
- Deleted the three legacy Azure workflows (`container_appservice`, `regular_appservice`, `telephony_bot`).

## Consequences
- Plus: `npm ci` works with **no `--legacy-peer-deps`**.
- Plus: Vite builds in ~3 s, against 40–60 s with CRA.
- Plus: the JS bundle is about 475 KB (~150 KB gzip), against several MB with the ACS/Fluent/msal SDKs.
- Plus: one secret is no longer exposed (the ACS connection string), and the login modal works again.
- Minus: the customer-support video/chat demo and B2C login are gone. They were Azure-specific and unconfigured in this fork. An AWS equivalent (Amazon Chime SDK / Cognito) would be new work.
- Minus: the router shim is transitional; converting components to hooks would remove it and allow React 19.
- Minus: dropping Bootstrap's reboot CSS changes a few base styles slightly. The shop's own SCSS defines the layout.
